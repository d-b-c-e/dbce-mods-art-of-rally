using System;
using ArtOfSimRally.Mod.Shim;
using Dbce.Wheel.Input;
using Rewired;

namespace ArtOfSimRally.Mod
{
    internal static partial class SharedInputStartup
    {
        // Presentation reads the already admitted startup binding and identity
        // witnesses. It never polls, re-qualifies, changes a map or reads a file.
        internal static bool TryGlyphBinding(Joystick controller, string action, out ControlBinding binding)
        {
            binding = null;
            if (action == null) return false;
            int index = Array.IndexOf(ShimFrame.Names, action);
            if (index < 0 || !ShimInput.Consumer.Owns(index)) return false;
            if (!SharedBindingCaptureHost.Instance.TryActiveBinding(action, out var active) || active == null ||
                !TryGlyphDevice(controller, active.Device, active.Product)) return false;
            binding = active;
            return true;
        }

        internal static bool TryGlyphDevice(Joystick controller, Guid instance, Guid? productIdentity)
        {
            if (controller == null || phase != SharedInputStatus.Phase.Attached || Stopped ||
                !controller.isConnected || !ShimInput.Consumer.Started) return false;
            var admitted = session as SharedInputSession;
            if (admitted == null || admitted.Before == null || admitted.Owned == null || admitted.RewiredInstances == null || admitted.Isolation == null ||
                admitted.Inventory.ConnectionEpoch != admitted.EpochBefore || !admitted.Isolation.OwnsDisabled(controller)) return false;

            // The displayed controller may be the wheel while the effective
            // pedal binding belongs to a separate device. Both must be owned;
            // never equate DirectInput and Rewired instance GUID namespaces.
            bool displayed = false;
            foreach (var row in admitted.Before)
                if (row != null && ReferenceEquals(row.Token, controller) &&
                    Guid.TryParse(row.Instance, out var raw) && Array.IndexOf(admitted.RewiredInstances, raw) >= 0)
                    displayed = true;
            if (!displayed) return false;
            int devices = 0, joins = 0;
            foreach (var owned in admitted.Owned)
            {
                if (owned == null || owned.Instance != instance || productIdentity.HasValue && owned.Product != productIdentity.Value ||
                    owned.Generation == 0 || string.IsNullOrEmpty(owned.InterfacePath)) continue;
                devices++;
                foreach (var row in admitted.Before)
                    if (row != null && row.Token is Joystick bound && bound.isConnected &&
                        admitted.Isolation.OwnsDisabled(bound) &&
                        Guid.TryParse(row.Instance, out var raw) && Array.IndexOf(admitted.RewiredInstances, raw) >= 0 &&
                        Guid.TryParse(row.ProductGuid, out var product) && product == owned.Product &&
                        string.Equals(row.InterfacePath, owned.InterfacePath, StringComparison.OrdinalIgnoreCase)) joins++;
            }
            return devices == 1 && joins == 1;
        }
    }
}
