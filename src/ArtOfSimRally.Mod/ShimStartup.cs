using System;
using System.Collections.Generic;
using System.IO;
using ArtOfSimRally.Mod.Shim;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    // Temporary diagnostic entry point. Normal launches never construct a shim
    // or inspect a profile. No Settings field or permanent Apply ceremony.
    internal static class ShimStartup
    {
        private static readonly string requestPath = Environment.GetEnvironmentVariable("DBCE_ART_SHIM_REQUEST");
        internal static bool Requested => !String.IsNullOrEmpty(requestPath);
        private static ProbeRequest request;
        private static ProbeReceipt receipt;
        private static ShimBridgeNative native;
        private static ExistingWheelSnapshots source;
        private static NativeInputIsolation isolation;
        private static ProbeIdentityLease identityLease;
        private static readonly RewiredInputIsolation inventory = new RewiredInputIsolation();
        private static bool finished, attached;
        private static int beganFrame, applyFrames;
        internal static string NativeLogPath => request == null ? null : Path.Combine(request.Result, "native.log");
        internal static bool Prepare(string modDirectory)
        {
            if (!Requested) return true;
            try
            {
                request = ProbeRequest.Read<ProbeRequest>(requestPath);
                request.Validate(DateTime.UtcNow, Path.GetDirectoryName(Application.dataPath), typeof(Main).Assembly.Location,
                    Path.Combine(modDirectory, "UnityForceFeedback.dll"), Path.Combine(modDirectory, "Settings.xml"));
                receipt = new ProbeReceipt(request);
                if (!Main.Settings.WheelInputEnabled || Main.Settings.UseDirectInputBackend)
                    throw new InvalidOperationException("probe requires existing wheel input enabled and unchanged RawInput backend");
                return true;
            }
            catch (Exception ex) { ModLog.Error("Shared input probe startup refused: " + ex.Message); return false; }
        }
        internal static bool Arm()
        {
            if (!Requested) return true;
            try
            {
                ProbeRequest.Pin(FfbNative.RequestedPath, request.NativeSha256);
                ProbeNoForce.Arm(FfbNative.RequestedPath);
                receipt.NoForceArmed = true;
                ModLog.Info("Shared input " + request.Mode + " PROBE: native no-force latch armed; settings writes suppressed; remain in menus.");
                return true;
            }
            catch (Exception ex) { Finish("no-force latch refused: " + ex.Message); return false; }
        }
        internal static void Tick()
        {
            if (!Requested || finished || receipt == null) return;
            try
            {
                if (DateTime.UtcNow >= request.Deadline) { Finish("probe deadline reached"); return; }
                if (!Main.Enabled || GameState.IsDriving) { Finish("probe stopped: mod disabled or driving started; menu-only qualification"); return; }
                if (attached)
                {
                    if (ShimInput.Consumer.Terminal) { Finish("Apply failed: " + ShimInput.Consumer.Status); return; }
                    if (ShimInput.Consumer.Started)
                    {
                        receipt.PublishedFrames = ++applyFrames;
                        receipt.LastAxisStatuses = new string[5]; receipt.LastAxisValues = new float[5];
                        for (int i = 0; i < 5; i++)
                        {
                            receipt.LastAxisStatuses[i] = ShimInput.Consumer.OriginalFrame[i].Status.ToString();
                            receipt.LastAxisValues[i] = ShimInput.Consumer.OriginalFrame[i].Value;
                        }
                        if (applyFrames >= 120) Finish("Apply probe completed 120 published menu frames; car/keyboard/FFB coexistence unqualified");
                    }
                    return;
                }
                if (isolation == null)
                {
                    if (!inventory.Ready || WheelInput.SharedProbeSlots.Length == 0) return;
                    inventory.BeginIdentityObservation(); receipt.IdentityEpochBefore = inventory.ConnectionEpoch;
                    native = new ShimBridgeNative(request.ShimPath, request.Result, File.ReadAllBytes(request.ProfilePath));
                    var expected = native.BoundProducts(); var ids = new Guid[expected.Count]; expected.Keys.CopyTo(ids, 0); receipt.Instances = ids;
                    receipt.Before = ProbeObservation.Capture(inventory);
                    source = new ExistingWheelSnapshots(FfbNative.RequestedPath);
                    // Metadata only: no second physical poll or device acquisition.
                    var owned = new List<OwnedDeviceIdentity>();
                    foreach (int slot in WheelInput.SharedProbeSlots)
                    {
                        var bytes = new byte[ShimBridge.DeviceSize];
                        if (source.Describe(slot, bytes) != 1 || BitConverter.ToUInt64(bytes, 8) == 0)
                            throw new InvalidOperationException("existing read slot metadata unavailable: " + slot);
                        string path;
                        var generation = BitConverter.ToUInt64(bytes, 8);
                        if (!source.TryGetPath(slot, generation, out path)) throw new InvalidOperationException("owned reader interface path unavailable for generation " + generation);
                        var id = new byte[16]; Buffer.BlockCopy(bytes, 16, id, 0, 16);
                        var product = new byte[16]; Buffer.BlockCopy(bytes, 32, product, 0, 16);
                        Guid expectedProduct;
                        if (expected.TryGetValue(new Guid(id), out expectedProduct) && expectedProduct != new Guid(product)) throw new InvalidOperationException("Core profile product differs from owned reader");
                        source.Pin(slot, bytes, path);
                        owned.Add(new OwnedDeviceIdentity { Instance = new Guid(id), Product = new Guid(product), Generation = generation, InterfacePath = path });
                    }
                    receipt.OwnedDevices = owned.ToArray();
                    receipt.RewiredInstances = ProbeIdentityMapper.Match(ids, receipt.OwnedDevices, receipt.Before);
                    identityLease = new ProbeIdentityLease(receipt.Before, receipt.RewiredInstances, receipt.IdentityEpochBefore);
                    receipt.IdentityQualification = "exact live owned path/product admission; while disabled use captured identity only with unchanged object, extension, GUID, connected state and event epoch; raw observations remain unmodified";
                    request.ValidateApply(request.Mode == "Apply" ? ProbeRequest.Read<ProbeReceipt>(request.ObserveReceipt) : null, ids, receipt.OwnedDevices, receipt.RewiredInstances);
                    isolation = new NativeInputIsolation(receipt.RewiredInstances, inventory); beganFrame = Time.frameCount;
                }
                bool clean = VerifyIsolation();
                if (!clean)
                {
                    if (Time.frameCount > beganFrame + 5) Finish(isolation.Status);
                    return;
                }
                receipt.IdentityQualified = true;
                receipt.Disabled = ProbeObservation.Capture(inventory);
                ProbeObservation.CompareDisabled(receipt, receipt.RewiredInstances);
                if (request.Mode == "Observe") { Finish("Observe complete: " + receipt.RawAxisQualification); return; }
                if (!receipt.FlagsDisabled || !receipt.RawAxesNeutralAfterDisable) { Finish("Apply refused: current disable/raw-axis check unqualified"); return; }
                WheelInput.AttachSharedInput(source, native, VerifyIsolation, () => isolation.Restore(), native.Transmission());
                attached = true; receipt.Applied = true;
                ModLog.Info("Shared input Apply PROBE attached to existing readers for 120 menu frames; saved backend unchanged.");
            }
            catch (Exception ex) { Finish("probe refused: " + ex.Message); }
        }
        internal static void Shutdown() { if (Requested && !finished && receipt != null) Finish("probe shutdown"); }
        private static bool VerifyIsolation()
        {
            source.VerifyPinned(WheelInput.SharedProbeSlots);
            var observed = ProbeObservation.Capture(inventory);
            var witnessed = identityLease.Verify(observed, inventory.ConnectionEpoch, isolation.OwnsDisabled);
            var current = ProbeIdentityMapper.Match(receipt.Instances, receipt.OwnedDevices, witnessed);
            if (!new HashSet<Guid>(current).SetEquals(receipt.RewiredInstances)) return false;
            bool clean = isolation.Check(Time.frameCount);
            // A synchronous disable callback can itself signal a connection
            // change. Reject before publishing even within this same frame.
            if (inventory.ConnectionEpoch != receipt.IdentityEpochBefore) throw new InvalidOperationException("Rewired connection epoch changed during isolation");
            return clean;
        }
        private static void Finish(string status)
        {
            if (finished) return;
            finished = true;
            try
            {
                if (attached) WheelInput.StopSharedProbe();
                else if (native != null) native.Stop();
            }
            catch (Exception ex) { status += "; stop failed: " + ex.Message; }
            try
            {
                bool restored = isolation != null && isolation.Restore();
                if (receipt.Before != null) receipt.Restored = ProbeObservation.Capture(inventory);
                receipt.FlagsRestored = restored && ProbeObservation.Restored(receipt.Before, receipt.Restored);
                if (receipt.FlagsRestored && identityLease != null)
                {
                    source.VerifyPinned(WheelInput.SharedProbeSlots);
                    var current = identityLease.Verify(receipt.Restored, inventory.ConnectionEpoch, token => false);
                    receipt.IdentityRestored = new HashSet<Guid>(ProbeIdentityMapper.Match(receipt.Instances, receipt.OwnedDevices, current)).SetEquals(receipt.RewiredInstances);
                }
            }
            catch (Exception ex) { status += "; restore unqualified: " + ex.Message; }
            receipt.IdentityEpochAfter = inventory.ConnectionEpoch;
            try { inventory.EndIdentityObservation(); } catch (Exception ex) { status += "; event cleanup failed: " + ex.Message; }
            receipt.Status = status; receipt.CompletedUtc = DateTime.UtcNow.ToString("o");
            try { ProbeRequest.Write(Path.Combine(request.Result, "probe-receipt.json"), receipt); }
            catch (Exception ex) { ModLog.Error("Probe receipt write failed: " + ex.Message); }
            ModLog.Info("Shared input probe: " + status + "; flags restored=" + receipt.FlagsRestored + ". Native no-force remains latched until exit.");
        }
    }
}
