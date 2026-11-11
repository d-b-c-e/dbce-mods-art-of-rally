using UnityEngine;

namespace ArtOfSimRally.Mod
{
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
        public static void MarkSettingsDirty() => CameraTuner.MarkDirty();
        public static bool SaveSettings()
        {
            Saves++;
            try
            {
                // Same edit boundary as production Main.SaveSettings. Reflection
                // also permits the actual rc38 source (without Capture) in RED modes.
                typeof(CameraMountMemory).GetMethod("Capture", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.Invoke(null, new object[] { Settings });
                SettingsPersistence.Write(Settings, Path); return true;
            }
            catch (IOException) { return false; }
        }
    }
}

namespace ArtOfSimRally.Mod.Triple
{
    internal static class CameraOverlay { internal enum Kind { Chase, Bonnet, Bumper } }
    internal static class TripleScreen
    {
        public static bool Realistic;
        public static double? RealisticStartDegrees = 34;
        public static bool RealisticFor(CameraOverlay.Kind kind) => Realistic;
        public static void ReannounceCamera(string note = null) { }
    }
}

namespace ArtOfSimRally.Mod
{
    // Managed host doubles for the production ChaseFov consumer, not a substitute
    // implementation of its offsets, reset, set choice or persistence.
    internal sealed class Camera { public float fieldOfView; public Transform transform = new(); }
    internal sealed class CameraAngle { }
    internal sealed class CarCameras
    {
        public readonly List<CameraAngle> CameraAnglesList = new();
        public CameraAngle CurrentCameraAngle;
        public Transform target = new() { name="fixture car" };
    }
    internal sealed class PanelManager { public Camera mainCamera; }
    internal sealed class UIManager
    {
        public static UIManager Instance = new();
        public PanelManager PanelManager = new();
    }
}

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPrefix : Attribute { }
    internal static class AccessTools
    {
        internal delegate F FieldRef<T, F>(T instance);
        internal static Type TypeByName(string name) => name == "RallyCustomVehicles.Main" ? typeof(ArtOfSimRally.Mod.CameraFixture) : null;
        internal static FieldRef<T, F> FieldRefAccess<T, F>(string name)
        {
            if (typeof(T) != typeof(ArtOfSimRally.Mod.CarCameras) || typeof(F) != typeof(List<ArtOfSimRally.Mod.CameraAngle>) || name != "CameraAnglesList")
                throw new InvalidOperationException("Unexpected game field");
            return instance => (F)(object)((ArtOfSimRally.Mod.CarCameras)(object)instance).CameraAnglesList;
        }
    }
    internal sealed class Traverse
    {
        public static Traverse Create(Type type) => new();
        public Traverse Field(string name) { if (name != "activePackage" && name != "id") throw new Exception("Unexpected reflected field"); return this; }
        public Traverse Property(string name) { if (name != "Manifest") throw new Exception("Unexpected reflected property"); return this; }
        public T GetValue<T>() => (T)(object)ArtOfSimRally.Mod.CameraFixture.CustomId;
    }
}
