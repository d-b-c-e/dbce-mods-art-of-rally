using ArtOfSimRally.Mod;
using ArtOfSimRally.Mod.Shim;
using Device = Dbce.Wheel.Ffb.WheelFfbNative;
using Host = ArtOfSimRally.Mod.Main;
using Clock = ArtOfSimRally.Mod.Time;

// Shared pieces of the scenarios: one fixture wheel, a scripted Core and a scripted qualification.
static class Fixture
{
    internal const ShimStatus L = ShimStatus.Live;
    internal static readonly Guid Wheel = new("d71b8350-61b7-11f1-8001-444553540000");

    // The mod enabled and idle in the menus, readers open on one fixture wheel for a pending shared session.
    internal static void Setup()
    {
        WheelReaders.Close(); Device.ReadOk = true; Device.ThrowRead = false; Device.FailOpen = false;
        Device.Devices = new[] { new Device.DeviceInfo { InstanceGuid = Wheel } };
        Array.Clear(Device.Axes); Array.Clear(Device.Buttons);
        Host.Settings = new Settings(); Host.Enabled = true; Host.SettingsVisible = false; Application.isFocused = true;
        Clock.realtimeSinceStartup += 20; GameEntryPoint.EventManager = new EventManager(); GameState.IsDriving = false;
        WheelReaders.Required = true; WheelReaders.Open();
    }
    // ModWatchdog's order for one rendered frame: the startup tick, then the frame's reader maintenance.
    internal static void Frame() { Clock.frameCount++; SharedInputStartup.Tick(); WheelReaders.EnsureUpdatedThisFrame(); }

    // A mod folder with the pinned shim, and no Wheelkit store (this machine's own store never counts here).
    internal static string ModFolder(string name, string profile = null)
    {
        var mod = Path.Combine(Path.GetTempPath(), "art-" + name + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(mod);
        string dll = Environment.GetEnvironmentVariable("ART_SHIM_DLL") ?? PinnedShim();
        if (!File.Exists(dll)) throw new Exception("pinned dbce_shim.dll unavailable: " + dll);
        File.Copy(dll, Path.Combine(mod, SharedInputStartup.ShimFileName));
        if (profile != null) File.WriteAllText(Path.Combine(mod, SharedInputStartup.ProfileFileName), profile);
        SharedInputStartup.RunRoot = Path.Combine(mod, "runs");
        SharedInputStartup.StorePath = Path.Combine(mod, "no-store", "profiles.json");
        return mod;
    }
    // The toolkit pin the package ships (lib/toolkit/shim); ART_SHIM_DLL overrides.
    internal static string PinnedShim()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "ArtOfSimRally.sln"))) return Path.Combine(dir.FullName, "lib", "toolkit", "shim", "dbce_shim.dll");
        return "dbce_shim.dll";
    }

    // The production GetInput postfix over the game's own (keyboard) values.
    internal static DrivingValues Car(float throttle = .3f, float brake = .1f, float steer = .9f, float handbrake = .2f, float clutch = .1f, float alignment = .02f)
    {
        var car = new AxisCarController { SteeringOutOfAlignmentEffect = alignment };
        object[] a = { car, throttle, brake, steer, handbrake, clutch, true };
        typeof(CarInputPatch).GetMethod("Override", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, a);
        return new DrivingValues { Throttle = (float)a[1], Brake = (float)a[2], Steer = (float)a[3], Handbrake = (float)a[4], Clutch = (float)a[5], StartEngine = (bool)a[6] };
    }
}

// Scripted Core frames: the listed actions with their status and value; the driving axes in AxesOwned are Live, the rest unbound.
sealed class ScriptedCore : IShimBridge
{
    internal Func<(int Action, ShimStatus Status, float Value)[]> Script = () => Array.Empty<(int, ShimStatus, float)>();
    internal int Ticks, Stops; internal bool Refuse;
    internal int[] AxesOwned = { 0, 1, 2, 3, 4 };
    public bool Devices(byte[] bytes, int count) => true;
    public bool Tick(byte[] samples, int count, byte[] frame)
    {
        Ticks++;
        if (Refuse) return false;
        BitConverter.GetBytes(296).CopyTo(frame, 0); frame[4] = 2; BitConverter.GetBytes((ulong)Ticks).CopyTo(frame, 8); BitConverter.GetBytes((long)Ticks).CopyTo(frame, 16);
        for (int i = 0; i < 31; i++) frame[52 + i * 8] = (byte)(i < 5 && Array.IndexOf(AxesOwned, i) >= 0 ? ShimStatus.Live : ShimStatus.Unbound);
        foreach (var a in Script()) { BitConverter.GetBytes(a.Value).CopyTo(frame, 48 + a.Action * 8); frame[52 + a.Action * 8] = (byte)a.Status; }
        return true;
    }
    public void Stop() => Stops++;
}

// The hardware-facing qualification steps, scripted; everything else is the production startup, reader, bridge
// and consumer code driven in the watchdog's order.
sealed class FakeQualification : ISharedQualification
{
    internal readonly BridgeTests.Source Src = new BridgeTests.Source();
    internal readonly ScriptedCore Core;
    internal bool Isolated = true; internal string Neutral; internal int RestoreFailures, Restores, Closes;
    internal FakeQualification(ScriptedCore core) { Core = core; }
    internal Action<byte[]> Began;
    public void Begin(string shimPath, string runFolder, byte[] exactProfile) { Began?.Invoke(exactProfile); }
    public string ProfileName => "Lifecycle";
    public string Transmission => "Sequential";
    public string[] DeviceNames => new[] { "TSS fixture" };
    public void Isolate() { }
    public bool VerifyIsolation() => Isolated;
    public bool IsolationOverdue(int frame) => false;
    public string IsolationStatus => RestorePending ? "one or more joystick flags could not be restored" : "native joystick flags restored";
    public string NeutralFailure() => Neutral;
    public IPhysicalSnapshotSource Source => Src;
    public IShimBridge Bridge => Core;
    public bool RestoreIsolation() { Restores++; return true; }
    public string Close() { Closes++; Core.Stop(); return RetryRestore() ? null : IsolationStatus; }
    public bool RestorePending { get; private set; }
    public bool RetryRestore() { Restores++; RestorePending = RestoreFailures-- > 0; return !RestorePending; }
    public void EndObservation() { }
}
