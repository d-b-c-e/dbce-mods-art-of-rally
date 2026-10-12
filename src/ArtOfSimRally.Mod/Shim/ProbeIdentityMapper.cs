using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArtOfSimRally.Mod.Shim
{
    [DataContract]
    internal sealed class OwnedDeviceIdentity
    {
        [DataMember] public Guid Instance = Guid.Empty, Product = Guid.Empty;
        [DataMember] public ulong Generation = 0;
        [DataMember] public string InterfacePath = null;
    }
    internal static class ProbeIdentityMapper
    {
        internal static bool SameBindings(OwnedDeviceIdentity[] before, OwnedDeviceIdentity[] current)
        {
            if (before == null || current == null || before.Length != current.Length) return false;
            var seen = new HashSet<Guid>();
            foreach (var item in before)
            {
                if (item == null || item.Generation == 0 || !seen.Add(item.Instance)) return false;
                int matches = 0;
                foreach (var now in current)
                    if (now != null && now.Generation != 0 && item.Instance == now.Instance && item.Product == now.Product &&
                        !String.IsNullOrEmpty(item.InterfacePath) && String.Equals(item.InterfacePath, now.InterfacePath, StringComparison.OrdinalIgnoreCase)) matches++;
                if (matches != 1) return false;
            }
            // Source generations legitimately differ between cold processes.
            return true;
        }
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
                    throw new InvalidOperationException("Rewired identity/path unavailable: cannot exclude a duplicate mapped wheel");
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
