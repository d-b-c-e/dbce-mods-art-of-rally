using UnityEngine;

namespace ArtOfSimRally.Mod
{
    internal static class WheelInput
    {
        internal enum Channel { CameraUp, CameraDown, CameraForward, CameraBack, CameraLeft, CameraRight, CameraPitchDown, CameraPitchUp, CameraFovUp, CameraFovDown, CameraReset }
        internal static readonly HashSet<Channel> Held = new(), Pressed = new();
        internal static Channel CameraChannel(int index) => (Channel)index;
        internal static float Value(Channel c) => Held.Contains(c) ? 1 : 0;
        internal static bool ShortcutPressed(Channel c) => Pressed.Remove(c);
    }
    // These test-only names shadow Unity's native input/clock ECalls when linking
    // the production tuner. No Unity scene, GUI or physical input is simulated.
    internal static class Input
    {
        public static readonly HashSet<KeyCode> Held = new();
        public static readonly HashSet<KeyCode> Pressed = new();
        public static bool anyKey => Held.Count != 0;
        public static bool GetKey(KeyCode key) => key != KeyCode.None && Held.Contains(key);
        public static bool GetKeyDown(KeyCode key) => key != KeyCode.None && Pressed.Contains(key);
        public static void Release() { Held.Clear(); Pressed.Clear(); }
    }
    internal static class Time { public static float unscaledTime, unscaledDeltaTime = .02f; }
    internal static class Application { public static bool isFocused = true; }
    internal static class GameState { public static bool IsDriving = false; }
    internal static class BonnetCamera { public enum View { None, Bonnet, Bumper } }
    // No car is tracked offline, so reset falls back to the defaults, as CameraMountMemory.Reset does without a car.
    internal static class CameraMountMemory { public static void Reset(Settings c, bool bumper) => c.ResetCameraMount(bumper); }
    internal static class ModLog
    {
        public static readonly List<string> Messages = new();
        public static void Info(string message) => Messages.Add(message);
    }
    internal static class Main
    {
        public static Settings Settings = new();
        public static bool Enabled = true, SettingsVisible = false;
        public static string Path;
        public static int Saves;
        public static bool SaveSettings()
        {
            Saves++;
            try { SettingsPersistence.Write(Settings, Path); return true; }
            catch (IOException) { return false; }
        }
    }
}

namespace ArtOfSimRally.Mod.Triple
{
    internal static class TripleScreen { public static void ReannounceCamera(string note = null) { } }
}
