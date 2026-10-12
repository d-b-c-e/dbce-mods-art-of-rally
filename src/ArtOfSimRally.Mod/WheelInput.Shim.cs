using System;
using ArtOfSimRally.Mod.Shim;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    internal static partial class WheelInput
    {
        private static ShimBridge sharedInput;
        private static Func<bool> verifyNativeIsolation;
        private static Func<bool> restoreNativeInput;
        private static int sharedFrame = -1;

        // Source-only handoff: no production caller and no saved backend flag.
        // The reviewed bootstrap must first isolate these exact wheel identities
        // from Rewired, leaving keyboard input independent. A test-session or
        // recorder bootstrap is not supported by copied-provider extension 1.
        internal static void AttachSharedInput(IPhysicalSnapshotSource source, IShimBridge native, Func<bool> isolation, Func<bool> restore = null)
        {
            if (sharedInput != null || !_open || _devices.Count == 0 || isolation == null || !isolation())
                throw new InvalidOperationException("shared input needs an idle, qualified reader and native isolation");
            var slots = new int[_devices.Count]; for (int i = 0; i < slots.Length; i++) slots[i] = _devices[i].Slot;
            try { sharedInput = new ShimBridge(source, native, ShimInput.Consumer, slots); }
            catch { if (restore != null) restore(); throw; }
            verifyNativeIsolation = isolation; restoreNativeInput = restore;
        }

        private static bool ReadSharedSnapshot()
        {
            if (sharedInput == null) return false;
            if (sharedFrame == Time.frameCount) return true;
            sharedFrame = Time.frameCount;
            // After handoff a failure cannot fall back to a second physical read
            // or revive the former legacy sample. The session needs a restart.
            foreach (var d in _devices)
            {
                d.Ok = false; Array.Clear(d.Axes, 0, d.Axes.Length); Array.Clear(d.Buttons, 0, d.Buttons.Length);
                for (int i = 0; i < d.Hats.Length; i++) d.Hats[i] = -1;
            }
            try
            {
                if (!verifyNativeIsolation()) { sharedInput.Stop("native input isolation lost"); return true; }
                ShimInput.IsolatedFrame = Time.frameCount;
                sharedInput.ReadFrame(Time.frameCount, (slot, bytes) =>
                {
                    foreach (var d in _devices) if (d.Slot == slot)
                    {
                        d.Ok = bytes[50] == 1;
                        if (!d.Ok) return;
                        for (int i = 0; i < 8; i++) d.Axes[i] = BitConverter.ToInt32(bytes, 52 + i * 4);
                        for (int i = 0; i < 128; i++) d.Buttons[i] = (byte)((bytes[84 + i] & 0x80) != 0 ? 1 : 0);
                        for (int i = 0; i < 4; i++) { uint hat = BitConverter.ToUInt32(bytes, 212 + i * 4); d.Hats[i] = hat <= 35999 ? (int)hat : -1; }
                        return;
                    }
                });
            }
            catch (Exception ex) { try { sharedInput.Stop(ex.Message); } catch { } }
            return true;
        }

        private static void StopSharedInput()
        {
            if (sharedInput == null) return;
            try { sharedInput.Stop("Art reader closed"); } catch (Exception ex) { ModLog.Error("Shared input stop: " + ex.Message); }
            try
            {
                if (restoreNativeInput != null)
                {
                    if (restoreNativeInput()) restoreNativeInput = null;
                    else ModLog.Error("Shared input: native joystick restoration remains incomplete");
                }
            }
            catch (Exception ex) { ModLog.Error("Shared input restore: " + ex.Message); }
            // Retain ownership through failures and toggles; only a process
            // restart may return to a legacy or a newly qualified backend.
        }
    }
}
