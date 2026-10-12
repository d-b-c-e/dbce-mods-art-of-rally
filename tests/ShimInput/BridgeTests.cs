using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using ArtOfSimRally.Mod.Shim;

internal static class BridgeTests
{
    private static int checks;
    private static void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
    internal sealed class Source : IPhysicalSnapshotSource
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
    internal sealed class Native : IShimBridge
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
        return checks;
    }
    internal static int Actual(string dll)
    {
        checks = 0;
        string folder = Path.Combine(Path.GetTempPath(), "art-copied-bridge-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string profile = "[Controls]\nSchema = 1\nProfile = Bridge\nRevision = r1\nTransmission = Sequential\nsteer = axis 0 dev={d71b8350-61b7-11f1-8001-444553540000} prod={0006346e-0000-0000-0000-504944564944} range=0..65535 rest=32768 travel=+1\n";
        var source = new Source(); var sink = new ShimAxisConsumer();
        var native = new ShimBridgeNative(dll, folder, System.Text.Encoding.UTF8.GetBytes(profile));
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
        return checks;
    }
}
