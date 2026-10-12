using System;
using System.Collections.Generic;
using System.IO;
using ArtOfSimRally.Mod.Shim;
using Rewired;
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
        private static SharedInputSession session;
        private static readonly RewiredInputIsolation inventory = new RewiredInputIsolation();
        private static bool finished, attached;
        private static int applyFrames;
        private static LatchedTrial trial;
        private static bool titleAdvanced;
        private static bool Trial => request != null && request.Mode == "Trial";
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
                if (attached && Trial)
                {
                    // Scene departure ends the trial; the menu is the only place it may run.
                    if (!OnMainMenu()) { Finish("Trial refused: left the main menu"); return; }
                    WheelInput.EnsureSharedFrame();
                    trial.Tick(Time.frameCount);
                    if (trial.Current == LatchedTrial.Step.Passed) Finish("Trial passed: navDown moved the main-menu selection and navUp returned it; session completed");
                    else if (trial.Current == LatchedTrial.Step.Failed) Finish("Trial failed: " + trial.Failure);
                    return;
                }
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
                if (session == null || session.Isolation == null)
                {
                    if (!SharedInputSession.Ready(inventory)) return;
                    ShimBridgeNative.LatchedSession latched = null;
                    if (Trial)
                    {
                        // The fence byte is asserted only from guards observed now, in this process. Being on
                        // the main menu and focused may still come (bounded by the request deadline); any
                        // other unmet fence refuses.
                        var fences = ObserveFences();
                        string unmet = LatchedTrial.Admission(fences, out bool wait);
                        if (LatchedTrial.AdvanceTitle(request.AdvanceTitle, titleAdvanced, OnSplash(), fences))
                        {
                            // Once, from the actual splash, through the game's own call; the main menu is still
                            // verified before Core opens and on every Trial frame.
                            titleAdvanced = receipt.TitleAdvanced = true;
                            SplashScreenControl.Instance.EndSplashScreen();
                            ModLog.Info("Shared input Trial PROBE: ended the title splash once (request flag) to reach the main menu.");
                        }
                        if (unmet != null)
                        {
                            receipt.TrialFences = (wait ? "waiting: " : "") + unmet;
                            if (!wait) Finish("Trial refused before open: " + unmet);
                            return;
                        }
                        receipt.TrialFences = "verified: no-force armed, settings writes suppressed, telemetry idle, input hooks attached, inspected build, primary player, main menu, focused";
                        string sessionFile = Path.Combine(request.Result, "shim-session.txt");
                        long expires = (long)(request.Deadline - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                        ProbeRequest.Replace(sessionFile, "nonce=" + request.Nonce + "\nexpires=" + expires + "\n");
                        latched = new ShimBridgeNative.LatchedSession { Nonce = request.Nonce, SessionFile = sessionFile, BatchFile = Path.Combine(request.Result, "shim-batch.txt"), FencesVerified = true, ForceAbsent = receipt.NoForceArmed };
                    }
                    session = new SharedInputSession(inventory);
                    session.Begin(request.ShimPath, request.Result, File.ReadAllBytes(request.ProfilePath), latched);
                    receipt.IdentityEpochBefore = session.EpochBefore; receipt.Instances = session.Instances; receipt.Before = session.Before;
                    receipt.OwnedDevices = session.Owned; receipt.RewiredInstances = session.RewiredInstances;
                    receipt.IdentityQualification = "exact live owned path/product admission; while disabled use captured identity only with unchanged object, extension, GUID, connected state and event epoch; raw observations remain unmodified";
                    request.ValidateApply(request.Mode != "Observe" ? ProbeRequest.Read<ProbeReceipt>(request.ObserveReceipt) : null, session.Instances, session.Owned, session.RewiredInstances);
                    session.Isolate();
                }
                bool clean = session.VerifyIsolation();
                if (!clean)
                {
                    if (session.IsolationOverdue(Time.frameCount)) Finish(session.Isolation.Status);
                    return;
                }
                receipt.IdentityQualified = true;
                receipt.Disabled = ProbeObservation.Capture(inventory);
                ProbeObservation.CompareDisabled(receipt, receipt.RewiredInstances);
                if (request.Mode == "Observe") { Finish("Observe complete: " + receipt.RawAxisQualification); return; }
                if (!receipt.FlagsDisabled || !receipt.RawAxesNeutralAfterDisable) { Finish(request.Mode + " refused: current disable/raw-axis check unqualified"); return; }
                if (Trial)
                {
                    // Fixed before the first frame: only latched frames, and only vertical menu navigation delivered.
                    ShimInput.Consumer.AcceptLatched = true;
                    ShimInput.Consumer.DeliverOnly(LatchedTrial.NavUp, LatchedTrial.NavDown);
                    string batchFile = Path.Combine(request.Result, "shim-batch.txt");
                    trial = new LatchedTrial(ShimInput.Consumer, Selection, text => ProbeRequest.Replace(batchFile, text), request.Nonce, NavigationEnabled);
                }
                WheelInput.AttachSharedInput(session.Source, session.Native, session.VerifyIsolation, () => session.Isolation.Restore(), session.Native.Transmission());
                attached = true; receipt.Applied = true;
                ModLog.Info(Trial ? "Shared input Trial PROBE attached (latched; navUp/navDown only) on the main menu; saved backend unchanged."
                    : "Shared input Apply PROBE attached to existing readers for 120 menu frames; saved backend unchanged.");
            }
            catch (Exception ex) { Finish("probe refused: " + ex.Message); }
        }
        internal static void Shutdown() { if (Requested && !finished && receipt != null) Finish("probe shutdown"); }

        private static bool OnSplash()
        {
            var splash = SplashScreenControl.Instance;
            var panels = UIManager.Instance == null ? null : UIManager.Instance.PanelManager;
            return splash != null && splash.isActiveAndEnabled && panels != null && panels.Peek() == panels.SplashScreenPanel;
        }
        private static bool OnMainMenu()
        {
            var panels = UIManager.Instance == null ? null : UIManager.Instance.PanelManager;
            return panels != null && panels.MainPanel != null && panels.Peek() == panels.MainPanel;
        }
        // PanelManager.Start disables EventSystem navigation behind the splash and re-enables it 0.5 s after
        // the main menu opens; the UI module sends no move events until then.
        private static bool NavigationEnabled()
        {
            var system = UnityEngine.EventSystems.EventSystem.current;
            return system != null && system.sendNavigationEvents;
        }
        // The menu's current selection (name and instance), read back after each injected step.
        private static string Selection()
        {
            var system = UnityEngine.EventSystems.EventSystem.current;
            var selected = system == null ? null : system.currentSelectedGameObject;
            return selected == null ? null : selected.name + "#" + selected.GetInstanceID();
        }
        private static LatchedTrial.Fences ObserveFences() => new LatchedTrial.Fences
        {
            NoForceArmed = receipt.NoForceArmed,
            SettingsWritesSuppressed = !Main.SaveSettings(), // the probe's settings guard answers without writing
            TelemetryIdle = TelemetryPump.ActiveEndpoint == null,
            MissingHooks = InputHooks.Missing(),
            Compatible = GameButtonCompatibility.Allowed,
            Primary = GameButtonInput.Primary(PadManager.GetPlayer()),
            MainMenu = OnMainMenu(), Driving = GameState.IsDriving, Focused = Application.isFocused,
        };

        private static void Finish(string status)
        {
            if (finished) return;
            finished = true;
            try
            {
                if (attached) WheelInput.StopSharedProbe();
                else if (session?.Native != null) session.Native.Stop();
            }
            catch (Exception ex) { status += "; stop failed: " + ex.Message; }
            try
            {
                bool restored = session?.Isolation != null && session.Isolation.Restore();
                if (receipt.Before != null) receipt.Restored = ProbeObservation.Capture(inventory);
                receipt.FlagsRestored = restored && ProbeObservation.Restored(receipt.Before, receipt.Restored);
                if (receipt.FlagsRestored) receipt.IdentityRestored = session.IdentityRestored(receipt.Restored);
            }
            catch (Exception ex) { status += "; restore unqualified: " + ex.Message; }
            if (trial != null)
            {
                receipt.TrialStatus = trial.Current + (trial.Failure == null ? "" : ": " + trial.Failure);
                receipt.TrialSelections = new[] { trial.Before, trial.AfterDown, trial.AfterUp };
                receipt.TrialRun = ShimInput.Consumer.Started ? ShimInput.Consumer.OriginalFrame.Run : -1;
                receipt.TrialNavigationWaitFrames = trial.NavigationWaitFrames;
            }
            receipt.IdentityEpochAfter = inventory.ConnectionEpoch;
            try { inventory.EndIdentityObservation(); } catch (Exception ex) { status += "; event cleanup failed: " + ex.Message; }
            receipt.Status = status; receipt.CompletedUtc = DateTime.UtcNow.ToString("o");
            try { ProbeRequest.Write(Path.Combine(request.Result, "probe-receipt.json"), receipt); }
            catch (Exception ex) { ModLog.Error("Probe receipt write failed: " + ex.Message); }
            ModLog.Info("Shared input probe: " + status + "; flags restored=" + receipt.FlagsRestored + ". Native no-force remains latched until exit.");
        }
    }
}
