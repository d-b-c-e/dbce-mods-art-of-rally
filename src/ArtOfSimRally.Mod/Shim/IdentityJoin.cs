using System;
using System.Collections.Generic;

namespace ArtOfSimRally.Mod.Shim
{
    // One of Art's open read slots: the DirectInput instance and product, the slot's generation and its interface path.
    internal sealed class OwnedDeviceIdentity
    {
        public Guid Instance = Guid.Empty, Product = Guid.Empty;
        public ulong Generation = 0;
        public string InterfacePath = null;
    }

    // Which Rewired joysticks are the devices the shared profile binds.
    internal static class IdentityJoin
    {
        // DirectInput and RawInput instance GUIDs are separate namespaces.
        // Join only exact owned-handle interface path AND product identity.
        // Ordinal case folding is the sole path normalization: no substrings,
        // name/index, VID/PID-only match, or removal of collection/interface IDs.
        internal static Guid[] Match(Guid[] required, OwnedDeviceIdentity[] owned, JoystickObservation[] rewired)
        {
            if (required == null || required.Length == 0 || owned == null || rewired == null) throw new InvalidOperationException("identity inventory missing");
            var paths = new Dictionary<string, OwnedDeviceIdentity>(StringComparer.OrdinalIgnoreCase);
            var ids = new HashSet<Guid>();
            foreach (var item in owned)
            {
                if (item.Instance == Guid.Empty || item.Product == Guid.Empty || item.Generation == 0 || String.IsNullOrEmpty(item.InterfacePath) ||
                    !ids.Add(item.Instance) || paths.ContainsKey(item.InterfacePath)) throw new InvalidOperationException("owned reader identity/path is missing or ambiguous");
                paths.Add(item.InterfacePath, item);
            }
            var wanted = new HashSet<Guid>(required);
            if (wanted.Contains(Guid.Empty) || wanted.Count != required.Length) throw new InvalidOperationException("Core identities invalid");
            foreach (var id in required) if (!ids.Contains(id)) throw new InvalidOperationException("Core-bound identity has no owned reader");
            var found = new HashSet<Guid>(); var mapped = new HashSet<Guid>();
            var rawIds = new Dictionary<Guid, JoystickObservation>();
            foreach (var row in rewired)
            {
                Guid raw, product;
                if (!Guid.TryParse(row.Instance, out raw) || raw == Guid.Empty || !Guid.TryParse(row.ProductGuid, out product) || product == Guid.Empty || String.IsNullOrEmpty(row.InterfacePath))
                    throw new InvalidOperationException("Rewired identity/path unavailable" + (String.IsNullOrEmpty(row.IdentityPathError) ? "" : " (" + row.IdentityPathError + ")") +
                        ": cannot exclude a duplicate mapped wheel");
                JoystickObservation previous;
                if (rawIds.TryGetValue(raw, out previous) && (!String.Equals(previous.InterfacePath, row.InterfacePath, StringComparison.OrdinalIgnoreCase) || Guid.Parse(previous.ProductGuid) != product))
                    throw new InvalidOperationException("Rewired instance GUID identifies different interfaces");
                rawIds[raw] = row;
                OwnedDeviceIdentity input;
                if (!paths.TryGetValue(row.InterfacePath, out input)) continue;
                if (input.Product != product) throw new InvalidOperationException("owned HID path has a different Rewired product identity");
                if (wanted.Contains(input.Instance)) { found.Add(input.Instance); mapped.Add(raw); }
            }
            if (!found.SetEquals(wanted)) throw new InvalidOperationException("Core-owned HID path not observed in Rewired; no identity fallback");
            var result = new Guid[mapped.Count]; mapped.CopyTo(result); return result;
        }
    }
}
