using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace ArtOfSimRally.Mod
{
    public static partial class Main
    {
        private static bool _uiDirty;
        private static float _uiSaveAt;
        private static bool _settingsWereVisible;
        internal static bool SuppressHostClose;
        /// <summary>Set by UiHostHook once the shared panel has loaded; null means it could not, and F6 opens the UMM panel.</summary>
        internal static Action<bool> HookPanelTick { get; set; }
        public static string SettingsSaveStatus { get; private set; } = "Saved";
        internal static string ModVersion => _modEntry?.Info.Version ?? "Development build";
        // Keep Unity ECalls out of methods patched by the separate CLR probe
        // fixture. The game evaluates this boundary on its own Mono runtime.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static bool HasFocus() => Application.isFocused;
        public static void MarkSettingsDirty()
        { _uiDirty = true; _uiSaveAt = Time.realtimeSinceStartup + .4f; SettingsSaveStatus = "Changes pending"; }
        internal static void FlushUiSettings(bool force = false)
        {
            if (!_uiDirty || (!force && (GameState.IsDriving || Time.realtimeSinceStartup < _uiSaveAt))) return;
            _uiSaveAt = Time.realtimeSinceStartup + 5;
            SaveSettings();
        }
        // ISS-0015: F8 (beside F7) or the F6 toggle switched force feedback off silently, and it stayed off after a
        // restart. Every switch-off is now logged and shown on screen (wired in Load).
        internal const string FeedbackOffNotice = "Force feedback off. It stays off until you turn it on in F6 > FFB";
        internal static Action<string> ReportFeedbackOff = _ => { };
        internal static void SetFeedbackEnabled(bool enabled)
        {
            if (Settings == null) return;
            ApplyFeedbackEnabled(enabled);
            MarkSettingsDirty();
        }
        // The switch without the save: the hook F6 (UiHostHook) writes only ForceFeedbackEnabled itself (STD-044).
        internal static void ApplyFeedbackEnabled(bool enabled)
        {
            if (Settings == null) return;
            if (Settings.ForceFeedbackEnabled && !enabled) ReportFeedbackOff(FeedbackOffNotice);
            Settings.ForceFeedbackEnabled = enabled;
            FfbNative.StopOutputs();
            if (!enabled) CancelForceRecovery();
            else if (!FfbNative.Ready) ReopenForceFeedback();
        }
        internal static void StopFeedback()
        {
            SetFeedbackEnabled(false);
            // Explicit stop is rare and user initiated; stop hardware before I/O.
            FlushUiSettings(true);
        }
        internal static void SelectForceDevice()
        {
            FfbNative.Shutdown();
            if (Settings.ForceFeedbackEnabled) ReopenForceFeedback();
        }
        private static readonly PropertyInfo UmmSelection = typeof(UnityModManager.UI).GetProperty("ShowModSettings", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo UmmFilter = typeof(UnityModManager.UI).GetField("mModFilter", BindingFlags.Instance | BindingFlags.NonPublic);
        internal static void ToggleSettings()
        {
            var ui = UnityModManager.UI.Instance;
            if (ui == null || _modEntry == null) return;
            if (SettingsVisible) { CloseSettings(); return; }
            ui.tabId = 0;
            UmmFilter?.SetValue(ui, _modEntry.Info.DisplayName);
            ui.ToggleWindow(true);
            UmmSelection?.SetValue(ui, UnityModManager.modEntries.IndexOf(_modEntry), null);
            FfbNative.StopOutputs();
            CameraTuner.SuppressUntilRelease();
        }
        internal static void CloseSettings()
        {
            // One close intent: cancel an edit first, then hold once for an unsaved binding draft (DraftCloseGuard).
            if (SettingsPanel.CancelPendingEdit() || SettingsPanel.HoldCloseForDraft()) return;
            UnityModManager.UI.Instance?.ToggleWindow(false);
            FlushUiSettings(true);
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal static void TickSettingsUi()
        {
            if (Settings == null) return;
            // The shared profile's Mute force feedback and Mod menu buttons (read every frame so an edge is never stale).
            bool stopButton = ProfileButtons.Pressed(ProfileButtons.MuteFfb);
            bool settingsButton = ProfileButtons.Pressed(ProfileButtons.ModMenu);
            if (Application.isFocused && (Input.GetKeyDown(KeyCode.F8) || (!SettingsPanel.Editing && stopButton))) StopFeedback();
            // The shared panel draws F6 and opens on F6 itself; the wheel's Settings button goes to it.
            if (HookPanelTick != null) HookPanelTick(settingsButton);
            else if (Enabled && Application.isFocused && (settingsButton || (!CameraKeys.ModifierHeld() && Input.GetKeyDown(Settings.SettingsKey))))
            {
                if (!SettingsPanel.Editing) ToggleSettings();
            }
            if (!Application.isFocused) SettingsPanel.CancelPendingEdit();
            if (SettingsVisible && !_settingsWereVisible) FfbNative.StopOutputs();
            if (!SettingsVisible && _settingsWereVisible)
            { SettingsPanel.CancelPendingEdit(); DraftCloseGuard.Reset(); CameraTuner.SuppressUntilRelease(); FlushUiSettings(true); }
            _settingsWereVisible = SettingsVisible;
            GameBindings.Tick();
            CameraKeys.Tick();
            FlushUiSettings();
        }
    }

    // UMM handles Escape on key-up outside OnGUI. Preserve cancel-first semantics
    // for our active edit without replacing the host's input/cursor management.
    [HarmonyPatch(typeof(UnityModManager.UI), "ToggleWindow", new[] { typeof(bool) })]
    internal static class SettingsCloseGuard
    {
        [HarmonyPrefix]
        private static bool BeforeClose(bool open)
        {
            if (Main.Enabled) StockUiInput.Capture();
            if (open || !Main.Enabled) return true;
            if (Main.SuppressHostClose) { Main.SuppressHostClose = false; return false; }
            // The same close intent as Main.CloseSettings: an acknowledged draft warning is not re-armed here.
            return !(SettingsPanel.CancelPendingEdit() || SettingsPanel.HoldCloseForDraft());
        }
    }
}
