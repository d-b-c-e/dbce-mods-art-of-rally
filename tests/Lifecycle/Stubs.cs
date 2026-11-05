// Minimal test doubles for ownership and callback ordering. These intentionally
// make no claim to simulate Unity transforms, rendering, destroyed objects or Harmony.
using ArtOfSimRally.Mod;

namespace HarmonyLib
{
    public class HarmonyPatch : Attribute { public HarmonyPatch(Type type, string name) { } }
    public class HarmonyPrefix : Attribute { }
    public class HarmonyPostfix : Attribute { }
    public static class AccessTools
    {
        public delegate ref F FieldRef<T, F>(T value);
        public static FieldRef<T,F> FieldRefAccess<T,F>(string name)
        {
            if (name == "CameraAnglesList") return (FieldRef<T,F>)(object)(FieldRef<CarCameras,List<CameraAngle>>)(x => ref x.CameraAnglesList);
            return (FieldRef<T,F>)(object)(FieldRef<CarCameras,CarDynamics>)(x => ref x.cardynamics);
        }
    }
}
namespace UnityEngine
{
    public sealed class DefaultExecutionOrder : Attribute { public DefaultExecutionOrder(int order) { } }
    public class MonoBehaviour { public static void DontDestroyOnLoad(object value) { } }
    public enum HideFlags { HideAndDontSave }
    public class GameObject
    {
        public HideFlags hideFlags;
        public GameObject(string name) { }
        public T AddComponent<T>() where T : new() => new T();
    }
    public struct Vector3
    {
        public float x,y,z;
        public Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; }
        public static Vector3 zero => new Vector3();
        public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
    }
    public struct Quaternion
    {
        public float w;
        public static Quaternion identity => new Quaternion { w=1 };
        public static Quaternion Euler(float x,float y,float z) => identity;
        public static Quaternion operator *(Quaternion a,Quaternion b) => a;
        public static Quaternion Slerp(Quaternion a,Quaternion b,float t) => b;
        public static float Angle(Quaternion a,Quaternion b) => 0f;
        public static Vector3 operator *(Quaternion a,Vector3 b) => b;
    }
    public class Transform
    {
        public Vector3 position, localPosition; public Quaternion rotation, localRotation;
        public T GetComponentInParent<T>() where T : class => null;
        public T[] GetComponentsInChildren<T>() => new T[0];
    }
    public class Camera { public Transform transform = new Transform(); public float fieldOfView=60, nearClipPlane=.3f; }
    public class Renderer { public Rendering.ShadowCastingMode shadowCastingMode; }
    public class MeshRenderer : Renderer { }
    public class SkinnedMeshRenderer : Renderer { }
    public static class Mathf
    {
        public static float Clamp(float v,float min,float max) => Math.Clamp(v,min,max);
        public static float Lerp(float a,float b,float t) => a+(b-a)*Math.Clamp(t,0,1);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Max(float a,float b) => Math.Max(a,b);
        public static float Min(float a,float b) => Math.Min(a,b);
    }
    public static class Time { public static float realtimeSinceStartup, unscaledDeltaTime=.016f, deltaTime=.016f; public static int frameCount; }
    public static class Application { public static string version="fixture", unityVersion="fixture"; public static bool isFocused=true; }
}
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly } }
public class CameraAngle
{
    public enum CameraAngles { CAMERA1 }
    public CameraAngle(float a,float b,float c,CameraAngles angle) { }
}
public class CarCameras
{
    public List<CameraAngle> CameraAnglesList = new List<CameraAngle>();
    public CarDynamics cardynamics;
    public CameraAngle CurrentCameraAngle;
    public UnityEngine.Transform target = new UnityEngine.Transform();
    public bool enabled = true;
    public int Snaps, Refreshes;
    // Selection part of the shipped method: clamp the saved preset without writing it back.
    // Native pose math is intentionally outside this fixture.
    public void RefreshCameraType() { Refreshes++; CurrentCameraAngle=CameraAnglesList[Math.Clamp(SaveGame.GetInt("SETTINGS_CAMERA_PRESET",3),0,CameraAnglesList.Count-1)]; }
    public void SetToWantedPositionImmediate() { Snaps++; }
}
public static class SaveGame
{
    public static int? Preset;
    public static int GetInt(string key,int fallback) => Preset ?? fallback;
}
public class CameraManager { }
public class CarDynamics { public Axles axles; public UnityEngine.Transform transform = new UnityEngine.Transform(); }
public class Axles { public Axle frontAxle; }
public class Axle { public Wheel leftWheel, rightWheel; }
public class Wheel { public float slipAngle; }
public class UIManager
{
    public static UIManager Instance = new UIManager();
    public PanelManager PanelManager = new PanelManager();
}
public class PanelManager { public UnityEngine.Camera mainCamera = new UnityEngine.Camera(); }
namespace ArtOfSimRally.Mod
{
    internal class Settings
    {
        public bool BonnetCameraEnabled=true, BumperCameraEnabled=true;
        public float BonnetLean=0, BonnetSide=0, BonnetHeight=1, BonnetForward=1, BonnetPitch=0, BonnetFOV=75;
        public float BumperSide=0, BumperHeight=1, BumperForward=1, BumperPitch=0, BumperFOV=75;
        public float FyReference=11500, GainFromStrength=1, Smoothing=0;
        public bool Invert=false, DiagnosticLogging=false, ForceFeedbackEnabled=true;
    }
    internal static class Calls { public static List<string> Log = new List<string>(); }
    internal static class ShimStartup
    {
        public static bool Requested => false;
        public static void Tick() { }
        public static void Shutdown() { }
    }
    internal static class SharedInputStartup
    {
        public static void Tick() { }
        public static void Shutdown() { }
    }
    internal static class Main
    {
        public static Settings Settings = new Settings(); public static bool Enabled=true, OtherCameraModLoaded=false;
        public static void RecoverForceFeedback() => Calls.Log.Add("force-recover");
        public static void CancelForceRecovery() { }
        public static bool SettingsVisible = false;
        public static void TickSettingsUi() { }
        public static bool HasFocus() => UnityEngine.Application.isFocused;
        public const string FeedbackOffNotice = "Force feedback off.";
        public static void ReportFeedbackOff(string notice) => Calls.Log.Add("ffb-off-notice");
    }
    internal static class GameState { public static bool IsDriving=false, IsPlayerView=true; }
    internal static class ModLog { public static void Info(string value) { } public static void Warning(string value) { } }
    internal static class CameraMountMemory { public static void Track(UnityEngine.Transform target, Settings cfg, bool bumper) { } }
    internal static class ChaseFov { public static void Apply(CarCameras rig, Settings cfg) { } }
    internal static class CameraTuner
    {
        public static float LensFov(float value) => value;
        public static void Update(BonnetCamera.View view) { }
        public static void Flush(bool shutdown=false) => Calls.Log.Add("camera-save");
    }
    internal static class InputBackend { public static void Tick() { } }
    internal static class FrameRate { public static void Tick() { } public static void Draw() { } }
    internal static class FrameHealthPersistence
    {
        public static void Flush(bool shutdown=false) => Calls.Log.Add("diagnostic-save");
    }
    internal static class WheelInput
    {
        public enum Channel { Steer, Throttle, Brake, Clutch, Handbrake }
        public static bool Enabled => true;
        public static float Value(Channel c) => 0;
        public static void EnsureUpdatedThisFrame() => Calls.Log.Add("input-snapshot");
        public static void Close() => Calls.Log.Add("input-close");
        public static void ResetGameButtons() => Calls.Log.Add("buttons-reset");
        public static void FlushLearnedRanges(bool shutdown=false) => Calls.Log.Add("save");
    }
    internal static class GameButtonInput { public static void TickTitle() => Calls.Log.Add("title-tick"); }
    internal static class FfbNative
    {
        public static void SetForce(int value) => Calls.Log.Add("force:"+value);
        public static void Shutdown() => Calls.Log.Add("native-close");
        public static void ReleaseInputs() => Calls.Log.Add("native-input-close");
    }
    internal static class FfbController { public static void Reset() => Calls.Log.Add("filter-reset"); }
    internal static class ImpactController
    {
        public static void Tick() => Calls.Log.Add("landing-tick");
        public static void Shutdown() => Calls.Log.Add("landing-stop");
    }
    internal static class TelemetryPump
    {
        public static void Prepare() => Calls.Log.Add("telemetry-prepare");
        public static void StopIfDisabled() => Calls.Log.Add("telemetry-stop-disabled");
        public static void Park() => Calls.Log.Add("telemetry-park");
        public static void Shutdown() => Calls.Log.Add("telemetry-close");
    }
    internal static class Shifter
    {
        public static void Close() => Calls.Log.Add("shifter-close");
        public static void FlushSelection(bool shutdown=false) => Calls.Log.Add("shifter-save");
    }
}

namespace ArtOfSimRally.Mod.Triple
{
    internal static class TripleScreen
    {
        public static bool IsCustomCockpit(CarCameras rig) => false;
        public static void DrawNotice() { }
        public static void DrawCameraLabel() { }
    }
}
