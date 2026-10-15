using System;
using System.IO;
using ArtOfSimRally.Mod.Shim;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    // Opt-in ordinary shared input (Settings.SharedInput, read at startup). Never active with the
    // diagnostic probe and never constructed otherwise. It qualifies once, idle in the menus, with the
    // same sequence the probe qualified; any refusal before the first accepted frame releases what it
    // took and leaves Art's bindings in charge with a stated reason. After the first accepted frame a
    // fault keeps the wheel neutral until restart (the shared consumer's existing rule).
    internal static class SharedInputStartup
    {
        internal const string ProfileFileName = "dbce-controls.ini", ShimFileName = "dbce_shim.dll";
        // Canonical toolkit shim c6594fa (built from 421bd4b): copied provider, fenced latched bridge,
        // pure profile inspection. Pinned in lib/input-shim/CANDIDATE.json; October 13 offline tests passed.
        internal const string ExpectedShimSha256 = "BC9B030A25834D8CCE562F22588EC5E0D3F5C1FA026AD7AD984776D60CD6254B";
        // A failed joystick restore is retried in the menus about once a second, then left for a restart.
        internal const int RestoreRetries = 10, RestoreRetryFrames = 60;
        private static readonly RewiredInputIsolation inventory = new RewiredInputIsolation();
        // Core's per-launch run folders (host-shutdown.json); tests point this elsewhere.
        internal static string RunRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArtOfSimRally", "shared-input");
        // The hardware-facing qualification steps; lifecycle fixtures substitute them.
        internal static Func<RewiredInputIsolation, ISharedQualification> NewSession = i => new SharedInputSession(i);
        // Force feedback's startup acquisition or a pending reconnect closes and reopens every Art reader.
        // Qualification waits for it so an attach is never closed straight away. Main supplies the check.
        internal static Func<bool> ForceSettled = () => true;
        internal static Func<string, byte[], SharedSteeringTarget> InspectSteering = ShimProfileNative.ReadSteering;
        private static ISharedQualification session;
        private static string shimPath, reason, profileName, transmission, waitingFor;
        private static string[] devices;
        private static byte[] profile;
        private static int restoreAttempts, nextRestoreFrame;
        private static SharedInputStatus.Phase phase = SharedInputStatus.Phase.Off;
        internal static bool Active => phase == SharedInputStatus.Phase.Waiting || phase == SharedInputStatus.Phase.Isolating || phase == SharedInputStatus.Phase.Attached;
        // The session has pinned Art's readers; force recovery waits these few frames instead of closing them.
        internal static bool Qualifying => phase == SharedInputStatus.Phase.Isolating;
        // Art's own shifter reader stays closed while shared input is pending or attached, and while a
        // failed joystick restore is unresolved.
        internal static bool HoldsShifter => Active || phase == SharedInputStatus.Phase.CleanupFailed;
        // The setting as read at startup; a later change applies after a restart.
        internal static bool RequestedAtStartup { get; private set; }

        private static void Enter(SharedInputStatus.Phase next)
        {
            phase = next;
            WheelInput.SharedReadersRequired = Active;
            WheelInput.SharedReadersPinned = next == SharedInputStatus.Phase.Isolating;
        }

        // At mod load, before FFB selection or Art's own shifter could open. Files and pure profile
        // inspection only; no host, provider, reader or force acquisition here.
        internal static void Prepare(string modDirectory)
        {
            SharedBindingCaptureHost.Configure(Path.Combine(modDirectory, ProfileFileName), null,
                () => Time.realtimeSinceStartup, () => Main.Enabled && Main.SettingsVisible && !SettingsPanel.LegacyEditing && Main.HasFocus() &&
                    !GameState.IsDriving && ShimInput.Consumer.Started);
            session = null; reason = waitingFor = null; startedLogged = stopLogged = false; restoreAttempts = 0;
            RequestedAtStartup = !ShimStartup.Requested && Main.Settings != null && Main.Settings.SharedInput;
            FfbSelection.PrepareShared(RequestedAtStartup);
            if (!RequestedAtStartup) { Enter(SharedInputStatus.Phase.Off); return; }
            try
            {
                string profilePath = Path.Combine(modDirectory, ProfileFileName);
                shimPath = Path.Combine(modDirectory, ShimFileName);
                // The portable Controls Setup download writes this file (Wheelkit 0.6.23 or later also can); Wheelkit is optional.
                if (!File.Exists(profilePath)) { Refuse("no shared profile beside the mod. Run Set Up Controls.bat from the matching Controls Setup download (or apply a profile in Wheelkit 0.6.23 or later); it writes " + ProfileFileName + " here. Then restart."); return; }
                if (!File.Exists(shimPath)) { Refuse(ShimFileName + " is missing beside the mod."); return; }
                if (!String.Equals(ProbeRequest.Hash(shimPath), ExpectedShimSha256, StringComparison.OrdinalIgnoreCase)) { Refuse(ShimFileName + " is not the qualified build."); return; }
                var info = new FileInfo(profilePath);
                if (info.Length == 0 || info.Length > 1024 * 1024) { Refuse("the shared profile is empty or larger than 1 MiB."); return; }
                profile = File.ReadAllBytes(profilePath);
                SharedBindingCaptureHost.Configure(profilePath, profile,
                    () => Time.realtimeSinceStartup, () => Main.Enabled && Main.SettingsVisible && !SettingsPanel.LegacyEditing && Main.HasFocus() &&
                        !GameState.IsDriving && ShimInput.Consumer.Started);
                // This byte array also goes to Begin later. Never reread the file after FFB selection,
                // copy it into legacy SteerBinding, or schedule recovery after the readers attach.
                string configuredTarget;
                try
                {
                    var target = InspectSteering(shimPath, profile);
                    if (target == null) throw new InvalidOperationException("Core returned no shared steering report.");
                    FfbSelection.PrepareShared(true, target.Name, target.Instance, target.Reason);
                    configuredTarget = String.IsNullOrEmpty(target.Instance) ? "unavailable: " + target.Reason : "instance=" + target.Instance;
                }
                catch (Exception ex) { FfbSelection.PrepareShared(true, reason: ex.Message); configuredTarget = "unavailable: " + ex.Message; }
                // One startup-only observation of configuration, never a connected-device or force verdict.
                ModLog.Info("Shared input: configured steering target " + configuredTarget.Replace('\r', ' ').Replace('\n', ' ') +
                    "; frozenProfileSha256=" + ShimProfileNative.ProfileHash(profile) + "; connection and force not verified.");
                Enter(SharedInputStatus.Phase.Waiting);
                ModLog.Info("Shared input: profile found; qualifying in the menus. Art's own shifter reader stays closed.");
            }
            catch (Exception ex) { Refuse("the shared profile could not be read: " + ex.Message); }
        }

        private static bool startedLogged, stopLogged;
        // Watchdog Update. Qualifies in the menus only; once attached, only reports (once each).
        internal static void Tick()
        {
            if (phase == SharedInputStatus.Phase.Attached) { Report(); return; }
            if (phase == SharedInputStatus.Phase.CleanupFailed) { RetryCleanup(); return; }
            if (phase != SharedInputStatus.Phase.Waiting && phase != SharedInputStatus.Phase.Isolating) return;
            if (!Main.Enabled || GameState.IsDriving) return;
            try
            {
                if (phase == SharedInputStatus.Phase.Waiting)
                {
                    if (!ForceSettled()) { waitingFor = "force feedback to finish connecting"; return; }
                    if (!SharedInputSession.Ready(inventory)) { waitingFor = inventory.Ready ? "the wheel readers" : "the game's input system"; return; }
                    waitingFor = null;
                    string folder = Path.Combine(RunRoot, "run-" + System.Diagnostics.Process.GetCurrentProcess().Id + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"));
                    Directory.CreateDirectory(folder);
                    session = NewSession(inventory);
                    session.Begin(shimPath, folder, profile);
                    profileName = session.ProfileName; transmission = session.Transmission;
                    devices = session.DeviceNames;
                    session.Isolate(); Enter(SharedInputStatus.Phase.Isolating);
                }
                if (!session.VerifyIsolation())
                {
                    if (session.IsolationOverdue(Time.frameCount)) Refuse(session.IsolationStatus + ".");
                    return;
                }
                string neutral = session.NeutralFailure();
                if (neutral != null) { Refuse("the isolated wheel did not read neutral (" + neutral + ")."); return; }
                var attached = session;
                WheelInput.AttachSharedInput(session.Source, session.Bridge, session.VerifyIsolation, () => attached.RestoreIsolation(), transmission,
                    System.Text.Encoding.UTF8.GetString(profile));
                Enter(SharedInputStatus.Phase.Attached);
                ModLog.Info("Shared input: attached profile '" + profileName + "' (" + transmission + "); isolated " + String.Join(", ", devices) + ".");
            }
            catch (Exception ex) { Refuse(ex.Message); }
        }

        // The first cause the shared session stopped for (the bridge keeps it), else the consumer's.
        private static string StopReason => WheelInput.SharedStopReason ?? ShimInput.Consumer.Status;
        private static bool Stopped => phase == SharedInputStatus.Phase.Attached && (ShimInput.Consumer.Terminal || WheelInput.SharedStopReason != null);

        private static void Report()
        {
            var consumer = ShimInput.Consumer;
            if (!startedLogged && consumer.Started)
            {
                startedLogged = true;
                var owned = new System.Collections.Generic.List<string>();
                for (int i = 0; i < ShimFrame.Count; i++) if (consumer.Owns(i)) owned.Add(ShimFrame.Names[i]);
                ModLog.Info("Shared input: first frame accepted; owned actions: " + String.Join(", ", owned) + ".");
            }
            if (!stopLogged && Stopped)
            {
                stopLogged = true;
                ModLog.Warning("Shared input stopped: " + StopReason + (WheelInput.SharedKeyboardRetained ? SharedInputStatus.StoppedKeyboardEffect : SharedInputStatus.StoppedEffect));
            }
        }

        internal static void Shutdown()
        {
            if (phase == SharedInputStatus.Phase.Attached) { WheelInput.StopSharedProbe(); session?.EndObservation(); }
            else if (phase == SharedInputStatus.Phase.CleanupFailed) session?.RetryRestore();
            else if (session != null) session.Close();
        }

        internal static string[] Lines(bool? carAutomatic = null, bool details = true)
        {
            return SharedInputStatus.Lines(new SharedInputStatus.Facts
            {
                Phase = phase, Reason = reason, WaitingFor = waitingFor, ProfileName = profileName, Transmission = transmission, Devices = devices,
                ArtTransmissionMode = Main.Settings?.TransmissionMode, CarAutomatic = carAutomatic,
                Stopped = Stopped, StopReason = StopReason, KeyboardRetained = WheelInput.SharedKeyboardRetained,
                RestoreStatus = session?.IsolationStatus, Retrying = restoreAttempts < RestoreRetries,
            }, details);
        }

        private static void Refuse(string why)
        {
            string cleanup = session?.Close();
            reason = why + (cleanup == null ? "" : " Cleanup: " + cleanup + ".");
            if (session != null && session.RestorePending)
            {
                // The game's own input for an isolated device is still off: keep the session and retry,
                // never claim Art's bindings are back.
                restoreAttempts = 0; nextRestoreFrame = Time.frameCount + RestoreRetryFrames;
                Enter(SharedInputStatus.Phase.CleanupFailed);
                ModLog.Error("Shared input not used: " + reason + " Retrying the joystick restore in the menus; Art's shifter stays closed until it succeeds.");
                return;
            }
            session = null;
            Release();
        }

        // Only after a complete cleanup: Art's bindings and its own shifter reader take over again.
        private static void Release()
        {
            bool held = HoldsShifter;
            Enter(SharedInputStatus.Phase.Refused);
            ModLog.Warning("Shared input not used: " + reason + " Art bindings remain.");
            var cfg = Main.Settings;
            if (held && Main.Enabled && cfg != null && cfg.ShifterEnabled && cfg.ShifterDeviceIndex >= 0 && !Shifter.IsOpen) Shifter.Open(cfg.ShifterDeviceIndex);
        }

        private static void RetryCleanup()
        {
            if (restoreAttempts >= RestoreRetries || GameState.IsDriving || Time.frameCount < nextRestoreFrame) return;
            restoreAttempts++; nextRestoreFrame = Time.frameCount + RestoreRetryFrames;
            if (!session.RetryRestore())
            {
                if (restoreAttempts == RestoreRetries) ModLog.Error("Shared input: the game's own wheel input is still off after " + RestoreRetries + " restore attempts; restart the game.");
                return;
            }
            session = null;
            reason += " The game's own input was restored on retry " + restoreAttempts + ".";
            Release();
        }
    }
}
