using System;
using ArtOfSimRally.Mod.Shim;
using Dbce.Wheel.Ui;
using Rewired;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    internal static partial class WheelInput
    {
        // Read the binding already loaded by the actual legacy fallback. Do not
        // parse Settings.xml or the editor's Saved/Draft values from a prompt.
        internal static bool TryGlyphFallback(Joystick controller, string sharedId, Channel channel, out string label)
        {
            label = null;
            var consumer = ShimInput.Consumer;
            int index = sharedId == null ? -1 : Array.IndexOf(ShimFrame.Names, sharedId);
            if (!Enabled || Main.Settings == null || !Main.Settings.WheelInputEnabled || !consumer.Started || consumer.Terminal ||
                consumer.LatchedDelivery(Time.frameCount) || index >= 0 && consumer.Owns(index) ||
                !_bindings.TryGetValue(channel, out var binding) || !binding.InstanceGuid.HasValue || SharedDuplicate(channel) != null) return false;
            var device = Resolve(binding); // existing reader cache only
            if (device == null || !device.Ok || device.InstanceGuid != binding.InstanceGuid ||
                !SharedInputStartup.TryGlyphDevice(controller, binding.InstanceGuid.Value, null)) return false;
            // NativeAction supplies only real Art routes. Unowned shared actions
            // and Art-only channels retain their existing copied-snapshot path.
            label = binding.IsHat ? PromptGlyphLabel.Hat(binding.Element, binding.Rest) :
                PromptGlyphLabel.Element(binding.IsButton ? ControlNames.Button(binding.Element) : ControlNames.Axis(binding.Element));
            return true;
        }
    }
}
