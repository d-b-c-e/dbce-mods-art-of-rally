using System.Linq;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ArtOfSimRally.Mod.Shim;

internal static class BridgeTests
{
    private static int checks;
    private static void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
    internal class Source : IPhysicalSnapshotSource
    {
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceCounter(out long value);
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceFrequency(out long value);
        internal int Descriptions, Reads, Raw = 32768, Result = 1, Pressed = 0;
        // Optional extra physical state: other axes, more held buttons, hat 0 (centred when null).
        internal Func<int, int> Axis { get; set; }
        internal readonly System.Collections.Generic.HashSet<int> Buttons = new System.Collections.Generic.HashSet<int>();
        internal uint? Hat0 { get; set; }
        internal ulong Generation = 7, Sequence;
        internal byte[] Last;
        public int Describe(int slot, byte[] bytes)
        {
            Descriptions++; BitConverter.GetBytes(144).CopyTo(bytes, 0); bytes[4] = 1; BitConverter.GetBytes(Generation).CopyTo(bytes, 8);
            new Guid("d71b8350-61b7-11f1-8001-444553540000").ToByteArray().CopyTo(bytes, 16);
            new Guid("0006346e-0000-0000-0000-504944564944").ToByteArray().CopyTo(bytes, 32);
            bytes[48] = bytes[52] = 1; BitConverter.GetBytes(65535).CopyTo(bytes, 112); return 1;
        }
        public int Read(int slot, byte[] bytes)
        {
            Reads++; BitConverter.GetBytes(232).CopyTo(bytes, 0); bytes[4] = 1; BitConverter.GetBytes(Generation).CopyTo(bytes, 8);
            BitConverter.GetBytes(++Sequence).CopyTo(bytes, 16);
            long tick, frequency;
            if (!QueryPerformanceCounter(out tick) || !QueryPerformanceFrequency(out frequency)) throw new Exception("QPC unavailable");
            long us = tick / frequency * 1000000 + tick % frequency * 1000000 / frequency;
            BitConverter.GetBytes(us).CopyTo(bytes, 24); BitConverter.GetBytes(us).CopyTo(bytes, 32);
            bytes[50] = 1; BitConverter.GetBytes(Raw).CopyTo(bytes, 52); if (Pressed >= 0) bytes[84 + Pressed] = 0x80;
            if (Axis != null) for (int i = 1; i < 8; i++) BitConverter.GetBytes(Axis(i)).CopyTo(bytes, 52 + i * 4);
            foreach (int b in Buttons) bytes[84 + b] = 0x80;
            for (int i = 212; i < 228; i++) bytes[i] = 0xff;
            if (Hat0.HasValue) BitConverter.GetBytes(Hat0.Value).CopyTo(bytes, 212);
            Last = (byte[])bytes.Clone(); return Result;
        }
    }
    internal class Native : IShimBridge
    {
        internal int Ticks, Stops, Descriptors; internal byte[] Raw; internal bool Accept = true;
        public bool Devices(byte[] bytes, int count) { Descriptors++; return Accept; }
        public bool Tick(byte[] samples, int count, byte[] frame)
        {
            Ticks++; Raw = (byte[])samples.Clone(); if (!Accept) return false;
            BitConverter.GetBytes(296).CopyTo(frame, 0); frame[4] = 2; BitConverter.GetBytes((ulong)Ticks).CopyTo(frame, 8);
            BitConverter.GetBytes((long)Ticks).CopyTo(frame, 16);
            for (int i = 0; i < 31; i++) frame[52 + i * 8] = (byte)(i < 5 ? ShimStatus.Live : ShimStatus.Unsupported);
            return true;
        }
        public void Stop() { Stops++; }
    }
    private sealed class PathSource : Source, IPhysicalSnapshotPathSource
    {
        internal bool PathValid = true;
        public bool TryGetPath(int slot, ulong generation, out string path)
        { path = @"\\?\hid#owned-source"; return generation == Generation && PathValid; }
    }
    private sealed class PathNative : Native, IShimPathBridge
    {
        internal string[] Paths;
        public bool DevicesWithPaths(byte[] bytes, int count, string[] paths)
        { Paths = (string[])paths.Clone(); return Devices(bytes, count); }
    }
    internal static int Fake()
    {
        checks = 0; var source = new Source(); var native = new Native(); var sink = new ShimAxisConsumer();
        var bridge = new ShimBridge(source, native, sink, new[] { 2 }); int copies = 0;
        Check(!sink.Started && source.Reads == 0 && source.Descriptions == 1, "prepare must not poll or activate sink");
        Check(bridge.ReadFrame(1, (slot, bytes) => { Check(slot == 2 && bytes[84] == 0x80, "original button byte"); copies++; }), "first frame");
        Check(source.Reads == 1 && native.Ticks == 1 && copies == 1 && sink.Started, "one read, tick and cache update");
        Check(Convert.ToBase64String(source.Last) == Convert.ToBase64String(native.Raw), "bridge rewrote physical evidence");
        Check(bridge.ReadFrame(1, (slot, bytes) => copies++) && source.Reads == 1 && copies == 1, "same game frame polled twice");
        source.Generation++;
        Check(!bridge.ReadFrame(2, (slot, bytes) => copies++) && bridge.Stopped && native.Ticks == 1 && copies == 1, "generation mismatch reached sink/cache");
        Check(native.Stops == 1 && sink.Terminal && !bridge.ReadFrame(3, (slot, bytes) => copies++) && source.Reads == 2, "terminal path resumed");
        source = new Source(); native = new Native(); bridge = new ShimBridge(source, native, new ShimAxisConsumer(), new[] { 0 }); source.Result = -3;
        Check(!bridge.ReadFrame(1, (slot, bytes) => copies++) && native.Ticks == 0 && native.Stops == 1, "legacy injection refusal ignored");
        source = new Source(); native = new Native { Accept = false }; bool refused = false;
        try { new ShimBridge(source, native, new ShimAxisConsumer(), new[] { 0 }); } catch (InvalidOperationException) { refused = true; }
        Check(refused && source.Reads == 0 && native.Stops == 1, "metadata refusal acquired input");
        var pathSource = new PathSource(); var pathNative = new PathNative();
        bridge = new ShimBridge(pathSource, pathNative, new ShimAxisConsumer(), new[] { 0 });
        Check(pathNative.Paths.Length == 1 && pathNative.Paths[0] == @"\\?\hid#owned-source" && pathSource.Reads == 0, "path and metadata did not publish together without polling"); bridge.Stop("fixture");
        pathSource = new PathSource { PathValid = false }; pathNative = new PathNative(); refused = false;
        try { new ShimBridge(pathSource, pathNative, new ShimAxisConsumer(), new[] { 0 }); } catch (InvalidOperationException) { refused = true; }
        Check(refused && pathNative.Paths == null && pathSource.Reads == 0 && pathNative.Stops == 1, "unqualified path published metadata or polled");
        native = new Native(); refused = false;
        try { new ShimBridge(new PathSource(), native, new ShimAxisConsumer(), new[] { 0 }); } catch (InvalidOperationException) { refused = true; }
        Check(refused && native.Descriptors == 0 && native.Stops == 1, "missing path API fell back to unqualified metadata");
        return checks;
    }
    internal static int Actual(string dll)
    {
        checks = 0;
        ActualOne(dll, false); ActualOne(dll, true); ActualActions(dll); LatchedBridgeGate(dll); return checks;
    }
    // Run only against a shim whose copied-provider bridge admits latched control sessions.
    internal static int ActualLatchedSession(string dll) { checks = 0; ActualLatched(dll); ActualTrial(dll); return checks; }
    // Slice B against the real Core: the probe's LatchedTrial drives action-level navDown/navUp through
    // atomically replaced batches; a fake menu moves its selection on the delivered values.
    private static void ActualTrial(string dll)
    {
        string folder = Path.Combine(Path.GetTempPath(), "art-copied-trial-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string session = Path.Combine(folder, "shim-session.txt"), batch = Path.Combine(folder, "shim-batch.txt"), nonce = Guid.NewGuid().ToString("N");
        ProbeRequest.Replace(session, "nonce=" + nonce + "\nexpires=" + (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600) + "\n");
        string r12 = " dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944}";
        string profile = "[Controls]\nSchema = 1\nProfile = Trial\nRevision = r1\nTransmission = Sequential\n" +
            "steer = axis 0" + r12 + " range=0..65535 rest=32768 travel=+1\nconfirm = button 31" + r12 + "\nnavUp = hat 0 0" + r12 + "\nnavDown = hat 0 18000" + r12 + "\n";
        var native = new ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(profile),
            new ShimBridgeNative.LatchedSession { Nonce = nonce, SessionFile = session, BatchFile = batch, FencesVerified = true, ForceAbsent = true });
        var sink = new ShimAxisConsumer { AcceptLatched = true }; sink.DeliverOnly(LatchedTrial.NavUp, LatchedTrial.NavDown);
        var source = new DeviceSource(); source.Buttons[0][31] = true; // a physical confirm held throughout stays masked
        var bridge = new ShimBridge(source, native, sink, new[] { 0 }); int frame = 0; string selected = "Career#11"; bool confirmed = false;
        var trial = new LatchedTrial(sink, () => selected, text => ProbeRequest.Replace(batch, text), nonce);
        try
        {
            while (frame < 4000 && trial.Current != LatchedTrial.Step.Passed && trial.Current != LatchedTrial.Step.Failed)
            {
                Check(bridge.ReadFrame(++frame, (slot, bytes) => { }), "trial frame: " + bridge.Failure);
                if (sink.Value(LatchedTrial.NavDown, frame) == 1f) selected = "FreeRoam#12";
                if (sink.Value(LatchedTrial.NavUp, frame) == 1f) selected = "Career#11";
                confirmed |= sink.Live(9, frame) || sink.Value(9, frame) != 0f;
                trial.Tick(frame); Thread.Sleep(5);
            }
            Check(trial.Current == LatchedTrial.Step.Passed && trial.AfterDown == "FreeRoam#12" && trial.AfterUp == "Career#11", "actual trial: " + trial.Current + " " + trial.Failure);
            Check(!confirmed && sink.OriginalFrame.Run == LatchedTrial.Completed, "masked confirm delivered or session not completed");
        }
        finally { bridge.Stop("trial qualification complete"); }
        Check(File.ReadAllText(Path.Combine(folder, "host-shutdown.json")).Contains("\"state\":\"resolved\""), "trial bridge stop did not resolve");
    }
    // The ordinary pin, 574bb88 (7DDC9621), refuses every open flag on the copied-provider bridge;
    // f38b855 (0D10F913, reviewed by romi 28531) admits a fenced latched open. Any other build is
    // unreviewed here, so admitting latched control stays a reviewed change, not a surprise.
    internal const string OrdinaryShim = "7DDC962132819EDAE76EF84CE6509F1936B8C38177A36D498B8ABBCFDCFFBCAF";
    internal const string LatchedShim = "0D10F913C998C97D7DDF3A8F5F470D010092C3681152059DC88D7E0A7BB0386B";
    private static void LatchedBridgeGate(string dll)
    {
        string hash;
        using (var sha = System.Security.Cryptography.SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(dll))).Replace("-", "");
        Check(hash == OrdinaryShim || hash == LatchedShim, "unreviewed dbce_shim.dll " + hash);
        string folder = Path.Combine(Path.GetTempPath(), "art-copied-latch-gate-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string profile = "[Controls]\nSchema = 1\nProfile = Gate\nRevision = r1\nTransmission = Sequential\nsteer = axis 0 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=32768 travel=+1\n";
        string why = ""; ShimBridgeNative opened = null;
        try
        {
            opened = new ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(profile), new ShimBridgeNative.LatchedSession {
                Nonce = Guid.NewGuid().ToString("N"), SessionFile = Path.Combine(folder, "s.txt"), BatchFile = Path.Combine(folder, "b.txt"), FencesVerified = true, ForceAbsent = true });
        }
        catch (InvalidOperationException e) { why = e.Message; }
        finally { opened?.Stop(); }
        if (hash == OrdinaryShim) Check(opened == null && why.Contains("bridge control/capture provenance is not qualified"), "ordinary pin admitted a latched open or refused differently: " + why);
        else Check(opened != null && opened.Latched && why == "", "reviewed latched build refused a fenced latched open: " + why);
    }
    // The real Core latched control session over the copied provider: admission, an action-level
    // injection resolved through the applied profile with Injected provenance, native suppression,
    // the probe's delivery mask, and end → Completed.
    private static void ActualLatched(string dll)
    {
        string folder = Path.Combine(Path.GetTempPath(), "art-copied-latched-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string session = Path.Combine(folder, "shim-session.txt"), batch = Path.Combine(folder, "shim-batch.txt"), nonce = Guid.NewGuid().ToString("N");
        File.WriteAllText(session, "nonce=" + nonce + "\nexpires=" + (DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600) + "\n");
        string r12 = " dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944}";
        string profile = "[Controls]\nSchema = 1\nProfile = Trial\nRevision = r1\nTransmission = Sequential\n" +
            "steer = axis 0" + r12 + " range=0..65535 rest=32768 travel=+1\nconfirm = button 31" + r12 + "\nnavUp = hat 0 0" + r12 + "\nnavDown = hat 0 18000" + r12 + "\n";
        var native = new ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(profile),
            new ShimBridgeNative.LatchedSession { Nonce = nonce, SessionFile = session, BatchFile = batch, FencesVerified = true, ForceAbsent = true });
        Check(native.Latched, "latched open not recorded");
        var sink = new ShimAxisConsumer { AcceptLatched = true }; sink.DeliverOnly(13, 14);
        var source = new DeviceSource(); source.Buttons[0][31] = true; // a physical confirm held throughout
        var bridge = new ShimBridge(source, native, sink, new[] { 0 }); int frame = 0;
        void Step() { Check(bridge.ReadFrame(++frame, (slot, bytes) => { }), "latched frame: " + bridge.Failure); Thread.Sleep(5); }
        bool Until(Func<bool> condition) { for (int i = 0; i < 600 && !condition(); i++) Step(); return condition(); }
        try
        {
            Check(Until(() => sink.Started && sink.OriginalFrame.Run == 2), "latched session never admitted (Waiting): run " + (sink.Started ? sink.OriginalFrame.Run.ToString() : "-"));
            Check(sink.LatchedDelivery(frame) && sink.OriginalFrame.Latched && sink.OriginalFrame.SuppressNative, "latched frame flags");
            Check(!sink.Live(9, frame) && sink.Value(9, frame) == 0, "masked physical confirm delivered");
            ProbeRequest.Replace(batch, "nonce=" + nonce + "\nbatch=1\ninject action navDown 1 ms=300\n");
            Check(Until(() => sink.Injected(14, frame)), "action-level navDown never injected: run " + sink.OriginalFrame.Run + " navDown " + sink.OriginalFrame[14].Status);
            Check(sink.OriginalFrame.Run == 3 && sink.Value(14, frame) == 1f && !sink.Live(13, frame) || sink.Value(13, frame) == 0f, "injected navDown not Running/Live");
            Check(!sink.Live(9, frame), "masked confirm delivered while injecting");
            var game = new DrivingValues { Steer = .9f, Throttle = .95f }; sink.Compose(frame, true, 0, ref game);
            Check(game.Throttle == 0 && Math.Abs(game.Steer) < .00001f, "latched session kept native/keyboard driving input");
            Check(Until(() => sink.Started && !sink.Injected(14, frame) && sink.Value(14, frame) == 0f), "injected navDown never released");
            ProbeRequest.Replace(batch, "nonce=" + nonce + "\nbatch=2\nend\n");
            Check(Until(() => sink.OriginalFrame.Run == 4), "end batch did not complete the session: run " + sink.OriginalFrame.Run);
        }
        finally { bridge.Stop("latched qualification complete"); }
        Check(File.ReadAllText(Path.Combine(folder, "host-shutdown.json")).Contains("\"state\":\"resolved\""), "latched bridge stop did not resolve");
        // An ordinary consumer refuses latched frames, and a latched consumer refuses ordinary ones.
        var ordinary = new ShimAxisConsumer(); string why;
        var latchedBytes = new byte[ShimFrame.Size]; BitConverter.GetBytes(ShimFrame.Size).CopyTo(latchedBytes, 0); latchedBytes[4] = 2; latchedBytes[8] = 1; latchedBytes[40] = 2; latchedBytes[41] = 1; latchedBytes[42] = 1;
        Check(!ordinary.Publish(latchedBytes, 1, out why) && ordinary.Terminal, "ordinary consumer accepted a latched frame");
        var strict = new ShimAxisConsumer { AcceptLatched = true }; var plainBytes = (byte[])latchedBytes.Clone(); plainBytes[40] = plainBytes[41] = plainBytes[42] = 0;
        Check(!strict.Publish(plainBytes, 1, out why) && strict.Terminal, "latched consumer accepted an ordinary frame");
        bool fixedAtOpen = false; try { strict.AcceptLatched = false; } catch (InvalidOperationException) { fixedAtOpen = true; }
        Check(fixedAtOpen, "latched acceptance changed after open");
    }
    // Two owned devices with independently scripted buttons and hats (R12 slot 0, DS-8X slot 1).
    // The owner profile's driving objects (controls.ini 5206EDFB shapes).
    internal const string DriveProfile = "[Controls]\nSchema = 1\nProfile = Drive\nRevision = r1\nTransmission = Sequential\n" +
        "steer = axis 0 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=33265 travel=-1 name=\"MOZA R12 Base\"\n" +
        "throttle = axis 2 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=0 travel=+1\n" +
        "brake = axis 5 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=1743 travel=+1\n" +
        "handbrake = axis 7 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=0 travel=+1\n" +
        "shiftUp = button 9 dev={d9b1d890-6f3f-11f1-8002-444553540000} prod={05310483-0000-0000-0000-504944564944}\n" +
        "shiftDown = button 10 dev={d9b1d890-6f3f-11f1-8002-444553540000} prod={05310483-0000-0000-0000-504944564944}\n";
    internal sealed class DeviceSource : IPhysicalSnapshotSource, IPhysicalSnapshotPathSource
    {
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceCounter(out long value);
        [DllImport("kernel32.dll")] private static extern bool QueryPerformanceFrequency(out long value);
        internal static readonly Guid[] Ids = { new Guid("d71b8350-61b7-11f1-8001-444553540000"), new Guid("d9b1d890-6f3f-11f1-8002-444553540000") };
        internal static readonly Guid[] Products = { new Guid("0006346e-0000-0000-0000-504944564944"), new Guid("05310483-0000-0000-0000-504944564944") };
        internal readonly bool[][] Buttons = { new bool[128], new bool[128] };
        internal readonly uint[] Hat0 = { uint.MaxValue, uint.MaxValue };
        internal int Reads; private ulong sequence;
        public int Describe(int slot, byte[] bytes)
        {
            BitConverter.GetBytes(144).CopyTo(bytes, 0); bytes[4] = 1; BitConverter.GetBytes((ulong)(7 + slot)).CopyTo(bytes, 8);
            Ids[slot].ToByteArray().CopyTo(bytes, 16); Products[slot].ToByteArray().CopyTo(bytes, 32);
            // axis_mask/range_mask: all eight axes (0..65535); pov_mask: hat 0; button_mask: all 128 buttons present.
            bytes[48] = bytes[52] = 0xff; bytes[56] = 1; for (int i = 64; i < 80; i++) bytes[i] = 0xff;
            for (int axis = 0; axis < 8; axis++) BitConverter.GetBytes(65535).CopyTo(bytes, 112 + axis * 4);
            return 1;
        }
        public bool TryGetPath(int slot, ulong generation, out string path) { path = @"\\?\hid#owned-" + slot; return generation == (ulong)(7 + slot); }
        public int Read(int slot, byte[] bytes)
        {
            Reads++; BitConverter.GetBytes(232).CopyTo(bytes, 0); bytes[4] = 1; BitConverter.GetBytes((ulong)(7 + slot)).CopyTo(bytes, 8);
            BitConverter.GetBytes(++sequence).CopyTo(bytes, 16);
            long tick, frequency;
            if (!QueryPerformanceCounter(out tick) || !QueryPerformanceFrequency(out frequency)) throw new Exception("QPC unavailable");
            long us = tick / frequency * 1000000 + tick % frequency * 1000000 / frequency;
            BitConverter.GetBytes(us).CopyTo(bytes, 24); BitConverter.GetBytes(us).CopyTo(bytes, 32);
            bytes[50] = 1; BitConverter.GetBytes(32768).CopyTo(bytes, 52);
            for (int b = 0; b < 128; b++) bytes[84 + b] = (byte)(Buttons[slot][b] ? 0x80 : 0);
            BitConverter.GetBytes(Hat0[slot]).CopyTo(bytes, 212);
            for (int i = 216; i < 228; i++) bytes[i] = 0xff;
            return 1;
        }
    }
    // The actual Core with the pinned owner profile's action shapes: every supported action resolves,
    // the unsupported ones are reported Unsupported, and both owning devices are bound.
    private static void ActualActions(string dll)
    {
        string folder = Path.Combine(Path.GetTempPath(), "art-copied-actions-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string r12 = " dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944}";
        string ds8x = " dev={d9b1d890-6f3f-11f1-8002-444553540000} prod={05310483-0000-0000-0000-504944564944}";
        string profile = "[Controls]\nSchema = 1\nProfile = Actions\nRevision = r1\nTransmission = Sequential\n" +
            "steer = axis 0" + r12 + " range=0..65535 rest=32768 travel=+1\n" +
            "confirm = button 31" + r12 + "\nnavDown = hat 0 18000" + r12 + "\nselect = button 5" + r12 + "\n" +
            "shiftUp = button 9" + ds8x + "\nshiftDown = button 10" + ds8x + "\n";
        var source = new DeviceSource(); var sink = new ShimAxisConsumer();
        var native = new ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(profile));
        var ids = new System.Collections.Generic.HashSet<Guid>(native.BoundInstances());
        Check(ids.SetEquals(DeviceSource.Ids), "Core did not bind both owning devices");
        Check(native.Transmission() == "Sequential" && native.ProfileValue("profile") == "Actions", "Core profile transmission/identity");
        var bridge = new ShimBridge(source, native, sink, new[] { 0, 1 }); int frame = 0;
        bool Step() { Check(bridge.ReadFrame(++frame, (slot, bytes) => { }), "actual action frame: " + bridge.Failure); Thread.Sleep(5); return true; }
        bool AllLive() => sink.Started && sink.Live(9, frame) && sink.Live(14, frame) && sink.Live(5, frame) && sink.Live(6, frame);
        try
        {
            for (int i = 0; i < 400 && Step() && !AllLive(); i++) { }
            Check(AllLive(), "actual confirm/navDown/shiftUp/shiftDown never became Live while released: " +
                (sink.Started ? string.Join(",", new[] { 9, 14, 5, 6 }.Select(a => ShimFrame.Names[a] + "=" + sink.OriginalFrame[a].Status)) : "not started"));
            Check(sink.Value(9, frame) == 0 && sink.Value(14, frame) == 0, "released actions not neutral");
            Check(!sink.Owns(12) && sink.OriginalFrame[12].Status == ShimStatus.Unsupported, "bound select not reported Unsupported");
            source.Buttons[0][31] = true; source.Hat0[0] = 18000; Step();
            Check(sink.Value(9, frame) == 1f && (sink.OriginalFrame.Down & (1UL << 9)) != 0, "actual confirm press");
            Check(sink.Value(14, frame) == 1f, "actual hat navDown");
            source.Buttons[0][31] = false; source.Hat0[0] = uint.MaxValue; Step();
            Check(sink.Value(9, frame) == 0 && sink.Value(14, frame) == 0, "actual release");
            source.Buttons[1][9] = true; Step(); source.Buttons[1][9] = false; Step();
            Check(sink.TakeShift() == 1 && sink.TakeShift() == 0, "actual DS-8X shiftUp did not queue exactly one step");
            source.Buttons[1][10] = true; Step(); source.Buttons[1][10] = false; Step();
            Check(sink.TakeShift() == -1, "actual DS-8X shiftDown");
        }
        finally { bridge.Stop("action qualification complete"); }
        Check(File.ReadAllText(Path.Combine(folder, "host-shutdown.json")).Contains("\"state\":\"resolved\""), "actual action bridge stop did not resolve");
    }
    private static void ActualOne(string dll, bool withPath)
    {
        string folder = Path.Combine(Path.GetTempPath(), "art-copied-bridge-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string profile = "[Controls]\nSchema = 1\nProfile = Bridge\nRevision = r1\nTransmission = Sequential\nsteer = axis 0 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=32768 travel=+1\n";
        Source source = withPath ? new PathSource() : new Source(); var sink = new ShimAxisConsumer();
        var native = new ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(profile));
        var ids = native.BoundInstances();
        Check(ids.Length == 1 && ids[0] == new Guid("d71b8350-61b7-11f1-8001-444553540000"), "Core device-report identity differs from profile");
        Check(native.BoundProducts()[ids[0]] == new Guid("0006346e-0000-0000-0000-504944564944"), "Core profile product lost");
        var bridge = new ShimBridge(source, native, sink, new[] { 0 }); int frame = 0, copies = 0;
        try
        {
            for (; frame < 400;)
            {
                Check(bridge.ReadFrame(++frame, (slot, bytes) => copies++), "actual bridge frame: " + bridge.Failure);
                if (sink.OriginalFrame[0].Status == ShimStatus.Live) break;
                Thread.Sleep(5);
            }
            Check(sink.OriginalFrame[0].Status == ShimStatus.Live, "actual copied provider never became Live");
            source.Raw = 65535; Check(bridge.ReadFrame(++frame, (slot, bytes) => copies++), "endpoint frame");
            var game = new DrivingValues(); sink.Compose(frame, true, .1f, ref game);
            Check(Math.Abs(game.Steer - 1.1f) < .00001f && copies == source.Reads, "actual Core-to-Art consumer endpoint");
            Check(File.Exists(Path.Combine(folder, "host-shutdown.json")), "host lifecycle report missing");
        }
        finally { bridge.Stop("fake-source qualification complete"); }
        Check(File.ReadAllText(Path.Combine(folder, "host-shutdown.json")).Contains("\"state\":\"resolved\""), "actual bridge stop did not resolve");
    }
}
