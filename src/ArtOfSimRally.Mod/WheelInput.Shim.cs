using System;
using System.Collections.Generic;
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
        internal static int[] SharedProbeSlots
        {
            get
            {
                if (!_open || _devices.Count == 0) return new int[0];
                var result = new int[_devices.Count];
                for (int i = 0; i < result.Length; i++) result[i] = _devices[i].Slot;
                return result;
            }
        }
        internal static void StopSharedProbe() => StopSharedInput();

        // Art channels with a shared action. Channels without one (camera tuning,
        // handbrake button) keep Art's own bindings on the same copied read.
        private static readonly Dictionary<Channel, int> SharedActions = new Dictionary<Channel, int>
        {
            { Channel.Confirm, 9 }, { Channel.Back, 10 }, { Channel.Start, 11 },
            { Channel.NavUp, 13 }, { Channel.NavDown, 14 }, { Channel.NavLeft, 15 }, { Channel.NavRight, 16 },
            { Channel.CameraSwitch, 17 }, { Channel.SettingsButton, 21 }, { Channel.StopFfbButton, 22 },
        };
        internal static bool SharedOwns(Channel channel) => SharedActions.TryGetValue(channel, out int action) && ShimInput.Consumer.Owns(action);
        // True when a started shared session owns the channel's action. Availability and
        // value then come only from this frame's shared input; once the session is
        // terminal the channel stays neutral rather than falling back to Art's binding.
        internal static bool SharedChannel(Channel channel, out bool available, out float value)
        {
            available = false; value = 0f;
            if (!SharedActions.TryGetValue(channel, out int action) || !ShimInput.Consumer.Owns(action)) return false;
            EnsureSharedFrame();
            available = ShimInput.Consumer.Live(action, Time.frameCount) || ShimInput.Consumer.KnownNeutral(action, Time.frameCount);
            value = ShimInput.Consumer.Value(action, Time.frameCount);
            return true;
        }
        // The first shared-input consumer in a Unity frame (car input, physics gear step,
        // Rewired query or watchdog) publishes this frame's snapshot; later ones reuse it.
        // Game scripts can run before the watchdog, so freshness must not depend on order.
        internal static void EnsureSharedFrame()
        {
            if (sharedInput != null && _sampledFrame != Time.frameCount) ReadSnapshot();
        }

        // Explicit muted probe handoff only; no saved backend flag. Ordinary
        // copied-provider mode is used: extension 1 has no capture/test fences.
        // The shared profile's transmission (Core's parse), fixed for the session.
        internal static ShimTransmission.Mode SharedTransmission { get; private set; }
        internal static void AttachSharedInput(IPhysicalSnapshotSource source, IShimBridge native, Func<bool> isolation, Func<bool> restore = null, string transmission = null)
        {
            if (sharedInput != null || !_open || _devices.Count == 0 || isolation == null || !isolation())
                throw new InvalidOperationException("shared input needs an idle, qualified reader and native isolation");
            var slots = new int[_devices.Count]; for (int i = 0; i < slots.Length; i++) slots[i] = _devices[i].Slot;
            try { sharedInput = new ShimBridge(source, native, ShimInput.Consumer, slots); }
            catch { if (restore != null) restore(); throw; }
            verifyNativeIsolation = isolation; restoreNativeInput = restore;
            SharedTransmission = ShimTransmission.ParseMode(transmission);
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
            // Released (a close) or lost once: the game's mixed values can no longer be trusted, so driving
            // stays neutral for good, keyboard included.
            if (isolationGone) return true;
            bool isolated;
            try { isolated = verifyNativeIsolation(); } catch { isolated = false; }
            if (!isolated)
            {
                isolationGone = true;
                try { sharedInput.Stop("native input isolation lost"); } catch { }
                return true;
            }
            // Isolation verified this frame: the game's independent (keyboard) input may be used.
            ShimInput.IsolatedFrame = Time.frameCount;
            // A transport-only stop (Core, frame or provider refusal) keeps the wheel neutral and Core
            // untouched, but leaves the verified keyboard path; nothing re-enables the wheel.
            if (sharedInput.Stopped) return true;
            try
            {
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

        // Force feedback reconnect: reader slots may share the old FFB handle, so every reader closes
        // first. An attached shared session stops for good with this stated cause; it never reattaches.
        internal const string ForceReconnectStop = "a force feedback reconnect closed the wheel readers";
        internal static void ReleaseForForceReconnect() => CloseReaders(ForceReconnectStop);
        internal static void ReopenAfterForceReconnect() { if (Enabled) Open(); }

        // Art-only buttons beside a started shared session, from this frame's shared copy (no second read).
        internal static float ArtOnlyValue(Channel c)
        {
            var cfg = Main.Settings;
            if (cfg == null || !SharedArtOnly(c) || (!cfg.WheelInputEnabled && !IsShortcut(c)) || !_bindings.TryGetValue(c, out var b)) return 0f;
            EnsureSharedFrame();
            var d = Resolve(b);
            if (d == null || !d.Ok) return 0f;
            if (b.IsHat) return b.HatPressed(d.Hats[b.Element]) ? 1f : 0f;
            return b.IsButton && b.Element < ButtonCount && d.Buttons[b.Element] != 0 ? 1f : 0f;
        }

        // What Art's settings show in place of an editor for a control the shared profile may set.
        internal static string SharedEditorText(Channel c)
        {
            if (!ShimInput.Consumer.Started) return "Set by the shared profile once it is in use.";
            int action = c <= Channel.Handbrake ? (int)c : SharedActions.TryGetValue(c, out int mapped) ? mapped : -1;
            if (action >= 0 && ShimInput.Consumer.Owns(action)) return "Set by the shared profile.";
            // An unmapped axis is left to the game; an unmapped button keeps Art's binding on the shared copy.
            if (c > Channel.Handbrake && IsBound(c) && (IsShortcut(c) || (Main.Settings != null && Main.Settings.WheelInputEnabled)))
                return "Not in the shared profile; Art's saved binding applies: " + Describe(c) + ".";
            return "Not in the shared profile; the game's own controls apply.";
        }

        // Why the shared session stopped (first cause only), for a truthful status after any later close.
        internal static string SharedStopReason => sharedInput?.Failure;
        private static bool isolationGone;
        // A stopped session whose own isolation still verifies: keyboard driving continues, wheel neutral.
        internal static bool SharedKeyboardRetained => sharedInput != null && sharedInput.Stopped && !isolationGone;
        private static void StopSharedInput(string reason = "Art reader closed")
        {
            if (sharedInput == null) return;
            try { sharedInput.Stop(reason); } catch (Exception ex) { ModLog.Error("Shared input stop: " + ex.Message); }
            isolationGone = true; // released below; from now on driving stays neutral
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
