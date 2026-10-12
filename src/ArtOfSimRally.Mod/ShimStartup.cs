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
                    native = new ShimBridgeNative(request.ShimPath, request.Result, File.ReadAllBytes(request.ProfilePath));
                    var ids = native.BoundInstances(); receipt.Instances = ids;
                    source = new ExistingWheelSnapshots(FfbNative.RequestedPath);
                    // Metadata only: no second physical poll or device acquisition.
                    var found = new HashSet<Guid>();
                    foreach (int slot in WheelInput.SharedProbeSlots)
                    {
                        var bytes = new byte[ShimBridge.DeviceSize];
                        if (source.Describe(slot, bytes) != 1 || BitConverter.ToUInt64(bytes, 8) == 0)
                            throw new InvalidOperationException("existing read slot metadata unavailable: " + slot);
                        var id = new byte[16]; Buffer.BlockCopy(bytes, 16, id, 0, 16); found.Add(new Guid(id));
                    }
                    foreach (var id in ids) if (!found.Contains(id)) throw new InvalidOperationException("Core-bound identity has no existing WheelFfb reader: " + id);
                    request.ValidateApply(request.Mode == "Apply" ? ProbeRequest.Read<ProbeReceipt>(request.ObserveReceipt) : null, ids);
                    receipt.Before = ProbeObservation.Capture(inventory);
                    isolation = new NativeInputIsolation(ids, inventory); beganFrame = Time.frameCount;
                }
                bool clean = isolation.Check(Time.frameCount);
                if (!clean)
                {
                    if (Time.frameCount > beganFrame + 5) Finish(isolation.Status);
                    return;
                }
                receipt.IdentityQualified = true;
                receipt.Disabled = ProbeObservation.Capture(inventory);
                ProbeObservation.CompareDisabled(receipt, receipt.Instances);
                if (request.Mode == "Observe") { Finish("Observe complete: " + receipt.RawAxisQualification); return; }
                if (!receipt.FlagsDisabled || !receipt.RawAxesNeutralAfterDisable) { Finish("Apply refused: current disable/raw-axis check unqualified"); return; }
                WheelInput.AttachSharedInput(source, native, () => isolation.Check(Time.frameCount), () => isolation.Restore());
                attached = true; receipt.Applied = true;
                ModLog.Info("Shared input Apply PROBE attached to existing readers for 120 menu frames; saved backend unchanged.");
            }
            catch (Exception ex) { Finish("probe refused: " + ex.Message); }
        }
        internal static void Shutdown() { if (Requested && !finished && receipt != null) Finish("probe shutdown"); }
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
            }
            catch (Exception ex) { status += "; restore unqualified: " + ex.Message; }
            receipt.Status = status; receipt.CompletedUtc = DateTime.UtcNow.ToString("o");
            try { ProbeRequest.Write(Path.Combine(request.Result, "probe-receipt.json"), receipt); }
            catch (Exception ex) { ModLog.Error("Probe receipt write failed: " + ex.Message); }
            ModLog.Info("Shared input probe: " + status + "; flags restored=" + receipt.FlagsRestored + ". Native no-force remains latched until exit.");
        }
    }
}
