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
        internal int Descriptions, Reads, Raw = 32768, Result = 1;
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
            bytes[50] = 1; BitConverter.GetBytes(Raw).CopyTo(bytes, 52); bytes[84] = 0x80;
            for (int i = 212; i < 228; i++) bytes[i] = 0xff;
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
        ActualOne(dll, false); ActualOne(dll, true); ActualActions(dll); return checks;
    }
    // Two owned devices with independently scripted buttons and hats (R12 slot 0, DS-8X slot 1).
    private sealed class DeviceSource : IPhysicalSnapshotSource, IPhysicalSnapshotPathSource
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
            // axis_mask/range_mask: axis 0 (0..65535); pov_mask: hat 0; button_mask: all 128 buttons present.
            bytes[48] = bytes[52] = 1; bytes[56] = 1; for (int i = 64; i < 80; i++) bytes[i] = 0xff;
            BitConverter.GetBytes(65535).CopyTo(bytes, 112); return 1;
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
