// Fake device transport and game boundary; the shared route's production sources are linked unchanged (SharedInput.csproj).
public sealed class SplashScreenControl
{
    public static SplashScreenControl Instance;
    public bool isActiveAndEnabled = true;
    public int Ends;
    public void EndSplashScreen() { Ends++; isActiveAndEnabled=false; UIManager.Instance.PanelManager.Current=null; }
}
public sealed class UIManager
{
    public static UIManager Instance;
    public PanelManager PanelManager = new();
}
public sealed class PanelManager
{
    public object Current;
    public object SplashScreenPanel = new();
    public object Peek()=>Current;
}
namespace Dbce.Wheel.Ffb
{
    public static class WheelFfbNative
    {
        public sealed class DeviceInfo
        {
            public string Name = "TSS fixture"; public int Index = 0; public Guid? InstanceGuid;
            public int[] StateAxes; public byte[] StateButtons; public bool Connected = true, CannotOpen;
            public string Label => Name;
        }
        public static DeviceInfo[] Devices = { new DeviceInfo() };
        public static readonly int[] Axes = new int[8];
        public static readonly byte[] Buttons = new byte[128];
        public static bool ReadOk = true, ThrowRead = false, FailOpen = false;
        public static int Reads, Enumerations, Closes, Opens;
        private static readonly Dictionary<int, DeviceInfo> slots = new();
        public static DeviceInfo[] ListAllDevices() { Enumerations++; return Devices; }
        public static int OpenRead(int index)
        {
            Opens++;
            var device = Devices.Single(d => d.Index == index);
            if (FailOpen || device.CannotOpen) return -1;
            int slot = slots.Count; slots.Add(slot, device); return slot;
        }
        public static bool Read(int slot, int[] axes, byte[] buttons)
        {
            Reads++;
            if (ThrowRead) throw new IOException("fixture read failure");
            if (!ReadOk || !slots.TryGetValue(slot, out var device) || !device.Connected) return false;
            Array.Copy(device.StateAxes ?? Axes, axes, Axes.Length); Array.Copy(device.StateButtons ?? Buttons, buttons, Buttons.Length);
            return true;
        }
        public static void CloseRead() { Closes++; slots.Clear(); }
    }
}
namespace HarmonyLib
{
    public sealed class HarmonyPatch : Attribute { public HarmonyPatch() {} public HarmonyPatch(Type type, string method, params Type[] args) { } }
    public sealed class HarmonyPostfix : Attribute { }
    public sealed class HarmonyTargetMethods : Attribute { }
    public static class AccessTools { public static System.Reflection.MethodInfo Method(Type type, string name, Type[] types) => type.GetMethod(name, types); }
}
namespace Rewired
{
    public enum ControllerType { Keyboard, Mouse, Joystick, Custom }
    public class Player
    {
        // Keyboard steering fixture: the action Art's keyboard map contributed to this frame, or a query fault.
        public static string KeyboardAction; public static bool QueryFault; public static int Queries;
        public bool IsCurrentInputSource(string actionName, ControllerType controllerType)
        {
            Queries++;
            if (QueryFault) throw new InvalidOperationException("fixture source query failure");
            return controllerType == ControllerType.Keyboard && actionName == KeyboardAction;
        }
    }
    public sealed class PlayerHelper { public Player GetPlayer(int playerId) => playerId == 0 ? global::PadManager.Player : null; }
    public sealed class Joystick
    {
        public Guid deviceInstanceGuid;
        public bool isConnected = true;
        public string name = "fixture";
        public int axisCount => 1;
        public float GetAxisRaw(int index) => enabled ? -1f : 0f;
        public T GetExtension<T>() where T : class => null;
        private bool value = true;
        public int Writes;
        public bool enabled { get => value; set { Writes++; this.value = value; } }
    }
    public sealed class ControllerInventory { public readonly List<Joystick> Joysticks = new(); }
    public sealed class ControllerStatusChangedEventArgs : EventArgs { }
    public static class ReInput
    {
        public static bool isReady; public static readonly ControllerInventory controllers = new();
        public static readonly PlayerHelper players = new();
        public static event Action<ControllerStatusChangedEventArgs> ControllerConnectedEvent, ControllerPreDisconnectEvent, ControllerDisconnectedEvent;
        public static event Action PreShutDownEvent, ShutDownEvent, InitializedEvent;
        internal static int Subscribers => (ControllerConnectedEvent?.GetInvocationList().Length ?? 0) + (ControllerPreDisconnectEvent?.GetInvocationList().Length ?? 0) +
            (ControllerDisconnectedEvent?.GetInvocationList().Length ?? 0) + (PreShutDownEvent?.GetInvocationList().Length ?? 0) +
            (ShutDownEvent?.GetInvocationList().Length ?? 0) + (InitializedEvent?.GetInvocationList().Length ?? 0);
    }
}
namespace Rewired.Platforms.Windows.RawInput
{
    public sealed class RawInputControllerExtension { public string devicePath; public Guid productGuid; }
}
// The DualShock 4 interface path reader's compile surface only: the stub joystick has no extension, so these never run here
// (tests/ShimInput's Ds4IdentityTests drives the reader and the lease).
namespace Rewired.ControllerExtensions
{
    public interface IHIDControllerExtension { ushort vendorId { get; } ushort productId { get; } }
    public sealed class DualShock4Extension : IHIDControllerExtension { ushort IHIDControllerExtension.vendorId => 0; ushort IHIDControllerExtension.productId => 0; }
}
public static class PadManager { public static readonly Rewired.Player Player = new(); public static Rewired.Player GetPlayer() => Player; }
public static class EventStatusEnums { public enum EventStatus { UNDERWAY, FINISHING_STAGE_ANIMATION, WAITING_TO_BEGIN } }
public sealed class EventManager { public EventStatusEnums.EventStatus status = EventStatusEnums.EventStatus.UNDERWAY; }
public static class GameEntryPoint { public static EventManager EventManager = new(); }
public class AxisCarController
{
    public string steerAxis = "Horizontal";
    public float SteeringOutOfAlignmentEffect = 0;
}
public class Drivetrain
{
    public bool automatic;
}
namespace ArtOfSimRally.Mod
{
    internal static class FfbNative { internal static string RequestedPath = ""; }
    internal static class Time { public static float realtimeSinceStartup = 100; public static int frameCount; }
    internal static class Application { public static bool isFocused = true; }
    internal static class GameState
    {
        public static bool IsDriving = true;
        public static EventManager ExistingManager => GameEntryPoint.EventManager;
    }
    internal static class Main
    {
        public static Settings Settings = new();
        public static bool Enabled = true, SettingsVisible;
        public static string Path;
        public static int Saves;
        public static bool SaveSettings() { Saves++; SettingsPersistence.Write(Settings, Path); return true; }
        internal static bool HasFocus() => Application.isFocused; // as Main.SettingsUi
    }
    // The panel's key/connection edit lock, read by SharedInputStartup's host editability callback. Those edits are UI only.
    internal static class SettingsPanel { internal static bool KeyOrConnectionEditing => false; }
    internal static class ModLog
    {
        public static Action<string> Observe;
        public static void Info(string message) { Observe?.Invoke(message); }
        public static void Warning(string message) { Observe?.Invoke(message); }
        public static void Error(string message) { Observe?.Invoke(message); }
    }
}
