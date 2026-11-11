using System;
using Rewired;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// The parts of the settings panel that read live hardware: the force feedback device picker and what the game's
    /// input layer can see.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="SettingsPanel"/>, which is only layout over stored values. These blocks talk to
    /// DirectInput and Rewired, and each is drawn inside the section of the feature it belongs to.
    /// </remarks>
    internal static class Panel
    {
        private static GUIStyle Wrap => new GUIStyle(GUI.skin.label) { wordWrap = true };

        private static string[] _ffbDevices;
        private static bool _ffbListed;
        private static string _ffbSelectionStatus = "";
        // An FFB device change reopens every wheel reader, which would stop the shared profile.
        internal const string DeviceChangeNeedsRestart = "Device changes need a restart while the shared profile is in use.";

        /// <summary>Forces the device list to be re-read.</summary>
        public static void Rescan()
        {
            _ffbListed = false;
            DeviceDropdown.CloseAll();
        }

        public static void DrawWheelPicker()
        {
            var cfg = Main.Settings;
            if (GameState.IsDriving) { GUILayout.Label("Pause before changing the FFB device.", Wrap); return; }
            if (WheelReaders.Required)
            {
                GUILayout.Label("FFB device: " + (FfbSelection.TryTarget(cfg, out var name, out _, out _, out var why) ? name : why), Wrap);
                GUILayout.Label(DeviceChangeNeedsRestart, Wrap);
                return;
            }
            if (!_ffbListed) { _ffbDevices = FfbNative.ListDevices(); _ffbListed = true; }
            bool follow = FfbSelection.FollowsSteering(cfg);
            int position = follow ? 0 : FfbNative.SelectedPosition(cfg) + 1;
            bool missing = !follow && position == 0;
            var labels = new string[_ffbDevices.Length + 1 + (missing ? 1 : 0)];
            labels[0] = "Follow the shared profile's steering wheel";
            for (int i = 0; i < _ffbDevices.Length; i++)
            {
                string guid = FfbNative.DeviceGuid(i);
                labels[i + 1] = _ffbDevices[i] + (guid.Length >= 6 ? " · " + guid.Substring(guid.Length - 6) : " (identity unavailable)");
            }
            if (missing) { position = labels.Length - 1; labels[position] = "Saved wheel unavailable: " + cfg.PreferredDevice; }
            int chosen = DeviceDropdown.Draw("wheel", "FFB device", labels, position,
                "No devices found.", _ffbDevices.Length + 1);
            if (chosen >= 0 && chosen <= _ffbDevices.Length)
            {
                string oldMode = cfg.FfbDeviceMode, oldName = cfg.PreferredDevice, oldGuid = cfg.PreferredDeviceGuid;
                int oldIndex = cfg.PreferredDeviceIndex;
                bool saved = SettingsCommit.TrySave(() => {
                    cfg.FfbDeviceMode = chosen == 0 ? "steering" : "explicit";
                    if (chosen > 0) { cfg.PreferredDeviceIndex = chosen - 1; cfg.PreferredDevice = _ffbDevices[chosen - 1]; cfg.PreferredDeviceGuid = FfbNative.DeviceGuid(chosen - 1); }
                }, () => { cfg.FfbDeviceMode = oldMode; cfg.PreferredDevice = oldName; cfg.PreferredDeviceGuid = oldGuid; cfg.PreferredDeviceIndex = oldIndex; });
                _ffbSelectionStatus = saved ? "Selection saved." : "Could not save. Previous FFB device kept; check Settings.xml is writable.";
                if (saved) Main.SelectForceDevice();
            }
            if (!string.IsNullOrEmpty(_ffbSelectionStatus)) GUILayout.Label(_ffbSelectionStatus, Wrap);
            if (!FfbSelection.TryTarget(cfg, out _, out _, out _, out var reason)) GUILayout.Label(reason, Wrap);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool refresh = GUILayout.Button("Refresh devices", SettingsPresentation.Width(150));
            GUILayout.EndHorizontal();
            if (refresh) { Rescan(); Main.SelectForceDevice(); }
        }

        public static void DrawInputStatus()
        {
            try
            {
                if (!ReInput.isReady) { GUILayout.Label("Input system not started yet.", Wrap); return; }

                var joysticks = ReInput.controllers.Joysticks;
                GUILayout.Label("What the game can see: " +
                                (joysticks == null ? 0 : joysticks.Count) + " controller(s)", Wrap);

                if (joysticks != null)
                    foreach (var j in joysticks)
                        GUILayout.Label("      " + j.name +
                                        (j.hardwareTypeGuid == Guid.Empty ? "   (no profile)" : ""),
                                        Wrap);

                // "Not recognised" reads like a fault, and it is not. Say what it
                // actually means, since it is the reason several fixes exist.
                GUILayout.Label(
                    "\"No profile\" only means the game's input library has no built-in entry " +
                    "for that model - its database predates most direct-drive wheels. Nothing " +
                    "is broken; it is why the steering and deadzone fixes above exist.", Wrap);

                if (joysticks != null && joysticks.Count < 2)
                    GUILayout.Label(
                        "Shifters and handbrakes often do not appear here at all, because the " +
                        "game's input layer skips devices that do not report as a joystick. " +
                        "The shared profile reads them directly.", Wrap);

                if (GUILayout.Button("Rescan devices", SettingsPresentation.Width(160))) Rescan();
            }
            catch (Exception ex)
            {
                GUILayout.Label("Could not read input state: " + ex.Message, Wrap);
            }
        }
    }
}
