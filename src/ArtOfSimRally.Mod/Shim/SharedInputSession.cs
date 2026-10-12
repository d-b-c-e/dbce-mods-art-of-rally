using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArtOfSimRally.Mod.Shim
{
    // What the opt-in startup needs from a qualification session. Production uses SharedInputSession;
    // lifecycle fixtures replace only these hardware-facing steps.
    internal interface ISharedQualification
    {
        void Begin(string shimPath, string runFolder, byte[] exactProfile);
        string ProfileName { get; }
        string Transmission { get; }
        string[] DeviceNames { get; }
        void Isolate();
        bool VerifyIsolation();
        bool IsolationOverdue(int frame);
        string IsolationStatus { get; }
        string NeutralFailure();
        IPhysicalSnapshotSource Source { get; }
        IShimBridge Bridge { get; }
        bool RestoreIsolation();
        string Close();
        bool RestorePending { get; }
        bool RetryRestore();
        void EndObservation();
    }

    // The qualified copied-provider attach sequence, shared by the diagnostic probe and the opt-in
    // ordinary backend: Core's profile identities, metadata-only pins of Art's owned read slots (no
    // second physical read), the exact path/product join with Rewired, the identity lease and
    // exact-object isolation. Callers decide what each refusal means; nothing here publishes a frame.
    internal sealed class SharedInputSession : ISharedQualification
    {
        internal readonly RewiredInputIsolation Inventory;
        internal ShimBridgeNative Native { get; private set; }
        internal ExistingWheelSnapshots Source { get; private set; }
        internal NativeInputIsolation Isolation { get; private set; }
        internal Guid[] Instances { get; private set; }
        internal OwnedDeviceIdentity[] Owned { get; private set; }
        internal Guid[] RewiredInstances { get; private set; }
        internal JoystickObservation[] Before { get; private set; }
        internal long EpochBefore { get; private set; }
        private ProbeIdentityLease lease;
        private int beganFrame;
        private bool observing;

        internal SharedInputSession(RewiredInputIsolation inventory) { Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory)); }
        internal static bool Ready(RewiredInputIsolation inventory) => inventory.Ready && WheelInput.SharedProbeSlots.Length > 0;

        // Opens Core with the exact profile, pins Art's existing readers by metadata and path, and joins
        // them to Rewired. Throws on any refusal; Close() releases whatever was acquired.
        internal void Begin(string shimPath, string runFolder, byte[] exactProfile, ShimBridgeNative.LatchedSession latched = null)
        {
            Inventory.BeginIdentityObservation(); observing = true; EpochBefore = Inventory.ConnectionEpoch;
            Native = new ShimBridgeNative(shimPath, runFolder, exactProfile, latched);
            var expected = Native.BoundProducts(); var ids = new Guid[expected.Count]; expected.Keys.CopyTo(ids, 0); Instances = ids;
            Before = ProbeObservation.Capture(Inventory);
            Source = new ExistingWheelSnapshots(FfbNative.RequestedPath);
            // Metadata only: no second physical poll or device acquisition.
            var owned = new List<OwnedDeviceIdentity>();
            foreach (int slot in WheelInput.SharedProbeSlots)
            {
                var bytes = new byte[ShimBridge.DeviceSize];
                if (Source.Describe(slot, bytes) != 1 || BitConverter.ToUInt64(bytes, 8) == 0)
                    throw new InvalidOperationException("existing read slot metadata unavailable: " + slot);
                string path;
                var generation = BitConverter.ToUInt64(bytes, 8);
                if (!Source.TryGetPath(slot, generation, out path)) throw new InvalidOperationException("owned reader interface path unavailable for generation " + generation);
                var id = new byte[16]; Buffer.BlockCopy(bytes, 16, id, 0, 16);
                var product = new byte[16]; Buffer.BlockCopy(bytes, 32, product, 0, 16);
                Guid expectedProduct;
                if (expected.TryGetValue(new Guid(id), out expectedProduct) && expectedProduct != new Guid(product)) throw new InvalidOperationException("Core profile product differs from owned reader");
                Source.Pin(slot, bytes, path);
                owned.Add(new OwnedDeviceIdentity { Instance = new Guid(id), Product = new Guid(product), Generation = generation, InterfacePath = path });
            }
            Owned = owned.ToArray();
            RewiredInstances = ProbeIdentityMapper.Match(ids, Owned, Before);
            lease = new ProbeIdentityLease(Before, RewiredInstances, EpochBefore);
        }

        void ISharedQualification.Begin(string shimPath, string runFolder, byte[] exactProfile) => Begin(shimPath, runFolder, exactProfile);
        public string ProfileName => Native.ProfileValue("profile");
        public string Transmission => Native.Transmission();
        IPhysicalSnapshotSource ISharedQualification.Source => Source;
        public IShimBridge Bridge => Native;
        public string IsolationStatus => Isolation?.Status;
        public bool RestoreIsolation() => Isolation.Restore();
        // Names of the Rewired devices this session isolates, for the status text.
        public string[] DeviceNames
        {
            get
            {
                var names = new List<string>();
                foreach (var row in Before)
                    if (Array.IndexOf(RewiredInstances, Guid.Parse(row.Instance)) >= 0) names.Add(String.IsNullOrEmpty(row.Name) ? row.Instance : row.Name);
                return names.ToArray();
            }
        }
        // The disabled objects' cached values must already read neutral before any frame is published.
        public string NeutralFailure()
        {
            var check = new ProbeReceipt { Before = Before, Disabled = ProbeObservation.Capture(Inventory) };
            ProbeObservation.CompareDisabled(check, RewiredInstances);
            return check.FlagsDisabled && check.RawAxesNeutralAfterDisable ? null : check.RawAxisQualification;
        }

        public void Isolate() { Isolation = new NativeInputIsolation(RewiredInstances, Inventory); beganFrame = Time.frameCount; }
        public bool IsolationOverdue(int frame) => frame > beganFrame + 5;

        // Every use re-proves the pinned readers, the identity lease and the exact isolated objects.
        public bool VerifyIsolation()
        {
            Source.VerifyPinned(WheelInput.SharedProbeSlots);
            var observed = ProbeObservation.Capture(Inventory);
            var witnessed = lease.Verify(observed, Inventory.ConnectionEpoch, Isolation.OwnsDisabled);
            var current = ProbeIdentityMapper.Match(Instances, Owned, witnessed);
            if (!new HashSet<Guid>(current).SetEquals(RewiredInstances)) return false;
            bool clean = Isolation.Check(Time.frameCount);
            // A synchronous disable callback can itself signal a connection
            // change. Reject before publishing even within this same frame.
            if (Inventory.ConnectionEpoch != EpochBefore) throw new InvalidOperationException("Rewired connection epoch changed during isolation");
            return clean;
        }

        // A fresh exact join after the isolated objects were re-enabled.
        internal bool IdentityRestored(JoystickObservation[] restored)
        {
            Source.VerifyPinned(WheelInput.SharedProbeSlots);
            var current = lease.Verify(restored, Inventory.ConnectionEpoch, token => false);
            return new HashSet<Guid>(ProbeIdentityMapper.Match(Instances, Owned, current)).SetEquals(RewiredInstances);
        }

        // Releases a session that never attached: Core stop, flags this session changed, observation.
        // A failed restore leaves RestorePending set: the caller keeps the session and retries it.
        public string Close()
        {
            string failure = null;
            try { Native?.Stop(); } catch (Exception ex) { failure = "stop failed: " + ex.Message; }
            try { if (Isolation != null && !Isolation.Restore()) { RestorePending = true; failure = (failure == null ? "" : failure + "; ") + Isolation.Status; } }
            catch (Exception ex) { RestorePending = true; failure = (failure == null ? "" : failure + "; ") + "restore failed: " + ex.Message; }
            EndObservation();
            return failure;
        }
        public bool RestorePending { get; private set; }
        // Only the flags this session wrote and could not yet re-enable are retried.
        public bool RetryRestore()
        {
            if (!RestorePending) return true;
            try { RestorePending = !Isolation.Restore(); } catch { RestorePending = true; }
            return !RestorePending;
        }
        public void EndObservation() { if (observing) { observing = false; Inventory.EndIdentityObservation(); } }
    }
}
