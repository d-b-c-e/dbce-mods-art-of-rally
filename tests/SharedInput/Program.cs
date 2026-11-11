using System.Text.Json;
using ArtOfSimRally.Mod;
using ArtOfSimRally.Mod.Shim;
using static Fixture;
using Device = Dbce.Wheel.Ffb.WheelFfbNative;
using Host = ArtOfSimRally.Mod.Main;
using Clock = ArtOfSimRally.Mod.Time;

// The shared profile, the mod's one input route, through its production sources. The shared consumer is a process
// singleton, so each scenario runs in its own process: SharedInput <scenario> (build.ps1 runs them all).
static class Program
{
    static int assertions;
    static void Check(bool ok, string message) { assertions++; if (!ok) throw new Exception(message); }
    static void Same(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .000001f, message);

    static int Main(string[] args)
    {
        var scenarios = new Dictionary<string, Action>
        {
            ["read"] = () => { ReaderHealth(); SharedRead(); },
            ["actions"] = SharedActions,
            ["buttons"] = () => assertions += GameButtonTests.Run(),
            ["startup"] = StartupPaths,
            ["ffb"] = ForceSelection,
            ["lifecycle"] = Lifecycle,
            ["transport"] = TransportStop,
            ["ordinary"] = OrdinarySession,
            ["keyboard"] = KeyboardSteering,
            ["consumer"] = () => { Host.SaveSettings(); assertions += ShimConsumerTests.Run(); },
            ["isolation"] = () => { assertions += IsolationBoundaryTests.Run(); Transmission(); },
        };
        string name = args.Length == 1 ? args[0] : "";
        if (!scenarios.TryGetValue(name, out var run)) { Console.Error.WriteLine("usage: SharedInput <" + string.Join("|", scenarios.Keys) + ">"); return 2; }
        try
        {
            var directory = Path.GetFullPath(Path.Combine("results", "shared-input-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory); Host.Path = Path.Combine(directory, "Settings.xml");
            run();
            Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", scenario = name, assertions })); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    // Before a session attaches the readers are only kept healthy: read each frame, and reopened while paused when one
    // stops responding. Their health is the last observation only, never a device query.
    static void ReaderHealth()
    {
        Setup(); WheelReaders.Close();
        Check(WheelReaders.DeviceReadHealth(Wheel.ToString()) == null, "closed reader reported a known failure");
        Clock.realtimeSinceStartup += 6; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        int reads = Device.Reads, enumerations = Device.Enumerations;
        Check(WheelReaders.DeviceReadHealth(Wheel.ToString()) == true, "responsive identity not observed");
        Check(WheelReaders.DeviceReadHealth("22222222-2222-2222-2222-222222222222") == false, "missing identity followed another device");
        Check(WheelReaders.DeviceReadHealth("bad-guid") == null, "invalid identity reported as a device");
        Check(Device.Reads == reads && Device.Enumerations == enumerations, "health query touched hardware");
        Device.ReadOk = false; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        Check(WheelReaders.DeviceReadHealth(Wheel.ToString()) == false, "failed read kept healthy status");
        // While driving nothing is reopened; paused, the unresponsive reader is reopened after the retry delay.
        int opens = Device.Opens; GameState.IsDriving = true; Clock.realtimeSinceStartup += 10;
        for (int i = 0; i < 50; i++) { Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame(); }
        Check(Device.Opens == opens && Device.Enumerations == enumerations, "reader rediscovery while driving");
        GameState.IsDriving = false; Device.ReadOk = true; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        Check(Device.Opens > opens, "unresponsive reader not reopened while paused");
        Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        Check(WheelReaders.DeviceReadHealth(Wheel.ToString()) == true, "resumed read not observed");
        // A Rewired query never discovers or reopens readers; the same frame's watchdog still does.
        WheelReaders.Close(); Clock.realtimeSinceStartup += 6; Clock.frameCount++;
        opens = Device.Opens; reads = Device.Reads;
        Check(GameButtonInput.Prepare(PadManager.Player) && Device.Opens == opens && Device.Reads == reads, "query discovered a closed reader");
        WheelReaders.EnsureUpdatedThisFrame();
        Check(Device.Opens > opens && Device.Reads == reads + 1, "query prevented same-frame watchdog discovery");
        // Readers are only for a shared session: without one they close.
        WheelReaders.Required = false; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        Check(WheelReaders.Slots.Length == 0 && !WheelReaders.Enabled && !GameButtonInput.Prepare(PadManager.Player), "readers stayed open without a shared profile");
    }

    // After the handoff every slot is read once per frame, only through the bridge.
    static void SharedRead()
    {
        Setup(); Clock.frameCount = 7000; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        int reads = Device.Reads, opens = Device.Opens, saves = Host.Saves;
        var source = new BridgeTests.Source { Raw = 40000 }; var native = new BridgeTests.Native(); bool isolated = true;
        int restores = 0;
        WheelReaders.AttachShared(source, native, () => isolated, () => { restores++; return true; });
        Check(source.Reads == 0 && Device.Reads == reads && Device.Opens == opens, "handoff performed an extra read or open");
        Clock.frameCount++; WheelReaders.PrepareGameQuery();
        Check(source.Reads == 1 && native.Ticks == 1 && Device.Reads == reads, "production path did not replace exactly one direct read");
        Check(WheelReaders.DeviceReadHealth(Wheel.ToString()) == true, "the accepted shared sample's validity was not the reader health");
        WheelReaders.EnsureUpdatedThisFrame();
        Check(source.Reads == 1 && native.Ticks == 1, "same-frame update polled/ticked again");
        Check(ShimInput.IsolatedFrame == Clock.frameCount, "current isolation stamp missing");
        Clock.frameCount++; source.Result = -2; WheelReaders.PrepareGameQuery();
        Check(source.Reads == 2 && native.Ticks == 1 && native.Stops == 1 && Device.Reads == reads, "failed snapshot fell back or reached Core");
        Check(WheelReaders.DeviceReadHealth(Wheel.ToString()) == null, "a stopped session reported read health");
        Clock.frameCount++; source.Result = 1; WheelReaders.PrepareGameQuery();
        Clock.realtimeSinceStartup += 30; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        Check(source.Reads == 2 && Device.Reads == reads && Device.Opens == opens, "stopped bridge resumed/fell back/opened a device");
        Check(Host.Saves == saves, "handoff wrote settings");
        WheelReaders.Close(); Check(native.Stops == 1, "close repeated spent native stop");
        Check(restores == 1, "close did not restore its native isolation lease");
    }

    // Owned buttons and shortcuts come only from this frame's shared values; an unowned action never presses.
    static void SharedActions()
    {
        Setup(); Clock.frameCount = 9000; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        var source = new BridgeTests.Source(); float confirm = 0, menu = 0; // the source holds physical button 0 throughout
        var native = new ScriptedCore { Script = () => new[] { (9, L, confirm), (21, L, menu) } };
        WheelReaders.AttachShared(source, native, () => true, () => true);
        void Step() { Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame(); }
        Step();
        Check(source.Reads == 1 && !ProfileButtons.Button(ProfileButtons.Confirm, false), "released owned confirm pressed");
        Check(!ProfileButtons.Button(ProfileButtons.Back, false) && !ProfileButtons.Pressed(ProfileButtons.MuteFfb), "an unowned button or shortcut pressed");
        confirm = 1; Step();
        Check(ProfileButtons.Button(ProfileButtons.Confirm, true) && ProfileButtons.Button(ProfileButtons.Confirm, false), "shared confirm press");
        Step(); Check(!ProfileButtons.Button(ProfileButtons.Confirm, true) && ProfileButtons.Button(ProfileButtons.Confirm, false), "held confirm repeated");
        confirm = 0; Step(); Check(ProfileButtons.Released(ProfileButtons.Confirm), "shared confirm release");
        Check(!ProfileButtons.Pressed(ProfileButtons.ModMenu), "released shared shortcut pressed");
        menu = 1; Step(); Check(ProfileButtons.Pressed(ProfileButtons.ModMenu), "shared shortcut edge");
        Step(); Check(!ProfileButtons.Pressed(ProfileButtons.ModMenu), "held shortcut repeated");
        // Car input can run before the watchdog: it must publish and use this frame's snapshot.
        confirm = 1; Clock.frameCount++; int reads = source.Reads; float t = 0, b = 0, s = 0, h = 0, c = 0; bool e = false;
        Check(ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e), "car input not owned");
        Check(source.Reads == reads + 1 && ShimInput.Consumer.Fresh(Clock.frameCount), "car input composed a previous frame");
        // The first Rewired query after that read must still project this frame's buttons, without another read.
        WheelReaders.PrepareGameQuery();
        Check(ProfileButtons.Button(ProfileButtons.Confirm, true) && ProfileButtons.Button(ProfileButtons.Confirm, false), "button query after car input saw a stale projection");
        Check(source.Reads == reads + 1, "button projection read again");
        WheelReaders.EnsureUpdatedThisFrame(); Check(source.Reads == reads + 1 && native.Ticks == reads + 1, "same frame polled twice");
        Check(ProfileButtons.Button(ProfileButtons.Confirm, false), "watchdog reprojection dropped the held button");
        confirm = 0;
        // A refused frame (ownership changed) keeps the owned actions neutral for good.
        native.Script = () => new[] { (9, L, 1f), (21, L, 1f), (10, L, 1f) };
        menu = 0; Step();
        Check(ShimInput.Consumer.Terminal, "ownership change after open accepted");
        Check(!ProfileButtons.Button(ProfileButtons.Confirm, false) && !ProfileButtons.Pressed(ProfileButtons.ModMenu), "terminal session stayed live");
        Step(); Check(!ProfileButtons.Button(ProfileButtons.Confirm, false), "terminal session revived");
        WheelReaders.Close();
    }

    // Startup: the profile is used whenever one exists (Wheelkit's store, else dbce-controls.ini); each refusal is stated,
    // qualification waits for the menus and ready readers, and a refusal during qualification (here the real dbce_shim
    // with no loaded WheelFfb owner) stops Core and leaves no Rewired subscription.
    static void StartupPaths()
    {
        var mod = ModFolder("shared-startup");
        string storePath = SharedInputStartup.StorePath;
        string profilePath = Path.Combine(mod, SharedInputStartup.ProfileFileName), shimPath = Path.Combine(mod, SharedInputStartup.ShimFileName);
        string profile = "[Controls]\nSchema = 1\nProfile = Startup\nRevision = r1\nTransmission = Sequential\nsteer = axis 0 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=32768 travel=+1\n";
        string Text() => string.Join(" | ", SharedInputStartup.Lines());
        Setup(); Clock.frameCount = 12000; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        SharedInputStartup.Prepare(mod);
        Check(!SharedInputStartup.Active && !SharedInputStartup.RequestedAtStartup && Text() == SharedInputStatus.NoProfile && !WheelReaders.Required, "no profile: " + Text());
        File.WriteAllText(profilePath, profile); File.Delete(shimPath);
        SharedInputStartup.Prepare(mod); Check(!SharedInputStartup.Active && SharedInputStartup.RequestedAtStartup && Text().Contains("dbce_shim.dll is missing"), "no dll: " + Text());
        File.WriteAllText(shimPath, "not the qualified build");
        SharedInputStartup.Prepare(mod); Check(!SharedInputStartup.Active && Text().Contains("not the qualified build"), "wrong dll: " + Text());
        File.Copy(Environment.GetEnvironmentVariable("ART_SHIM_DLL") ?? PinnedShim(), shimPath, true); File.WriteAllText(profilePath, "");
        SharedInputStartup.Prepare(mod); Check(!SharedInputStartup.Active && Text().Contains("empty"), "empty profile: " + Text());
        File.WriteAllText(profilePath, profile);
        SharedInputStartup.Prepare(mod); Check(SharedInputStartup.Active && Text().Contains("waiting") && WheelReaders.Required, "valid preflight: " + Text());
        Check(!SharedInputStartup.FromStore, "a missing store counted");
        StoreCases(mod, storePath, Text);
        SharedInputStartup.Prepare(mod); Check(SharedInputStartup.Active && !SharedInputStartup.FromStore, "file preflight after the store cases: " + Text());
        // No qualification while driving or before Rewired is ready; nothing acquired meanwhile.
        GameState.IsDriving = true; Rewired.ReInput.isReady = true; SharedInputStartup.Tick();
        GameState.IsDriving = false; Rewired.ReInput.isReady = false; SharedInputStartup.Tick();
        Check(SharedInputStartup.Active && Text().Contains("waiting") && Rewired.ReInput.Subscribers == 0 && !Directory.Exists(SharedInputStartup.RunRoot), "qualified while driving or before Rewired was ready");
        // Real Core opens, then the missing WheelFfb owner refuses: everything is released.
        Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        Check(WheelReaders.Slots.Length > 0, "readers closed for a pending shared session");
        Rewired.ReInput.isReady = true; FfbNative.RequestedPath = "";
        SharedInputStartup.Tick();
        Check(!SharedInputStartup.Active && Text().Contains("WheelFfb owner has not loaded a module") && Text().Contains("game's own controls apply"), "qualification refusal: " + Text());
        Check(Rewired.ReInput.Subscribers == 0, "refusal leaked Rewired event subscriptions");
        Check(Directory.GetFiles(SharedInputStartup.RunRoot, "host-shutdown.json", SearchOption.AllDirectories).Length == 1, "refusal did not stop the opened Core host");
        Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        Check(!WheelReaders.Required && WheelReaders.Slots.Length == 0, "readers stayed open after the refusal");
        SharedInputStartup.Tick(); Check(!SharedInputStartup.Active && !ShimInput.Consumer.Started, "refused session retried or attached");
        WheelReaders.Close();
    }
    // Wheelkit's store decides art's profile when it exists (CFG-2): the profile's transmission sets TransmissionMode, a
    // profile without steering refuses, and F6 does not edit it.
    static void StoreCases(string mod, string storePath, Func<string> text)
    {
        const string wheel = "\"InstanceGuid\": \"d71b8350-61b7-11f1-8001-444553540000\", \"ProductGuid\": \"0006346e-0000-0000-0000-504944564944\", \"DeviceName\": \"Wheel\"";
        const string shifter = "\"InstanceGuid\": \"e71b8350-61b7-11f1-8001-444553540000\", \"ProductGuid\": \"1006346e-0000-0000-0000-504944564944\", \"DeviceName\": \"Shifter\"";
        string Store(string assigned, string extra = "") =>
            "{ \"Schema\": 2, \"ActiveId\": \"seq\", \"Profiles\": [" +
            "{ \"Id\": \"seq\", \"Profile\": { \"Name\": \"Store Seq\", \"Transmission\": \"Sequential\", \"Bindings\": {" +
            "\"steer\": { " + wheel + ", \"Kind\": \"Axis\", \"Index\": 0, \"Direction\": 1, \"Rest\": 32768, \"Minimum\": 0, \"Maximum\": 65535 }," +
            "\"shiftUp\": { " + shifter + ", \"Kind\": \"Button\", \"Index\": 4 }," +
            "\"select\": { " + wheel + ", \"Kind\": \"Button\", \"Index\": 9 } } } }," +
            "{ \"Id\": \"pedals\", \"Profile\": { \"Name\": \"No Steering\", \"Transmission\": \"Automatic\", \"Bindings\": {" +
            "\"throttle\": { " + wheel + ", \"Kind\": \"Axis\", \"Index\": 1, \"Direction\": 1, \"Rest\": 0, \"Minimum\": 0, \"Maximum\": 65535 } } } }" +
            "], \"Games\": { \"art-of-rally\": { \"Profile\": \"" + assigned + "\" } }" + extra + " }";
        Directory.CreateDirectory(Path.GetDirectoryName(storePath));
        Host.Settings.TransmissionMode = "Follow game";
        File.WriteAllText(storePath, Store("seq", ", \"FutureField\": { \"x\": 1 }"));
        var source = StoreProfileSource.Read(storePath);
        string written = System.Text.Encoding.UTF8.GetString(source.Profile);
        Check(source.Name == "Store Seq" && written.Contains("Profile = Store Seq\r\n") && written.Contains("\r\nshiftUp = button 4 ") &&
            !written.Contains("select") && source.Gearbox == "Sequential", "store text: " + written);
        SharedInputStartup.Prepare(mod);
        Check(SharedInputStartup.Active && SharedInputStartup.FromStore && SharedInputStartup.RequestedAtStartup && SharedInputStartup.StoreProfileName == "Store Seq" &&
            Host.Settings.TransmissionMode == "Sequential" && text().Contains("waiting"), "store preflight: " + text());
        Check(SharedBindingCaptureHost.Instance.Access != Dbce.Wheel.Input.SharedBindingAccess.Editable, "F6 can edit a store profile");
        File.WriteAllText(storePath, Store("pedals")); Host.Settings.TransmissionMode = "Follow game";
        SharedInputStartup.Prepare(mod);
        Check(!SharedInputStartup.Active && text().Contains("'No Steering' cannot be used") && Host.Settings.TransmissionMode == "Follow game", "store without steering: " + text());
        File.WriteAllText(storePath, "{");
        SharedInputStartup.Prepare(mod);
        Check(!SharedInputStartup.Active && SharedInputStartup.FromStore && text().Contains("store could not be read"), "unreadable store: " + text());
        Check(StoreProfileSource.GearboxFor("[Controls]\r\nneutral = button 1\r\n", "H-pattern") == null &&
            StoreProfileSource.GearboxFor("[Controls]\r\ngear1 = button 1\r\n", "H-pattern") == "H-pattern" &&
            StoreProfileSource.GearboxFor("[Controls]\r\nsteer = axis 0\r\n", "Sequential") == null, "gearbox rule");
        Directory.Delete(Path.GetDirectoryName(storePath), true);
    }

    // Production Prepare/selection/qualification order; only Core inspection and the qualification's hardware steps are faked.
    // Force feedback follows the profile's steering wheel frozen at startup, or an explicit choice; with no profile there
    // is nothing to follow.
    static void ForceSelection()
    {
        const string shared = "d71b8350-61b7-11f1-8001-444553540000", explicitId = "aaaaaaaa-0000-0000-0000-000000000001";
        var mod = ModFolder("shared-ffb");
        string profilePath = Path.Combine(mod, SharedInputStartup.ProfileFileName);
        var original = System.Text.Encoding.UTF8.GetBytes("[Controls]\nSchema = 1\nProfile = Frozen\n");
        File.WriteAllBytes(profilePath, original);
        byte[] inspected = null, begun = null; int inspections = 0, sessions = 0; bool settled = false;
        var targetLines = new List<string>();
        ModLog.Observe = line => { if (line.StartsWith("Shared input: configured steering target ")) targetLines.Add(line); };
        var core = new ScriptedCore { Script = () => new[] { (0, L, 0f) } };
        SharedInputStartup.InspectSteering = (path, bytes) => { inspections++; inspected = bytes; return new SharedSteeringTarget("Shared wheel", shared, null); };
        SharedInputStartup.NewSession = inventory => { sessions++; return new FakeQualification(core) { Began = bytes => begun = bytes }; };
        SharedInputStartup.ForceSettled = () => settled;
        Setup(); Clock.frameCount = 30000; Host.Settings.ForceFeedbackEnabled = false; Rewired.ReInput.isReady = true;
        int saves = Host.Saves, opens = Device.Opens;
        SharedInputStartup.Prepare(mod);
        Check(inspections == 1 && sessions == 0 && Device.Opens == opens, "target inspection opened/qualified readers or was delayed until attach");
        Check(targetLines.Count == 1 && targetLines[0].Contains("instance=" + shared) &&
            targetLines[0].Contains(ShimProfileNative.ProfileHash(original)) && targetLines[0].Contains("connection and force not verified"),
            "startup target trace lacks the configured identity/frozen hash or claims physical evidence: " + string.Join(" | ", targetLines));
        Check(FfbSelection.TryTarget(Host.Settings, out var name, out var index, out var guid, out _) && name == "Shared wheel" && index == -1 && guid == shared,
            "fresh shared-only configuration has no strict steering force target before recovery");
        Check(!Host.Settings.ForceFeedbackEnabled && Host.Saves == saves, "target resolution enabled force or saved settings");
        Frame(); Check(sessions == 0, "shared qualification ran before force ownership settled");
        Host.Settings.PreferredDeviceGuid = explicitId; Host.Settings.FfbDeviceMode = "explicit";
        Check(FfbSelection.TryTarget(Host.Settings, out _, out _, out guid, out _) && guid == explicitId, "explicit force selection did not win");
        Host.Settings.FfbDeviceMode = "steering";
        File.WriteAllText(profilePath, "changed after startup");
        Check(FfbSelection.TryTarget(Host.Settings, out _, out _, out guid, out _) && guid == shared, "a profile edit retargeted an active launch");
        settled = true; Frame(); Frame(); Frame();
        Check(sessions == 1 && ReferenceEquals(inspected, begun) && begun.SequenceEqual(original), "Begin did not consume the exact inspected byte array");
        Check(inspections == 1 && !Host.Settings.ForceFeedbackEnabled && Host.Saves == saves, "attach reran target resolution or changed force state");
        Check(targetLines.Count == 1, "target trace repeated after attachment");
        WheelReaders.Close(); SharedInputStartup.Shutdown();
        // The next start without a profile: nothing to follow, while an explicit choice still works.
        File.Delete(profilePath);
        SharedInputStartup.Prepare(mod);
        bool followed = FfbSelection.TryTarget(Host.Settings, out _, out _, out guid, out var none);
        Check(!SharedInputStartup.RequestedAtStartup && !followed && guid == "" && none.Contains("No shared profile"), "no profile still followed a steering wheel: " + none);
        Host.Settings.FfbDeviceMode = "explicit";
        Check(FfbSelection.TryTarget(Host.Settings, out _, out _, out guid, out _) && guid == explicitId, "explicit selection failed without a profile");
        Host.Settings.FfbDeviceMode = "steering";
        File.WriteAllBytes(profilePath, original);
        foreach (string refusal in new[] { "profile invalid", "steer unbound", ShimProfileNative.MatchingPackage })
        {
            SharedInputStartup.InspectSteering = (path, bytes) => { throw new InvalidDataException(refusal); };
            SharedInputStartup.Prepare(mod);
            Check(targetLines.Last().Contains("unavailable: " + refusal) && targetLines.Last().Contains(ShimProfileNative.ProfileHash(original)),
                "startup refusal trace lost its actual reason or frozen profile hash");
            Check(!FfbSelection.TryTarget(Host.Settings, out _, out _, out guid, out var why) && guid == "" && why == refusal,
                "refused/missing inspection fell back to another force target");
            Host.Settings.FfbDeviceMode = "explicit";
            Check(FfbSelection.TryTarget(Host.Settings, out _, out _, out guid, out _) && guid == explicitId, "inspection failure broke explicit selection");
            Host.Settings.FfbDeviceMode = "steering";
        }
        ModLog.Observe = null;
    }

    static string nextNeutral; static int nextFailures; static bool nextIsolated = true;
    // romi 28533: force ownership settling before the attach, a later force reconnect stopping the session truthfully
    // without reattach, and a failed joystick restore kept and retried instead of handed back.
    static void Lifecycle()
    {
        var mod = ModFolder("shared-lifecycle", "[Controls]\nSchema = 1\nProfile = Lifecycle\n");
        string Text() => string.Join(" | ", SharedInputStartup.Lines());
        float confirm = 0, steer = 0;
        var core = new ScriptedCore { Script = () => new[] { (0, L, steer), (9, L, confirm) } };
        FakeQualification fake = null; int sessions = 0; bool settled = true;
        SharedInputStartup.NewSession = inventory => { sessions++; return fake = new FakeQualification(core) { RestoreFailures = nextFailures, Neutral = nextNeutral, Isolated = nextIsolated }; };
        SharedInputStartup.ForceSettled = () => settled;
        void Start() { Setup(); Rewired.ReInput.isReady = true; SharedInputStartup.Prepare(mod); }
        Clock.frameCount = 20000;

        // A failed joystick restore keeps the session and never claims the game's controls are back.
        nextNeutral = "unqualified: raw axes unavailable or nonzero after disable"; nextFailures = 3;
        Start(); Frame();
        Check(sessions == 1 && fake.Closes == 1 && fake.RestorePending, "cleanup failure not retained");
        Check(Text().Contains("still off") && Text().Contains("retrying") && !Text().Contains("controls apply"), "cleanup failure claimed a handback: " + Text());
        GameState.IsDriving = true; int restores = fake.Restores;
        for (int i = 0; i < 200; i++) Frame();
        Check(fake.Restores == restores, "restore retried while driving");
        GameState.IsDriving = false; Frame();
        Check(fake.Restores == restores + 1 && Text().Contains("still off"), "restore not retried in the menus: " + Text());
        for (int i = 0; i < 59; i++) Frame();
        Check(fake.Restores == restores + 1, "restore retried every frame");
        for (int i = 0; i < 121; i++) Frame();
        Check(!fake.RestorePending && Text().Contains("game's own controls apply") && Text().Contains("restored on retry 3"), "restored session not released: " + Text());
        // Exhausted: stays explicit and stops retrying.
        nextFailures = 1000; Start(); Frame();
        for (int i = 0; i < 61 * 12; i++) Frame();
        Check(fake.Restores == 1 + SharedInputStartup.RestoreRetries && Text().Contains("restart the game"), "exhausted restore not explicit or still retrying: " + Text());

        // The readers open and stay open for the pending session.
        nextNeutral = null; nextFailures = 0; nextIsolated = false; settled = false; sessions = 0;
        int Runs() => Directory.Exists(SharedInputStartup.RunRoot) ? Directory.GetDirectories(SharedInputStartup.RunRoot).Length : 0;
        Start(); WheelReaders.Close(); Clock.realtimeSinceStartup += 6; int runs = Runs();
        for (int i = 0; i < 5; i++) Frame();
        Check(WheelReaders.Slots.Length > 0 && WheelReaders.Enabled, "readers closed or never opened for the shared profile");
        // Force ownership settles first: nothing is acquired while a reconnect is pending.
        Check(sessions == 0 && Text().Contains("force feedback") && Runs() == runs, "qualified before force feedback settled: " + Text());
        settled = true; Frame();
        Check(sessions == 1 && SharedInputStartup.Qualifying && WheelReaders.Pinned, "isolation phase not entered");
        // Pinned readers: no discovery reopen.
        int opens = Device.Opens; Device.ReadOk = false; Clock.realtimeSinceStartup += 6; Frame(); Device.ReadOk = true; Clock.realtimeSinceStartup += 6; Frame();
        Check(Device.Opens == opens && SharedInputStartup.Qualifying, "pinned readers reopened during isolation");
        fake.Isolated = true; Frame(); Frame();
        var consumer = ShimInput.Consumer;
        Check(consumer.Started && Text().Contains("drives the wheel") && Text().Contains(SharedInputStatus.Editors), "attach after settle failed: " + Text());
        confirm = 1; steer = .5f; Frame();
        Check(ProfileButtons.Button(ProfileButtons.Confirm, false), "shared confirm missing");
        float t = 0, b = 0, s = 0, h = 0, c = 0; bool e = false;
        Check(ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e) && Math.Abs(s - .5f) < .001f, "shared steering missing");
        confirm = 0; steer = 0; Frame();

        // A later force reconnect (Main.RecoverForceFeedback's reader handoff) stops the session for good.
        int restoresBefore = fake.Restores, ticks = core.Ticks;
        WheelReaders.ReleaseForForceReconnect(); WheelReaders.ReopenAfterForceReconnect();
        for (int i = 0; i < 5; i++) Frame();
        Check(Text().Contains("Shared input stopped: " + WheelReaders.ForceReconnectStop) && Text().Contains("until the game restarts"), "reconnect stop not stated: " + Text());
        Check(sessions == 1 && core.Ticks == ticks && fake.Restores == restoresBefore + 1, "session reattached, kept ticking or skipped its restore");
        Check(WheelReaders.Slots.Length > 0 && !ProfileButtons.Button(ProfileButtons.Confirm, false), "readers not reopened or stopped session not neutral");
        // Neutral driving, even for the game's own (keyboard) values; nothing falls back.
        confirm = 1; steer = 1; Clock.frameCount++; s = h = t = .3f;
        ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e);
        Check(s == 0 && h == 0 && t == 0, "stopped session drove the car or passed native input");
        Check(WheelReaders.DeviceReadHealth(Guid.NewGuid().ToString()) == null, "stopped session reported a read failure");
        WheelReaders.Close();
    }

    // romi 28966: a transport-only fault (Core refuses a tick) keeps the wheel neutral and Core untouched but leaves
    // keyboard driving while the session's own isolation still verifies; once isolation is lost or released (a close),
    // driving is neutral for good, and nothing re-enables the wheel.
    static void TransportStop()
    {
        var mod = ModFolder("shared-transport", "[Controls]\nSchema = 1\nProfile = Transport\n");
        string Text() => string.Join(" | ", SharedInputStartup.Lines());
        var core = new ScriptedCore { Script = () => new[] { (0, L, .5f), (9, L, 1f) } };
        FakeQualification fake = null;
        SharedInputStartup.NewSession = inventory => fake = new FakeQualification(core);
        SharedInputStartup.ForceSettled = () => true;
        float Drive(out float throttle)
        {
            Clock.frameCount++; float t = .3f, b = 0, s = .3f, h = 0, c = 0; bool e = false; // .3: the game's own (keyboard) values
            ShimInput.TryOverride(new AxisCarController(), ref t, ref b, ref s, ref h, ref c, ref e);
            throttle = t; return s;
        }
        Setup(); Rewired.ReInput.isReady = true;
        WheelReaders.Close(); Clock.realtimeSinceStartup += 6; Clock.frameCount = 30000;
        SharedInputStartup.Prepare(mod);
        for (int i = 0; i < 3; i++) Frame();
        Check(ShimInput.Consumer.Started && Math.Abs(Drive(out _) - .5f) < .001f, "shared steering not live before the fault: " + Text());
        // Core refuses a tick: transport-only stop with isolation still verified.
        core.Refuse = true; Frame(); core.Refuse = false; int ticks = core.Ticks;
        Check(WheelReaders.SharedStopReason == "shared provider refused physical batch" && WheelReaders.SharedKeyboardRetained, "transport stop not recorded: " + WheelReaders.SharedStopReason);
        float steer = Drive(out float throttle);
        Check(Math.Abs(steer - .3f) < .001f && Math.Abs(throttle - .3f) < .001f, "keyboard driving lost after a transport-only stop: " + steer);
        Frame(); Frame();
        Check(core.Ticks == ticks && !ProfileButtons.Button(ProfileButtons.Confirm, false), "stopped session ticked Core or delivered the wheel");
        Check(Text().Contains("keyboard driving continues") && Text().Contains("refused physical batch"), "transport stop text: " + Text());
        Check(WheelReaders.DeviceReadHealth(Wheel.ToString()) == null, "stopped session reported read health");
        // Isolation lost once: neutral for good, even if it later verifies again.
        fake.Isolated = false; Frame();
        Check(!WheelReaders.SharedKeyboardRetained && Drive(out _) == 0f, "keyboard kept after isolation was lost");
        fake.Isolated = true; Frame(); Frame();
        Check(Drive(out throttle) == 0f && throttle == 0f && Text().Contains("wheel and keyboard alike"), "neutral not sticky after isolation loss: " + Text());
        WheelReaders.Close();
    }

    // romi 31428: an ordinary session through the production GetInput postfix. Owned actions come only from the shared
    // profile; the game's own (keyboard) values pass through for unowned axes; the horn owes one release after a stop.
    static void OrdinarySession()
    {
        Setup(); Clock.frameCount = 70000; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        float horn = 0f, clutch = .4f;
        var source = new BridgeTests.Source { Pressed = -1 };
        var native = new ScriptedCore { AxesOwned = new[] { 0, 2, 3 }, Script = () => new[] { (0, L, .25f), (2, L, .5f), (3, L, clutch), (9, L, 0f), (17, L, 0f),
            (18, L, 0f), (19, L, 0f), (20, L, horn) } };
        WheelReaders.AttachShared(source, native, () => true, () => true, "Sequential");
        void Step() { Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame(); }
        Step(); Step();
        var consumer = ShimInput.Consumer;
        Check(consumer.Started && consumer.Owns(0) && consumer.Owns(2) && consumer.Owns(3) && !consumer.Owns(1) && !consumer.Owns(4), "fixture ownership");
        Step(); var v = Car();
        Same(v.Steer, .27f, "owned steer not the shared value plus alignment");
        Same(v.Brake, .5f, "owned brake not the shared value");
        Same(v.Clutch, .4f, "owned clutch not the larger of game and shared");
        Same(v.Throttle, .3f, "keyboard throttle did not pass through an unowned axis");
        Same(v.Handbrake, .2f, "keyboard handbrake did not pass through an unowned axis");
        Check(!ProfileButtons.Button(ProfileButtons.Back, false) && !ProfileButtons.Button(ProfileButtons.Start, false), "an unowned button pressed");
        Check(consumer.Owns(18) && consumer.Owns(19) && consumer.Owns(20) && !ProfileButtons.Button(ProfileButtons.Horn, false) &&
            !ProfileButtons.Button(ProfileButtons.LookBack, false) && !ProfileButtons.Button(ProfileButtons.Recover, false), "released owned buttons pressed");
        horn = 1f; Step();
        Check(ProfileButtons.Button(ProfileButtons.Horn, true) && ProfileButtons.Button(ProfileButtons.Horn, false), "shared horn press not delivered");
        horn = 0f; Step(); Check(ProfileButtons.Released(ProfileButtons.Horn) && !ProfileButtons.Button(ProfileButtons.Horn, false), "shared horn release not delivered");
        horn = 1f; Step(); Check(ProfileButtons.Button(ProfileButtons.Horn, true), "second shared horn press");
        // The owned clutch: the larger of the game's and the shared value while driving; the game's own in the finish animation.
        foreach (float travel in new[] { 0f, .03f, .5f, 1f })
        {
            clutch = travel; Step();
            foreach (var status in new[] { EventStatusEnums.EventStatus.UNDERWAY, EventStatusEnums.EventStatus.WAITING_TO_BEGIN })
            foreach (float stock in new[] { 0f, .7f, 1f })
            {
                GameEntryPoint.EventManager.status = status;
                Check(Car(clutch: stock).Clutch == Math.Max(stock, travel), $"shared clutch {status} stock={stock} travel={travel}");
            }
        }
        GameEntryPoint.EventManager.status = EventStatusEnums.EventStatus.FINISHING_STAGE_ANIMATION;
        Check(Car(clutch: .4f).Clutch == .4f, "shared clutch changed finishing input");
        Host.SettingsVisible = true;
        Check(Car(clutch: 1f).Clutch == 0f, "clutch leaked while the panel was open");
        Host.SettingsVisible = false; Application.isFocused = false;
        Check(Car(clutch: 1f).Clutch == 0f, "clutch leaked without focus");
        Application.isFocused = true;
        GameEntryPoint.EventManager.status = EventStatusEnums.EventStatus.UNDERWAY;
        // A transport-only stop keeps the verified keyboard path but nothing comes from the wheel.
        // The horn is still held: the stop owes the game its GetButtonUp, once.
        native.Refuse = true; Step();
        Check(ProfileButtons.Released(ProfileButtons.Horn) && !ProfileButtons.Button(ProfileButtons.Horn, false), "stopped session left the horn sounding");
        native.Refuse = false; Step();
        Check(!ProfileButtons.Released(ProfileButtons.Horn), "horn released twice after the stop");
        v = Car();
        Check(consumer.Terminal && WheelReaders.SharedKeyboardRetained, "transport stop not reached");
        Same(v.Clutch, .1f, "shared clutch applied after the session stopped");
        Same(v.Steer, .9f, "keyboard steering lost after a transport-only stop");
        Same(v.Throttle, .3f, "keyboard throttle lost after a transport-only stop");
        WheelReaders.Close();
    }

    // sana 35373: Art's keyboard map contributing to the car's own steering action keeps the game's steer value over a
    // resting off-centre shared wheel, through the production GetInput postfix; release returns to the wheel at once.
    // A failed query keeps the wheel and is recorded Unknown, not Absent. Lost isolation never asks.
    static void KeyboardSteering()
    {
        Setup(); Clock.frameCount = 71000; Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame();
        float wheel = .25f; bool isolation = true; // the wheel resting off centre without force
        var native = new ScriptedCore { AxesOwned = new[] { 0 }, Script = () => new[] { (0, L, wheel) } };
        WheelReaders.AttachShared(new BridgeTests.Source(), native, () => isolation, () => true, "Sequential");
        Rewired.ReInput.isReady = true; Rewired.Player.KeyboardAction = null; Rewired.Player.QueryFault = false;
        float CarSteer(float game, float alignment = .02f, string axis = "Horizontal")
        {
            var car = new AxisCarController { SteeringOutOfAlignmentEffect = alignment, steerAxis = axis };
            object[] a = { car, 0f, 0f, game, 0f, 0f, false };
            typeof(CarInputPatch).GetMethod("Override", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, a);
            return (float)a[3];
        }
        void Step() { Clock.frameCount++; WheelReaders.EnsureUpdatedThisFrame(); }
        void State(KeyboardSteer expected, bool kept, string why)
        {
            var last = ShimInput.Last;
            Check(last.Frame == Clock.frameCount && last.Keyboard == expected && last.KeyboardSteered == kept, why + ": " + last.Keyboard + " kept=" + last.KeyboardSteered);
        }
        Step(); Step();
        Check(ShimInput.Consumer.Owns(0), "fixture steer ownership");
        Step(); Same(CarSteer(.02f), .27f, "resting wheel lost without keyboard"); State(KeyboardSteer.Absent, false, "no keyboard source");
        Rewired.Player.KeyboardAction = "Horizontal";
        Step(); Same(CarSteer(1.02f), 1.02f, "keyboard right lost to the resting wheel"); State(KeyboardSteer.Present, true, "keyboard right");
        Step(); Same(CarSteer(-.98f), -.98f, "keyboard left lost to the resting wheel"); State(KeyboardSteer.Present, true, "keyboard left");
        wheel = -.3f; Step(); Same(CarSteer(1.02f), 1.02f, "keyboard lost to a wheel resting the other way");
        Rewired.Player.KeyboardAction = null;
        Step(); Same(CarSteer(.02f), -.28f, "release did not return to the wheel at once"); State(KeyboardSteer.Absent, false, "released");
        // A reported source with zero composed steer stays zero: not the wheel, and alignment is not added again.
        Rewired.Player.KeyboardAction = "Horizontal";
        Step(); Same(CarSteer(0f, 0f), 0f, "zero keyboard steer replaced by the wheel"); State(KeyboardSteer.Present, true, "zero keyboard steer");
        Step(); Same(CarSteer(.02f), .02f, "alignment applied twice under keyboard steering");
        // Only the car's own steering action counts.
        Step(); Same(CarSteer(1.02f, .02f, "SteerOther"), -.28f, "keyboard on another action kept"); State(KeyboardSteer.Absent, false, "other action");
        // A failed query keeps the wheel, recorded as Unknown rather than Absent.
        Rewired.Player.QueryFault = true;
        Step(); Same(CarSteer(1.02f), -.28f, "failed query kept the game's steer"); State(KeyboardSteer.Unknown, false, "query fault");
        Rewired.Player.QueryFault = false; Rewired.ReInput.isReady = false;
        Step(); Same(CarSteer(1.02f), -.28f, "unready Rewired kept the game's steer"); State(KeyboardSteer.Unknown, false, "Rewired not ready");
        Rewired.ReInput.isReady = true;
        // An ordinary transport stop keeps the verified keyboard path: it still asks, and keyboard steer is kept.
        native.Refuse = true;
        Step(); Same(CarSteer(1.02f), 1.02f, "ordinary transport stop lost keyboard steering"); State(KeyboardSteer.Present, true, "ordinary transport stop");
        Check(ShimInput.Consumer.Terminal, "ordinary transport stop not reached");
        // Lost isolation: Rewired is never asked and the game's mixed value is never kept.
        int queries = Rewired.Player.Queries; isolation = false;
        Step(); Same(CarSteer(1.02f), 0f, "lost isolation kept the game's mixed steer"); State(KeyboardSteer.NotQueried, false, "lost isolation");
        Check(Rewired.Player.Queries == queries, "keyboard queried without isolation");
        WheelReaders.Close();
    }

    // Art's gearbox switch (TransmissionInput) on the game's own transmission setter.
    static void Transmission()
    {
        Host.Settings = new Settings(); Host.Enabled = true;
        var drivetrain = new Drivetrain { automatic = true };
        Host.Settings.TransmissionMode = "Sequential"; TransmissionInput.Apply(drivetrain); Check(!drivetrain.automatic, "manual preference ignored");
        Host.Settings.TransmissionMode = "Automatic"; TransmissionInput.Apply(drivetrain); Check(drivetrain.automatic, "automatic preference ignored");
        Host.Settings.TransmissionMode = "Follow game"; drivetrain.automatic = false; TransmissionInput.Apply(drivetrain); Check(!drivetrain.automatic, "game preference overridden by default");
        Host.Enabled = false; Host.Settings.TransmissionMode = "Automatic"; TransmissionInput.Apply(drivetrain); Check(!drivetrain.automatic, "disabled mod applied transmission"); Host.Enabled = true;
    }
}
