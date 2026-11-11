using System;
using System.Collections.Generic;
using System.Text;
using ArtOfSimRally.Mod.Shim;
using Dbce.Wheel.Ffb;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// The DirectInput read slots the shared profile's session borrows, and that session's read once per Unity frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The game reads the wheel through Rewired's Raw Input backend, which cannot read some devices at all (a Fanatec
    /// direct-drive base reports 32 axes and never moves one), and switching Rewired to DirectInput at runtime left the
    /// menus dead (2026-09-01 and 09-02). So the native plugin opens every DirectInput controller non-exclusively here,
    /// and the shared Core (<c>dbce_shim.dll</c>) reads those same slots through the bridge.
    /// </para>
    /// <para>
    /// While <see cref="SharedInputStartup"/> has a session pending or attached the readers stay open. Until it attaches
    /// they are only kept healthy: read each frame, and reopened while paused when one stops responding. After the attach
    /// each frame's first consumer (car input, a gear step, a Rewired query or the watchdog) reads every slot once and
    /// publishes one shared frame; later consumers reuse it. A stop is final until the game restarts.
    /// </para>
    /// </remarks>
    internal static class WheelReaders
    {
        private const int AxisCount = 8, ButtonCount = 128;
        private sealed class Device
        {
            public int Slot, Index;
            public string Name;
            public Guid? InstanceGuid;
            public readonly int[] Axes = new int[AxisCount];
            public readonly byte[] Buttons = new byte[ButtonCount];
            public bool Ok;
        }

        private static readonly List<Device> _devices = new List<Device>();
        private static WheelFfbNative.DeviceInfo[] _catalog = new WheelFfbNative.DeviceInfo[0];
        private static bool _open, _firstReadLogged;
        private static float _nextOpenRetry;
        private static int _sampledFrame = -1, _watchdogFrame = -1;

        public static string Status { get; private set; } = "";
        internal static bool IsOpen => _open;
        // Set by SharedInputStartup while its session is pending or attached: the readers stay open for it.
        internal static bool Required { get; set; }
        // While the session has pinned the readers' identities, nothing may reopen them.
        internal static bool Pinned { get; set; }
        public static bool Enabled => Main.Enabled && Main.Settings != null && Required;

        internal static int[] Slots
        {
            get
            {
                if (!_open || _devices.Count == 0) return new int[0];
                var result = new int[_devices.Count];
                for (int i = 0; i < result.Length; i++) result[i] = _devices[i].Slot;
                return result;
            }
        }

        // The last observation only. Never enumerate, acquire or guess by name/index.
        // Null means there is no open reader observation to assess.
        internal static bool? DeviceReadHealth(string guid)
        {
            if (!_open || !Guid.TryParse(guid, out var identity) || identity == Guid.Empty) return null;
            // A stopped shared session keeps every reader neutral on purpose: health is unknown, not failed.
            if (_shared != null && _shared.Stopped) return null;
            foreach (var device in _devices)
                if (device.InstanceGuid == identity) return device.Ok;
            return false;
        }

        public static string DeviceSummary
        {
            get
            {
                if (!_open || _devices.Count == 0) return "no controllers open";
                var sb = new StringBuilder();
                foreach (var d in _devices) { if (sb.Length > 0) sb.Append(", "); sb.Append(d.Name); if (!d.Ok) sb.Append(" (not responding)"); }
                return sb.ToString();
            }
        }

        /// <summary>Opens every DirectInput controller for reading. Safe to call repeatedly.</summary>
        public static void Open()
        {
            if (_open) return;
            if (GameState.IsDriving) { Status = "Pause before opening wheel input devices."; return; }
            try
            {
                Close();
                _catalog = WheelFfbNative.ListAllDevices();
                foreach (var device in _catalog)
                {
                    int slot = WheelFfbNative.OpenRead(device.Index);
                    if (slot < 0) { ModLog.Warning("Wheel input: could not open " + device.Name); continue; }
                    _devices.Add(new Device { Slot = slot, Index = device.Index, Name = device.Name,
                        InstanceGuid = device.InstanceGuid == Guid.Empty ? null : device.InstanceGuid });
                }
                _open = _devices.Count > 0;
                _firstReadLogged = false;
                var names = new StringBuilder();
                foreach (var d in _devices) { if (names.Length > 0) names.Append(", "); names.Append(d.Name); }
                ModLog.Info("Wheel input: opened " + _devices.Count + " controller(s): " + names);
                if (!_open) Status = "No controllers found to read.";
                _nextOpenRetry = Time.realtimeSinceStartup + 5f;
            }
            catch (Exception ex)
            {
                ModLog.Error("Wheel input: open failed: " + ex.Message);
                Status = "Could not open controllers: " + ex.Message;
                Close();
            }
        }

        public static void Close() => CloseReaders("Art's wheel readers were closed");
        // The reason is kept as the shared session's truthful stop cause; it never reattaches. Not an
        // overload of Close: test tooling looks that method up by name.
        private static void CloseReaders(string reason)
        {
            StopShared(reason);
            try { if (_devices.Count > 0 || _open) WheelFfbNative.CloseRead(); } catch { }
            _devices.Clear();
            _catalog = new WheelFfbNative.DeviceInfo[0];
            _open = false;
            ProfileButtons.Close();
        }

        /// <summary>Watchdog maintenance, reusing a query's current snapshot.</summary>
        internal static void EnsureUpdatedThisFrame()
        {
            if (_watchdogFrame == Time.frameCount) return;
            _watchdogFrame = Time.frameCount;
            Update();
        }

        // The first Rewired query of a frame for the primary player: only already-open readers. No discovery,
        // logging, persistence or scene transitions. Snapshot and button projection are stamped separately: a car or
        // gear consumer may already have read this frame without projecting buttons.
        internal static void PrepareGameQuery()
        {
            if (!_open || !Enabled || Main.SettingsVisible || !Application.isFocused) return;
            if (_sampledFrame != Time.frameCount) ReadSnapshot();
            if (!ProfileButtons.Current) ProfileButtons.Tick();
        }

        private static void ReadSnapshot()
        {
            // Stamp before native callbacks to prevent recursive polling.
            _sampledFrame = Time.frameCount;
            if (ReadShared()) return;
            foreach (var d in _devices)
            {
                try { d.Ok = WheelFfbNative.Read(d.Slot, d.Axes, d.Buttons); }
                catch { d.Ok = false; }
            }
        }

        private static void Update()
        {
            if (Main.Settings == null) return;
            if (!Required || !Main.Enabled)
            {
                if (_open || _devices.Count > 0) CloseReaders(Main.Enabled ? "Art's wheel readers were closed" : "the mod was disabled");
                return;
            }
            bool mayDiscover = _shared == null && !Pinned && !GameState.IsDriving;
            if (_open && mayDiscover && Time.realtimeSinceStartup >= _nextOpenRetry && NeedsRefresh())
                Close();
            if (!_open)
            {
                if (!mayDiscover || Time.realtimeSinceStartup < _nextOpenRetry) return;
                _nextOpenRetry = Time.realtimeSinceStartup + 5f;
                Open();
                if (!_open) return;
            }

            // Discovery may have discarded the early snapshot. Defer reading
            // replacement handles to the next frame rather than poll twice.
            if (_sampledFrame != Time.frameCount) ReadSnapshot();
            if (!_firstReadLogged)
            {
                // Once, with raw values: proves the reads work on every handle,
                // and shows the resting position of each axis for support.
                _firstReadLogged = true;
                var sb = new StringBuilder("Wheel input: first read -");
                foreach (var d in _devices)
                {
                    sb.Append(" | ").Append(d.Name).Append(d.Ok ? " axes " : " NOT RESPONDING");
                    if (d.Ok) for (int i = 0; i < AxisCount; i++) sb.Append(d.Axes[i]).Append(i < AxisCount - 1 ? "," : "");
                }
                ModLog.Info(sb.ToString());
            }
            if (!ProfileButtons.Current) ProfileButtons.Tick();
        }

        // A reader that stopped responding, or an attached device whose reader failed to open, is retried while paused.
        private static bool NeedsRefresh()
        {
            foreach (var d in _devices) if (!d.Ok) return true;
            foreach (var device in _catalog)
            {
                bool opened = false;
                foreach (var d in _devices) if (d.Index == device.Index) { opened = true; break; }
                if (!opened) return true;
            }
            return false;
        }

        // ---- the shared session ------------------------------------------------------------------------------------

        private static ShimBridge _shared;
        private static Func<bool> _verifyIsolation;
        private static Func<bool> _restoreIsolation;
        private static bool _isolationGone;
        private static int _sharedFrame = -1;

        // The shared profile's transmission (Core's parse), fixed for the session.
        internal static ShimTransmission.Mode SharedTransmission { get; private set; }

        // The handoff, once: the qualified bridge borrows the open slots; from now on they are read only through it.
        internal static void AttachShared(IPhysicalSnapshotSource source, IShimBridge native, Func<bool> isolation, Func<bool> restore = null, string transmission = null)
        {
            if (_shared != null || !_open || _devices.Count == 0 || isolation == null || !isolation())
                throw new InvalidOperationException("shared input needs an idle, qualified reader and native isolation");
            var slots = Slots;
            try { _shared = new ShimBridge(source, native, ShimInput.Consumer, slots); }
            catch { if (restore != null) restore(); throw; }
            _shared.AcceptedSnapshot = (slot, metadata, sample, path) =>
            {
                if (!Main.SettingsVisible) { SharedBindingCaptureHost.Instance.CancelCapture(); return; }
                foreach (var device in _devices) if (device.Slot == slot)
                {
                    try { SharedBindingCaptureHost.Instance.ObserveAccepted(device.Name, path, metadata, sample, device.InstanceGuid); }
                    catch (Exception ex) { SharedBindingCaptureHost.Instance.ReaderStopped(); ModLog.Warning("Binding capture stopped: " + ex.Message); }
                    break;
                }
            };
            _verifyIsolation = isolation; _restoreIsolation = restore;
            SharedTransmission = ShimTransmission.ParseMode(transmission);
        }

        // The first shared-input consumer in a Unity frame publishes this frame's snapshot; later ones reuse it.
        // Game scripts can run before the watchdog, so freshness must not depend on order.
        internal static void EnsureSharedFrame()
        {
            if (_shared != null && _sampledFrame != Time.frameCount) ReadSnapshot();
        }

        private static bool ReadShared()
        {
            if (_shared == null) return false;
            if (_sharedFrame == Time.frameCount) return true;
            _sharedFrame = Time.frameCount;
            // After the handoff a failure cannot fall back to a second physical read. The session needs a restart.
            foreach (var d in _devices) d.Ok = false;
            // Released (a close) or lost once: the game's mixed values can no longer be trusted, so driving
            // stays neutral for good, keyboard included.
            if (_isolationGone) { SharedBindingCaptureHost.Instance.ReaderStopped(); return true; }
            bool isolated;
            try { isolated = _verifyIsolation(); } catch { isolated = false; }
            if (!isolated)
            {
                _isolationGone = true;
                try { _shared.Stop("native input isolation lost"); } catch { }
                SharedBindingCaptureHost.Instance.ReaderStopped();
                return true;
            }
            // Isolation verified this frame: the game's independent (keyboard) input may be used.
            ShimInput.IsolatedFrame = Time.frameCount;
            // A transport-only stop (Core, frame or provider refusal) keeps the wheel neutral and Core
            // untouched, but leaves the verified keyboard path; nothing re-enables the wheel.
            if (_shared.Stopped) { SharedBindingCaptureHost.Instance.ReaderStopped(); return true; }
            try
            {
                // Each accepted sample's validity is the reader health force feedback recovery watches.
                _shared.ReadFrame(Time.frameCount, (slot, bytes) =>
                {
                    foreach (var d in _devices) if (d.Slot == slot) { d.Ok = bytes[50] == 1; return; }
                });
            }
            catch (Exception ex) { try { _shared.Stop(ex.Message); } catch { } }
            if (_shared.Stopped || _shared.ObserverFailure != null) SharedBindingCaptureHost.Instance.ReaderStopped();
            return true;
        }

        // Force feedback reconnect: reader slots may share the old FFB handle, so every reader closes
        // first. An attached shared session stops for good with this stated cause; it never reattaches.
        internal const string ForceReconnectStop = "a force feedback reconnect closed the wheel readers";
        internal static void ReleaseForForceReconnect() => CloseReaders(ForceReconnectStop);
        internal static void ReopenAfterForceReconnect() { if (Enabled) Open(); }

        // Why the shared session stopped (first cause only), for a truthful status after any later close.
        internal static string SharedStopReason => _shared?.Failure;
        // A stopped session whose own isolation still verifies: keyboard driving continues, wheel neutral.
        internal static bool SharedKeyboardRetained => _shared != null && _shared.Stopped && !_isolationGone;

        internal static void StopShared(string reason = "Art reader closed")
        {
            if (_shared == null) return;
            try { _shared.Stop(reason); } catch (Exception ex) { ModLog.Error("Shared input stop: " + ex.Message); }
            _isolationGone = true; // released below; from now on driving stays neutral
            try
            {
                if (_restoreIsolation != null)
                {
                    if (_restoreIsolation()) _restoreIsolation = null;
                    else ModLog.Error("Shared input: native joystick restoration remains incomplete");
                }
            }
            catch (Exception ex) { ModLog.Error("Shared input restore: " + ex.Message); }
            // Ownership is kept through failures and toggles; only a process restart may qualify a new session.
        }
    }
}
