using System;
using Dbce.Wheel.Input;   // the shared binding-editor contract once vendored there (Hera 123632)
using Dbce.Wheel.Ui;
using Dbce.Wheel.Ui.Unity;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// The F6 settings panel inside UMM's window, drawn with the shared toolkit F6 rows (Toolkit/Ui/F6Widgets.cs, the
    /// same rows as #DRIVE Rally): title with Close, page tabs with the Simple/Advanced switch, aligned setting rows, and a
    /// footer outside the scrolling body with the force state and Stop FFB. Page content, settings, saving and every action
    /// stay here and in their owners; the shared rows only draw and report edits. UMM keeps its own chrome and scale.
    /// </summary>
    internal static class SettingsPanel
    {
        private static readonly F6Widgets W = new F6Widgets();
        private static readonly string[] PageIds = { "page-0", "page-1", "page-2", "page-3", "page-4" };
        private static readonly string[] ViewIds = { "view-Simple", "view-Advanced" }, Views = { "Simple", "Advanced" };
        private static Vector2 _scroll;
        private static bool _shifter, _cameraKeys, _clutch, _settingsKey, _modButtons, _draftCloseWarned;
        private static float _keyDeadline;
        private static string _keyStatus = "";
        private static int _mount;
        private static readonly ConnectionEdit Connection = new ConnectionEdit();
        private static ISharedBindingHost Bindings => SharedBindingCaptureHost.Current;
        internal static bool Editing => WheelInput.Assigning.HasValue || CameraKeys.Listening >= 0 ||
            Panel.BindingActive || Connection.Editing || _settingsKey || Bindings?.CapturingAction != null;
        internal static bool CancelPendingEdit()
        {
            bool editing = Editing;
            WheelInput.CancelAssign(); CameraKeys.Cancel(); Panel.CancelBinding(); Connection.Cancel(); _settingsKey = false;
            Bindings?.CancelCapture();
            DeviceDropdown.CloseAll();
            if (editing) return true;
            // Unsaved shared-binding edits are never saved by closing: the first close says so and stays open; a second
            // close keeps the draft unsaved (it stays in this session) and closes.
            if (Bindings != null && Bindings.HasDraft && !_draftCloseWarned) { _draftCloseWarned = true; return true; }
            _draftCloseWarned = false;
            return false;
        }
        public static void Draw()
        {
            if (Main.Settings == null) return;
            using (new SettingsPresentation()) DrawContent();
        }
        private static void DrawContent()
        {
            var c = Main.Settings;
            if (c == null) return;
            W.Scale = SettingsPresentation.Scale;
            W.Begin(SettingsPresentation.BodyWidth / SettingsPresentation.Scale);
            HandleKey(c);
            if (W.TitleBar("Wheel settings", "Art of Sim Rally " + Main.ModVersion, "close", "Close")) Main.CloseSettings();
            bool advanced = SettingsViewPolicy.Advanced(c);
            int page = W.Header(PageIds, SettingsViewPolicy.Pages, SettingsViewPolicy.Page(c), !Editing,
                ViewIds, Views, advanced ? 1 : 0, !Editing, out int view);
            if (view >= 0 && (view == 1) != advanced) Select(c, view == 1, SettingsViewPolicy.Page(c));
            else if (page >= 0 && page != SettingsViewPolicy.Page(c)) Select(c, advanced, page);
            if (_draftCloseWarned && Bindings != null && Bindings.HasDraft)
                W.StatusCard("draft-close", "Unsaved binding changes", "Save bindings or Discard under Controls. Close again to keep them unsaved for now.", true, null);
            // Header stays outside our page scroll; explicit host sizes win.
            _scroll = W.BeginBody(_scroll, SettingsPresentation.PageHeight / SettingsPresentation.Scale);
            GUILayout.BeginVertical(GUILayout.Width(SettingsPresentation.BodyWidth));
            switch (SettingsViewPolicy.Page(c))
            {
                case 0: Controls(c); break;
                case 1: Feedback(c); break;
                case 2: Cameras(c); break;
                case 3: Telemetry(c); break;
                case 4: Support(c); break;
            }
            GUILayout.EndVertical(); W.EndBody();
            Footer(c);
            W.End();
        }
        /// <summary>Outside the scrolling body: the saved force preference with the current output, Stop FFB, and the
        /// save state with this game's own shortcuts (F8 stops FFB here).</summary>
        private static void Footer(Settings c)
        {
            W.Divider();
            string title = c.ForceFeedbackEnabled ? "FFB enabled" : "FFB off";
            string detail = !c.ForceFeedbackEnabled ? "Turn it On under FFB when ready." : !FfbNative.Ready ? FfbNative.Status : "Output paused while settings are open";
            GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
            GUILayout.BeginVertical();
            W.Inline(title);
            W.Inline(detail);
            GUILayout.EndVertical();
            W.Spacer();
            if (W.Command("ffb-stop", "Stop FFB (F8)")) Main.StopFeedback();
            GUILayout.EndHorizontal();
            W.Help(Main.SettingsSaveStatus + " · F8 stops FFB · " + CameraKeys.Name(c.SettingsKey) + " opens and closes settings");
        }
        private static void Select(Settings c, bool advanced, int page)
        {
            if (!SettingsViewPolicy.Select(c, advanced, page, Editing)) return;
            _scroll = Vector2.zero; DeviceDropdown.CloseAll(); GUIUtility.keyboardControl = 0; Main.MarkSettingsDirty();
        }
        private static void HandleKey(Settings c)
        {
            if (_settingsKey && Time.unscaledTime > _keyDeadline)
            { _settingsKey = false; _keyStatus = "Binding timed out. Previous Settings key kept."; }
            var e = Event.current;
            if (e.type != EventType.KeyDown) return;
            if (e.keyCode == KeyCode.Escape && Editing)
            { CancelPendingEdit(); Main.SuppressHostClose = true; e.Use(); return; }
            if (CameraKeys.HandleKey(c, e.keyCode, e.shift || e.control || e.alt || e.command)) { e.Use(); return; }
            if (!_settingsKey) return;
            _keyStatus = "Use a single keyboard key; F8/F10 and camera adjustment keys are reserved.";
            if (!e.shift && !e.control && !e.alt && !e.command && CameraKeys.IsKeyboardKey(e.keyCode) &&
                e.keyCode != KeyCode.F8 && e.keyCode != KeyCode.F10 && !CameraKeys.ModifierHeld())
            {
                bool conflict = false;
                foreach (var key in CameraKeys.Bindings) if (key.Get(c) == e.keyCode) conflict = true;
                if (!conflict && !NativeKeyboardBindings.Available(e.keyCode, out string reason))
                { conflict = true; _keyStatus = reason; }
                if (!conflict)
                {
                    var previous = c.SettingsKey;
                    if (SettingsCommit.TrySave(() => c.SettingsKey = e.keyCode, () => c.SettingsKey = previous))
                    { _settingsKey = false; _keyStatus = "Settings key saved."; }
                    else _keyStatus = "Could not save. Previous Settings key kept; pause and check Settings.xml is writable.";
                }
            }
            e.Use();
        }
        private static void Controls(Settings c)
        {
            if (GameState.IsDriving)
                Help("Pause the stage before binding, calibrating or clearing controls. Opening Wheel settings does not pause the game.");
            bool active = Toggle(c.WheelInputEnabled, "Use assigned controls");
            if (active != c.WheelInputEnabled) { c.WheelInputEnabled = active; if (!active) WheelInput.CancelAssign(); }
            if (!c.WheelInputEnabled)
                Help("Assigned USB controls are Off, including a separate handbrake. Game-bound wheel and pedals may still work. Turn On to use the handbrake.");
            if (!WheelInput.Assigning.HasValue) Help(WheelInput.Status);
            c.SharedInput = Toggle(c.SharedInput, "Use shared profile");
            if (c.SharedInput != SharedInputStartup.RequestedAtStartup) Help("Restart the game to " + (c.SharedInput ? "use" : "stop using") + " the shared profile.");
            foreach (var line in SharedInputStartup.Lines(SharedCarAutomatic())) Help(line);
            if (c.SharedInput || SharedInputStartup.RequestedAtStartup) SharedBindings();
            Heading("Steering and pedals");
            Axis(c, WheelInput.Channel.Steer, "Steering"); Axis(c, WheelInput.Channel.Throttle, "Throttle");
            Axis(c, WheelInput.Channel.Brake, "Brake"); Axis(c, WheelInput.Channel.Handbrake, "Handbrake (axis)");
            Axis(c, WheelInput.Channel.HandbrakeButton, "Handbrake (button)");
            Help("Unbound controls keep the game's bindings.");
            Help("Handbrake axis, button and game controls use the greater value; a held button contributes 100%.");
            _clutch = Disclosure(_clutch, "clutch binding");
            if (_clutch) Axis(c, WheelInput.Channel.Clutch, "Clutch");
            string[] transmissions = { "Follow game", "Automatic", "Sequential", "H-pattern" };
            int transmission = Math.Max(0, Array.IndexOf(transmissions, c.TransmissionMode));
            Heading("Transmission");
            int chosen = W.ChoiceRow("transmission", "Gearbox", "An explicit choice overrides the game's transmission option. Choose Follow game to use that option again.",
                transmissions, transmission, true);
            if (chosen >= 0 && chosen != transmission)
            {
                c.TransmissionMode = transmissions[chosen];
                if (chosen > 0) c.ShifterIsHPattern = chosen == 3;
                SettingsManager.SetTransmission();
            }
            Heading("Camera and menus");
            Axis(c, WheelInput.Channel.CameraSwitch, "Change camera");
            Axis(c, WheelInput.Channel.Confirm, "Confirm"); Axis(c, WheelInput.Channel.Back, "Back");
            Axis(c, WheelInput.Channel.Start, "Start / pause");
            Axis(c, WheelInput.Channel.NavUp, "Navigate up"); Axis(c, WheelInput.Channel.NavDown, "Navigate down");
            Axis(c, WheelInput.Channel.NavLeft, "Navigate left"); Axis(c, WheelInput.Channel.NavRight, "Navigate right");
            Help("Buttons and hat directions use the same saved bindings as the shared profile. Release after focus or device changes. Game keyboard and controller bindings remain available.");
            _shifter = Disclosure(_shifter, "shifter bindings");
            if (_shifter && SharedInputStartup.HoldsShifter)
            {
                Help(SharedInputStartup.Active ? "Gears come from the shared profile; Art's separate shifter stays closed. Turn the shared profile off and restart to use it."
                    : "Art's separate shifter stays closed until the game's own wheel input is restored.");
                if (!c.ShifterIsHPattern) c.SkipNeutral = Toggle(c.SkipNeutral, "Skip neutral");
            }
            else if (_shifter)
            {
                c.ShifterEnabled = Toggle(c.ShifterEnabled, "Separate shifter");
                if (c.ShifterEnabled) Panel.DrawShifterBinding(c);
                if (!c.ShifterIsHPattern) c.SkipNeutral = Toggle(c.SkipNeutral, "Skip neutral");
            }
            Heading("Driving and menu buttons");
            Help("The game's binding screen owns Shift up/down, Change camera, held Look behind, Reset car, Pause and menu controls. It preserves keyboard/pad action maps. Settings/Stop FFB buttons below read USB devices directly.");
            if (RightButton("Open game bindings", 180)) GameBindings.Open();
            Help(GameBindings.Status);
            _modButtons = Disclosure(_modButtons, "mod buttons and Settings key");
            if (_modButtons)
            {
                Heading("Mod shortcuts");
                string keyText = _settingsKey ? "Press a key; Escape cancels." : "Settings key: " + CameraKeys.Name(c.SettingsKey);
                if (CommandRow(keyText, _settingsKey ? "Cancel capture" : "Bind", 110))
                {
                    if (_settingsKey) { _settingsKey = false; _keyStatus = "Settings key capture canceled."; }
                    else if (!Editing) { _settingsKey = true; _keyStatus = "Press a key within 10 seconds. Escape cancels."; _keyDeadline = Time.unscaledTime + 10; }
                }
                Help(_keyStatus);
                Axis(c, WheelInput.Channel.SettingsButton, "Settings (button)");
                Axis(c, WheelInput.Channel.StopFfbButton, "Stop FFB (button)");
                Help("F8 always stops FFB. These optional device buttons work even with assigned driving controls Off. Release held buttons after reconnecting.");
            }
            if (!SettingsViewPolicy.Advanced(c) && SettingsViewPolicy.CustomControls(c) &&
                LinkButton("Review custom controls in Advanced >", 245)) Select(c, true, 0);
            if (SettingsViewPolicy.Advanced(c))
            {
                Heading("Steering compatibility");
                c.DirectSteering = Toggle(c.DirectSteering, "Direct steering");
                c.ZeroAxisDeadzone = Toggle(c.ZeroAxisDeadzone, "Remove hidden deadzone");
                c.BindAnyDevice = Toggle(c.BindAnyDevice, "Bind whichever device you touch");
                c.GlyphTextFallback = Toggle(c.GlyphTextFallback, "Show button names without icons");
                c.DisableSteerAssist = Toggle(c.DisableSteerAssist, "Legacy steering limiter override");
                Help("The legacy override affects car behavior on spawn, not the game's numeric assist setting. Keep Off and use the game's assist controls.");
            }
        }

        /// <summary>
        /// The shared profile's bindings, edited in F6 through the host (SharedBindingCaptureHost): one row per action
        /// with its active value, a saved value waiting for the next start and an unsaved draft value, then Bind (Cancel
        /// with the seconds left while listening), Clear and Invert. Save writes the draft; the active profile stays in use
        /// until the game restarts. A missing or read-only profile shows the rows read-only with the host's reason.
        /// The panel reads only the host's snapshot: no device discovery or polling here.
        /// </summary>
        private static void SharedBindings()
        {
            var host = Bindings;
            if (host == null) return;
            Heading("Shared profile");
            var access = host.Access;
            bool editable = access == SharedBindingAccess.Editable;
            Help(host.ActiveSource);
            if (!editable && !string.IsNullOrEmpty(host.AccessReason)) W.StatusCard("bindings-access", access == SharedBindingAccess.Missing ? "No shared profile" : "Shared profile is read-only",
                host.AccessReason, false, null);
            string capturing = host.CapturingAction;
            if (capturing != null)
            {
                Help(host.CaptureMessage);
                var devices = host.Devices;
                if (devices != null && devices.Count > 1)
                {
                    var names = new string[devices.Count]; int selected = -1;
                    for (int i = 0; i < devices.Count; i++) { names[i] = devices[i].Name; if (devices[i].IsDefault) selected = i; }
                    int pick = W.ChoiceRow("binding-device", "Device", null, names, selected, true);
                    if (pick >= 0 && pick != selected) host.BeginCapture(capturing, devices[pick].InstanceGuid);
                }
            }
            else if (!string.IsNullOrEmpty(host.CaptureMessage)) Help(host.CaptureMessage);
            // Mutually exclusive with every other edit (an Art calibration, camera or Settings key capture, a connection edit):
            // shared actions start only when nothing else is being edited (kiki 123770).
            bool idle = !Editing;
            var rows = host.Rows;
            for (int i = 0; rows != null && i < rows.Count; i++) BindingRow(host, rows[i], editable, capturing, idle);
            if (editable && (host.HasDraft || host.SavedPendingRestart))
            {
                GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
                if (W.Command("bindings-save", "Save bindings", host.HasDraft && idle) && !host.Save(out string message)) Help(message);
                if (W.Command("bindings-discard", "Discard", host.HasDraft && idle)) host.Discard();
                W.Spacer();
                GUILayout.EndHorizontal();
                W.Help(host.HasDraft ? "Unsaved changes. Save bindings writes the profile; the game uses it from the next start."
                    : "Saved. Restart the game to use these bindings.");
            }
        }

        private static void BindingRow(ISharedBindingHost host, SharedBindingRowView row, bool editable, string capturing, bool idle)
        {
            string value = row.Active.Bound ? row.Active.Text : "Unbound";
            if (row.ActiveInverted) value += " · inverted";
            if (row.NextStart.HasValue) value += "  →  next start: " + (row.NextStart.Value.Bound ? row.NextStart.Value.Text : "Unbound");
            if (row.Draft.HasValue) value += "  ·  unsaved: " + (row.Draft.Value.Bound ? row.Draft.Value.Text : "Unbound");
            var layout = W.BeginRow(row.Label, value + (string.IsNullOrEmpty(row.Note) ? "" : "\n" + row.Note), 300);
            if (!layout.Stacked) W.Spacer();
            bool listening = capturing == row.Action;
            if (listening)
            {
                if (W.Command("bind-" + row.Action, "Cancel (" + Math.Ceiling(host.CaptureSecondsLeft).ToString("0") + " s)")) host.CancelCapture();
            }
            else if (W.Command("bind-" + row.Action, "Bind", editable && idle)) host.BeginCapture(row.Action);
            bool bound = row.Draft.HasValue ? row.Draft.Value.Bound : row.NextStart.HasValue ? row.NextStart.Value.Bound : row.Active.Bound;
            if (W.Command("clear-" + row.Action, "Clear", editable && idle && bound)) host.Clear(row.Action);
            if (row.IsAxis)
            {
                bool inverted = row.DraftInverted ?? row.NextStartInverted ?? row.ActiveInverted;
                if (W.Segment("invert-" + row.Action, "Invert", inverted, editable && idle && bound)) host.SetInverted(row.Action, !inverted);
            }
            W.EndRow(layout);
        }

        private static void Axis(Settings c, WheelInput.Channel channel, string label)
        {
            // A shared profile may set this control: no Art editor, which could reopen its readers.
            if (WheelInput.SharedReadersRequired && !WheelInput.SharedArtOnly(channel))
            { W.Caption(label); Help(WheelInput.SharedEditorText(channel)); GUILayout.Space(8 * SettingsPresentation.Scale); return; }
            W.Caption(label + (WheelInput.IsBound(channel) ? " · Device input: " + InputText(channel) : ""));
            Binding(c, channel, label, false);
            GUILayout.Space(8 * SettingsPresentation.Scale);
        }
        private static void Binding(Settings c, WheelInput.Channel channel, string label, bool inputLabel = true)
        {
            bool bound = WheelInput.IsBound(channel);
            W.Text(WheelInput.Describe(channel));
            if (bound && WheelInput.Assigning != channel) Bar(channel, "Device input", inputLabel);
            if (SettingsViewPolicy.Advanced(c) && !WheelInput.IsButtonChannel(channel) && bound)
                Help(WheelInput.CalibrationDescription(channel));
            if (WheelInput.Assigning == channel)
            {
                CalibrationEditor(c, label);
                return;
            }
            Help(!WheelInput.IsButtonChannel(channel)
                ? channel == WheelInput.Channel.Steer ? "Center first, then bind or calibrate." : "Release first, then bind or calibrate."
                : "Release the button first, then bind.");
            bool enabled = GUI.enabled;
            string id = "art-" + channel;
            GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
            GUI.enabled = enabled && !Editing && !GameState.IsDriving;
            if (W.Command(id + "-bind", "Bind " + label.ToLowerInvariant())) WheelInput.BeginCalibration(channel);
            GUI.enabled = enabled && !Editing && !GameState.IsDriving && bound;
            if (!WheelInput.IsButtonChannel(channel) && W.Command(id + "-calibrate", "Calibrate")) WheelInput.BeginCalibration(channel, true);
            if (W.Command(id + "-clear", "Clear"))
            { if (WheelInput.Clear(channel) && channel == WheelInput.Channel.Steer && FfbSelection.FollowsSteering(c)) Main.SelectForceDevice(); }
            GUI.enabled = enabled;
            W.Spacer();
            GUILayout.EndHorizontal();
        }
        private static void CalibrationEditor(Settings c, string label)
        {
            W.Caption("Binding " + label.ToLowerInvariant());
            W.Text(WheelInput.Status);
            var pending = WheelInput.PendingCalibration;
            if (pending != null)
            {
                W.Text("Device input: " + (WheelInput.CalibrationValue * 100).ToString("F0") + "%");
                if (!pending.IsDigital)
                {
                    pending.Inverted = Toggle(pending.Inverted, "Invert " + (WheelInput.Assigning == WheelInput.Channel.Handbrake ? "handbrake" : "axis"));
                    pending.Deadzone = Slider(pending.Deadzone, 0, .1f, 0, "Deadzone", 100, "%");
                }
            }
            Help("Escape cancels and keeps the previous binding.");
            bool enabled = GUI.enabled;
            GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
            W.Spacer();
            GUI.enabled = enabled && WheelInput.CanSaveCalibration;
            if (W.Command("calibration-save", pending != null && pending.IsDigital ? "Save binding" : "Save calibration"))
            {
                string previous = c.SteerBinding;
                if (WheelInput.SaveCalibration() && previous != c.SteerBinding && FfbSelection.FollowsSteering(c)) Main.SelectForceDevice();
            }
            GUI.enabled = enabled;
            if (W.Command("calibration-cancel", "Cancel")) WheelInput.CancelAssign();
            GUILayout.EndHorizontal();
        }
        private static string InputText(WheelInput.Channel channel)
        {
            float value = WheelInput.Value(channel);
            return !WheelInput.IsBound(channel) ? "Game controls / not bound here" : channel == WheelInput.Channel.Steer
                ? Math.Abs(value) < .005f ? "Center" : (value < 0 ? "Left " : "Right ") + (Math.Abs(value) * 100).ToString("F0") + "%"
                : (value * 100).ToString("F0") + "%";
        }
        private static void Bar(WheelInput.Channel channel, string label, bool showLabel = true)
        {
            if (showLabel) W.Text(label + ": " + InputText(channel));
            if (!WheelInput.IsBound(channel)) return;
            float value = WheelInput.Value(channel);
            var rect = GUILayoutUtility.GetRect(20, 8 * SettingsPresentation.Scale, GUILayout.Width(SettingsPresentation.BodyWidth));
            F6Widgets.Fill(rect, F6Palette.Track);
            var fill = rect;
            bool steering = channel == WheelInput.Channel.Steer;
            float travel = Math.Max(0, Math.Min(1, Math.Abs(value)));
            fill.width *= steering ? travel * .5f : travel;
            if (steering) fill.x += rect.width * .5f - (value < 0 ? fill.width : 0);
            F6Widgets.Fill(fill, F6Palette.Accent);
            if (steering) F6Widgets.Fill(new Rect(rect.x + rect.width * .5f, rect.y, 1, rect.height), F6Palette.Text);
        }
        private static void Feedback(Settings c)
        {
            Heading("Force feedback");
            bool enabled = Toggle(c.ForceFeedbackEnabled, "FFB");
            if (enabled != c.ForceFeedbackEnabled) Main.SetFeedbackEnabled(enabled);
            Panel.DrawWheelPicker();
            Heading("Steering");
            c.Strength = (int)Slider(c.Strength, 0, 100, 50, "Steering strength", 1, "%");
            Help("Cornering resistance and return-to-center feel. Crash, landing and shift strengths are independent.");
            if (!SettingsViewPolicy.Advanced(c))
            {
                if (LinkButton("Steering tuning, crashes and other effects >", 350)) Select(c, true, 1);
                return;
            }
            c.Smoothing = Slider(c.Smoothing, 0, .95f, .2f, "Smoothing", 100, "%");
            Help("Higher smoothing softens rapid force changes and delays their response.");
            c.Invert = Toggle(c.Invert, "Invert force direction");
            Help("Force reference: " + c.FyReference.ToString("0.##") + " N (standard " + Settings.DefaultFyReference.ToString("0.##") + " N; standard 50% matches the former 30%).");
            if (c.FyReference != Settings.DefaultFyReference && RightButton("Use standard reference", 180)) { c.FyReference = Settings.DefaultFyReference; Main.MarkSettingsDirty(); }
            Heading("Effects");
            c.LandingEffectsEnabled = Toggle(c.LandingEffectsEnabled, "Landing vibration");
            c.LandingStrength = Slider(c.LandingStrength, 0, 40, 5, "Landing strength", 1, "%");
            Help(LandingController.Status);
            c.CrashEffectsEnabled = Toggle(c.CrashEffectsEnabled, "Crash kick (experimental)");
            c.CrashStrength = Slider(c.CrashStrength, 0, 100, 50, "Crash strength", 1, "%");
            Help("Short constant push/release, separate from steering and telemetry. Full nominal force can saturate alongside steering. " + CrashController.Status);
            c.ShiftEffectsEnabled = Toggle(c.ShiftEffectsEnabled, "Shift vibration (experimental)");
            c.ShiftStrength = Slider(c.ShiftStrength, 0, 20, 5, "Shift strength", 1, "%");
            Help("A short wheel rumble when the player's gear engages. Landing and crash cues take priority. " + ImpactController.Status(ImpactKind.Shift));
            if (CommandRow("Keeps FFB Off/On, the device and all other settings.", "Reset FFB tuning", 150))
            { c.ResetFfbTuning(); Main.MarkSettingsDirty(); }
        }
        private static void Cameras(Settings c)
        {
            Heading("View and screens");
            W.Caption("Triple screens");
            Triple.TripleScreen.DrawSettings();
            GUILayout.Space(8 * SettingsPresentation.Scale);
            c.ShowFrameRate = Toggle(c.ShowFrameRate, "Show frame rate on screen");
            Help("Frame rate, last 10 s: " + FrameRate.Summary + ". The counter sits at the top right of the center screen.");
            if (Main.OtherCameraModLoaded) { Help(BonnetCamera.ExternalCameraHelp); return; }
            c.BonnetCameraEnabled = Toggle(c.BonnetCameraEnabled, "Bonnet");
            c.BumperCameraEnabled = Toggle(c.BumperCameraEnabled, "Bumper");
            Help("Included in the game's Change camera cycle. Change camera can be bound under Controls or in the game. Held Look behind uses the game's bindings. Close settings to adjust the active mount.");
            if (RightButton("Open game bindings", 180)) GameBindings.Open();
            Help(GameBindings.Status);
            _cameraKeys = Disclosure(_cameraKeys, "adjustment bindings");
            if (_cameraKeys)
            {
                Heading("Adjustment bindings");
                c.CameraTuningKeys = Toggle(c.CameraTuningKeys, "Live adjustment shortcuts");
                Help("Use keyboard keys or separate USB buttons; no numpad required. F8 and the Settings key are reserved. Bind only buttons not already used by the game.");
                for (int i = 0; i < CameraKeys.Bindings.Length; i++)
                {
                    var binding = CameraKeys.Bindings[i];
                    W.Caption(binding.Label);
                    GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
                    W.Inline("Keyboard: " + CameraKeys.Name(binding.Get(c)));
                    W.Spacer();
                    bool old = GUI.enabled; GUI.enabled = old && (!Editing || CameraKeys.Listening == i);
                    if (W.Command("camkey-bind-" + i, CameraKeys.Listening == i ? "Cancel" : "Bind key"))
                    { if (CameraKeys.Listening == i) CameraKeys.Cancel(); else CameraKeys.Begin(i); }
                    if (W.Command("camkey-clear-" + i, "Clear")) CameraKeys.Clear(c, i);
                    GUI.enabled = old;
                    GUILayout.EndHorizontal();
                    if (CameraKeys.Listening == i) Help(CameraKeys.Status);
                    Binding(c, WheelInput.CameraChannel(i), binding.Label + " (button)");
                    GUILayout.Space(8 * SettingsPresentation.Scale);
                }
                bool enabled = GUI.enabled; GUI.enabled = enabled && !Editing;
                if (RightButton("Restore numpad defaults", 180)) CameraKeys.Reset(c);
                GUI.enabled = enabled; Help(CameraKeys.Status);
            }
            if (!SettingsViewPolicy.Advanced(c))
            { if (SettingsViewPolicy.CustomCamera(c) && LinkButton("Review custom camera tuning in Advanced >", 275)) Select(c, true, 2); return; }
            Heading("Camera adjustment");
            _mount = Segmented(_mount, new[] { "Bonnet pose", "Bumper pose" }, "Camera pose", 230);
            if (RightButton("Reset " + (_mount == 0 ? "bonnet" : "bumper") + " view", 150)) { c.ResetCameraMount(_mount != 0); Main.MarkSettingsDirty(); }
            if (_mount == 0)
            {
                c.BonnetHeight = Slider(c.BonnetHeight, -.5f, 3, .95f, "Height", 1, " m");
                c.BonnetForward = Slider(c.BonnetForward, -3, 5, 1, "Forward", 1, " m");
                c.BonnetSide = Slider(c.BonnetSide, -2, 2, 0, "Side", 1, " m");
                c.BonnetPitch = Slider(c.BonnetPitch, -45, 45, 3, "Tilt", 1, "°");
                c.BonnetFOV = Slider(c.BonnetFOV, 40, 120, 75, "Field of view", 1, "°");
            }
            else
            {
                c.BumperHeight = Slider(c.BumperHeight, -.5f, 3, .45f, "Height", 1, " m");
                c.BumperForward = Slider(c.BumperForward, -3, 5, 1.9f, "Forward", 1, " m");
                c.BumperSide = Slider(c.BumperSide, -2, 2, 0, "Side", 1, " m");
                c.BumperPitch = Slider(c.BumperPitch, -45, 45, 2, "Tilt", 1, "°");
                c.BumperFOV = Slider(c.BumperFOV, 40, 120, 80, "Field of view", 1, "°");
            }
            c.BonnetLean = Slider(c.BonnetLean, 0, 1, .1f, "Corner lean (both views)", 100, "%");
            Help("Shortcut steps: one step per press; holding repeats about ten steps a second.");
            // Rounded only after an edit, so opening this page never rewrites a saved off-grid step.
            float move = Slider(c.CameraMoveStep, .005f, .25f, .02f, "Move per press", 100, " cm");
            if (move != c.CameraMoveStep) c.CameraMoveStep = Mathf.Round(move * 1000f) / 1000f;
            float tilt = Slider(c.CameraTiltStep, .1f, 10, 1, "Tilt per press", 1, "°");
            if (tilt != c.CameraTiltStep) c.CameraTiltStep = Mathf.Round(tilt * 10f) / 10f;
            float fov = Slider(c.CameraFovStep, .5f, 10, 2, "Field of view per press", 1, "°");
            if (fov != c.CameraFovStep) c.CameraFovStep = Mathf.Round(fov * 10f) / 10f;
            if (RightButton("Default steps", 150)) { c.ResetCameraSteps(); Main.MarkSettingsDirty(); }
        }
        private static void Telemetry(Settings c)
        {
            Heading("Output status");
            c.TelemetryEnabled = Toggle(c.TelemetryEnabled, "Telemetry");
            Help("Forza Horizon 5-compatible UDP. In SimHub choose that receiver and match this destination.");
            Heading("Destination");
            W.Text("Saved destination: " + c.TelemetryHost + ":" + c.TelemetryPort);
            Help(!c.TelemetryEnabled ? "Off" : TelemetryPump.ActiveEndpoint == null ? "Unavailable — pause to connect; inspect Help if it fails." :
                "Sending to " + TelemetryPump.ActiveEndpoint + ". UDP does not confirm receiver delivery.");
            if (!SettingsViewPolicy.Advanced(c))
            {
                if (CommandRow("Standard local SimHub receiver: 127.0.0.1:8000", "Use local preset", 140)) { c.TelemetryHost = "127.0.0.1"; c.TelemetryPort = 8000; Main.MarkSettingsDirty(); }
                if (LinkButton("Connection settings in Advanced >", 225)) Select(c, true, 3);
                return;
            }
            if (!Connection.Editing && RightButton("Edit connection", 130)) Connection.Begin(c);
            if (Connection.Editing)
            {
                Connection.Host = Field("telemetry-host", "Host", Connection.Host ?? "");
                Connection.Port = Field("telemetry-port", "Port", Connection.Port ?? "");
                GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
                W.Spacer();
                if (W.Command("connection-apply", "Apply connection") && Connection.Apply(c)) Main.MarkSettingsDirty();
                if (W.Command("connection-cancel", "Cancel")) Connection.Cancel();
                GUILayout.EndHorizontal();
                Help(Connection.Error);
            }
            Help("Connection applies atomically while paused. Recording remains a separate development probe and is not shipped in this panel.");
        }
        private static void Support(Settings c)
        {
            Heading("Recovery");
            W.Text("Art of Sim Rally " + Main.ModVersion);
            Help("No FFB: pause, open FFB, check On and the selected wheel, then Refresh. Missing controls: bind them in Controls.");
            Heading("Interface scale");
            int textMode = Segmented(c.SettingsFollowHostScale ? 1 : 0, new[] { "Auto", "Use UMM scale" }, "Settings text size", 240);
            c.SettingsFollowHostScale = textMode == 1;
            Help("Uses UMM's scale by default. Auto can enlarge only this mod at high screen resolutions. The surrounding window keeps UMM's own size preference.");
            Heading("Version and support details");
            string support = string.IsNullOrEmpty(SupportBundle.LastResult)
                ? "Includes settings, device identifiers, paths and logs; nothing is uploaded." : SupportBundle.LastResult;
            if (CommandRow(support, "Create support file", 160)) SupportBundle.Create();
            if (!SettingsViewPolicy.Advanced(c))
            { if (LinkButton("Diagnostic details in Advanced >", 220)) Select(c, true, 4); return; }
            c.DiagnosticLogging = Toggle(c.DiagnosticLogging, "Log detail for support");
            Help("Enable, reproduce briefly, pause, then create the support file. Detail logging switches itself off the next time the game starts.");
            Panel.DrawInputStatus();
        }

        // ---- shared-row helpers (the width arguments are kept from the earlier layout and no longer size anything) ----

        /// <summary>Guidance text, then its command at the right on the next line: nothing is clipped at any scale.</summary>
        private static bool CommandRow(string guidance, string label, float width)
        {
            if (!string.IsNullOrEmpty(guidance)) Help(guidance);
            GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
            W.Spacer();
            bool clicked = W.Command("cmd-" + label, label);
            GUILayout.EndHorizontal();
            return clicked;
        }
        private static bool RightButton(string label, float width) => CommandRow(null, label, width);
        private static bool LinkButton(string label, float width) => CommandRow(null, label, width);
        private static int Segmented(int value, string[] labels, string caption, float width)
        {
            int clicked = W.ChoiceRow("seg-" + caption, caption, null, labels, value, true);
            return clicked >= 0 ? clicked : value;
        }
        // The current player car's gearbox while driving, so the shared-profile hint never claims gears work on an automatic.
        private static bool? SharedCarAutomatic()
        {
            if (!GameState.IsDriving) return null;
            try
            {
                var car = GameState.ExistingManager?.playerManager?.carcontroller;
                var drivetrain = car == null ? null : car.GetComponent<Drivetrain>();
                return drivetrain == null ? (bool?)null : drivetrain.automatic;
            }
            catch { return null; }
        }
        private static bool Toggle(bool value, string label) => W.ToggleRow("toggle-" + label, label, null, value, true);
        /// <summary>
        /// A value row: slider, minus, value, plus and Default. The slider keeps Art's continuous values; minus and plus
        /// move one readable step. With no input the saved value is returned unchanged (never rounded on draw).
        /// </summary>
        private static float Slider(float value, float min, float max, float normal, string label, float scale, string unit)
        {
            double shown = value * (double)scale;
            double proposed = W.NumberRow("slider-" + label, label, null, shown, min * (double)scale, max * (double)scale,
                Step((max - min) * (double)scale), "0.##", unit, true, normal * (double)scale);
            if (proposed == shown || double.IsNaN(proposed) || double.IsInfinity(proposed)) return value;
            return Mathf.Clamp((float)(proposed / scale), min, max);
        }
        /// <summary>About one hundredth of the range, rounded to 1, 2 or 5 × 10^n (1% for 0..100, 0.05 m for −0.5..3 m).</summary>
        private static double Step(double range)
        {
            double raw = Math.Max(1e-6, range / 100), power = Math.Pow(10, Math.Floor(Math.Log10(raw))), m = raw / power;
            return (m < 1.5 ? 1 : m < 3.5 ? 2 : m < 7.5 ? 5 : 10) * power;
        }
        private static string Field(string id, string label, string value)
        {
            W.Caption(label);
            GUI.SetNextControlName(id);
            W.Register(id);
            string result = GUILayout.TextField(value, GUILayout.Width(Math.Min(360 * SettingsPresentation.Scale, SettingsPresentation.BodyWidth)));
            W.Reveal(id);
            return result;
        }
        private static void Heading(string text) => W.Heading(text);
        private static bool Disclosure(bool open, string label)
        {
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !Editing;
            GUILayout.BeginHorizontal(GUILayout.Width(SettingsPresentation.BodyWidth));
            if (W.Command("disclosure-" + label, (open ? "Hide " : "Show ") + label)) open = !open;
            W.Spacer();
            GUILayout.EndHorizontal();
            GUI.enabled = enabled;
            return open;
        }
        private static void Help(string text) { if (!string.IsNullOrEmpty(text)) W.Help(text); }
    }
}
