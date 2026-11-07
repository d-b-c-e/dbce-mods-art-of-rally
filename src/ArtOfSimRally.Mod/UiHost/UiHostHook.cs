using System;
using System.IO;
using Dbce.Wheel.Ui;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Developer-only F6 drawn by the toolkit's panel core inside the game's own frames (UI-4 spike, host A of
    /// docs/architecture/2026-11/00-overview.md section 3). Only with <c>[Dev] UiHost=hook</c> in <c>dbce-dev.ini</c>
    /// beside the mod: it loads <c>dbce_ui.dll</c> (its present and input hooks start themselves), hands it the FFB and
    /// Cameras pages, applies each change through the settings store with the single-element writer (STD-044), and
    /// forwards the wheel's menu D-pad, Confirm and Back as navigation while F6 is open. Without the file nothing here
    /// runs and F6 stays the UMM panel. Everything runs on the mod's own Update; the core never calls back.
    /// </summary>
    internal static class UiHostHook
    {
        private const string DescriptorResource = "ArtOfSimRally.Mod.UiHost.settings.json";
        private static string _settingsPath;
        private static float _nextPush;

        /// <summary>The core's F6 is open (read once per frame); counts as settings visible for every input block.</summary>
        internal static bool IsOpen { get; private set; }

        internal static void Load(string modDir, string settingsPath)
        {
            if (!UiHostRows.HookRequested(modDir)) return;
            try
            {
                if (!DbceUi.Load(Path.Combine(modDir, "dbce_ui.dll"), out string error))
                { ModLog.Warning("UiHost=hook: " + error + "; F6 stays the standard panel."); return; }
                string json;
                using (var stream = typeof(UiHostHook).Assembly.GetManifestResourceStream(DescriptorResource))
                using (var reader = new StreamReader(stream))
                    json = reader.ReadToEnd().Replace("@VERSION@", Main.ModVersion);
                // ffbStop off: F8 keeps the mod's own path (TickSettingsUi), exactly as in the standard build.
                if (!DbceUi.Configure(json, Path.Combine(modDir, UiHostRows.DevFile), false, out error))
                { ModLog.Warning("UiHost=hook: descriptor refused: " + error + "; F6 stays the standard panel."); return; }
                _settingsPath = settingsPath;
                Push();
                Main.HookPanelTick = Tick;
                ModLog.Info("UiHost=hook: F6 is drawn by dbce_ui.dll (developer switch in " + UiHostRows.DevFile + ").");
            }
            catch (Exception ex) { ModLog.Warning("UiHost=hook failed to start; F6 stays the standard panel: " + ex.Message); }
        }

        /// <summary>Runs in place of the standard F6 toggle, from Main.TickSettingsUi.</summary>
        private static void Tick(bool settingsButton)
        {
            // Read every frame so an edge is never stale when F6 opens.
            bool up = WheelInput.ShortcutPressed(WheelInput.Channel.NavUp), down = WheelInput.ShortcutPressed(WheelInput.Channel.NavDown);
            bool left = WheelInput.ShortcutPressed(WheelInput.Channel.NavLeft), right = WheelInput.ShortcutPressed(WheelInput.Channel.NavRight);
            bool select = WheelInput.ShortcutPressed(WheelInput.Channel.Confirm), back = WheelInput.ShortcutPressed(WheelInput.Channel.Back);
            bool open = DbceUi.IsOpen;
            if (settingsButton && Main.Enabled && Application.isFocused) DbceUi.Navigate(open ? DbceUiNav.Close : DbceUiNav.Open);
            else if (open)
            {
                if (up) DbceUi.Navigate(DbceUiNav.Up);
                if (down) DbceUi.Navigate(DbceUiNav.Down);
                if (left) DbceUi.Navigate(DbceUiNav.Left);
                if (right) DbceUi.Navigate(DbceUiNav.Right);
                if (select) DbceUi.Navigate(DbceUiNav.Select);
                if (back) DbceUi.Navigate(DbceUiNav.Back);
            }
            IsOpen = DbceUi.IsOpen;
            while (DbceUi.TryPollEvent(out var e)) Apply(e);
            if (IsOpen && Time.realtimeSinceStartup >= _nextPush) { _nextPush = Time.realtimeSinceStartup + .25f; Push(); }
            while (DbceUi.TryPollLog(out string line)) ModLog.Info(line);
        }

        private static void Apply(DbceUiEvent e)
        {
            var c = Main.Settings;
            var row = UiHostRows.Find(e.Id);
            if (c == null || e.Type != DbceUiEventType.Changed || row == null)
            { ModLog.Info("UiHost=hook: ignored " + e.Type + " " + e.Id); return; }
            bool bumper = BumperDriven();
            var before = UiHostRows.Snapshot(c, row);
            double value = row.Kind == UiHostRows.Kind.Toggle ? (e.Toggle ? 1 : 0) : e.Number;
            if (row.Id == "ffb.enabled") Main.ApplyFeedbackEnabled(e.Toggle);   // the F6 switch's side effects, no full save
            row.Set(c, bumper, value);
            if (row.Id == "cameras.fov") CameraMountMemory.Capture(c);
            try
            {
                var written = UiHostRows.Save(_settingsPath, c, before);
                DbceUi.NoteSaved(true);
                ModLog.Info("UiHost=hook: " + row.Id + " = " + value + "; wrote " + (written.Count == 0 ? "nothing" : string.Join(", ", written.ToArray())));
            }
            catch (Exception ex)
            {
                DbceUi.NoteSaved(false, "Could not save. Check the mod folder is writable.");
                ModLog.Warning("UiHost=hook: could not save " + row.Id + ": " + ex.Message);
            }
            Push();
        }

        private static void Push()
        {
            var c = Main.Settings;
            if (c == null) return;
            bool bumper = BumperDriven();
            foreach (var row in UiHostRows.Rows)
            {
                if (row.Kind == UiHostRows.Kind.Toggle) DbceUi.SetToggle(row.Id, row.Get(c, bumper) != 0);
                else DbceUi.SetNumber(row.Id, row.Get(c, bumper));
            }
            DbceUi.SetText("ffb.status", !c.ForceFeedbackEnabled ? "Off" : FfbNative.Ready ? "On" : "Starts when you drive",
                c.ForceFeedbackEnabled ? DbceUiTone.Normal : DbceUiTone.Warning);
            DbceUi.SetText("cameras.view", bumper ? "Adjusting the bumper camera" : "Adjusting the bonnet camera");
        }

        private static bool BumperDriven()
        {
            var camera = UIManager.Instance?.PanelManager?.mainCamera;
            var rig = camera == null || camera.transform.parent == null ? null : camera.transform.parent.GetComponent<CarCameras>();
            return rig != null && BonnetCamera.ActiveView(rig) == BonnetCamera.View.Bumper;
        }
    }
}
