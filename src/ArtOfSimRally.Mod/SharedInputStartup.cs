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
        // The copied-provider build qualified by the October 12 Observe/Apply (art-shim-bridge 574bb88).
        internal const string ExpectedShimSha256 = "7DDC962132819EDAE76EF84CE6509F1936B8C38177A36D498B8ABBCFDCFFBCAF";
        private static readonly RewiredInputIsolation inventory = new RewiredInputIsolation();
        // Core's per-launch run folders (host-shutdown.json); tests point this elsewhere.
        internal static string RunRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArtOfSimRally", "shared-input");
        private static SharedInputSession session;
        private static string shimPath, reason, profileName, transmission;
        private static string[] devices;
        private static byte[] profile;
        private static SharedInputStatus.Phase phase = SharedInputStatus.Phase.Off;
        internal static bool Active => phase == SharedInputStatus.Phase.Waiting || phase == SharedInputStatus.Phase.Isolating || phase == SharedInputStatus.Phase.Attached;
        // The setting as read at startup; a later change applies after a restart.
        internal static bool RequestedAtStartup { get; private set; }

        // At mod load, before Art's own shifter could open. Files only; nothing is acquired here.
        internal static void Prepare(string modDirectory)
        {
            RequestedAtStartup = !ShimStartup.Requested && Main.Settings != null && Main.Settings.SharedInput;
            if (!RequestedAtStartup) { phase = SharedInputStatus.Phase.Off; return; }
            try
            {
                string profilePath = Path.Combine(modDirectory, ProfileFileName);
                shimPath = Path.Combine(modDirectory, ShimFileName);
                if (!File.Exists(profilePath)) { Refuse("no shared profile beside the mod (" + ProfileFileName + "); apply one with Wheelkit."); return; }
                if (!File.Exists(shimPath)) { Refuse(ShimFileName + " is missing beside the mod."); return; }
                if (!String.Equals(ProbeRequest.Hash(shimPath), ExpectedShimSha256, StringComparison.OrdinalIgnoreCase)) { Refuse(ShimFileName + " is not the qualified build."); return; }
                var info = new FileInfo(profilePath);
                if (info.Length == 0 || info.Length > 1024 * 1024) { Refuse("the shared profile is empty or larger than 1 MiB."); return; }
                profile = File.ReadAllBytes(profilePath);
                phase = SharedInputStatus.Phase.Waiting;
                ModLog.Info("Shared input: profile found; qualifying in the menus. Art's own shifter reader stays closed.");
            }
            catch (Exception ex) { Refuse("the shared profile could not be read: " + ex.Message); }
        }

        // Watchdog Update. Qualifies in the menus only; once attached, only reports.
        internal static void Tick()
        {
            if (phase != SharedInputStatus.Phase.Waiting && phase != SharedInputStatus.Phase.Isolating) return;
            if (!Main.Enabled || GameState.IsDriving) return;
            try
            {
                if (phase == SharedInputStatus.Phase.Waiting)
                {
                    if (!SharedInputSession.Ready(inventory)) return;
                    string folder = Path.Combine(RunRoot, "run-" + System.Diagnostics.Process.GetCurrentProcess().Id + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"));
                    Directory.CreateDirectory(folder);
                    session = new SharedInputSession(inventory);
                    session.Begin(shimPath, folder, profile);
                    profileName = session.Native.ProfileValue("profile"); transmission = session.Native.Transmission();
                    devices = DeviceNames(session.Before, session.RewiredInstances);
                    session.Isolate(); phase = SharedInputStatus.Phase.Isolating;
                }
                if (!session.VerifyIsolation())
                {
                    if (session.IsolationOverdue(Time.frameCount)) Refuse(session.Isolation.Status + ".");
                    return;
                }
                // The disabled objects' cached values must already read neutral before any frame is published.
                var check = new ProbeReceipt { Before = session.Before, Disabled = ProbeObservation.Capture(inventory) };
                ProbeObservation.CompareDisabled(check, session.RewiredInstances);
                if (!check.FlagsDisabled || !check.RawAxesNeutralAfterDisable) { Refuse("the isolated wheel did not read neutral (" + check.RawAxisQualification + ")."); return; }
                WheelInput.AttachSharedInput(session.Source, session.Native, session.VerifyIsolation, () => session.Isolation.Restore(), transmission);
                phase = SharedInputStatus.Phase.Attached;
                ModLog.Info("Shared input: attached profile '" + profileName + "' (" + transmission + "); isolated " + String.Join(", ", devices) + ".");
            }
            catch (Exception ex) { Refuse(ex.Message); }
        }

        internal static void Shutdown()
        {
            if (phase == SharedInputStatus.Phase.Attached) { WheelInput.StopSharedProbe(); session?.EndObservation(); }
            else if (session != null) session.Close();
        }

        internal static string[] Lines(bool? carAutomatic = null)
        {
            var consumer = ShimInput.Consumer;
            return SharedInputStatus.Lines(new SharedInputStatus.Facts
            {
                Phase = phase, Reason = reason, ProfileName = profileName, Transmission = transmission, Devices = devices,
                ArtTransmissionMode = Main.Settings?.TransmissionMode, CarAutomatic = carAutomatic,
                Stopped = phase == SharedInputStatus.Phase.Attached && consumer.Terminal, StopReason = consumer.Status,
            });
        }

        private static void Refuse(string why)
        {
            bool wasActive = Active;
            string cleanup = session?.Close();
            session = null; phase = SharedInputStatus.Phase.Refused;
            reason = why + (cleanup == null ? "" : " Cleanup: " + cleanup + ".");
            ModLog.Warning("Shared input not used: " + reason + " Art bindings remain.");
            // Art's own shifter stayed closed while the shared profile was pending; give it back.
            var cfg = Main.Settings;
            if (wasActive && Main.Enabled && cfg != null && cfg.ShifterEnabled && cfg.ShifterDeviceIndex >= 0 && !Shifter.IsOpen) Shifter.Open(cfg.ShifterDeviceIndex);
        }

        private static string[] DeviceNames(JoystickObservation[] before, Guid[] isolated)
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var row in before)
                if (Array.IndexOf(isolated, Guid.Parse(row.Instance)) >= 0) names.Add(String.IsNullOrEmpty(row.Name) ? row.Instance : row.Name);
            return names.ToArray();
        }
    }
}
