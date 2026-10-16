using System.Reflection;
using ArtOfSimRally.Mod;
using Device = Dbce.Wheel.Ffb.WheelFfbNative;
using Host = ArtOfSimRally.Mod.Main;
using Clock = ArtOfSimRally.Mod.Time;

static partial class Program
{
    static class RewiredUpName { public static bool GetButtonUp(int a) => false; }
    // The production GetButtonUp postfix (Program.Query covers the other four button methods).
    static bool UpQuery(Rewired.Player player, int action)
    {
        object[] args = { player, action, typeof(RewiredUpName).GetMethod("GetButtonUp"), false };
        typeof(GameButtonPatch).GetMethod("After", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        return (bool)args[3];
    }

    // sana 184473: the newly routed in-stage actions through their real accepted path, without the game.
    // - A fake physical device (BridgeTests.DeviceSource) is the reader interface the production bridge reads.
    // - The real ordinary dbce_shim resolves the profile, and the copied bridge fills Art's device arrays.
    // - The production Rewired postfixes answer the game's queries.
    // Shared, owned by the profile: horn (action 20) on 30, look back (18) on 63-, reset (19) on recover 44. Art-only, from
    // Art's own bindings on the same copied read: repair puncture 45, quick restart 46, look left 22-, right 22+, forward 63+.
    // There is no RAW injection, no latched session and no native game action, and keyboard results still pass through.
    // The DeviceSource is fixture evidence for the Core-to-consumer path, not for hardware or for the game's reaction.
    // 30 held frames show GetButton(44) staying true, not the game's 1 s recovery, and the Art-only actions on real
    // controls remain a physical UAT check. The caller selects the qualified shim in ART_SHIM_DLL. The fallback below is
    // a developer path on one machine, neither portable nor qualified.
    static void RealCoreSupportedActions()
    {
        string dll = Environment.GetEnvironmentVariable("ART_SHIM_DLL") ?? @"E:\Source\games\.worktrees\art-shim-probe-evidence\apply-20261012-021108\dbce_shim.dll";
        Check(File.Exists(dll), "ordinary dbce_shim.dll unavailable for the real-Core action test: " + dll);
        Guid id = BridgeTests.DeviceSource.Ids[0], product = BridgeTests.DeviceSource.Products[0];
        Setup(""); GameButtonCompatibility.Allowed = true; GameButtonInput.Observe(12, 13, 16, 17);
        Device.Devices = new[] { new Device.DeviceInfo { Name = "MOZA R12 Base", Index = 0, InstanceGuid = id } };
        string Bind(string element, int rest = 0) => "TSS fixture|0|" + element + "|" + rest + "|1|guid:" + id.ToString("D");
        var cfg = Host.Settings;
        cfg.RepairPunctureBinding = Bind("button:23"); cfg.QuickRestartBinding = Bind("button:24");
        cfg.LookLeftBinding = Bind("pov:0", 27000); cfg.LookRightBinding = Bind("pov:0", 9000); cfg.LookForwardBinding = Bind("pov:0", 0);
        cfg.HornBinding = Bind("button:25"); // the profile owns horn, so this binding is never used
        WheelInput.Close(); WheelInput.LoadBindings(); WheelInput.Open(); Clock.frameCount = 90000; WheelInput.Update();
        Check(WheelInput.SharedProbeSlots.Length == 1, "the R12 reader is not open");
        string r12 = " dev={" + id.ToString("D") + "} prod={" + product.ToString("D") + "}";
        string profile = "[Controls]\nSchema = 1\nProfile = Actions\nRevision = r1\nTransmission = Sequential\n" +
            "steer = axis 0" + r12 + " range=0..65535 rest=32768 travel=+1\n" +
            "horn = button 20" + r12 + "\nlookBack = button 21" + r12 + "\nreset = button 22" + r12 + "\n";
        string folder = Path.Combine(Path.GetTempPath(), "art-real-core-actions-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var native = new ArtOfSimRally.Mod.Shim.ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(profile));
        var source = new BridgeTests.DeviceSource();
        WheelInput.AttachSharedInput(source, native, () => true, () => true, "Sequential", profile);
        var consumer = ArtOfSimRally.Mod.Shim.ShimInput.Consumer;
        var pad = PadManager.Player;
        void Frame() { Clock.frameCount++; WheelInput.EnsureUpdatedThisFrame(); System.Threading.Thread.Sleep(5); }
        bool Until(Func<bool> condition) { for (int i = 0; i < 400 && !condition(); i++) Frame(); return condition(); }
        bool Down(int action, bool negative = false) => Query(pad, negative ? "GetNegativeButtonDown" : "GetButtonDown", action, false);
        bool Held(int action, bool negative = false) => Query(pad, negative ? "GetNegativeButton" : "GetButton", action, false);
        bool Up(int action) => UpQuery(pad, action);
        string Status() => consumer.Started ? string.Join(",", new[] { 18, 19, 20 }.Select(a =>
            ArtOfSimRally.Mod.Shim.ShimFrame.Names[a] + "=" + consumer.OriginalFrame[a].Status)) : "not started";
        try
        {
            Check(Until(() => consumer.Started && consumer.Live(18, Clock.frameCount) && consumer.Live(19, Clock.frameCount) && consumer.Live(20, Clock.frameCount)),
                "real Core never made look back, reset and horn Live while released: " + Status());
            Check(consumer.Owns(18) && consumer.Owns(19) && consumer.Owns(20) && !consumer.Owns(9), "real Core ownership is not horn, look back and reset");
            // Released: nothing reaches the game, while the game's own (keyboard) results still pass (ordinary sessions OR).
            Check(!Held(30) && !Down(30) && !Held(44) && !Held(63, true) && AxisQuery(63, 0f) == 0f && AxisQuery(22, 0f) == 0f, "released actions reached the game");
            Check(Query(pad, "GetButton", 30, true) && Query(pad, "GetButtonDown", 44, true) && AxisQuery(22, .7f) == .7f, "keyboard horn, recover or look lost beside the session");

            // Horn: GetButtonDown(30) once, GetButton(30) while held, then GetButtonUp(30) once (SoundController's reads).
            source.Buttons[0][20] = true; Frame();
            Check(Down(30) && Held(30) && !Up(30), "shared horn press did not reach GetButtonDown(30): " + Status());
            Frame(); Check(!Down(30) && Held(30), "held horn repeated its press");
            source.Buttons[0][20] = false; Frame();
            Check(Up(30) && !Held(30), "shared horn release did not reach GetButtonUp(30)");
            Frame(); Check(!Up(30), "horn released twice");

            // Recover (reset): ShortcutManager arms on GetButtonDown(44) and acts after GetButton(44) stays held for 1 s.
            source.Buttons[0][22] = true; Frame();
            Check(Down(44) && Held(44), "shared reset did not reach GetButtonDown(44)");
            for (int i = 0; i < 30; i++) { Frame(); Check(!Down(44) && Held(44), "recover hold broke after " + i + " frames"); }
            source.Buttons[0][22] = false; Frame(); Check(!Held(44), "recover held after release");

            // Look back: GetAxis(63) negative and GetNegativeButton(63), never the forward pole.
            source.Buttons[0][21] = true; Frame();
            Check(AxisQuery(63, 0f) == -1f && Down(63, true) && Held(63, true) && !Held(63), "shared look back is not GetAxis(63) negative");
            source.Buttons[0][21] = false; Frame(); Check(AxisQuery(63, 0f) == 0f && !Held(63, true), "look back held after release");

            // Art-only actions read this frame's shared copy with Art's own bindings, and never read the device again.
            int reads = source.Reads;
            source.Buttons[0][23] = true; Frame();
            Check(Down(45) && Held(45), "Art-only repair puncture did not reach GetButtonDown(45)");
            Check(source.Reads == reads + 1, "Art-only actions read the device again");
            source.Buttons[0][23] = false; Frame(); Check(!Held(45), "repair puncture held after release");
            source.Buttons[0][24] = true; Frame();
            Check(Down(46) && Held(46), "Art-only quick restart did not reach GetButtonDown(46)");
            source.Buttons[0][24] = false; Frame(); Check(!Held(46), "quick restart held after release");
            foreach (var (angle, action, expected) in new[] { (27000u, 22, -1f), (9000u, 22, 1f), (0u, 63, 1f) })
            {
                source.Hat0[0] = angle; Frame();
                Check(AxisQuery(action, 0f) == expected, "Art-only look at " + angle + " did not reach GetAxis(" + action + ")");
                source.Hat0[0] = uint.MaxValue; Frame();
                Check(AxisQuery(action, 0f) == 0f, "Art-only look at " + angle + " held after release");
            }

            // The profile owns horn: Art's own horn binding on a free button stays unused.
            source.Buttons[0][25] = true; Frame(); Check(!Held(30) && !Down(30), "Art's horn binding was used beside the profile's horn");
            source.Buttons[0][25] = false; Frame();
        }
        finally { WheelInput.Close(); }
        Check(File.ReadAllText(Path.Combine(folder, "host-shutdown.json")).Contains("\"state\":\"resolved\""), "real-Core action stop did not resolve");
    }
}
