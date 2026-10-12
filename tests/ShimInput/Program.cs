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
        Check(ShimAxisConsumer.UnsupportedActions.Contains("select=") && ShimAxisConsumer.UnsupportedActions.Contains("horn=") &&
            !ShimAxisConsumer.UnsupportedActions.Contains("camera=") && !ShimAxisConsumer.UnsupportedActions.Contains("clutch=") &&
            !ShimAxisConsumer.UnsupportedActions.Contains("gear6="), "capability wrong");
    }
    // Actions beyond the axes: menu buttons, shortcuts, sequential edges and H-pattern gates.
    private static byte[] Actions(ulong seq, ulong down, params (int Action, ShimStatus Status, float Value)[] owned)
    {
        var bytes = Frame(seq);
        BitConverter.GetBytes(down).CopyTo(bytes, 24); BitConverter.GetBytes(0UL).CopyTo(bytes, 32);
        foreach (var a in owned) { BitConverter.GetBytes(a.Value).CopyTo(bytes, 48 + a.Action * 8); bytes[48 + a.Action * 8 + 4] = (byte)a.Status; }
        return bytes;
    }
    private static void SharedActions()
    {
        string why; const ShimStatus L = ShimStatus.Live;
        int[] unsupported = { 12, 18, 19, 20 };
        for (int i = 0; i < ShimFrame.Count; i++) Check(ShimAxisConsumer.Supported(i) == Array.IndexOf(unsupported, i) < 0, "supported set " + ShimFrame.Names[i]);
        foreach (int action in unsupported)
        { var s = new ShimAxisConsumer(); Check(!s.Publish(Actions(1, 0, (action, L, 1f)), 1, out why) && s.Terminal, "unsupported action owned " + ShimFrame.Names[action]); }

        // Menu buttons and shortcuts: current, Live and owned only.
        var sink = new ShimAxisConsumer();
        Check(!sink.Owns(9) && !sink.Live(9, 1) && sink.Value(9, 1) == 0, "ownership before the first accepted frame");
        Check(sink.Publish(Actions(1, 0, (9, L, 1f), (10, L, 0f), (21, ShimStatus.Rearming, 1f)), 1, out why), why);
        Check(sink.Owns(9) && sink.Live(9, 1) && sink.Value(9, 1) == 1f, "confirm held lost");
        Check(sink.Owns(10) && sink.Live(10, 1) && sink.Value(10, 1) == 0f, "released back");
        Check(sink.Owns(21) && !sink.Live(21, 1) && sink.Value(21, 1) == 0f, "rearming shortcut presented as held");
        Check(!sink.Owns(11) && sink.Value(11, 1) == 0f, "unbound start owned");
        Check(sink.Value(9, 2) == 0f && !sink.Live(9, 2), "previous frame presented as current");
        Check(!sink.Publish(Actions(2, 0, (9, L, 1f), (10, L, 0f), (21, L, 1f), (11, L, 1f)), 2, out why) && sink.Terminal, "ownership change after open accepted");
        Check(!sink.Owns(9) || (sink.Value(9, 2) == 0f && !sink.Live(9, 2)), "terminal session not neutral");

        // Sequential: Core press edges, Live only, latched until a physics step takes one.
        var seq = new ShimAxisConsumer(); ulong up = 1UL << 5, dn = 1UL << 6;
        Check(seq.Publish(Actions(1, up, (5, L, 1f), (6, L, 0f)), 1, out why) && seq.TakeShift() == 0, "press before the first accepted frame replayed");
        Check(seq.Publish(Actions(2, up, (5, L, 1f), (6, L, 0f)), 2, out why), why);
        Check(seq.Publish(Actions(3, 0, (5, L, 0f), (6, L, 0f)), 3, out why), why);
        Check(seq.Publish(Actions(4, up, (5, L, 1f), (6, L, 0f)), 4, out why), why);
        Check(seq.TakeShift() == 1 && seq.TakeShift() == 1 && seq.TakeShift() == 0, "two presses across frames without a physics step");
        foreach (ulong n in new ulong[] { 5, 6, 7 }) Check(seq.Publish(Actions(n, up, (5, L, 1f), (6, L, 0f)), (int)n, out why), why);
        Check(seq.TakeShift() == 1 && seq.TakeShift() == 1 && seq.TakeShift() == 0, "queued presses not capped at two");
        Check(seq.Publish(Actions(8, dn, (5, L, 0f), (6, L, 1f)), 8, out why) && seq.TakeShift() == -1, "downshift lost");
        Check(seq.Publish(Actions(9, up | dn, (5, L, 1f), (6, L, 1f)), 9, out why) && seq.TakeShift() == 0, "simultaneous up/down not neutral");
        Check(seq.Publish(Actions(10, up, (5, ShimStatus.Rearming, 1f), (6, L, 0f)), 10, out why) && seq.TakeShift() == 0, "non-live edge shifted");
        Check(seq.Publish(Actions(11, up, (5, L, 1f), (6, L, 0f)), 11, out why), why); seq.ClearShifts();
        Check(seq.TakeShift() == 0, "cleared press shifted");
        Check(seq.Publish(Actions(12, up, (5, L, 1f), (6, L, 0f)), 12, out why), why); seq.Stop();
        Check(seq.TakeShift() == 0, "terminal session shifted");

        // H-pattern gates: reverse first, no gate is neutral, stale or unowned is no decision.
        var plain = new ShimAxisConsumer(); Check(plain.Publish(Actions(1, 0), 1, out why), why);
        Check(!ShimTransmission.OwnsGates(plain) && ShimTransmission.Gate(plain, 1) == null, "gates without ownership");
        var h = new ShimAxisConsumer(); var gates = new System.Collections.Generic.List<(int, ShimStatus, float)> { (7, L, 0f), (8, L, 0f) };
        for (int g = 0; g < 8; g++) gates.Add((23 + g, L, 0f));
        (int, ShimStatus, float)[] Hold(params int[] held) => gates.ConvertAll(x => (x.Item1, x.Item2, Array.IndexOf(held, x.Item1) >= 0 ? 1f : 0f)).ToArray();
        Check(h.Publish(Actions(1, 0, Hold()), 1, out why) && ShimTransmission.Gate(h, 1) == ShimTransmission.Neutral, "no gate is not neutral");
        Check(h.Publish(Actions(2, 0, Hold(25)), 2, out why) && ShimTransmission.Gate(h, 2) == ShimTransmission.FirstGear + 2, "third gate");
        Check(h.Publish(Actions(3, 0, Hold(8, 23)), 3, out why) && ShimTransmission.Gate(h, 3) == ShimTransmission.Reverse, "reverse collar not first");
        Check(h.Publish(Actions(4, 0, Hold(7)), 4, out why) && ShimTransmission.Gate(h, 4) == ShimTransmission.Neutral, "explicit neutral");
        Check(h.Publish(Actions(5, 0, Hold(28)), 5, out why) && ShimTransmission.Gate(h, 5) == ShimTransmission.FirstGear + 5, "sixth gate index");
        Check(ShimTransmission.Gate(h, 6) == null, "stale gate decided");
        Check(!ShimTransmission.Exists(ShimTransmission.FirstGear + 5, 6) && ShimTransmission.Exists(ShimTransmission.FirstGear + 4, 6), "car range not checked");
        h.Stop(); Check(ShimTransmission.Gate(h, 5) == null, "terminal gate decided");

        // Sequential targets never clamp; neutral may be stepped over.
        Check(ShimTransmission.Step(2, 1, false, 6) == 3 && ShimTransmission.Step(2, -1, false, 6) == 1, "plain steps");
        Check(ShimTransmission.Step(2, -1, true, 6) == 0 && ShimTransmission.Step(0, 1, true, 6) == 2, "skip neutral");
        Check(ShimTransmission.Step(6, 1, false, 6) == null && ShimTransmission.Step(0, -1, false, 6) == null, "out-of-range step clamped");
        Check(ShimTransmission.Step(3, 0, false, 6) == null && ShimTransmission.Step(3, 1, false, -1) == null, "no step or no ratios shifted");

        // Latched Core sessions: accepted only when the opener asked for them; every frame latched;
        // a delivery mask keeps owned actions undelivered; native input suppressed while latched.
        byte[] Latched(ulong n, ulong down, params (int Action, ShimStatus Status, float Value)[] a)
        { var b = Actions(n, down, a); b[40] = 3; b[41] = 1; b[42] = 1; foreach (var x in a) b[48 + x.Action * 8 + 5] = 1; return b; }
        var ordinary = new ShimAxisConsumer();
        Check(!ordinary.Publish(Latched(1, 0), 1, out why) && ordinary.Terminal, "ordinary consumer accepted a latched frame");
        var trial = new ShimAxisConsumer { AcceptLatched = true }; trial.DeliverOnly(13, 14);
        Check(trial.Publish(Latched(1, 0, (9, L, 1f), (13, L, 0f), (14, L, 1f), (5, L, 0f)), 1, out why), why);
        Check(trial.LatchedDelivery(1) && trial.Live(14, 1) && trial.Value(14, 1) == 1f && trial.Injected(14, 1), "injected navDown not delivered");
        Check(trial.Owns(9) && !trial.Live(9, 1) && trial.Value(9, 1) == 0f, "masked confirm delivered (progression)");
        var suppressed = Keyboard(); trial.Compose(1, true, .05f, ref suppressed);
        Same(suppressed, default(DrivingValues), "latched session kept keyboard or masked axes");
        Check(trial.Publish(Latched(2, 1UL << 5, (9, L, 1f), (13, L, 0f), (14, L, 0f), (5, L, 1f)), 2, out why) && trial.TakeShift() == 0, "masked shift edge queued");
        bool fixedLatch = false, fixedMask = false;
        try { trial.AcceptLatched = false; } catch (InvalidOperationException) { fixedLatch = true; }
        try { trial.DeliverOnly(9); } catch (InvalidOperationException) { fixedMask = true; }
        Check(fixedLatch && fixedMask, "latched acceptance or mask changed after open");
        Check(!trial.Publish(Actions(3, 0, (9, L, 1f), (13, L, 0f), (14, L, 0f), (5, L, 0f)), 3, out why) && trial.Terminal, "latched consumer accepted an ordinary frame");
        var plainSession = new ShimAxisConsumer(); Check(plainSession.Publish(Actions(1, 0, (9, L, 1f)), 1, out why), why);
        Check(!plainSession.LatchedDelivery(1) && plainSession.Live(9, 1), "ordinary session suppressed or masked");

        // Transmission policy: the profile's own mode only, never an automatic gearbox, one change per step.
        var S = ShimTransmission.Mode.Sequential; var H = ShimTransmission.Mode.HPattern; var N = ShimTransmission.Mode.None;
        Check(ShimTransmission.ParseMode("Sequential") == S && ShimTransmission.ParseMode("H-pattern") == H && ShimTransmission.ParseMode("Automatic") == N &&
            ShimTransmission.ParseMode(null) == N && ShimTransmission.ParseMode("sequential") == N, "transmission contract words");
        int last = 4;
        Check(ShimTransmission.Next(H, true, 2, 4, ref last, 0, false, 6) == null && last == int.MinValue, "automatic gearbox shifted by a gate");
        Check(ShimTransmission.Next(S, true, 2, null, ref last, 1, false, 6) == null, "automatic gearbox shifted by a press");
        Check(ShimTransmission.Next(H, false, 2, 4, ref last, 1, false, 6) == 4 && last == 4, "gate not applied, or step mixed in");
        Check(ShimTransmission.Next(H, false, 4, 4, ref last, 1, false, 6) == null, "unchanged gate reapplied or step mixed in");
        Check(ShimTransmission.Next(H, false, 4, 7, ref last, 0, false, 6) == null && last == 7, "missing gear clamped");
        Check(ShimTransmission.Next(H, false, 4, 7, ref last, 0, false, 6) == null, "missing gear retried");
        Check(ShimTransmission.Next(H, false, 4, null, ref last, 1, false, 6) == null, "stale gate or step shifted");
        last = int.MinValue;
        Check(ShimTransmission.Next(S, false, 2, 5, ref last, 0, false, 6) == null, "gate applied in a sequential profile");
        Check(ShimTransmission.Next(S, false, 2, 5, ref last, 1, false, 6) == 3, "sequential step");
        Check(ShimTransmission.Next(N, false, 2, 4, ref last, 1, false, 6) == null, "automatic or unknown profile shifted");
    }
    // The panel's shared-profile text: every phase stated, and gears never claimed on an automatic box.
    private static void SharedStatus()
    {
        string[] Lines(SharedInputStatus.Facts f) => SharedInputStatus.Lines(f);
        Check(Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Off })[0].Contains("is off") , "off text");
        var refused = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Refused, Reason = "no shared profile beside the mod." });
        Check(refused.Length == 1 && refused[0].Contains("no shared profile") && refused[0].Contains("Art bindings are used"), "refusal text");
        Check(Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Waiting })[0].Contains("waiting") && Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Isolating })[0].Contains("isolating"), "pending text");
        var attached = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, ProfileName = "Moza KS Pro Triple", Transmission = "Sequential",
            ArtTransmissionMode = "Sequential", Devices = new[] { "MOZA R12 Base", "DS-8X Shifter" } });
        Check(attached[0].Contains("Moza KS Pro Triple") && attached[0].Contains("Keyboard stays native") && attached[1].Contains("MOZA R12 Base, DS-8X Shifter"), "attached identity text");
        Check(Array.IndexOf(attached, SharedInputStatus.Unsupported) > 0 && Array.IndexOf(attached, SharedInputStatus.ArtOnly) > 0 && attached[attached.Length - 1] == SharedInputStatus.Ffb, "scope/FFB caveats missing");
        Check(attached[attached.Length - 3].Contains("Gears follow the profile (Sequential)") && attached[attached.Length - 2] == SharedInputStatus.Editors, "manual gear/editor text");
        var onAutomatic = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, ProfileName = "p", Transmission = "Sequential", ArtTransmissionMode = "Sequential", CarAutomatic = true });
        Check(onAutomatic[onAutomatic.Length - 3].Contains("automatic now"), "attached text claimed gears on an automatic car");
        var stopped = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, Stopped = true, StopReason = "native input isolation lost" });
        Check(stopped.Length == 2 && stopped[0].Contains("native input isolation lost") && stopped[0].Contains("wheel and keyboard alike") && stopped[1] == SharedInputStatus.Editors, "stopped text");
        Check(Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Waiting, WaitingFor = "force feedback to finish connecting" })[0].Contains("for force feedback to finish connecting"), "waiting cause missing");
        var cleanup = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.CleanupFailed, Reason = "x.", RestoreStatus = "one or more joystick flags could not be restored", Retrying = true });
        Check(cleanup.Length == 1 && cleanup[0].Contains("still off (one or more joystick flags") && cleanup[0].Contains("retrying") && !cleanup[0].Contains("Art bindings are used"), "cleanup failure text claimed a handback");
        Check(Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.CleanupFailed, Reason = "x.", RestoreStatus = "s", Retrying = false })[0].Contains("restart the game"), "exhausted cleanup text");
        Check(SharedInputStatus.Gears("Sequential", "Automatic", null).Contains("ignored"), "Art Automatic not flagged");
        Check(SharedInputStatus.Gears("Sequential", "Follow game", null).Contains("only while the game's own transmission option is manual"), "Follow game not explained");
        Check(SharedInputStatus.Gears("H-pattern", "H-pattern", true).Contains("automatic now"), "automatic car not flagged");
        Check(SharedInputStatus.Gears("Automatic", "Sequential", false).Contains("not used") && SharedInputStatus.Gears(null, "Sequential", false).Contains("names no transmission"), "non-manual profile claimed gears");
        Check(SharedInputStatus.Gears("H-pattern", "Sequential", false).Contains("Gears follow the profile (H-pattern)"), "manual mismatch text");
    }
    private static int Main(string[] args)
    {
        try
        {
            Decode(); Consumption(); StatusesAndFailures(); SharedActions(); SharedStatus();
            assertions += BridgeTests.Fake();
            assertions += IsolationTests.Run();
            assertions += ProbeTests.Run();
            assertions += IdentityLeaseTests.Run();
            if (args.Length == 2 && args[0] == "--abi") assertions += AbiProbe.Check(args[1]);
            if (args.Length == 2 && args[0] == "--no-force") assertions += AbiProbe.NoForce(args[1]);
            if (args.Length == 2 && args[0] == "--load-muted") assertions += AbiProbe.NoForce(args[1], true);
            if (args.Length == 2 && args[0] == "--bridge") assertions += BridgeTests.Actual(args[1]);
            if (args.Length == 2 && args[0] == "--bridge-latched") assertions += BridgeTests.ActualLatchedSession(args[1]);
            Console.WriteLine("{\"status\":\"passed\",\"assertions\":" + assertions + "}"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
