using System.Reflection;
using System.Text.Json;
using System.Xml.Serialization;
using ArtOfSimRally.Mod;
using Device = Dbce.Wheel.Ffb.WheelFfbNative;
using Host = ArtOfSimRally.Mod.Main;
using Clock = ArtOfSimRally.Mod.Time;

static class Program
{
    static int assertions;
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static void Same(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .000001f, message);
    static object[] CarInput()
    {
        var args = new object[] { new AxisCarController(), .21f, .32f, -.43f, .54f, .65f, true };
        typeof(WheelInputPatch).GetMethod("Override", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
        return args;
    }
    static void BoundOnlyHandbrake(float expected)
    {
        var values = CarInput();
        Same((float)values[4], Math.Max(.54f, expected), "handbrake did not preserve max of stock and analog input");
        Check((float)values[1] == .21f && (float)values[2] == .32f && (float)values[3] == -.43f &&
            (float)values[5] == .65f && (bool)values[6], "handbrake overwrote unbound channels");
    }
    static void Setup(string binding)
    {
        WheelInput.Close(); Device.ReadOk = true; Device.ThrowRead = false; Device.FailOpen = false;
        Device.Devices = new[] { new Device.DeviceInfo() };
        Array.Clear(Device.Axes); Array.Clear(Device.Buttons);
        Host.Settings = new Settings { WheelInputEnabled = true, HandbrakeBinding = binding };
        Host.Enabled = true; Clock.realtimeSinceStartup += 20;
        GameEntryPoint.EventManager = new EventManager();
        GameState.IsDriving=false;
        WheelInput.LoadBindings(); WheelInput.Open();
    }
    static void Travel(int rest, int far)
    {
        Setup($"TSS fixture|0|axis:2|{rest}|{far}");
        foreach (float fraction in new[] { 0f, .25f, .5f, .75f, 1f, .5f, 0f })
        {
            int raw = rest + (int)((far - rest) * fraction); Device.Axes[2] = raw;
            WheelInput.Update(); float expected = (raw - rest) / (float)(far - rest);
            Same(WheelInput.Value(WheelInput.Channel.Handbrake), expected, "partial travel quantized");
            BoundOnlyHandbrake(expected);
        }
    }
    static void Lifecycle()
    {
        Setup("TSS fixture|0|axis:2|0|65535");
        Device.Axes[2] = 65535; WheelInput.Update(); BoundOnlyHandbrake(1);
        Device.ReadOk = false; WheelInput.Update(); BoundOnlyHandbrake(0);
        Device.ThrowRead = true; WheelInput.Update(); BoundOnlyHandbrake(0);
        Device.ThrowRead = false; Device.ReadOk = true; Device.Axes[2] = 32000;
        WheelInput.Update(); BoundOnlyHandbrake(32000f / 65535);
        GameEntryPoint.EventManager.status = EventStatusEnums.EventStatus.FINISHING_STAGE_ANIMATION;
        Same((float)CarInput()[4], .54f, "finish animation handbrake overwritten");
        GameEntryPoint.EventManager = null;
        Same((float)CarInput()[4], .54f, "missing manager handbrake overwritten");
        GameEntryPoint.EventManager = new EventManager();
        Host.Enabled = false; Same((float)CarInput()[4], .54f, "disabled mod overrode game");
        Host.Enabled = true;
        Host.Settings.WheelInputEnabled = false; Same((float)CarInput()[4], .54f, "disabled direct input overrode game");
        Host.Settings.WheelInputEnabled = true;
        WheelInput.Close(); Device.FailOpen = true; Clock.realtimeSinceStartup += 20; WheelInput.Update();
        BoundOnlyHandbrake(0); // Reader loss/reopen must not keep a stale full pull.
    }
    static void RangesAndAssignment()
    {
        Setup("TSS fixture|0|axis:2|500|500"); Device.Axes[2] = 60000; WheelInput.Update(); BoundOnlyHandbrake(0);
        Setup("TSS fixture|0|button:6|0|1"); Device.Buttons[6] = 128; WheelInput.Update(); BoundOnlyHandbrake(1);
        Device.Buttons[6] = 0; WheelInput.Update(); BoundOnlyHandbrake(0);
        Setup(""); Device.Axes[2] = 65535;
        WheelInput.BeginAssign(WheelInput.Channel.Handbrake);
        Device.Axes[2] = 40000; WheelInput.Update();
        Check(Host.Settings.HandbrakeBinding == "TSS fixture|0|axis:2|65535|40000", "reverse-axis assignment wrong");
        int saved = Host.Saves;
        Device.Axes[2] = 0; WheelInput.Update(); BoundOnlyHandbrake(1);
        Check(Host.Settings.HandbrakeBinding.EndsWith("|65535|0") && Host.Saves == saved, "range learning saved during input update");
        Device.Axes[2] = 32768; WheelInput.Update(); BoundOnlyHandbrake(32767f / 65535);
        Device.Axes[2] = 65535; WheelInput.Update(); BoundOnlyHandbrake(0);
        WheelInput.Clear(WheelInput.Channel.Handbrake);
        Same((float)CarInput()[4], .54f, "cleared binding overrode game");
    }
    static void FlipPersistence()
    {
        Setup("TSS fixture|0|axis:2|0|65535");
        WheelInput.Flip(WheelInput.Channel.Handbrake);
        Device.Axes[2] = 0; WheelInput.Update(); BoundOnlyHandbrake(1);
        Device.Axes[2] = 65535; WheelInput.Update(); BoundOnlyHandbrake(0);
        using (var stream = File.OpenRead(Host.Path)) Host.Settings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
        WheelInput.LoadBindings(); Device.Axes[2] = 32768; WheelInput.Update(); BoundOnlyHandbrake(32767f / 65535);
        WheelInput.Flip(WheelInput.Channel.Handbrake);
        Check(Host.Settings.HandbrakeBinding == "TSS fixture|0|axis:2|0|65535", "double flip changed calibration");
        Host.Settings.SteerBinding = "TSS fixture|0|axis:0|32767|65535";
        WheelInput.LoadBindings(); WheelInput.Flip(WheelInput.Channel.Steer);
        WheelInput.LoadBindings();
        Check(WheelInput.IsBound(WheelInput.Channel.Steer), "flipped steering binding lost on restart");
        Device.Axes[0] = 65535; WheelInput.Update(); Same(WheelInput.Value(WheelInput.Channel.Steer), -1, "steering flip lost sign or calibration");
        WheelInput.Flip(WheelInput.Channel.Steer);
        Check(Host.Settings.SteerBinding == "TSS fixture|0|axis:0|32767|65535", "double steering flip changed range");
        foreach (string invalid in new[] { "TSS fixture|0|axis:2|0|65536", "TSS fixture|0|axis:2|65535|-1",
            "TSS fixture|0|axis:2|0|2147483647", "TSS fixture|0|axis:2|0|-2147483648" })
            Check(WheelInput.Binding.Parse(invalid) == null, "oversized calibration span accepted");
        Setup("TSS fixture|0|axis:2|32767|-1");
        Check(!WheelInput.IsBound(WheelInput.Channel.Handbrake), "virtual steering endpoint accepted as a physical pedal endpoint");
        Setup("TSS fixture|0|axis:2|0|65535"); Device.Axes[2] = 65535; WheelInput.Update();
        Host.Settings.HandbrakeBinding = "TSS fixture|0|axis:1|0|65535"; WheelInput.LoadBindings();
        BoundOnlyHandbrake(0);
    }
    static void AssignmentReadFailure()
    {
        Setup(""); Device.ReadOk = false; WheelInput.BeginAssign(WheelInput.Channel.Handbrake);
        Device.ReadOk = true; Device.Axes[2] = 65535; WheelInput.Update();
        Check(!WheelInput.IsBound(WheelInput.Channel.Handbrake) && WheelInput.Assigning.HasValue,
            "first successful read after failure was mistaken for lever movement");
        Device.Axes[2] = 40000; WheelInput.Update();
        Check(Host.Settings.HandbrakeBinding == "TSS fixture|0|axis:2|65535|40000", "recovered baseline did not bind real movement");
    }
    static readonly Guid LeverA = Guid.Parse("df1786e0-bf89-4194-a8c9-a1bddbfb2c90");
    static readonly Guid LeverB = Guid.Parse("ad643763-846c-48cc-b03e-b5c9688f9b60");
    static Device.DeviceInfo Lever(int index, Guid id, int raw, string name = "TSS fixture")
    {
        var axes = new int[8]; axes[2] = raw;
        return new Device.DeviceInfo { Index=index, Name=name, InstanceGuid=id, StateAxes=axes };
    }
    static void Devices(params Device.DeviceInfo[] devices)
    {
        WheelInput.Close(); Device.Devices=devices; GameState.IsDriving=false;
        Clock.realtimeSinceStartup+=20; WheelInput.Open(); WheelInput.Update();
    }
    static void DeviceIdentity()
    {
        Setup("TSS fixture|0|axis:2|0|65535");
        Devices(Lever(0,LeverB,65535),Lever(1,LeverA,16384));
        BoundOnlyHandbrake(0); // Legacy index cannot distinguish identical devices after reordering.
        Setup("TSS fixture|0|axis:2|0|65535|guid:"+LeverA);
        Devices(Lever(0,LeverB,65535),Lever(1,LeverA,16384));
        BoundOnlyHandbrake(16384f/65535); // Index 0 now belongs to another identical lever.
        Devices(Lever(0,LeverB,65535)); BoundOnlyHandbrake(0);
        Devices(Lever(3,LeverA,32768,"renamed lever")); BoundOnlyHandbrake(32768f/65535);
        WheelInput.Flip(WheelInput.Channel.Handbrake); WheelInput.LoadBindings();
        Check(Host.Settings.HandbrakeBinding.EndsWith("|guid:"+LeverA),"Flip/reload lost GUID");
        using (var stream = File.OpenRead(Host.Path)) Host.Settings = (Settings)new XmlSerializer(typeof(Settings)).Deserialize(stream);
        WheelInput.LoadBindings(); WheelInput.Update(); BoundOnlyHandbrake(32767f/65535);
        Setup("TSS fixture|0|axis:2|0|65535");
        Devices(Lever(0,LeverA,65535),Lever(1,LeverB,65535)); BoundOnlyHandbrake(0);
        Check(WheelInput.Describe(WheelInput.Channel.Handbrake).Contains("Assign"),"ambiguous legacy binding not explained");
        Device.Devices[1].CannotOpen=true; Devices(Device.Devices); BoundOnlyHandbrake(0);
        Setup("TSS fixture|0|axis:2|0|65535"); int saves=Host.Saves; Devices(Lever(4,LeverA,32768));
        BoundOnlyHandbrake(32768f/65535);
        Check(Host.Settings.HandbrakeBinding.EndsWith("|guid:"+LeverA),"unique legacy binding not pinned");
        Check(Host.Saves==saves,"legacy migration wrote settings during input update");
        Clock.realtimeSinceStartup+=6; WheelInput.FlushLearnedRanges();
        Check(Host.Saves==saves+1,"legacy migration not persisted while idle");
        Devices(Lever(4,LeverB,65535)); BoundOnlyHandbrake(0);
        foreach (string invalid in new[] {"|guid:bad", "|guid:"+Guid.Empty, "|guid:"+LeverA+"|extra", "|unknown:"+LeverA})
            Check(WheelInput.Binding.Parse("TSS fixture|0|axis:2|0|65535"+invalid)==null,"invalid identity silently became a legacy binding");
        Setup(""); Devices(Lever(0,LeverA,0),Lever(1,LeverB,0));
        WheelInput.BeginAssign(WheelInput.Channel.Handbrake); Device.Devices[1].StateAxes[2]=32000;
        WheelInput.Update();
        Check(Host.Settings.HandbrakeBinding.EndsWith("|guid:"+LeverB),"assignment did not identify moved lever");
        GameState.IsDriving=true;
    }
    static void DeviceRecovery()
    {
        Setup("TSS fixture|1|axis:2|0|65535|guid:"+LeverB);
        var wheel=Lever(0,LeverA,0,"wheel"); Devices(wheel); BoundOnlyHandbrake(0);
        var lever=Lever(1,LeverB,32768); Device.Devices=new[] {wheel,lever};
        int enums=Device.Enumerations; GameState.IsDriving=true; Clock.realtimeSinceStartup+=10;
        for(int i=0;i<100;i++) WheelInput.Update();
        Check(Device.Enumerations==enums,"device discovery occurred while driving"); BoundOnlyHandbrake(0);
        GameState.IsDriving=false; WheelInput.Update(); BoundOnlyHandbrake(32768f/65535);
        lever.Connected=false; WheelInput.Update(); BoundOnlyHandbrake(0);
        enums=Device.Enumerations;
        for(int i=0;i<100;i++) WheelInput.Update();
        Check(Device.Enumerations==enums,"failed device caused an enumeration loop");
        var returned=Lever(7,LeverB,16384); Device.Devices=new[] {wheel,returned};
        Clock.realtimeSinceStartup+=6; WheelInput.Update(); BoundOnlyHandbrake(16384f/65535);
        // Unbound newly attached devices are discovered on explicit Assign.
        Setup(""); Devices(wheel); Device.Devices=new[] {wheel,Lever(2,LeverB,0)};
        WheelInput.BeginAssign(WheelInput.Channel.Handbrake); Device.Devices[1].StateAxes[2]=20000;
        WheelInput.Update(); Check(Host.Settings.HandbrakeBinding.EndsWith("|guid:"+LeverB),"Assign missed late USB device");
        // A separate unbound lever may be listed but fail its first reader open.
        // A later Bind must retry it even though the attached catalog is unchanged.
        Setup(""); var retryLever=Lever(2,LeverB,0); retryLever.CannotOpen=true;
        Devices(wheel,retryLever); retryLever.CannotOpen=false;
        WheelInput.BeginAssign(WheelInput.Channel.Handbrake);
        retryLever.StateAxes[2]=24000; WheelInput.Update();
        Check(Host.Settings.HandbrakeBinding.EndsWith("|guid:"+LeverB),"Bind did not reopen previously failed USB reader");
        GameState.IsDriving=true; WheelInput.BeginAssign(WheelInput.Channel.Brake);
        Check(!WheelInput.Assigning.HasValue,"assignment refreshed readers during driving");
    }
    static void AssignmentResume()
    {
        Setup(""); WheelInput.BeginAssign(WheelInput.Channel.Handbrake);
        int saves=Host.Saves; GameState.IsDriving=true; Device.Axes[2]=40000; WheelInput.Update();
        Check(!WheelInput.IsBound(WheelInput.Channel.Handbrake) && !WheelInput.Assigning.HasValue && Host.Saves==saves,
            "resuming from pause completed assignment and wrote settings during driving");
    }
    static void BindingSaves()
    {
        Setup("TSS fixture|0|axis:2|0|65535"); Host.SaveSettings();
        int saves=Host.Saves; GameState.IsDriving=true;
        WheelInput.Flip(WheelInput.Channel.Handbrake); WheelInput.Clear(WheelInput.Channel.Handbrake);
        Check(Host.Saves==saves,"Flip/Clear wrote settings during driving");
        GameState.IsDriving=false; Clock.realtimeSinceStartup+=6; WheelInput.FlushLearnedRanges();
        Check(Host.Saves==saves+1 && File.ReadAllText(Host.Path).Contains("TSS fixture"),"drive-time clear changed saved binding instead of rejecting edit");
        Setup("TSS fixture|0|axis:2|0|65535"); Host.SaveSettings();
        string before=File.ReadAllText(Host.Path);
        using(File.Open(Host.Path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            WheelInput.Flip(WheelInput.Channel.Handbrake);
        Check(File.ReadAllText(Host.Path)==before,"failed binding save damaged previous settings");
        Clock.realtimeSinceStartup+=6; WheelInput.FlushLearnedRanges();
        Check(File.ReadAllText(Host.Path).Contains("|65535|0"),"failed binding edit did not retry");
    }
    static int Main(string[] args)
    {
        try
        {
            var directory = Path.GetFullPath(Path.Combine("results", "wheel-input-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory); Host.Path = Path.Combine(directory, "Settings.xml");
            if (args.Contains("--shim-read-only"))
            {
                SharedRead(); Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
            }
            // The shared consumer is a process singleton; each shim scenario needs its own process.
            if (args.Contains("--shim-actions-only"))
            {
                SharedActionChannels(); Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
            }
            if (args.Contains("--shared-startup-only"))
            {
                SharedStartupPaths(); Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
            }
            if (args.Contains("--shared-lifecycle-only"))
            {
                SharedLifecycle(); Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
            }
            if (args.Contains("--shared-transport-only"))
            {
                SharedTransportStop(); Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
            }
            if (args.Contains("--drive-trial-only"))
            {
                DriveRealCore(); Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
            }
            if (args.Contains("--latched-menu-only"))
            {
                LatchedMenu(); Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
            }
            if (args.Contains("--shim-latched-only"))
            {
                SharedLatchedSuppression(); Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
            }
            if (args.Contains("--resume-assignment-only")) AssignmentResume();
            else if (args.Contains("--shifter-only")) assertions+=ShifterIdentityTests.Run();
            else if (args.Contains("--identity-only")) DeviceIdentity();
            else if (args.Contains("--reconnect-only")) DeviceRecovery();
            else if (args.Contains("--flip-only")) FlipPersistence();
            else if (args.Contains("--assign-only")) AssignmentReadFailure();
            else { Travel(0, 65535); Travel(65535, 0); RangesAndAssignment(); Lifecycle(); FlipPersistence(); AssignmentReadFailure(); DeviceIdentity(); DeviceRecovery(); ReadHealth(); AssignmentResume(); BindingSaves(); assertions+=ShifterIdentityTests.Run(); assertions+=CalibrationTests.Run(); }
            ProbeDoesNotLearnOrAssign();
            assertions += GameButtonTests.Run();
            assertions += IsolationBoundaryTests.Run();
            assertions += ShimConsumerTests.Run();
            Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions })); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    static void ProbeDoesNotLearnOrAssign()
    {
        Setup("TSS fixture|0|axis:2|0|30000");
        string before = Host.Settings.HandbrakeBinding; int saves = Host.Saves;
        ShimStartup.Requested = true;
        try
        {
            WheelInput.BeginAssign(WheelInput.Channel.Brake);
            Check(!WheelInput.Assigning.HasValue && WheelInput.Status.Contains("probe"), "probe allowed assignment");
            Device.Axes[2] = 40000; WheelInput.Update();
            Check(Host.Settings.HandbrakeBinding == before && Host.Saves == saves, "probe changed learned range or settings");
        }
        finally { ShimStartup.Requested = false; }
    }

    static void ReadHealth()
    {
        Setup("");
        var id = new Guid("11111111-1111-1111-1111-111111111111");
        WheelInput.Close();
        Check(WheelInput.DeviceReadHealth(id.ToString()) == null, "closed reader reported a known failure");
        Device.Devices = new[] { new Device.DeviceInfo { Index=0, InstanceGuid=id } };
        WheelInput.Open(); WheelInput.Update();
        int reads=Device.Reads, enumerations=Device.Enumerations;
        Check(WheelInput.DeviceReadHealth(id.ToString()) == true, "responsive identity not observed");
        Check(WheelInput.DeviceReadHealth("22222222-2222-2222-2222-222222222222") == false, "missing identity followed another device");
        Check(WheelInput.DeviceReadHealth("bad-guid") == null, "invalid identity reported as a device");
        Check(Device.Reads==reads && Device.Enumerations==enumerations, "health query touched hardware");
        Device.ReadOk=false; WheelInput.Update();
        Check(WheelInput.DeviceReadHealth(id.ToString()) == false, "failed read kept healthy status");
        Device.ReadOk=true; WheelInput.Update();
        Check(WheelInput.DeviceReadHealth(id.ToString()) == true, "resumed read not observed");
    }

    // Scripted Core frames: the listed actions owned with their status/value, the rest unbound.
    sealed class ActionNative : ArtOfSimRally.Mod.Shim.IShimBridge
    {
        internal Func<(int Action, ArtOfSimRally.Mod.Shim.ShimStatus Status, float Value)[]> Script = () => Array.Empty<(int, ArtOfSimRally.Mod.Shim.ShimStatus, float)>();
        internal int Ticks, Stops; internal bool Latched, Refuse;
        public bool Devices(byte[] bytes, int count) => true;
        public bool Tick(byte[] samples, int count, byte[] frame)
        {
            Ticks++;
            if (Refuse) return false;
            BitConverter.GetBytes(296).CopyTo(frame, 0); frame[4] = 2; BitConverter.GetBytes((ulong)Ticks).CopyTo(frame, 8); BitConverter.GetBytes((long)Ticks).CopyTo(frame, 16);
            for (int i = 0; i < 31; i++) frame[52 + i * 8] = (byte)(i < 5 ? ArtOfSimRally.Mod.Shim.ShimStatus.Live : ArtOfSimRally.Mod.Shim.ShimStatus.Unbound);
            foreach (var a in Script()) { BitConverter.GetBytes(a.Value).CopyTo(frame, 48 + a.Action * 8); frame[52 + a.Action * 8] = (byte)a.Status; if (Latched && a.Status == ArtOfSimRally.Mod.Shim.ShimStatus.Live) frame[53 + a.Action * 8] = 1; }
            if (Latched) { frame[40] = 3; frame[41] = 1; frame[42] = 1; } // Running, latched, native suppressed
            return true;
        }
        public void Stop() => Stops++;
    }
    static class RewiredNames
    {
        public static bool GetButton(int a) => false; public static bool GetButtonDown(int a) => false;
        public static bool GetNegativeButton(int a) => false; public static bool GetNegativeButtonDown(int a) => false;
    }
    static bool Query(Rewired.Player player, string method, int action, bool native)
    {
        object[] args = { player, action, typeof(RewiredNames).GetMethod(method), native };
        typeof(GameButtonPatch).GetMethod("After", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, args);
        return (bool)args[3];
    }
    static float AxisQuery(int action, float native)
    {
        object[] args = { PadManager.Player, action, native };
        typeof(GameButtonAxisPatch).GetMethod("After", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, args);
        return (float)args[2];
    }
    static bool ContinueQuery(Rewired.Player player, bool native)
    {
        object[] args = { player, native };
        typeof(GameContinuePatch).GetMethod("After", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, args);
        return (bool)args[1];
    }
    // A latched Core trial: injected nav is the only input for the primary player; native keyboard
    // results are replaced, masked confirm never progresses, other players are untouched.
    static void SharedLatchedSuppression()
    {
        const ArtOfSimRally.Mod.Shim.ShimStatus L = ArtOfSimRally.Mod.Shim.ShimStatus.Live;
        Setup(""); GameButtonCompatibility.Allowed = true;
        // Art's handbrake button is pressed on the same copied read; a latched trial must not pass it.
        Host.Settings.HandbrakeButtonBinding = "TSS fixture|0|button:0|0|1"; WheelInput.LoadBindings();
        Clock.frameCount = 9500; WheelInput.Update();
        var source = new BridgeTests.Source(); float down = 0;
        var native = new ActionNative { Latched = true, Script = () => new[] { (9, L, 1f), (13, L, 0f), (14, L, down) } };
        ArtOfSimRally.Mod.Shim.ShimInput.Consumer.AcceptLatched = true; ArtOfSimRally.Mod.Shim.ShimInput.Consumer.DeliverOnly(13, 14);
        WheelInput.AttachSharedInput(source, native, () => true, () => true);
        GameButtonInput.Observe(12, 13, 16, 17);
        void Frame() { Clock.frameCount++; WheelInput.EnsureUpdatedThisFrame(); }
        Frame(); Frame();
        Check(ArtOfSimRally.Mod.Shim.ShimInput.Consumer.LatchedDelivery(Clock.frameCount), "latched trial not delivering");
        Check(!Query(PadManager.Player, "GetButtonDown", 16, true) && !Query(PadManager.Player, "GetButton", 16, true), "native submit or masked confirm reached the game");
        Check(!Query(PadManager.Player, "GetButton", 13, true), "native vertical (keyboard) kept during latched delivery");
        Check(!Query(PadManager.Player, "GetButtonDown", 99, true) && !ContinueQuery(PadManager.Player, true), "unmapped/continue native input kept");
        Check(AxisQuery(13, .7f) == 0f, "native vertical axis kept during latched delivery");
        Check(Query(new Rewired.Player(), "GetButtonDown", 16, true), "another player's native input suppressed");
        down = 1; Frame();
        Check(Query(PadManager.Player, "GetNegativeButtonDown", 13, false) && Query(PadManager.Player, "GetNegativeButton", 13, false), "injected navDown missing from vertical");
        Check(AxisQuery(13, 0f) == -1f, "injected navDown missing from the vertical axis");
        Check(!Query(PadManager.Player, "GetButtonDown", 16, false), "confirm progressed during the trial");
        Clock.frameCount++; float t = 0, b = 0, s = 0, h = 0, c = 0; bool e = false;
        ArtOfSimRally.Mod.Shim.ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e);
        Check(h == 0f, "Art's handbrake button reached the car during latched delivery");
        WheelInput.Close();
    }
    // The opt-in ordinary startup: every preflight refusal keeps Art's bindings, qualification waits for
    // the menus and ready readers, and a refusal during qualification (here: the real dbce_shim with no
    // loaded WheelFfb owner) stops Core, leaves no Rewired subscription and hands Art's shifter back.
    static void SharedStartupPaths()
    {
        // The qualified copied-provider build (7DDC9621), preserved with the October 12 Apply evidence.
        string dll = Environment.GetEnvironmentVariable("ART_SHIM_DLL") ?? @"E:\Source\games\.worktrees\art-shim-probe-evidence\apply-20261012-021108\dbce_shim.dll";
        Check(File.Exists(dll), "candidate dbce_shim.dll unavailable for the startup test: " + dll);
        var mod = Path.Combine(Path.GetTempPath(), "art-shared-startup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(mod);
        SharedInputStartup.RunRoot = Path.Combine(mod, "runs");
        string profilePath = Path.Combine(mod, SharedInputStartup.ProfileFileName), shimPath = Path.Combine(mod, SharedInputStartup.ShimFileName);
        string profile = "[Controls]\nSchema = 1\nProfile = Startup\nRevision = r1\nTransmission = Sequential\nsteer = axis 0 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=32768 travel=+1\n";
        string Text() => string.Join(" | ", SharedInputStartup.Lines());
        Setup(""); Clock.frameCount = 12000; WheelInput.Update();
        // Shared input alone: Art's assigned controls Off and no shortcut bindings.
        Host.Settings.WheelInputEnabled = false;
        Host.Settings.SharedInput = false; SharedInputStartup.Prepare(mod);
        Check(!SharedInputStartup.Active && !SharedInputStartup.RequestedAtStartup && Text().Contains("is off"), "off: " + Text());
        Host.Settings.SharedInput = true;
        SharedInputStartup.Prepare(mod); Check(!SharedInputStartup.Active && SharedInputStartup.RequestedAtStartup && Text().Contains("no shared profile"), "no profile: " + Text());
        File.WriteAllText(profilePath, profile);
        SharedInputStartup.Prepare(mod); Check(!SharedInputStartup.Active && Text().Contains("dbce_shim.dll is missing"), "no dll: " + Text());
        File.WriteAllText(shimPath, "not the qualified build");
        SharedInputStartup.Prepare(mod); Check(!SharedInputStartup.Active && Text().Contains("not the qualified build"), "wrong dll: " + Text());
        File.Copy(dll, shimPath, true); File.WriteAllText(profilePath, "");
        SharedInputStartup.Prepare(mod); Check(!SharedInputStartup.Active && Text().Contains("empty"), "empty profile: " + Text());
        File.WriteAllText(profilePath, profile);
        SharedInputStartup.Prepare(mod); Check(SharedInputStartup.Active && Text().Contains("waiting"), "valid preflight: " + Text());
        // No qualification while driving or before Rewired is ready; nothing acquired meanwhile.
        GameState.IsDriving = true; Rewired.ReInput.isReady = true; SharedInputStartup.Tick();
        GameState.IsDriving = false; Rewired.ReInput.isReady = false; SharedInputStartup.Tick();
        Check(SharedInputStartup.Active && Text().Contains("waiting") && Rewired.ReInput.Subscribers == 0 && !Directory.Exists(SharedInputStartup.RunRoot), "qualified while driving or before Rewired was ready");
        // Real Core opens, then the missing WheelFfb owner refuses: everything is released.
        Clock.frameCount++; WheelInput.EnsureUpdatedThisFrame();
        Check(WheelInput.SharedProbeSlots.Length > 0, "readers closed for shared input without Art bindings");
        Rewired.ReInput.isReady = true; FfbNative.RequestedPath = "";
        Host.Settings.ShifterEnabled = true; Host.Settings.ShifterDeviceIndex = 0; Host.Settings.ShifterDeviceName = Device.Devices[0].Name; Host.Settings.ShifterDeviceGuid = "";
        SharedInputStartup.Tick();
        Check(!SharedInputStartup.Active && Text().Contains("WheelFfb owner has not loaded a module") && Text().Contains("Art bindings are used"), "qualification refusal: " + Text());
        Check(Rewired.ReInput.Subscribers == 0, "refusal leaked Rewired event subscriptions");
        Check(Directory.GetFiles(SharedInputStartup.RunRoot, "host-shutdown.json", SearchOption.AllDirectories).Length == 1, "refusal did not stop the opened Core host");
        Check(Shifter.IsOpen, "Art's shifter was not handed back after the refusal");
        SharedInputStartup.Tick(); Check(!SharedInputStartup.Active && !ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Started, "refused session retried or attached");
        WheelInput.Close();
    }
    // The hardware-facing qualification steps, scripted; everything else is the production startup,
    // reader, bridge and consumer code driven in the watchdog's order.
    sealed class FakeQualification : ArtOfSimRally.Mod.Shim.ISharedQualification
    {
        internal readonly BridgeTests.Source Src = new BridgeTests.Source();
        internal readonly ActionNative Core;
        internal bool Isolated = true; internal string Neutral; internal int RestoreFailures, Restores, Closes;
        internal FakeQualification(ActionNative core) { Core = core; }
        public void Begin(string shimPath, string runFolder, byte[] exactProfile) { }
        public string ProfileName => "Lifecycle";
        public string Transmission => "Sequential";
        public string[] DeviceNames => new[] { "TSS fixture" };
        public void Isolate() { }
        public bool VerifyIsolation() => Isolated;
        public bool IsolationOverdue(int frame) => false;
        public string IsolationStatus => RestorePending ? "one or more joystick flags could not be restored" : "native joystick flags restored";
        public string NeutralFailure() => Neutral;
        public ArtOfSimRally.Mod.Shim.IPhysicalSnapshotSource Source => Src;
        public ArtOfSimRally.Mod.Shim.IShimBridge Bridge => Core;
        public bool RestoreIsolation() { Restores++; return true; }
        public string Close() { Closes++; Core.Stop(); return RetryRestore() ? null : IsolationStatus; }
        public bool RestorePending { get; private set; }
        public bool RetryRestore() { Restores++; RestorePending = RestoreFailures-- > 0; return !RestorePending; }
        public void EndObservation() { }
    }
    // romi 28533: shared input alone (no Art bindings or shortcuts), force ownership settling before the
    // attach, Art editors refused except Art-only buttons, a later force reconnect stopping the session
    // truthfully without reattach, and a failed joystick restore kept and retried instead of handed back.
    static void SharedLifecycle()
    {
        const ArtOfSimRally.Mod.Shim.ShimStatus L = ArtOfSimRally.Mod.Shim.ShimStatus.Live;
        string dll = Environment.GetEnvironmentVariable("ART_SHIM_DLL") ?? @"E:\Source\games\.worktrees\art-shim-probe-evidence\apply-20261012-021108\dbce_shim.dll";
        Check(File.Exists(dll), "candidate dbce_shim.dll unavailable for the lifecycle test: " + dll);
        var mod = Path.Combine(Path.GetTempPath(), "art-shared-lifecycle-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(mod);
        File.Copy(dll, Path.Combine(mod, SharedInputStartup.ShimFileName));
        File.WriteAllText(Path.Combine(mod, SharedInputStartup.ProfileFileName), "[Controls]\nSchema = 1\nProfile = Lifecycle\n");
        SharedInputStartup.RunRoot = Path.Combine(mod, "runs");
        string Text() => string.Join(" | ", SharedInputStartup.Lines());
        float confirm = 0, steer = 0;
        var core = new ActionNative { Script = () => new[] { (0, L, steer), (9, L, confirm) } };
        FakeQualification fake = null; int sessions = 0; bool settled = true;
        SharedInputStartup.NewSession = inventory => { sessions++; return fake = new FakeQualification(core) { RestoreFailures = nextFailures, Neutral = nextNeutral, Isolated = nextIsolated }; };
        SharedInputStartup.ForceSettled = () => settled;
        // ModWatchdog.Update order: startup tick, then the frame's reader maintenance.
        void Frame() { Clock.frameCount++; SharedInputStartup.Tick(); WheelInput.EnsureUpdatedThisFrame(); }
        void Start() { Setup(""); Host.Settings.WheelInputEnabled = false; Host.Settings.SharedInput = true; Host.Settings.ShifterEnabled = true; Host.Settings.ShifterDeviceIndex = 0; Host.Settings.ShifterDeviceName = Device.Devices[0].Name; Host.Settings.ShifterDeviceGuid = ""; Shifter.Close(); Rewired.ReInput.isReady = true; SharedInputStartup.Prepare(mod); }
        Clock.frameCount = 20000;

        // (4) A failed joystick restore keeps the session and never claims Art's bindings are back.
        nextNeutral = "unqualified: raw axes unavailable or nonzero after disable"; nextFailures = 3;
        Start(); Frame();
        Check(sessions == 1 && fake.Closes == 1 && fake.RestorePending, "cleanup failure not retained");
        Check(Text().Contains("still off") && Text().Contains("retrying") && !Text().Contains("Art bindings are used"), "cleanup failure claimed a handback: " + Text());
        Check(SharedInputStartup.HoldsShifter && !Shifter.IsOpen && !SharedInputStartup.Active, "Art shifter reopened before the joystick restore");
        GameState.IsDriving = true; int restores = fake.Restores;
        for (int i = 0; i < 200; i++) Frame();
        Check(fake.Restores == restores, "restore retried while driving");
        GameState.IsDriving = false; Frame();
        Check(fake.Restores == restores + 1 && Text().Contains("still off"), "restore not retried in the menus: " + Text());
        for (int i = 0; i < 59; i++) Frame();
        Check(fake.Restores == restores + 1, "restore retried every frame");
        for (int i = 0; i < 121; i++) Frame();
        Check(!fake.RestorePending && Text().Contains("Art bindings are used") && Text().Contains("restored on retry 3"), "restored session not released: " + Text());
        Check(!SharedInputStartup.HoldsShifter && Shifter.IsOpen, "Art shifter not handed back after the restore");
        // Exhausted: stays explicit and stops retrying.
        nextFailures = 1000; Start(); Frame();
        for (int i = 0; i < 61 * 12; i++) Frame();
        Check(fake.Restores == 1 + SharedInputStartup.RestoreRetries && Text().Contains("restart the game") && !Shifter.IsOpen, "exhausted restore not explicit or still retrying: " + Text());

        // (1) Shared alone: no Art bindings or shortcuts, yet the readers open and stay open for it.
        nextNeutral = null; nextFailures = 0; nextIsolated = false; settled = false; sessions = 0;
        int Runs() => Directory.Exists(SharedInputStartup.RunRoot) ? Directory.GetDirectories(SharedInputStartup.RunRoot).Length : 0;
        Start(); Device.Devices[0].InstanceGuid = new Guid("d71b8350-61b7-11f1-8001-444553540000");
        WheelInput.Close(); Clock.realtimeSinceStartup += 6; int runs = Runs();
        for (int i = 0; i < 5; i++) Frame();
        Check(WheelInput.SharedProbeSlots.Length > 0 && WheelInput.Enabled, "readers closed or never opened for shared input alone");
        // (3) Force ownership settles first: nothing is acquired while a reconnect is pending.
        Check(sessions == 0 && Text().Contains("force feedback") && Runs() == runs, "qualified before force feedback settled: " + Text());
        settled = true; Frame();
        Check(sessions == 1 && SharedInputStartup.Qualifying && WheelInput.SharedReadersPinned, "isolation phase not entered");
        // Pinned readers: no discovery reopen, and no Art edit may reopen them.
        int opens = Device.Opens; Device.ReadOk = false; Clock.realtimeSinceStartup += 6; Frame(); Device.ReadOk = true; Clock.realtimeSinceStartup += 6; Frame();
        Check(Device.Opens == opens && SharedInputStartup.Qualifying, "pinned readers reopened during isolation");
        WheelInput.BeginCalibration(WheelInput.Channel.HandbrakeButton);
        Check(!WheelInput.Assigning.HasValue && WheelInput.Status == WheelInput.SharedDeviceChange && Device.Opens == opens, "Art-only edit reopened pinned readers");
        fake.Isolated = true; Frame(); Frame();
        var consumer = ArtOfSimRally.Mod.Shim.ShimInput.Consumer;
        Check(consumer.Started && Text().Contains("drives the wheel") && Text().Contains(ArtOfSimRally.Mod.Shim.SharedInputStatus.Editors), "attach after settle failed: " + Text());
        confirm = 1; steer = .5f; Frame();
        Check(WheelInput.GameButton(WheelInput.Channel.Confirm, false), "shared confirm missing with Art bindings off");
        float t = 0, b = 0, s = 0, h = 0, c = 0; bool e = false;
        Check(ArtOfSimRally.Mod.Shim.ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e) && Math.Abs(s - .5f) < .001f, "shared steering missing with Art bindings off");
        confirm = 0; steer = 0; Frame();

        // (2) Editors: shared-set controls refuse every edit; Art-only buttons bind on the shared copy.
        Host.Settings.ConfirmBinding = "TSS fixture|0|button:1|0|1"; WheelInput.LoadBindings();
        WheelInput.BeginCalibration(WheelInput.Channel.Confirm);
        Check(!WheelInput.Assigning.HasValue && WheelInput.Status == WheelInput.SharedEditsRefused, "shared-owned control editable");
        Check(!WheelInput.Clear(WheelInput.Channel.Confirm) && Host.Settings.ConfirmBinding != "" && !WheelInput.Clear(WheelInput.Channel.Steer), "shared-owned control cleared");
        Check(WheelInput.SharedEditorText(WheelInput.Channel.Confirm) == "Set by the shared profile." && WheelInput.SharedEditorText(WheelInput.Channel.Back).Contains("game's own controls"), "editor replacement text wrong");
        opens = Device.Opens; int reads = Device.Reads, coreReads = fake.Src.Reads;
        fake.Src.Pressed = -1; Frame();
        WheelInput.BeginCalibration(WheelInput.Channel.HandbrakeButton);
        Check(WheelInput.Assigning == WheelInput.Channel.HandbrakeButton && Device.Opens == opens && Device.Reads == reads && fake.Src.Reads == coreReads + 1, "Art-only bind reopened or read outside the shared copy");
        fake.Src.Pressed = 3; Frame(); fake.Src.Pressed = -1; Frame();
        Check(WheelInput.SaveCalibration() && Host.Settings.HandbrakeButtonBinding.Contains("button:3"), "Art-only button not bound from the shared copy: " + Host.Settings.HandbrakeButtonBinding);
        Check(Device.Opens == opens && Device.Reads == reads, "Art-only bind touched the readers");
        // The handbrake button applies beside the shared profile (with assigned controls On).
        Host.Settings.WheelInputEnabled = true; fake.Src.Pressed = 3; Clock.frameCount++; h = 0;
        ArtOfSimRally.Mod.Shim.ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e);
        Check(h == 1f, "Art-only handbrake button ignored beside the shared profile");
        fake.Src.Pressed = -1; Clock.frameCount++; h = 0;
        ArtOfSimRally.Mod.Shim.ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e);
        Check(h == 0f, "released handbrake button held");
        Frame();
        // A device change is refused rather than reopening the shared readers.
        Host.Settings.CameraButtonBindings = new string[11]; Host.Settings.CameraButtonBindings[0] = "Other wheel|5|button:1|0|1"; WheelInput.LoadBindings();
        WheelInput.BeginCalibration(WheelInput.Channel.HandbrakeButton);
        Check(!WheelInput.Assigning.HasValue && WheelInput.Status == WheelInput.SharedDeviceChange && Device.Opens == opens, "device change reopened the shared readers");
        Check(WheelInput.Clear(WheelInput.Channel.CameraUp) && Host.Settings.CameraButtonBindings[0] == "", "Art-only camera button could not be cleared");

        // (3) A later force reconnect (Main.RecoverForceFeedback's reader handoff) stops the session for good.
        int restoresBefore = fake.Restores, ticks = core.Ticks;
        WheelInput.ReleaseForForceReconnect(); WheelInput.ReopenAfterForceReconnect();
        for (int i = 0; i < 5; i++) Frame();
        Check(Text().Contains("Shared input stopped: " + WheelInput.ForceReconnectStop) && Text().Contains("until the game restarts"), "reconnect stop not stated: " + Text());
        Check(sessions == 1 && core.Ticks == ticks && fake.Restores == restoresBefore + 1, "session reattached, kept ticking or skipped its restore");
        Check(WheelInput.SharedProbeSlots.Length > 0 && !WheelInput.GameButton(WheelInput.Channel.Confirm, false), "readers not reopened or stopped session not neutral");
        // Neutral driving, even for the game's own (keyboard) values; nothing falls back.
        confirm = 1; steer = 1; fake.Src.Pressed = 3; Clock.frameCount++; s = h = t = .3f;
        ArtOfSimRally.Mod.Shim.ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e);
        Check(s == 0 && h == 0 && t == 0, "stopped session drove the car or passed native input");
        Check(WheelInput.DeviceReadHealth(Guid.NewGuid().ToString()) == null && SharedInputStartup.HoldsShifter, "stopped session reported a read failure or released the shifter");
        WheelInput.Close();
    }
    static string nextNeutral; static int nextFailures; static bool nextIsolated = true;
    // romi 28966: a transport-only fault (Core refuses a tick) keeps the wheel neutral and Core untouched but
    // leaves keyboard driving while the session's own isolation still verifies; once isolation is lost or
    // released (a close), driving is neutral for good, and nothing re-enables the wheel.
    static void SharedTransportStop()
    {
        const ArtOfSimRally.Mod.Shim.ShimStatus L = ArtOfSimRally.Mod.Shim.ShimStatus.Live;
        string dll = Environment.GetEnvironmentVariable("ART_SHIM_DLL") ?? @"E:\Source\games\.worktrees\art-shim-probe-evidence\apply-20261012-021108\dbce_shim.dll";
        var mod = Path.Combine(Path.GetTempPath(), "art-shared-transport-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(mod);
        File.Copy(dll, Path.Combine(mod, SharedInputStartup.ShimFileName));
        File.WriteAllText(Path.Combine(mod, SharedInputStartup.ProfileFileName), "[Controls]\nSchema = 1\nProfile = Transport\n");
        SharedInputStartup.RunRoot = Path.Combine(mod, "runs");
        string Text() => string.Join(" | ", SharedInputStartup.Lines());
        var core = new ActionNative { Script = () => new[] { (0, L, .5f), (9, L, 1f) } };
        FakeQualification fake = null;
        SharedInputStartup.NewSession = inventory => fake = new FakeQualification(core);
        SharedInputStartup.ForceSettled = () => true;
        void Frame() { Clock.frameCount++; SharedInputStartup.Tick(); WheelInput.EnsureUpdatedThisFrame(); }
        float Drive(out float throttle)
        {
            Clock.frameCount++; float t = .3f, b = 0, s = .3f, h = 0, c = 0; bool e = false; // .3: the game's own (keyboard) values
            ArtOfSimRally.Mod.Shim.ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e);
            throttle = t; return s;
        }
        Setup(""); Host.Settings.SharedInput = true; Rewired.ReInput.isReady = true; Device.Devices[0].InstanceGuid = new Guid("d71b8350-61b7-11f1-8001-444553540000");
        WheelInput.Close(); Clock.realtimeSinceStartup += 6; Clock.frameCount = 30000;
        SharedInputStartup.Prepare(mod);
        for (int i = 0; i < 3; i++) Frame();
        Check(ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Started && Math.Abs(Drive(out _) - .5f) < .001f, "shared steering not live before the fault: " + Text());
        // Core refuses a tick: transport-only stop with isolation still verified.
        core.Refuse = true; Frame(); core.Refuse = false; int ticks = core.Ticks;
        Check(WheelInput.SharedStopReason == "shared provider refused physical batch" && WheelInput.SharedKeyboardRetained, "transport stop not recorded: " + WheelInput.SharedStopReason);
        float steer = Drive(out float throttle);
        Check(Math.Abs(steer - .3f) < .001f && Math.Abs(throttle - .3f) < .001f, "keyboard driving lost after a transport-only stop: " + steer);
        Frame(); Frame();
        Check(core.Ticks == ticks && !WheelInput.GameButton(WheelInput.Channel.Confirm, false), "stopped session ticked Core or delivered the wheel");
        Check(Text().Contains("keyboard driving continues") && Text().Contains("refused physical batch"), "transport stop text: " + Text());
        Check(WheelInput.DeviceReadHealth(Device.Devices[0].InstanceGuid.ToString()) == null, "stopped session reported read health");
        // Isolation lost once: neutral for good, even if it later verifies again.
        fake.Isolated = false; Frame();
        Check(!WheelInput.SharedKeyboardRetained && Drive(out _) == 0f, "keyboard kept after isolation was lost");
        fake.Isolated = true; Frame(); Frame();
        Check(Drive(out throttle) == 0f && throttle == 0f && Text().Contains("wheel and keyboard alike"), "neutral not sticky after isolation loss: " + Text());
        WheelInput.Close();
    }
    // The in-game Trial failure (2026-10-12 04:21): the real Core reports navDown test-neutral until a batch
    // activates, so the release-first arming never armed and the injected press was swallowed. Here the
    // real latched Core drives Art's actual Rewired postfixes, read the way RewiredStandaloneInputModule's
    // GetRawMoveVector reads them (GetAxis plus GetNegativeButtonDown on the vertical action).
    static void LatchedMenu()
    {
        string dll = Environment.GetEnvironmentVariable("ART_LATCHED_SHIM_DLL") ?? @"E:\Source\research\.worktrees\art-shim-bridge\build-art-bridge\windows\Release\dbce_shim.dll";
        Check(File.Exists(dll), "latched dbce_shim.dll unavailable: " + dll);
        Setup(""); GameButtonCompatibility.Allowed = true; Clock.frameCount = 40000; WheelInput.Update();
        GameButtonInput.Observe(12, 13, 16, 17);
        string folder = Path.Combine(Path.GetTempPath(), "art-latched-menu-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string session = Path.Combine(folder, "shim-session.txt"), batch = Path.Combine(folder, "shim-batch.txt"), nonce = Guid.NewGuid().ToString("N");
        ArtOfSimRally.Mod.Shim.ProbeRequest.Replace(session, "nonce=" + nonce + "\nexpires=" + (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600) + "\n");
        string r12 = " dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944}";
        string profile = "[Controls]\nSchema = 1\nProfile = Menu\nRevision = r1\nTransmission = Sequential\n" +
            "steer = axis 0" + r12 + " range=0..65535 rest=32768 travel=+1\nconfirm = button 31" + r12 + "\nnavUp = hat 0 0" + r12 + "\nnavDown = hat 0 18000" + r12 + "\n";
        var native = new ArtOfSimRally.Mod.Shim.ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(profile),
            new ArtOfSimRally.Mod.Shim.ShimBridgeNative.LatchedSession { Nonce = nonce, SessionFile = session, BatchFile = batch, FencesVerified = true, ForceAbsent = true });
        var consumer = ArtOfSimRally.Mod.Shim.ShimInput.Consumer;
        consumer.AcceptLatched = true; consumer.DeliverOnly(ArtOfSimRally.Mod.Shim.LatchedTrial.NavUp, ArtOfSimRally.Mod.Shim.LatchedTrial.NavDown);
        WheelInput.AttachSharedInput(new BridgeTests.DeviceSource(), native, () => true, () => true);
        int selected = 0, moves = 0, navigationFrame = Clock.frameCount + 60;
        bool Navigation() => Clock.frameCount >= navigationFrame; // Art's 0.5 s EventSystem navigation delay
        // GetRawMoveVector with moveOneElementPerAxisPress: an axis sign plus the matching button edge,
        // sent only once EventSystem navigation is enabled.
        void Menu()
        {
            if (!Navigation()) return;
            float vertical = AxisQuery(13, 0f);
            if (Query(PadManager.Player, "GetNegativeButtonDown", 13, false) && vertical < 0) { selected++; moves++; }
            if (Query(PadManager.Player, "GetButtonDown", 13, false) && vertical > 0) { selected--; moves++; }
        }
        var trial = new ArtOfSimRally.Mod.Shim.LatchedTrial(consumer, () => "item#" + selected, text => ArtOfSimRally.Mod.Shim.ProbeRequest.Replace(batch, text), nonce, Navigation);
        try
        {
            for (int i = 0; i < 4000 && trial.Current != ArtOfSimRally.Mod.Shim.LatchedTrial.Step.Passed && trial.Current != ArtOfSimRally.Mod.Shim.LatchedTrial.Step.Failed; i++)
            {
                Clock.frameCount++; WheelInput.EnsureUpdatedThisFrame(); Menu(); trial.Tick(Clock.frameCount); System.Threading.Thread.Sleep(5);
            }
            Check(trial.Current == ArtOfSimRally.Mod.Shim.LatchedTrial.Step.Passed && trial.AfterDown == "item#1" && trial.AfterUp == "item#0" && moves == 2,
                "menu did not move once down and once up through the patched queries: " + trial.Current + " " + trial.Failure + " moves " + moves);
        }
        finally { WheelInput.Close(); }
        Check(File.ReadAllText(Path.Combine(folder, "host-shutdown.json")).Contains("\"state\":\"resolved\""), "latched menu stop did not resolve");
    }
    // Slice C against the real latched Core: raw stimuli built from an owner-shaped profile, published whole,
    // composed by the production ShimInput.TryOverride; the car stores what GetInput returned and a manual
    // sequential box takes the consumer's shift queue the way ShifterPatch does.
    static void DriveRealCore()
    {
        string dll = Environment.GetEnvironmentVariable("ART_LATCHED_SHIM_DLL") ?? @"E:\Source\research\.worktrees\art-shim-bridge\build-art-bridge\windows\Release\dbce_shim.dll";
        Check(File.Exists(dll), "latched dbce_shim.dll unavailable: " + dll);
        Setup(""); Clock.frameCount = 50000;
        Device.Devices = new[] { new Device.DeviceInfo { Name = "MOZA R12 Base", Index = 0 }, new Device.DeviceInfo { Name = "DS-8X Shifter", Index = 1 } };
        WheelInput.Close(); WheelInput.Open(); WheelInput.Update();
        Check(WheelInput.SharedProbeSlots.Length == 2, "two readers not open");
        string folder = Path.Combine(Path.GetTempPath(), "art-drive-core-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string session = Path.Combine(folder, "shim-session.txt"), batch = Path.Combine(folder, "shim-batch.txt"), nonce = Guid.NewGuid().ToString("N");
        ArtOfSimRally.Mod.Shim.ProbeRequest.Replace(session, "nonce=" + nonce + "\nexpires=" + (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600) + "\n");
        var native = new ArtOfSimRally.Mod.Shim.ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(BridgeTests.DriveProfile),
            new ArtOfSimRally.Mod.Shim.ShimBridgeNative.LatchedSession { Nonce = nonce, SessionFile = session, BatchFile = batch, FencesVerified = true, ForceAbsent = true });
        var consumer = ArtOfSimRally.Mod.Shim.ShimInput.Consumer;
        consumer.AcceptLatched = true; consumer.DeliverOnly(ArtOfSimRally.Mod.Shim.DriveTrial.Delivered);
        WheelInput.AttachSharedInput(new BridgeTests.DeviceSource(), native, () => true, () => true);
        var trial = new ArtOfSimRally.Mod.Shim.DriveTrial(consumer, BridgeTests.DriveProfile, text => ArtOfSimRally.Mod.Shim.ProbeRequest.Replace(batch, text), nonce);
        int gear = 2, changing = 0;
        try
        {
            for (int i = 0; i < 8000 && !trial.Finished; i++)
            {
                Clock.frameCount++; WheelInput.EnsureUpdatedThisFrame();
                float t = 0, b = 0, s = 0, h = 0, c = 0; bool e = false;
                var car = new AxisCarController { SteeringOutOfAlignmentEffect = .02f };
                Check(ArtOfSimRally.Mod.Shim.ShimInput.TryOverride(car, ref t, ref b, ref s, ref h, ref c, ref e), "car input not composed by the shared session");
                int shift = consumer.TakeShift(); if (shift != 0) { gear += shift; changing = 6; }
                if (changing > 0) changing--;
                var last = ArtOfSimRally.Mod.Shim.ShimInput.Last;
                var sample = new ArtOfSimRally.Mod.Shim.DriveTrial.Sample
                {
                    Frame = Clock.frameCount, Underway = true, Composed = last.Frame == Clock.frameCount, SteerLive = last.SteerLive,
                    AppliedSteer = last.AppliedSteer, AppliedThrottle = last.AppliedThrottle, AppliedBrake = last.AppliedBrake, AppliedHandbrake = last.AppliedHandbrake, Alignment = last.Alignment,
                    OutSteer = last.Steer, OutThrottle = last.Throttle, OutBrake = last.Brake, OutHandbrake = last.Handbrake,
                    EngineSteer = s, EngineThrottle = t, EngineBrake = b, EngineHandbrake = h,
                    Gear = gear, Automatic = false, ChangingGear = changing > 0, Speed = 4f,
                };
                foreach (int a in ArtOfSimRally.Mod.Shim.DriveTrial.Delivered) sample.Injected[a] = consumer.Injected(a, Clock.frameCount);
                trial.Tick(sample); System.Threading.Thread.Sleep(5);
            }
            Check(trial.Passed, "real-Core drive did not pass: " + trial.Failure + " | " + string.Join(" | ", trial.Steps.ConvertAll(x => x.Name + "=" + x.Result + " " + x.Detail + " h" + x.HoldSamples + " r" + x.ReleaseSamples + " g" + x.GearBefore + ">" + x.GearAfter)));
            File.WriteAllText(Path.Combine(folder, "drive-samples.tsv"), trial.Samples.ToString());
        }
        finally { WheelInput.Close(); }
        Check(File.ReadAllText(Path.Combine(folder, "host-shutdown.json")).Contains("\"state\":\"resolved\""), "drive stop did not resolve");
    }
    static void SharedActionChannels()
    {
        const ArtOfSimRally.Mod.Shim.ShimStatus L = ArtOfSimRally.Mod.Shim.ShimStatus.Live;
        Setup("");
        // Art's own bindings press the same copied button: owned channels must ignore them, unowned ones keep them.
        Host.Settings.ConfirmBinding = "TSS fixture|0|button:0|0|1"; Host.Settings.BackBinding = "TSS fixture|0|button:0|0|1";
        Host.Settings.SettingsButtonBinding = "TSS fixture|0|button:0|0|1";
        WheelInput.LoadBindings(); Clock.frameCount = 9000; WheelInput.Update();
        var source = new BridgeTests.Source(); float confirm = 0, menu = 0;
        var native = new ActionNative { Script = () => new[] { (9, L, confirm), (21, L, menu) } };
        WheelInput.AttachSharedInput(source, native, () => true, () => true);
        void Frame() { Clock.frameCount++; WheelInput.EnsureUpdatedThisFrame(); }
        Frame();
        Check(source.Reads == 1 && !WheelInput.GameButton(WheelInput.Channel.Confirm, false), "owned confirm used Art's pressed binding");
        Check(WheelInput.GameButton(WheelInput.Channel.Back, false), "unowned back lost Art's binding on the same copied read");
        confirm = 1; Frame();
        Check(WheelInput.GameButton(WheelInput.Channel.Confirm, true) && WheelInput.GameButton(WheelInput.Channel.Confirm, false), "shared confirm press");
        Frame(); Check(!WheelInput.GameButton(WheelInput.Channel.Confirm, true) && WheelInput.GameButton(WheelInput.Channel.Confirm, false), "held confirm repeated");
        confirm = 0; Frame(); Check(WheelInput.GameButtonReleased(WheelInput.Channel.Confirm), "shared confirm release");
        Check(!WheelInput.ShortcutPressed(WheelInput.Channel.SettingsButton), "released shared shortcut pressed (Art binding leaked)");
        menu = 1; Frame(); Check(WheelInput.ShortcutPressed(WheelInput.Channel.SettingsButton), "shared shortcut edge");
        Frame(); Check(!WheelInput.ShortcutPressed(WheelInput.Channel.SettingsButton), "held shortcut repeated");
        // Car input can run before the watchdog: it must publish and use this frame's snapshot.
        confirm = 1; Clock.frameCount++; int reads = source.Reads; float t = 0, b = 0, s = 0, h = 0, c = 0; bool e = false;
        Check(ArtOfSimRally.Mod.Shim.ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e), "car input not owned");
        Check(source.Reads == reads + 1 && ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Fresh(Clock.frameCount), "car input composed a previous frame");
        // The first Rewired query after that read must still project this frame's buttons, without another read.
        WheelInput.PrepareGameQuery();
        Check(WheelInput.GameButton(WheelInput.Channel.Confirm, true) && WheelInput.GameButton(WheelInput.Channel.Confirm, false), "button query after car input saw a stale projection");
        Check(source.Reads == reads + 1, "button projection read again");
        WheelInput.EnsureUpdatedThisFrame(); Check(source.Reads == reads + 1 && native.Ticks == reads + 1, "same frame polled twice");
        Check(WheelInput.GameButton(WheelInput.Channel.Confirm, false), "watchdog reprojection dropped the held button");
        confirm = 0;
        // A refused frame keeps the owned channels neutral; nothing falls back to Art's binding.
        native.Script = () => new[] { (9, L, 1f), (21, L, 1f), (10, L, 1f) };
        menu = 0; Frame();
        Check(ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Terminal, "ownership change after open accepted");
        Check(!WheelInput.GameButton(WheelInput.Channel.Confirm, false) && !WheelInput.ShortcutPressed(WheelInput.Channel.SettingsButton), "terminal session fell back or stayed live");
        Frame(); Check(!WheelInput.GameButton(WheelInput.Channel.Confirm, false), "terminal session revived");
        WheelInput.Close();
    }

    static void SharedRead()
    {
        Setup("TSS fixture|0|axis:0|0|65535"); Clock.frameCount = 7000; WheelInput.Update();
        int reads = Device.Reads, opens = Device.Opens, saves = Host.Saves;
        string binding = Host.Settings.HandbrakeBinding;
        var source = new BridgeTests.Source { Raw = 40000 }; var native = new BridgeTests.Native(); bool isolated = true;
        int restores = 0;
        WheelInput.AttachSharedInput(source, native, () => isolated, () => { restores++; return true; });
        Check(source.Reads == 0 && Device.Reads == reads && Device.Opens == opens, "handoff performed an extra read or open");
        Clock.frameCount++; WheelInput.PrepareGameQuery();
        Check(source.Reads == 1 && native.Ticks == 1 && Device.Reads == reads, "production path did not replace exactly one legacy read");
        Same(WheelInput.Value(WheelInput.Channel.Handbrake), 40000f / 65535, "legacy cache was not fed the same physical sample");
        WheelInput.Update();
        Check(source.Reads == 1 && native.Ticks == 1, "same-frame update polled/ticked again");
        Same(WheelInput.Value(WheelInput.Channel.Handbrake), 40000f / 65535, "same-frame update cleared copied cache");
        Check(ArtOfSimRally.Mod.Shim.ShimInput.IsolatedFrame == Clock.frameCount, "current isolation stamp missing");
        Clock.frameCount++; source.Result = -2; WheelInput.PrepareGameQuery();
        Check(source.Reads == 2 && native.Ticks == 1 && native.Stops == 1 && Device.Reads == reads, "failed snapshot fell back or reached Core");
        Same(WheelInput.Value(WheelInput.Channel.Handbrake), 0, "failed snapshot retained old cache");
        Clock.frameCount++; source.Result = 1; WheelInput.PrepareGameQuery();
        Clock.realtimeSinceStartup += 30; WheelInput.Update();
        Check(source.Reads == 2 && Device.Reads == reads && Device.Opens == opens, "stopped bridge resumed/fell back/opened a device");
        Check(Host.Saves == saves && Host.Settings.HandbrakeBinding == binding, "handoff rewrote legacy settings");
        WheelInput.Close(); Check(native.Stops == 1, "close repeated spent native stop");
        Check(restores == 1, "close did not restore its native isolation lease");
    }
}
