using System.Globalization;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Xml.Linq;
using ArtOfSimRally.Testing;

static class Program
{
    static int assertions;
    static void Check(bool ok, string reason) { assertions++; if (!ok) throw new Exception(reason); }
    static XElement Identity() => new XElement("capture", new XAttribute("origin", "synthetic"), new XElement("modSha256", "synthetic-fixture"));
    static void Samples(CaptureSession session)
    {
        for (int i = 0; i < 4; i++) session.Frame(new FrameSample { Number = 100 + i, Time = i + 1, Delta = .016f, Driving = 1, Direct = 1, Steer = .2f, Throttle = .5f, Brake = 0, Clutch = 0, Handbrake = 0 });
        float previous = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i == 2) { session.Reset(); previous = 0; }
            float fy = i % 2 == 0 ? 8000 : -1500;
            float output = LegacyForceCurve.Evaluate(fy, 8, 8.5f, 40, 11500, .3f, false, .5f, previous);
            session.Force(new ForceSample { Time = i + 1, Fy = fy, Slip = 8, Ideal = 8.5f, Speed = 40, Reference = 11500, Gain = .3f, Smoothing = .5f, Previous = previous, Output = output, Device = (int)(output * 10000), Invert = 0,
                Motion = new MotionSample { Valid = 1, PhysicsTime = i * .02f, ContactMask = 15, Qx = 0, Qy = 0, Qz = 0, Qw = 1,
                    Px = 0, Py = 0, Pz = i * .2f, Vx = 0, Vy = 0, Vz = 10, LocalVx = 0, LocalVy = 0, LocalVz = 10,
                    CompressionFL = .1f, CompressionFR = .12f, CompressionRL = .08f, CompressionRR = .15f, TravelFL = .2f, TravelFR = .2f, TravelRL = .25f, TravelRR = .25f } });
            session.Delivery(true); previous = output;
            session.Collision(Collision(i + 1, i * .02f));
        }
    }
    static CollisionSample Collision(float time, float physics) => new CollisionSample
    {
        Time = time, PhysicsTime = physics, BodyId = -123456789, OtherId = 123456789, OtherLayer = 8,
        Road = 0, Crowd = 1, Contacts = 1, Examined = 1, Selected = 0,
        Rvx = 0, Rvy = 0, Rvz = -10, Ix = 0, Iy = 0, Iz = 100, Mass = 100,
        Px = 1, Py = 2, Pz = 3, Qx = 0, Qy = 0, Qz = 0, Qw = 1,
        Vx = 0, Vy = 0, Vz = 10, Nx = 0, Ny = 0, Nz = -1, Cpx = 1, Cpy = 2, Cpz = 4
    };
    static string Capture(string root)
    {
        var session = new CaptureSession();
        Check(!session.Start(true, Identity(), root), "START accepted while driving");
        Check(session.Start(false, Identity(), root), "START failed");
        Check(!Directory.Exists(root), "START wrote to disk");
        Check(!session.Start(false, Identity(), root), "START overwrote pending capture");
        Samples(session);
        Check(!Directory.Exists(root), "sampling wrote to disk");
        Check(!session.Stop(true) && session.Active, "STOP accepted while driving");
        Check(session.Stop(false) && !session.Pending && !session.Active, "STOP failed");
        string saved = session.SavedDirectory;
        var receipt = XDocument.Load(Path.Combine(saved, "manifest.xml")).Root;
        Check((bool)receipt.Attribute("complete"), "complete capture rejected");
        Check((int)receipt.Attribute("schema") == 4, "collision schema not declared");
        foreach (string kind in new[] { "frames", "forces", "signals", "collisions" })
        {
            Check((int)receipt.Element(kind).Attribute("count") == 4, "wrong count");
            Check(receipt.Element(kind).Value == ArtifactHash.FileHash(Path.Combine(saved, kind + ".csv")), "wrong hash");
        }
        var signalRows = File.ReadAllLines(Path.Combine(saved, "signals.csv")).Skip(1).Select(row => row.Split(',')).ToArray();
        Check(signalRows.All(row => row.Length == 27 && row[3] == "1" && row[5] == "15"), "signal layout/availability/contact lost");
        Check(signalRows[2][0] == "2" && signalRows[2][1] == "3" && signalRows[2][2] == "1", "motion did not follow force reset epoch");
        Check(signalRows[1][4] == "0.02" && signalRows[1][11] == "10" && signalRows[1][18] == "10" && signalRows[1][19] == "0.1" && signalRows[1][26] == "0.25", "signal values/units lost");
        var collisionRows = File.ReadAllLines(Path.Combine(saved, "collisions.csv")).Skip(1).Select(row => row.Split(',')).ToArray();
        Check(collisionRows.All(row => row.Length == 36), "collision layout changed");
        Check(collisionRows[2][3] == "1" && collisionRows[2][4] == "2", "collision epoch/preceding row lost");
        Check(collisionRows[0][5] == "-123456789" && collisionRows[0][6] == "123456789", "collision IDs rounded through floats");
        Check(!session.Stop(false), "duplicate STOP rewrote evidence");
        Check(session.Start(false, Identity(), root, 1, 1), "second START failed");
        Samples(session); Check(session.Stop(false), "truncated save failed");
        Check(!(bool)XDocument.Load(Path.Combine(session.SavedDirectory, "manifest.xml")).Root.Attribute("complete"), "overflow silently complete");
        Check(session.Start(false, Identity(), root, 8, 8), "third START failed");
        Samples(session); session.AbortSampling("broken hook");
        Check(!session.Active && session.Pending, "probe failure lost buffers/kept sampling");
        Check(session.Stop(false), "aborted save failed");
        Check(!(bool)XDocument.Load(Path.Combine(session.SavedDirectory, "manifest.xml")).Root.Attribute("complete"), "broken hook silently complete");
        string blocker = Path.Combine(root, "blocked"); File.WriteAllText(blocker, "keep");
        Check(session.Start(false, Identity(), blocker, 8, 8), "retry setup failed"); Samples(session);
        Check(!session.Stop(false) && session.Pending && !session.Active, "failed write lost buffers");
        File.Move(blocker, blocker + ".old");
        Check(session.Stop(false) && !session.Pending, "retry failed");
        Check(File.ReadAllText(blocker + ".old") == "keep", "writer damaged existing file");
        Check(session.Start(false, Identity(), root, 1, 10001), "allocation setup failed");
        session.Force(default); // JIT/warmup before allocation measurement.
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) session.Force(new ForceSample { Motion = new MotionSample { Valid = 1, Qw = 1 } });
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "motion sampling allocated managed objects");
        Check(session.Stop(false), "allocation capture save failed");
        Check(session.Start(false, Identity(), root, 1, 1), "collision allocation setup failed");
        session.Collision(Collision(1, 1));
        allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 5000; i++) session.Collision(Collision(i + 2, i + 2));
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "collision sampling allocated");
        Check(session.Describe.Contains("collisions=4096") && session.Describe.Contains("incomplete=True"), "collision overflow hidden");
        Check(session.Stop(false), "collision overflow save failed");
        var overflow = XDocument.Load(Path.Combine(session.SavedDirectory, "manifest.xml")).Root;
        Check(!(bool)overflow.Attribute("complete") && (int)overflow.Element("collisions").Attribute("count") == 4096, "collision overflow silently complete");
        session.Collision(Collision(9999, 9999));
        Check(!session.Pending, "stopped collision resurrected capture");
        return saved;
    }
    static void Pipe()
    {
        foreach (string state in new[] { "listening", "reading", "queued" })
        {
            using var blocked = new ControlServer("AOSR-stop-" + Guid.NewGuid().ToString("N"));
            string blockedName = (string)typeof(ControlServer).GetField("name", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(blocked)!;
            using var connection = new NamedPipeClientStream(".", blockedName, PipeDirection.InOut);
            if (state != "listening")
            {
                connection.Connect(2000);
                if (state == "queued") { var bytes = System.Text.Encoding.UTF8.GetBytes("START\n"); connection.Write(bytes); connection.Flush(); }
            }
            Thread.Sleep(50);
            var elapsed = System.Diagnostics.Stopwatch.StartNew(); blocked.Dispose();
            Check(!blocked.IsAlive && elapsed.ElapsedMilliseconds < 1000, "pipe shutdown hung while " + state);
            blocked.Pump(_ => throw new Exception("cancelled shutdown command executed"));
        }
        string name = "AOSR-test-" + Guid.NewGuid().ToString("N");
        using var server = new ControlServer(name);
        foreach (string command in new[] { "START", "STATUS", "STOP", "INVALID" })
        {
            var client = Task.Run(() =>
            {
                using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
                pipe.Connect(3000);
                using var writer = new StreamWriter(pipe, new System.Text.UTF8Encoding(false), 1024, true) { AutoFlush = true };
                using var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, false, 1024, true);
                writer.WriteLine(command);
                return reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(7)).GetAwaiter().GetResult();
            });
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (!client.IsCompleted && timer.Elapsed.TotalSeconds < 8) { server.Pump(c => "OK " + c); Thread.Sleep(5); }
            Check(client.IsCompleted, "pipe client hung");
            Check(client.GetAwaiter().GetResult() == (command == "INVALID" ? "ERROR unknown command" : "OK " + command), "pipe command/reply mismatch");
        }
    }
    static void Quit(string root)
    {
        var session = new CaptureSession();
        Check(session.BeforeProcessExit(() => throw new Exception("idle exit ran shutdown")), "idle quit blocked");
        session.Start(false, Identity(), root, 8, 8); Samples(session);
        Check(!session.BeforeProcessExit(() => { }), "quit allowed without a successful save");
        Check(session.Pending && session.Active && !Directory.Exists(root), "missing save lost buffers");
        Check(!session.BeforeProcessExit(() => throw new IOException("release failed")), "quit allowed after failed output release");
        Check(session.Pending && !Directory.Exists(root), "failed release wrote or lost capture");
        bool released = false;
        Check(session.BeforeProcessExit(() => { released = true; session.Stop(false); }), "saved capture blocked quit");
        Check(released && !session.Pending && File.Exists(Path.Combine(session.SavedDirectory, "manifest.xml")), "quit lost saved capture");
        string blocker = Path.Combine(root, "blocked"); File.WriteAllText(blocker, "keep");
        session.Start(false, Identity(), blocker, 8, 8); Samples(session);
        Check(!session.BeforeProcessExit(() => session.Stop(false)) && session.Pending, "quit discarded failed write");
        File.Move(blocker, blocker + ".old");
        Check(session.BeforeProcessExit(() => session.Stop(false)), "quit retry failed");
        Check(File.ReadAllText(blocker + ".old") == "keep", "quit retry damaged existing file");
    }
    static void Contract(string modPath, string gameDir)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(modPath));
        var paths = new[] { root, Path.Combine(gameDir, "artofrally_Data", "Managed"), Path.GetFullPath("lib/umm"), Path.GetFullPath("lib/toolkit/dotnet") };
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (string directory in paths)
            {
                string candidate = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
            }
            return null;
        };
        var mod = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(modPath));
        var force = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath("lib/toolkit/dotnet/Dbce.Wheel.Ffb.dll"));
        var access = new SubjectAccess(mod, force); // Compiles every getter without invoking Unity/native APIs.
        Check(access.Drive.GetParameters().Single().ParameterType.Name == "CarDynamics", "physics signature changed");
        Check(access.Send.ReturnType == typeof(bool) && access.Send.GetParameters().Single().ParameterType == typeof(int), "native observation signature changed");
        Check(!access.Update.IsStatic && access.Shutdown.IsStatic && access.Reset.IsStatic, "lifecycle signatures changed");
        Check(!access.Exit.IsStatic && access.Exit.DeclaringType.Name == "ExitGame" && access.Exit.ReturnType == typeof(void), "menu exit signature changed");
        Check(access.Collision.GetParameters().Single().ParameterType.Name == "Collision" && access.Collision.ReturnType == typeof(void), "collision callback signature changed");
        Check(access.PlayerBody() == null && !access.Restarting(), "passive menu query created state");
        Check(access.Smoothed() == 0, "unexpected filter initialization");
        var main = mod.GetType("ArtOfSimRally.Mod.Main", true);
        var cfg = Activator.CreateInstance(mod.GetType("ArtOfSimRally.Mod.Settings", true));
        var settings = main.GetProperty("Settings", BindingFlags.NonPublic | BindingFlags.Static);
        settings.SetValue(null, cfg);
        var tune = access.ReadTune();
        Check(tune.Reference == 11500f / .6f && tune.Smoothing == .2f && tune.Gain == 1 && !tune.Invert, "probe does not record the new standard reference");
        cfg.GetType().GetField("FyReference").SetValue(cfg, 11500f);
        cfg.GetType().GetField("Strength").SetValue(cfg, 30);
        tune = access.ReadTune();
        Check(tune.Reference == 11500 && tune.Gain == .6f, "probe substitutes the new default for an explicit legacy tune");
    }
    // carInput=shared leaves the car to the mod's shared input and ends on the probe's receipt or a bound.
    static void Policy()
    {
        CapturePolicy Parse(params string[] pairs)
        {
            var request = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pairs.Length; i += 2) request[pairs[i]] = pairs[i + 1];
            return CapturePolicy.Parse(request);
        }
        string Refusal(params string[] pairs) { try { Parse(pairs); return ""; } catch (InvalidDataException e) { return e.Message; } }
        var synthetic = Parse();
        Check(!synthetic.SharedCarInput && synthetic.Seconds == 12f && synthetic.StopFile == null && synthetic.Source == "synthetic-integration", "synthetic default changed");
        string stop = Path.Combine(Path.GetTempPath(), "probe-receipt.json");
        var shared = Parse("carInput", "shared", "captureSeconds", "40", "stopWhenFile", stop);
        Check(shared.SharedCarInput && shared.Seconds == 40f && shared.StopFile == stop && shared.Source == "shared-shim-integration", "shared policy not parsed");
        var native = Parse("carInput", "native");
        Check(native.NativeCarInput && !native.SharedCarInput && native.Seconds == 12f && native.StopFile == null && native.Source == "native-script-integration", "native policy not parsed");
        Check(Refusal("carInput", "native", "stopWhenFile", stop).Contains("needs carInput=shared"), "native capture accepted a stop file");
        // Native: the route waits at the title for the shared session (it qualifies only in the menus), at most 120 s.
        float held = -1;
        Check(!shared.Holds(false, 1, ref held) && !synthetic.Holds(false, 1, ref held) && held < 0, "a shared or synthetic route waited");
        Check(native.Holds(false, 2, ref held) && held == 2 && native.Holds(false, 122, ref held) && !native.Holds(true, 123, ref held), "native route did not wait for the shared session");
        bool late = false;
        try { native.Holds(false, 122.5f, ref held); } catch (InvalidOperationException e) { late = e.Message.Contains("did not start within 120 s"); }
        Check(late, "a shared session later than 120 s was waited for");
        // holdForShared=1: an ordinary shared route waits at the title like native; it is opt-in and shared-only.
        var holding = Parse("carInput", "shared", "holdForShared", "1", "captureSeconds", "30");
        float heldShared = -1;
        Check(holding.SharedCarInput && holding.HoldForShared && holding.WaitsForShared && !shared.HoldForShared && !shared.WaitsForShared && native.WaitsForShared,
            "holdForShared not parsed or not opt-in");
        Check(holding.Holds(false, 3, ref heldShared) && heldShared == 3 && !holding.Holds(true, 4, ref heldShared), "a holding shared route did not wait for the shared session");
        bool lateShared = false;
        try { holding.Holds(false, 123.5f, ref heldShared); } catch (InvalidOperationException e) { lateShared = e.Message.Contains("did not start within 120 s"); }
        Check(lateShared, "a holding shared route waited past 120 s");
        Check(Refusal("holdForShared", "1").Contains("needs carInput=shared") && Refusal("carInput", "native", "holdForShared", "1").Contains("needs carInput=shared") &&
            Refusal("carInput", "shared", "holdForShared", "yes").Contains("must be 1"), "bad holdForShared accepted");
        Check(Refusal("carInput", "keyboard").Contains("synthetic, shared or native") && Refusal("carInput", "shared", "captureSeconds", "600").Contains("5..120") &&
            Refusal("carInput", "shared", "captureSeconds", "NaN").Contains("5..120") && Refusal("carInput", "shared", "captureSeconds", "Infinity").Contains("5..120") &&
            Refusal("stopWhenFile", stop).Contains("needs carInput=shared") && Refusal("carInput", "shared", "stopWhenFile", "receipt.json").Contains("absolute"), "bad capture policy accepted");
        float underwayAt = -1; bool present = false;
        Func<string, bool> exists = p => present && p == stop;
        Check(!shared.Done(false, 1, ref underwayAt, exists) && underwayAt < 0, "menus ended the capture or started its clock");
        Check(!shared.Done(true, 10, ref underwayAt, exists) && underwayAt == 10 && !shared.Done(true, 49.9f, ref underwayAt, exists), "bound ended early");
        Check(shared.Done(true, 50, ref underwayAt, exists), "bound not applied");
        float early = -1; present = true;
        Check(shared.Done(false, 1, ref early, exists), "probe receipt before the stage did not end the capture");
        // The production readiness binding (SharedReadiness.For, what SessionTape hands the capture driver), with stub
        // ShimInput types (sana 45326): 8989f63 handed holdForShared a delegate whose consumer only native bound.
        Readiness(holding, native, shared, synthetic);
    }
    // The opt-in recording diagnostics (sana 47008): the exact object SessionTape parses for each request and asks on every
    // recorded frame. An ordinary request never dumps Rewired's rows; a requested one dumps at first readiness and 5 s
    // later; a later ordinary request in the same process dumps nothing; bad values and replays are refused.
    static void Diagnostics()
    {
        RecordingDiagnostics Parse(bool replay, params string[] pairs)
        {
            var request = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["mode"] = replay ? "replay" : "record", ["tape"] = "t" };
            for (int i = 0; i < pairs.Length; i += 2) request[pairs[i]] = pairs[i + 1];
            return RecordingDiagnostics.Parse(request, replay);
        }
        // 30 s of recorded frames at 10 Hz, Rewired ready from 2 s: the frames a pass falls due on.
        int[] Due(RecordingDiagnostics d)
        {
            var due = new List<int>();
            for (int i = 0; i <= 300; i++) { float now = i / 10f; if (d.InventoryPending && d.InventoryPassDue(now >= 2f, now) > 0) due.Add(i); }
            return due.ToArray();
        }
        var ordinary = Parse(false);
        Check(!ordinary.RewiredInventory && !ordinary.IdentityProbe && !ordinary.InventoryPending && Due(ordinary).Length == 0, "an ordinary recording dumped Rewired's rows");
        var asked = Parse(false, "rewiredInventory", "1");
        var passes = Due(asked);
        Check(asked.RewiredInventory && !asked.IdentityProbe && passes.Length == 2 && passes[0] == 20 && passes[1] == 70 && !asked.InventoryPending,
            "a requested inventory was not written at first readiness and 5 s later: " + string.Join(",", passes));
        var later = Parse(false);
        Check(!later.InventoryPending && Due(later).Length == 0, "the inventory carried into a later ordinary request");
        Check(!RecordingDiagnostics.Off.InventoryPending && Due(RecordingDiagnostics.Off).Length == 0, "the reset state dumped");
        var both = Parse(false, "identityProbe", "1", "RewiredInventory", "1");
        Check(both.IdentityProbe && both.RewiredInventory, "identityProbe, or a key in another case (the request reader folds case), not honoured");
        string Refused(bool replay, params string[] pairs) { try { Parse(replay, pairs); return ""; } catch (InvalidDataException e) { return e.Message; } }
        foreach (var bad in new[] { "0", "true", "", "2" })
            Check(Refused(false, "rewiredInventory", bad) == "rewiredInventory must be 1", "rewiredInventory=" + bad + " accepted");
        Check(Refused(false, "identityProbe", "yes") == "identityProbe must be 1", "identityProbe=yes accepted");
        Check(Refused(true, "rewiredInventory", "1") == "rewiredInventory is only valid for recording" && Refused(true, "identityProbe", "1") == "identityProbe is only valid for recording",
            "a replay accepted a recording diagnostic");
        Check(Refused(true) == "" && !Parse(true).InventoryPending, "an ordinary replay (existing tapes) refused or dumped");
        // SessionTape's own wiring is Unity-bound, so it is read here as source: each request resets the diagnostics
        // before reading the request file, parses them with this object, and reaches the inventory only through it.
        string tape = null;
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null && tape == null; d = d.Parent)
        { var f = Path.Combine(d.FullName, "tools", "testing", "Recorder", "SessionTape.cs"); if (File.Exists(f)) tape = File.ReadAllText(f); }
        Check(tape != null, "tools/testing/Recorder/SessionTape.cs not found above the test binary");
        int arm = tape.IndexOf("internal static void TryArm(", StringComparison.Ordinal);
        int read = arm < 0 ? -1 : tape.IndexOf("File.Exists(RequestPath)", arm, StringComparison.Ordinal);
        int reset = arm < 0 ? -1 : tape.IndexOf("_diagnostics = RecordingDiagnostics.Off;", arm, StringComparison.Ordinal);
        int disarm = arm < 0 ? -1 : tape.IndexOf("IdentityProbe.Disarm();", arm, StringComparison.Ordinal);
        Check(arm > 0 && reset > arm && reset < read && disarm > arm && disarm < read, "SessionTape does not reset the diagnostics before reading each request");
        Check(System.Text.RegularExpressions.Regex.Matches(tape, @"RecordingDiagnostics\.Parse\(").Count == 1 && tape.Contains("_diagnostics = RecordingDiagnostics.Parse(request, replay);"),
            "SessionTape does not parse each request's diagnostics with RecordingDiagnostics");
        int gate = tape.IndexOf("if (_mode == Mode.Record && _diagnostics.InventoryPending)", StringComparison.Ordinal);
        var calls = System.Text.RegularExpressions.Regex.Matches(tape, @"RewiredInventory\.\w+\(");
        Check(gate > 0 && calls.Count == 1 && calls[0].Index > gate && calls[0].Index - gate < 600 && !tape.Contains("RewiredInventory.Maybe"),
            "the Rewired inventory is reachable outside the request's opt-in");
        Check(System.Text.RegularExpressions.Regex.Matches(tape, @"IdentityProbe\.Arm\(").Count == 1 && tape.Contains("if (_diagnostics.IdentityProbe) IdentityProbe.Arm("),
            "the identity probe is armed outside the request's opt-in");
    }
    public sealed class StubConsumer { internal bool Started { get; set; } }
    public static class StubShimInput { internal static StubConsumer Consumer; }
    public static class StubShimNoStarted { internal static object Consumer = new object(); }
    static void Readiness(CapturePolicy holding, CapturePolicy native, CapturePolicy shared, CapturePolicy synthetic)
    {
        Type Resolve(string name) => name == SharedReadiness.ShimInputType ? typeof(StubShimInput) : null;
        StubShimInput.Consumer = new StubConsumer();
        var hold = SharedReadiness.For(holding, Resolve);
        var nativeReady = SharedReadiness.For(native, Resolve);
        Check(hold != null && nativeReady != null && !hold() && !nativeReady(), "a waiting policy did not bind the shared session's readiness");
        StubShimInput.Consumer.Started = true;
        Check(hold() && nativeReady(), "the bound readiness does not follow the consumer's Started");
        Check(SharedReadiness.For(shared, Resolve) == null && SharedReadiness.For(synthetic, Resolve) == null, "a policy that does not wait bound the shared session");
        float held = -1;
        StubShimInput.Consumer.Started = false;
        Check(holding.Holds(hold(), 1, ref held) && (StubShimInput.Consumer.Started = true) && !holding.Holds(hold(), 2, ref held), "the bound readiness does not release the holding route");
        string Bind(Func<string, Type> resolve) { try { SharedReadiness.For(holding, resolve); return ""; } catch (Exception e) { return e.GetType().Name + ": " + e.Message; } }
        Check(Bind(_ => null).Contains("ShimInput"), "a missing ShimInput type bound");
        StubShimInput.Consumer = null;
        Check(Bind(Resolve).Contains("ShimInput.Consumer is null"), "a null consumer bound (the frame-0 NullReferenceException of 8989f63)");
        Check(Bind(n => n == SharedReadiness.ShimInputType ? typeof(StubShimNoStarted) : null).Contains("ShimAxisConsumer.Started"), "a consumer without Started bound");
        StubShimInput.Consumer = new StubConsumer();
    }
    // carInput=native: the stage script and the verdict over native.tape and car.tape (romi 31616).
    static void Native()
    {
        string Axis(float t, string action) => NativeEvidence.At(t).TryGetValue("R0.GetAxisRaw.s" + action, out var v) ? v : "0";
        // Each action alone and short, so the car stays near the start (06:52: a long throttle met Art's out-of-bounds resets).
        Check(Axis(1, "Throttle") == "0.8" && NativeEvidence.At(1).Count == 1 && Axis(2, "Brake") == "0.8" && NativeEvidence.At(2).Count == 1 &&
            Axis(3, "Handbrake") == "1" && NativeEvidence.At(3).Count == 1 && Axis(5, "Horizontal") == "0.8" && NativeEvidence.At(5).Count == 1 &&
            Axis(6, "Horizontal") == "-0.8" && NativeEvidence.At(6).Count == 1 && NativeEvidence.At(7).Count == 0 && NativeEvidence.At(11).Count == 0, "stage script changed");
        Check(Enumerable.Range(0, 120).Count(i => Axis(i / 10f, "Throttle") != "0") == 15, "throttle held longer than 1.5 s");
        Check(Enumerable.Range(0, 180).SelectMany(i => NativeEvidence.At(i / 10f).Values).All(v => Math.Abs(float.Parse(v, CultureInfo.InvariantCulture)) > 0.75f), "a scripted value is inside Art's largest deadzone");
        const int Stage = 7, Menu = 3;
        string R(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        // A clean ordinary session: Art returns the script (plus alignment on steering); the wheel at rest keeps its
        // shared steering when off centre (wheel != 0) and the mod otherwise leaves Art's result unchanged.
        List<string[]> Rows(float alignment = 0f, float wheel = 0f)
        {
            var rows = new List<string[]> { new[] { "1", Menu.ToString(), "1", "0", "0", "0", "0", "9", "9", "9", "9", "9", "1", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0" } };
            for (int i = 0; i < 18 * 60; i++)
            {
                float t = i / 60f; string s = Axis(t, "Horizontal"), th = Axis(t, "Throttle"), b = Axis(t, "Brake"), h = Axis(t, "Handbrake");
                string steer = R(float.Parse(s, CultureInfo.InvariantCulture) + alignment), final = wheel != 0f ? R(wheel + alignment) : steer;
                string start = th != "0" ? "1" : "0";
                rows.Add(new[] { (100 + i).ToString(), Stage.ToString(), "1", s, th, b, h, steer, th, b, h, "0", start, final, th, b, h, "0", start, "1", "1", "1", R(wheel), "0", "0", "0", R(alignment) });
            }
            return rows;
        }
        List<string> Car(List<string[]> rows)
        {
            var car = new List<string> { "step\tmarker\tsteer\tthrottle\tbrake\thandbrake", "0\t" + Stage + "\t0\t0\t1\t1" };
            foreach (var r in rows.Where(r => r[1] == Stage.ToString())) car.Add(r[0] + "\t" + Stage + "\t" + r[13] + "\t" + r[14] + "\t" + r[15] + "\t" + r[16]);
            return car;
        }
        string Verdict(List<string[]> rows, List<string> car, out bool ok) =>
            NativeEvidence.Evaluate(new[] { NativeEvidence.Header }.Concat(rows.Select(r => string.Join("\t", r))), car, m => m == Stage, out ok);
        var clean = Rows();
        string verdict = Verdict(clean, Car(clean), out bool passed);
        Check(passed && verdict.StartsWith("passed: 1080 stage frames") && verdict.Contains("the wheel at centre passed Art's steering on 180 scripted-steer frames") &&
            verdict.Contains("1 leading start-line ticks") && verdict.EndsWith("scripted Rewired native-path evidence only"), "clean native capture: " + verdict);
        var aligned = Rows(0.03f);
        Check(Verdict(aligned, Car(aligned), out passed) != null && passed, "steering alignment term refused");
        // Without force the wheel rests where it was left (0.1407 in the 02:11 Apply receipt): it keeps steering.
        var offCentre = Rows(0f, 0.1407f);
        Check(Verdict(offCentre, Car(offCentre), out passed).Contains("the wheel at rest off centre (shared steer 0.1407) held steering on 180 scripted-steer frames") && passed, "off-centre wheel refused");
        var offAligned = Rows(0.03f, 0.1407f);
        Check(Verdict(offAligned, Car(offAligned), out passed) != null && passed, "off-centre wheel with alignment refused");
        // Automatic reverse swaps throttle and brake in the car's fields (a 0.6 brake keeps the swapped throttle row unique).
        var reverse = Rows(); foreach (var x in reverse.Where(x => x[5] == "0.8")) { x[5] = "0.6"; x[9] = "0.6"; x[15] = "0.6"; }
        var swapped = Car(reverse); var f = swapped[2 + 30].Split('\t'); swapped[2 + 30] = string.Join("\t", f[0], f[1], f[2], f[4], f[3], f[5]);
        Check(Verdict(reverse, swapped, out passed) != null && passed, "automatic reverse swap refused");
        string Broken(Action<List<string[]>> change, float wheel = 0f)
        {
            var rows = Rows(0f, wheel); change(rows);
            string v = Verdict(rows, Car(rows), out bool ok);
            return ok ? "PASSED " + v : v;
        }
        int row = 1 + 60, steerRow = 1 + 300; // throttle; right steering
        Check(Broken(r => r[row][2] = "0").StartsWith("game unfocused at frame"), "unfocused frame accepted");
        Check(Broken(r => r[row][19] = "0").StartsWith("the shared composition did not run"), "frame without shared composition accepted");
        Check(Broken(r => r[row][20] = "0").StartsWith("Rewired isolation unverified"), "unisolated frame accepted");
        Check(Broken(r => r[row][21] = "0").StartsWith("shared steering not live"), "frame without live steering accepted");
        Check(Broken(r => r[row][22] = "0.004").StartsWith("shared steering moved from 0 to 0.004 (wheel not at rest)"), "moving wheel accepted");
        Check(Broken(r => r[row][22] = "0.2", 0.1407f).StartsWith("shared steering moved from 0.1407 to 0.2"), "moved off-centre wheel accepted");
        Check(Broken(r => r[steerRow][13] = r[steerRow][7], 0.1407f).StartsWith("steer 0.8 is not the ordinary composition of Art's 0.8 and the wheel's 0.1407"), "script leaked into wheel-owned steering");
        Check(Broken(r => r[row][23] = "0.01", 0.1407f).StartsWith("shared appliedThrottle 0.01 (wheel not at rest)"), "pressed throttle pedal accepted");
        Check(Broken(r => r[row][25] = "0.2").Contains("appliedHandbrake"), "shared handbrake accepted");
        Check(Broken(r => { r[row][14] = "0"; r[row][18] = "0"; }).StartsWith("the shared path changed Art's throttle: 0.8 -> 0"), "zeroed native throttle accepted");
        Check(Broken(r => r[steerRow][13] = "0.5").StartsWith("steer 0.5 is not the ordinary composition of Art's 0.8 and the wheel's 0"), "replaced steering accepted");
        Check(Broken(r => { r[steerRow][7] = "0"; r[steerRow][13] = "0"; }).StartsWith("Art's GetInput result differs from the script"), "deadzone-swallowed script accepted");
        Check(Broken(r => r.RemoveRange(1 + 299, r.Count - 1 - 299)).StartsWith("only 299 stage frames"), "short capture accepted");
        Check(Broken(r => { foreach (var x in r.Where(x => x[5] != "0" && x[1] == Stage.ToString())) { x[5] = "0"; x[9] = "0"; x[15] = "0"; } }).StartsWith("the script did not reach every action") , "script without brake accepted");
        Check(Broken(r => { foreach (var x in r.Where(x => x[3] != "0" && x[3].StartsWith("-"))) { x[3] = "0"; x[7] = "0"; x[13] = "0"; } }).Contains("left 0"), "script without left steering accepted");
        Check(Broken(r => r.RemoveAll(x => x[1] == Stage.ToString() && Enumerable.Range(3, 4).All(i => x[i] == "0"))).Contains("released 0"), "capture without the release accepted");
        var stale = Car(clean); stale[900] = "999\t" + Stage + "\t0.8\t0.8\t0.8\t0";
        Check(Verdict(clean, stale, out passed).StartsWith("car input at step") && !Verdict(clean, stale, out _).Contains("reset") && !passed, "car tick without a composed result accepted");
        // Art's out-of-bounds reset forces the car's handbrake over a composed result; it fails, and is named.
        var reset = Car(clean); reset[400] = "998\t" + Stage + "\t0\t0\t0\t1"; reset[401] = "999\t" + Stage + "\t0\t0.8\t0\t1";
        Check(Verdict(clean, reset, out passed).EndsWith("is no composed result (handbrake forced over a composed result: Art's out-of-bounds reset?)") && !passed, "forced handbrake accepted or not named");
        Check(Verdict(clean, new List<string> { "header", "0\t" + Stage + "\t0\t0\t1\t1" }, out passed) == "no car tick carried a composed result" && !passed, "car without composed ticks accepted");
        // Menu rows are not judged.
        Check(Broken(r => r[0][2] = "0") == "PASSED " + Verdict(clean, Car(clean), out _), "menu row judged");
        Check(NativeEvidence.Evaluate(new[] { "frame\tmarker" }, Car(clean), m => true, out passed) == "native.tape header differs" && !passed, "foreign header accepted");
    }
    static int Main(string[] args)
    {
        try
        {
            string root = Path.GetFullPath(args[0]);
            Check(!Directory.Exists(root), "use a fresh test evidence directory");
            string capture = Capture(root); Pipe(); Quit(Path.Combine(root, "quit-cases")); Policy(); Diagnostics(); Native(); assertions += SharedEvidenceTests.Run(Path.Combine(root, "shared-evidence"), (ok, why) => { if (!ok) throw new Exception(why); });
            if (args.Length == 3) Contract(args[1], args[2]);
            Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", assertions, syntheticCapture = capture })); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
