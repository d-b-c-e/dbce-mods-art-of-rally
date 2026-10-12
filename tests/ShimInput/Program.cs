using System;
using System.Runtime.InteropServices;
using System.Threading;
using ArtOfSimRally.Mod.Shim;

internal static class Program
{
    private static int assertions;
    private static void Check(bool value, string reason) { assertions++; if (!value) throw new Exception(reason); }
    private static void Same(float value, float expected, string reason) => Check(Math.Abs(value - expected) < 0.000001f, reason);

    // Independent C layout fixture mirrors the pinned native header, not the
    // decoder's byte offsets. Marshal writes the actual packed object to bytes.
    [StructLayout(LayoutKind.Sequential)] private struct ActionWire
    { public float value; public byte status, injected; public ushort reserved; }
    [StructLayout(LayoutKind.Sequential)] private struct FrameWire
    {
        public uint size, abi; public ulong seq; public long tick; public ulong down, up;
        public byte run, latched, suppress, reserved; public uint samples;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 31)] public ActionWire[] actions;
    }
    private static byte[] Frame(ulong seq = 1, ShimStatus status = ShimStatus.Live, float steer = -.4f)
    {
        var frame = new FrameWire { size = 296, abi = 2, seq = seq, tick = (long)seq * 1000,
            down = 1UL << 30, up = 1UL << 5, samples = 3, actions = new ActionWire[31] };
        float[] values = { steer, .6f, .3f, .8f, .45f };
        for (int i = 0; i < frame.actions.Length; i++)
            frame.actions[i] = new ActionWire { value = i < 5 ? values[i] : 0,
                status = (byte)(i < 5 ? status : ShimStatus.Unsupported), injected = (byte)(i == 4 ? 1 : 0) };
        var memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(FrameWire)));
        try { Marshal.StructureToPtr(frame, memory, false); var bytes = new byte[296]; Marshal.Copy(memory, bytes, 0, bytes.Length); return bytes; }
        finally { Marshal.FreeHGlobal(memory); }
    }
    private static DrivingValues Keyboard() => new DrivingValues {
        Steer = .9f, Throttle = .95f, Brake = .1f, Clutch = .1f, Handbrake = .2f, StartEngine = true
    };
    private static void Same(DrivingValues a, DrivingValues b, string reason)
    {
        Same(a.Steer, b.Steer, reason + " steer"); Same(a.Throttle, b.Throttle, reason + " throttle");
        Same(a.Brake, b.Brake, reason + " brake"); Same(a.Clutch, b.Clutch, reason + " clutch");
        Same(a.Handbrake, b.Handbrake, reason + " handbrake"); Check(a.StartEngine == b.StartEngine, reason + " start");
    }
    private static void Decode()
    {
        Check(Marshal.SizeOf(typeof(FrameWire)) == 296, "native frame size");
        Check(Marshal.OffsetOf(typeof(FrameWire), "actions").ToInt32() == 48, "native actions offset");
        ShimFrame frame; string why;
        byte[] bytes = Frame();
        Check(ShimFrame.TryDecode(bytes, out frame, out why), why);
        Check(frame.Sequence == 1 && frame.TickMicroseconds == 1000 && frame.Down == 1UL << 30 && frame.Up == 1UL << 5 && frame.SampleCount == 3, "source fields lost");
        Same(frame[0].Value, -.4f, "steer wire value"); Check(frame[4].Injected, "injection provenance lost");
        bytes[48] = 0; bytes[49] = 0; bytes[50] = 0; bytes[51] = 0;
        Same(frame[0].Value, -.4f, "source buffer mutation changed accepted frame");
        Check(!ShimFrame.TryDecode(null, out frame, out why), "null frame accepted");
        Check(!ShimFrame.TryDecode(new byte[297], out frame, out why), "oversize accepted");
        foreach (int offset in new[] { 0, 4, 40, 41, 42, 43, 52, 53, 54, 55 })
        { bytes = Frame(); bytes[offset] = 255; Check(!ShimFrame.TryDecode(bytes, out frame, out why), "bad field accepted " + offset); }
        bytes = Frame(); bytes[31] = 128; Check(!ShimFrame.TryDecode(bytes, out frame, out why), "unknown edge accepted");
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1.01f, 1.01f })
        { bytes = Frame(1, ShimStatus.Live, invalid); Check(!ShimFrame.TryDecode(bytes, out frame, out why), "invalid steer accepted"); }
        bytes = Frame(); BitConverter.GetBytes(-.1f).CopyTo(bytes, 56); Check(!ShimFrame.TryDecode(bytes, out frame, out why), "negative pedal accepted");
    }
    private static void Consumption()
    {
        var sink = new ShimAxisConsumer(); var keyboard = Keyboard(); var game = keyboard; string why;
        Check(!sink.Compose(10, false, .05f, ref game), "dormant consumer took ownership"); Same(game, keyboard, "dormant passthrough");
        Check(sink.Publish(Frame(), 10, out why), why);
        Check(sink.Compose(10, true, .05f, ref game), "no consumption");
        Same(game.Steer, -.35f, "alignment lost"); Same(game.Throttle, .95f, "keyboard throttle lost");
        Same(game.Brake, .3f, "analog brake lost"); Same(game.Clutch, .8f, "analog clutch lost"); Same(game.Handbrake, .45f, "analog handbrake thresholded");
        game = keyboard; sink.Compose(10, false, 0, ref game); Same(game.Throttle, .6f, "unisolated wheel mixed");
        Same(game.Steer, -.4f, "unclean steer"); Check(game.StartEngine, "throttle did not start engine");
        Check(sink.Publish(Frame(2, ShimStatus.Live, 0), 11, out why), why);
        game = keyboard; sink.Compose(11, true, .05f, ref game); Same(game.Steer, .9f, "centre lost keyboard steering");
        game = default(DrivingValues); sink.Compose(11, true, .05f, ref game); Same(game.Steer, .05f, "centred wheel lost alignment");
        game = keyboard; sink.Compose(12, true, .05f, ref game); Same(game, keyboard, "stale input lost independent keyboard");
        game = keyboard; sink.Compose(12, false, .05f, ref game); Same(game, default(DrivingValues), "stale input leaked wheel");
        var original = sink.OriginalFrame;
        sink.Stop(); game = keyboard; sink.Compose(11, true, 0, ref game); Same(game, keyboard, "stop lost keyboard");
        Check(!sink.Publish(Frame(3), 12, out why), "stopped sink resumed");
        Check(object.ReferenceEquals(original, sink.OriginalFrame), "stop rewrote original capture frame");
    }
    private static void StatusesAndFailures()
    {
        string why;
        foreach (ShimStatus status in Enum.GetValues(typeof(ShimStatus)))
        {
            var sink = new ShimAxisConsumer(); var game = Keyboard();
            Check(sink.Publish(Frame(1, status), 1, out why), why);
            sink.Compose(1, true, 0, ref game);
            if (status != ShimStatus.Live) Same(game, Keyboard(), "nonlive action affected keyboard " + status);
            game = Keyboard(); sink.Compose(1, false, 0, ref game);
            if (status != ShimStatus.Live) Same(game, default(DrivingValues), "nonlive action leaked native " + status);
        }
        foreach (int kind in new[] { 0, 1, 2, 3, 4, 5, 6 })
        {
            var sink = new ShimAxisConsumer(); Check(sink.Publish(Frame(), 1, out why), why);
            byte[] bad = Frame(2);
            if (kind == 0) bad[4] = 3;
            if (kind == 1) bad = Frame(1);
            if (kind == 2) bad[52] = (byte)ShimStatus.Unbound;
            if (kind == 3) bad[41] = 1;
            if (kind == 4) bad[42] = 1;
            if (kind == 5) bad[40] = 3;
            if (kind == 6) bad[92] = (byte)ShimStatus.Live;
            Check(!sink.Publish(bad, 2, out why) && sink.Terminal, "bad subsequent frame accepted " + kind);
            var game = Keyboard(); sink.Compose(1, false, .5f, ref game); Same(game, default(DrivingValues), "fault replayed force/input " + kind);
            game = Keyboard(); sink.Compose(2, true, 0, ref game); Same(game, Keyboard(), "fault lost keyboard " + kind);
            Check(!sink.Publish(Frame(3), 3, out why), "terminal error resumed " + kind);
        }
        var refused = new ShimAxisConsumer(); Check(!refused.Publish(new byte[1], 1, out why) && !refused.Started, "refusal claimed ownership");
        var values = Keyboard(); Check(!refused.Compose(1, false, 0, ref values), "refused session changed input"); Same(values, Keyboard(), "refusal fallback");
        var wrongThread = new ShimAxisConsumer(); bool accepted = true;
        var thread = new Thread(() => { string reason; accepted = wrongThread.Publish(Frame(), 1, out reason); }); thread.Start(); thread.Join();
        Check(!accepted && !wrongThread.Started, "wrong thread accepted");
        var duplicate = new ShimAxisConsumer(); Check(duplicate.Publish(Frame(), 5, out why), why);
        Check(!duplicate.Publish(Frame(2), 5, out why), "polled twice in one Unity frame");
        Check(ShimAxisConsumer.UnsupportedActions.Contains("camera=") && !ShimAxisConsumer.UnsupportedActions.Contains("clutch="), "capability wrong");
    }
    private static int Main(string[] args)
    {
        try
        {
            Decode(); Consumption(); StatusesAndFailures();
            assertions += BridgeTests.Fake();
            assertions += IsolationTests.Run();
            assertions += ProbeTests.Run();
            assertions += IdentityLeaseTests.Run();
            if (args.Length == 2 && args[0] == "--abi") assertions += AbiProbe.Check(args[1]);
            if (args.Length == 2 && args[0] == "--no-force") assertions += AbiProbe.NoForce(args[1]);
            if (args.Length == 2 && args[0] == "--load-muted") assertions += AbiProbe.NoForce(args[1], true);
            if (args.Length == 2 && args[0] == "--bridge") assertions += BridgeTests.Actual(args[1]);
            Console.WriteLine("{\"status\":\"passed\",\"assertions\":" + assertions + "}"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
