using System;
using ArtOfSimRally.Mod;
using Dbce.Wheel.Input;

internal static class Program
{
    private static int checks;
    private static readonly Guid Device = new Guid("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Product = new Guid("22222222-2222-2222-2222-222222222222");
    private static void Check(bool ok, string message)
    { checks++; if (!ok) throw new Exception(message); }
    private static ControlBinding Binding(string action, ControlKind kind, int index, int direction = 1)
        => ControlBinding.FromCapture(action, kind, index, direction, kind == ControlKind.Axis ? 32767 : 0, Device, Product, "Fixture device");

    private static int Main()
    {
        Check(PromptGlyphLabel.Binding(Binding("shiftUp", ControlKind.Button, 12)) == "B13", "shared zero-based button is displayed one-based");
        Check(PromptGlyphLabel.Element("Button 13") == "B13", "Rewired displayed number is not incremented");
        Check(PromptGlyphLabel.Binding(Binding("camera", ControlKind.Button, 32)) == "B33", "camera uses its actual physical button");
        Check(PromptGlyphLabel.Binding(Binding("shiftDown", ControlKind.Button, 11)) == "B12", "shift down remains B12");
        Check(PromptGlyphLabel.Binding(Binding("steer", ControlKind.Axis, 0)) == "X", "steering axis");
        Check(PromptGlyphLabel.Binding(Binding("throttle", ControlKind.Axis, 1)) == "Y", "pedal axis");
        Check(PromptGlyphLabel.Binding(Binding("clutch", ControlKind.Axis, 5)) == "Rz", "rotation axis is distinct from translation");
        Check(PromptGlyphLabel.Binding(Binding("handbrake", ControlKind.Axis, 6)) == "S1", "slider number survives compact formatting");
        Check(PromptGlyphLabel.Element("X Rotation") == "Rx" && PromptGlyphLabel.Element("X Axis") == "X", "native rotation does not masquerade as X axis");
        Check(PromptGlyphLabel.Element("Slider 2") == "S2", "native sliders remain distinguishable");
        Check(PromptGlyphLabel.Binding(Binding("navUp", ControlKind.Hat, 0, 0)) == "H1N", "hat north");
        Check(PromptGlyphLabel.Binding(Binding("navRight", ControlKind.Hat, 0, 9000)) == "H1E", "hat east");
        Check(PromptGlyphLabel.Binding(null) == null && PromptGlyphLabel.Element(null) == "?", "missing binding does not invent a shared control");
        IdentityChecks();
        Console.WriteLine("Prompt glyphs PASS " + checks);
        return 0;
    }

    private static void IdentityChecks()
    {
        var wheel = new Rewired.Joystick(); var pedals = new Rewired.Joystick(); var gamepad = new Rewired.Joystick();
        var rawWheel = Guid.NewGuid(); var rawPedals = Guid.NewGuid();
        var binding = Binding("throttle", ControlKind.Axis, 1);
        var admitted = new ArtOfSimRally.Mod.Shim.SharedInputSession {
            Before = new[] {
                new ArtOfSimRally.Mod.Shim.JoystickObservation { Token = wheel, Instance = rawWheel.ToString(), ProductGuid = Product.ToString(), InterfacePath = "wheel-path" },
                new ArtOfSimRally.Mod.Shim.JoystickObservation { Token = pedals, Instance = rawPedals.ToString(), ProductGuid = Product.ToString(), InterfacePath = "pedal-path" } },
            Owned = new[] { new ArtOfSimRally.Mod.Shim.OwnedDeviceIdentity { Instance = Device, Product = Product, InterfacePath = "pedal-path" } },
            RewiredInstances = new[] { rawWheel, rawPedals }
        };
        admitted.Isolation.Tokens.Add(wheel); admitted.Isolation.Tokens.Add(pedals);
        SharedInputStartup.Fixture(admitted);
        ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Owned.Add(1);
        SharedBindingCaptureHost.Instance.Active["throttle"] = binding;
        Check(SharedInputStartup.TryGlyphBinding(wheel, "throttle", out var actual) && ReferenceEquals(actual, binding), "wheel-selected row resolves active separate pedal by path/product, across distinct GUID namespaces");
        Check(!SharedInputStartup.TryGlyphBinding(gamepad, "throttle", out _), "unrelated gamepad keeps its native prompt");
        admitted.Isolation.Tokens.Add(gamepad);
        Check(!SharedInputStartup.TryGlyphBinding(gamepad, "throttle", out _), "disabled ownership without exact admitted object is insufficient");
        Check(!SharedInputStartup.TryGlyphBinding(wheel, "clutch", out _), "unowned action keeps native mapping");
        admitted.Owned[0].Product = Guid.NewGuid();
        Check(!SharedInputStartup.TryGlyphBinding(wheel, "throttle", out _), "product mismatch refuses label");
        admitted.Owned[0].Product = Product; admitted.Before[1].InterfacePath = "other-pedal-path";
        Check(!SharedInputStartup.TryGlyphBinding(wheel, "throttle", out _), "name/index cannot substitute for exact path");
        admitted.Before[1].InterfacePath = "PEDAL-PATH";
        Check(SharedInputStartup.TryGlyphBinding(wheel, "throttle", out _), "only path case folding is allowed");
        admitted.Inventory.ConnectionEpoch++;
        Check(!SharedInputStartup.TryGlyphBinding(wheel, "throttle", out _), "connection epoch invalidation refuses old witness");
        admitted.Inventory.ConnectionEpoch--; pedals.isConnected = false;
        Check(!SharedInputStartup.TryGlyphBinding(wheel, "throttle", out _), "disconnected bound device refuses label");
        pedals.isConnected = true; admitted.Isolation.Tokens.Remove(pedals);
        Check(!SharedInputStartup.TryGlyphBinding(wheel, "throttle", out _), "restored device cannot retain shared label");
        admitted.Isolation.Tokens.Add(pedals); SharedInputStartup.Stopped = true;
        Check(!SharedInputStartup.TryGlyphBinding(wheel, "throttle", out _), "stopped shared session cannot advertise active prompt");
        SharedInputStartup.Stopped = false;
        admitted.Owned = new[] { admitted.Owned[0], admitted.Owned[0] };
        Check(!SharedInputStartup.TryGlyphBinding(wheel, "throttle", out _), "ambiguous admitted bindings refuse label");
        admitted.Owned = new[] { admitted.Owned[0] };
        AdapterChecks(wheel, gamepad);
    }

    private static void AdapterChecks(Rewired.Joystick wheel, Rewired.Joystick gamepad)
    {
        var action = new Rewired.InputAction { id = 2 };
        var native = new Rewired.ActionElementMap { elementIdentifierName = "X Axis" };
        object generic = new object();
        var row = new ControlsRemapper.UIActionSet(); row.glyphImage.sprite = generic;
        GlyphFallback.ShowNameWhenNoGlyph(row, action, native, wheel);
        Check(!row.glyphImage.enabled && row.keyboardGlyph.activeSelf && row.keyboardText.text == "Y", "owned controls row replaces generic artwork with separate pedal Active binding, not native X map");

        var keyboardRow = new ControlsRemapper.UIActionSet(); keyboardRow.keyboardText.text = "W";
        GlyphFallback.ShowNameWhenNoGlyph(keyboardRow, action, native, null);
        Check(keyboardRow.keyboardText.text == "W" && !keyboardRow.keyboardGlyph.activeSelf, "keyboard path remains unchanged even with null sprite");
        var padRow = new ControlsRemapper.UIActionSet(); padRow.glyphImage.sprite = generic;
        GlyphFallback.ShowNameWhenNoGlyph(padRow, action, native, gamepad);
        Check(padRow.glyphImage.enabled && ReferenceEquals(padRow.glyphImage.sprite, generic) && !padRow.keyboardGlyph.activeSelf, "real gamepad artwork remains");
        var unmappedRow = new ControlsRemapper.UIActionSet(); unmappedRow.glyphImage.sprite = generic;
        GlyphFallback.ShowNameWhenNoGlyph(unmappedRow, new Rewired.InputAction { id = 999 }, native, wheel);
        Check(unmappedRow.glyphImage.enabled && !unmappedRow.keyboardGlyph.activeSelf, "unrouted native action is not relabeled as shared");

        var image = new UnityEngine.UI.Image { sprite = generic }; var text = new UnityEngine.UI.Text { text = "old" }; var badge = new UnityEngine.UI.Image { enabled = false };
        var glyphs = new ControllerGlyphs { Artwork = generic };
        // No native element map exists: an effective shared prompt must still resolve.
        PromptGlyphFallback.ShowNameWhenNoGlyph(PadManager.GetPlayer(), wheel, action, ControllerButtonDisplay.AxisPolarity.NONE, image, text, badge, glyphs);
        Check(!image.enabled && badge.enabled && text.enabled && text.text == "Y", "owned menu prompt overrides nonnull generic sprite even without a native map");
        image.enabled = true; badge.enabled = false; text.text = "other player";
        PromptGlyphFallback.ShowNameWhenNoGlyph(new Rewired.Player(), wheel, action, ControllerButtonDisplay.AxisPolarity.NONE, image, text, badge, glyphs);
        Check(image.enabled && !badge.enabled && text.text == "other player", "other player's native prompt is not replaced by primary player's shared binding");
        image.enabled = true; badge.enabled = true; text.text = "space";
        PromptGlyphFallback.ShowNameWhenNoGlyph(PadManager.GetPlayer(), wheel, new Rewired.InputAction { id = 999 }, ControllerButtonDisplay.AxisPolarity.NONE, image, text, badge, glyphs);
        Check(!image.enabled && badge.enabled && text.text == "space", "wheel with no binding for the action drops the stale sprite over the keyboard badge");
        var maps = PadManager.GetPlayer().controllers.maps.Items; maps.Add(native);
        image.enabled = true; badge.enabled = false; text.text = "pad";
        PromptGlyphFallback.ShowNameWhenNoGlyph(PadManager.GetPlayer(), gamepad, action, ControllerButtonDisplay.AxisPolarity.NONE, image, text, badge, glyphs);
        Check(image.enabled && !badge.enabled && text.text == "pad", "nonowned gamepad prompt preserves native artwork");
        PromptGlyphFallback.ShowNameWhenNoGlyph(PadManager.GetPlayer(), new Rewired.Controller(), action, ControllerButtonDisplay.AxisPolarity.NONE, image, text, badge, glyphs);
        Check(image.enabled && !badge.enabled && text.text == "pad", "nonjoystick prompt preserves native presentation");
        var missingRow = new ControlsRemapper.UIActionSet();
        GlyphFallback.ShowNameWhenNoGlyph(missingRow, new Rewired.InputAction { id = 999 }, native, gamepad);
        Check(!missingRow.glyphImage.enabled && missingRow.keyboardText.text == "X", "existing missing-artwork fallback remains for native joystick");
        glyphs.Artwork = null;
        PromptGlyphFallback.ShowNameWhenNoGlyph(PadManager.GetPlayer(), gamepad, new Rewired.InputAction { id = 999 }, ControllerButtonDisplay.AxisPolarity.NONE, image, text, badge, glyphs);
        Check(!image.enabled && badge.enabled && text.text == "X", "existing native menu fallback still removes stale sprite");
        image.enabled = true; badge.enabled = false; text.text = "disabled";
        ArtOfSimRally.Mod.Main.Settings.GlyphTextFallback = false;
        PromptGlyphFallback.ShowNameWhenNoGlyph(PadManager.GetPlayer(), wheel, action, ControllerButtonDisplay.AxisPolarity.NONE, image, text, badge, glyphs);
        Check(image.enabled && !badge.enabled && text.text == "disabled", "setting off leaves stock prompt intact");
        ArtOfSimRally.Mod.Main.Settings.GlyphTextFallback = true;
        var legacy = WheelInput.Binding.Parse("Fixture device|4|axis:2|0|65535|guid:" + Device.ToString("D"));
        WheelInput.Fixture(WheelInput.Channel.Throttle, legacy);
        ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Owned.Remove(1);
        Check(OwnedPromptGlyph.TryLabel(PadManager.GetPlayer(), wheel, action, false, out var fallback) && fallback == "Z", "unowned shared throttle labels actual loaded Art fallback, not native X or shared Y");
        WheelInput.Duplicate = "brake";
        Check(!OwnedPromptGlyph.TryLabel(PadManager.GetPlayer(), wheel, action, false, out _), "shared collision suppresses legacy fallback label");
        WheelInput.Duplicate = null;
        ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Owned.Add(1);
        SharedBindingCaptureHost.Instance.Active.Remove("throttle");
        Check(!OwnedPromptGlyph.TryLabel(PadManager.GetPlayer(), wheel, action, false, out _), "owned shared throttle with missing Active witness cannot advertise legacy binding");
        ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Owned.Remove(1);
        legacy.InstanceGuid = null;
        Check(!OwnedPromptGlyph.TryLabel(PadManager.GetPlayer(), wheel, action, false, out _), "legacy name/index-only binding cannot claim an exact owned device");
        maps.Clear();
    }
}
