using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArtOfSimRally.Mod.Shim
{
    internal interface IRawIsolationJoystick : IIsolationJoystick
    {
        string Name { get; }
        float[] ReadRawAxes();
    }
    internal interface IRawIdentityDiagnostic
    {
        string InterfacePath { get; }
        string ProductGuid { get; }
    }
    internal interface IIdentityLifetimeDiagnostic
    {
        object ExtensionToken { get; }
        bool Connected { get; }
    }
    [DataContract]
    internal sealed class JoystickObservation
    {
        [DataMember] public string Name, Instance, RawError, InterfacePath, ProductGuid, IdentityPathError;
        [DataMember] public bool Enabled;
        [DataMember] public bool Connected;
        [DataMember] public float[] RawAxes;
        internal object Token;
        internal object ExtensionToken;
    }
    internal static class ProbeObservation
    {
        internal static JoystickObservation[] Capture(IJoystickInventory inventory)
        {
            if (!inventory.Ready) throw new InvalidOperationException("Rewired not ready");
            var result = new List<JoystickObservation>();
            foreach (var item in inventory.Joysticks)
            {
                if (item == null || result.Count == 256) throw new InvalidOperationException("incomplete joystick inventory");
                var row = new JoystickObservation { Token = item.Token, Instance = item.Instance.ToString("D"), Enabled = item.Enabled };
                try
                {
                    var identity = item as IRawIdentityDiagnostic;
                    if (identity != null) { row.InterfacePath = identity.InterfacePath; row.ProductGuid = identity.ProductGuid; }
                    var lifetime = item as IIdentityLifetimeDiagnostic;
                    if (lifetime != null) { row.ExtensionToken = lifetime.ExtensionToken; row.Connected = lifetime.Connected; }
                }
                catch (Exception ex) { row.IdentityPathError = ex.Message; }
                try
                {
                    var raw = item as IRawIsolationJoystick;
                    if (raw == null) throw new InvalidOperationException("raw axis API unavailable");
                    row.Name = raw.Name; row.RawAxes = raw.ReadRawAxes();
                    if (row.RawAxes == null || row.RawAxes.Length > 256) throw new InvalidOperationException("raw axis count invalid");
                    foreach (var value in row.RawAxes) if (Single.IsNaN(value) || Single.IsInfinity(value)) throw new InvalidOperationException("raw axis is not finite");
                }
                catch (Exception ex) { row.RawAxes = null; row.RawError = ex.Message; }
                result.Add(row);
            }
            return result.ToArray();
        }
        internal static void CompareDisabled(ProbeReceipt receipt, Guid[] owned)
        {
            var ids = new HashSet<Guid>(owned); bool nonzero = false, axes = true, flags = true;
            if (receipt.Before == null || receipt.Disabled == null || receipt.Before.Length != receipt.Disabled.Length) throw new InvalidOperationException("Rewired inventory changed during probe");
            foreach (var before in receipt.Before)
            {
                var after = Match(before, receipt.Disabled);
                bool mapped = ids.Contains(Guid.Parse(before.Instance));
                flags &= mapped ? !after.Enabled : before.Enabled == after.Enabled;
                if (!mapped) continue;
                if (before.RawAxes == null || after.RawAxes == null || before.RawAxes.Length != after.RawAxes.Length) { axes = false; continue; }
                for (int i = 0; i < after.RawAxes.Length; i++)
                {
                    if (after.RawAxes[i] != 0f) axes = false;
                    if (before.Enabled && before.RawAxes[i] != 0f && after.RawAxes[i] == 0f) nonzero = true;
                }
            }
            receipt.FlagsDisabled = flags;
            receipt.RawAxesNeutralAfterDisable = axes;
            receipt.NonzeroCacheClearObserved = axes && nonzero;
            receipt.RawAxisQualification = !axes ? "unqualified: raw axes unavailable or nonzero after disable" :
                nonzero ? "observed: existing nonzero raw axis cleared after disable" : "inconclusive: raw axes neutral before and after; no movement requested";
        }
        internal static bool Restored(JoystickObservation[] before, JoystickObservation[] after)
        {
            if (before == null || after == null || before.Length != after.Length) return false;
            try { foreach (var row in before) if (row.Enabled != Match(row, after).Enabled) return false; }
            catch { return false; }
            return true;
        }
        private static JoystickObservation Match(JoystickObservation before, JoystickObservation[] after)
        {
            JoystickObservation found = null;
            foreach (var candidate in after)
                if (ReferenceEquals(before.Token, candidate.Token) && before.Instance == candidate.Instance)
                { if (found != null) throw new InvalidOperationException("duplicate joystick token"); found = candidate; }
            return found ?? throw new InvalidOperationException("joystick identity changed during probe");
        }
    }
}
