using System;
using System.IO;
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
        var transport = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, Stopped = true, StopReason = "shared provider refused physical batch", KeyboardRetained = true });
        Check(transport[0].Contains("refused physical batch") && transport[0].Contains("keyboard driving continues") && !transport[0].Contains("alike"), "transport stop text");
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
    // Slice B trial against a scripted latched Core: admission, then navDown and navUp injections the
    // fake menu reacts to, each read back, then end. Every way it can fail is named, never passed.
    private static LatchedTrial SimulateTrial(System.Collections.Generic.List<string> batches, bool admits = true, bool injects = true,
        bool moves = true, bool returns = true, bool readable = true, int stopAt = -1, int navigationFrom = 0, bool waitForNavigation = true)
    {
        const ShimStatus L = ShimStatus.Live; string why;
        var sink = new ShimAxisConsumer { AcceptLatched = true }; sink.DeliverOnly(LatchedTrial.NavUp, LatchedTrial.NavDown);
        string selected = "Career#11", pending = null; int injectFrames = 0, action = 0; byte run = admits ? LatchedTrial.Waiting : (byte)1;
        int now = 0;
        var trial = new LatchedTrial(sink, () => readable ? selected : null, text => { batches.Add(text); pending = text; }, "n0nce1234",
            waitForNavigation ? () => now >= navigationFrom : (Func<bool>)null);
        for (int frame = 1; frame <= 3000 && trial.Current != LatchedTrial.Step.Passed && trial.Current != LatchedTrial.Step.Failed; frame++)
        {
            if (pending != null)
            {
                string command = pending.Split('\n')[2]; pending = null;
                if (command == "end") run = LatchedTrial.Completed;
                else if (injects) { run = 3; injectFrames = 5; action = command.Contains("navDown") ? LatchedTrial.NavDown : LatchedTrial.NavUp; }
            }
            bool injecting = injectFrames-- > 0;
            float down = injecting && action == LatchedTrial.NavDown ? 1f : 0f, up = injecting && action == LatchedTrial.NavUp ? 1f : 0f;
            var b = Actions((ulong)frame, 0, (9, L, 0f), (13, L, up), (14, L, down)); b[40] = run; b[41] = 1; b[42] = 1;
            if (down > 0) b[48 + 14 * 8 + 5] = 1;
            if (up > 0) b[48 + 13 * 8 + 5] = 1;
            if (frame == stopAt) sink.Stop(); else Check(sink.Publish(b, frame, out why), why);
            now = frame; bool navigating = frame >= navigationFrom; // the game ignores moves before its navigation delay
            if (down > 0 && moves && navigating) selected = "FreeRoam#12";
            if (up > 0 && returns && navigating) selected = "Career#11";
            trial.Tick(frame);
        }
        return trial;
    }
    private static void TrialSteps()
    {
        var batches = new System.Collections.Generic.List<string>();
        var passed = SimulateTrial(batches);
        Check(passed.Current == LatchedTrial.Step.Passed && passed.Before == "Career#11" && passed.AfterDown == "FreeRoam#12" && passed.AfterUp == "Career#11", "trial did not pass: " + passed.Current + " " + passed.Failure);
        var delayed = SimulateTrial(new System.Collections.Generic.List<string>(), navigationFrom: 200);
        Check(delayed.Current == LatchedTrial.Step.Passed && delayed.NavigationWaitFrames > 0, "trial did not wait for menu navigation: " + delayed.Current + " " + delayed.Failure);
        Check(batches.Count == 3 && batches[0] == "nonce=n0nce1234\nbatch=1\ninject action navDown 1 ms=300\n" &&
            batches[1] == "nonce=n0nce1234\nbatch=2\ninject action navUp 1 ms=300\n" && batches[2] == "nonce=n0nce1234\nbatch=3\nend\n", "trial batches: " + string.Join("|", batches));
        foreach (var (name, trial, expected) in new[] {
            ("no move", SimulateTrial(new System.Collections.Generic.List<string>(), moves: false), "navDown did not move"),
            ("no return", SimulateTrial(new System.Collections.Generic.List<string>(), returns: false), "navUp did not return"),
            ("no selection", SimulateTrial(new System.Collections.Generic.List<string>(), readable: false), "no menu selection"),
            ("not admitted", SimulateTrial(new System.Collections.Generic.List<string>(), admits: false), "no progress in Admission"),
            ("never injected", SimulateTrial(new System.Collections.Generic.List<string>(), injects: false), "no progress in Down"),
            ("stopped", SimulateTrial(new System.Collections.Generic.List<string>(), stopAt: 40), "shared session stopped"),
            ("navigation ignored", SimulateTrial(new System.Collections.Generic.List<string>(), navigationFrom: 200, waitForNavigation: false), "navDown did not move") })
            Check(trial.Current == LatchedTrial.Step.Failed && trial.Failure.Contains(expected), name + ": " + trial.Current + " " + trial.Failure);
        // The fence byte: every fact observed, the first failing one named.
        LatchedTrial.Fences All() => new LatchedTrial.Fences { NoForceArmed = true, SettingsWritesSuppressed = true, TelemetryIdle = true, Compatible = true, Primary = true, MainMenu = true, Focused = true };
        Check(LatchedTrial.Unfenced(All()) == null && LatchedTrial.Unfenced(null) != null, "verified fences refused or missing fences accepted");
        foreach (var (mutate, expected) in new (Action<LatchedTrial.Fences>, string)[] {
            (f => f.NoForceArmed = false, "no-force"), (f => f.SettingsWritesSuppressed = false, "settings writes"), (f => f.TelemetryIdle = false, "telemetry"),
            (f => f.MissingHooks = "GetAxis", "hooks missing: GetAxis"), (f => f.Compatible = false, "game build"), (f => f.Primary = false, "primary player"),
            (f => f.MainMenu = false, "main menu"), (f => f.Driving = true, "main menu"), (f => f.Focused = false, "not focused") })
        { var f = All(); mutate(f); var why = LatchedTrial.Unfenced(f); Check(why != null && why.Contains(expected), "fence not refused: " + expected + " -> " + why); }
        // Only reaching the main menu or focus may be waited for; nothing else, and never while driving.
        bool wait;
        var title = All(); title.MainMenu = false; Check(LatchedTrial.Admission(title, out wait) != null && wait, "title screen not waited for");
        var away = All(); away.Focused = false; Check(LatchedTrial.Admission(away, out wait) != null && wait, "unfocused window not waited for");
        var hooks = All(); hooks.MainMenu = false; hooks.MissingHooks = "GetAxis"; Check(LatchedTrial.Admission(hooks, out wait) != null && !wait, "missing hook waited for");
        var force = All(); force.Focused = false; force.NoForceArmed = false; Check(LatchedTrial.Admission(force, out wait) != null && !wait, "unarmed no-force waited for");
        var driving = All(); driving.Driving = true; Check(LatchedTrial.Admission(driving, out wait) != null && !wait, "driving waited for");
        driving.MainMenu = false; Check(LatchedTrial.Admission(driving, out wait) != null && !wait, "driving away from the menu waited for");
        Check(LatchedTrial.Admission(All(), out wait) == null && !wait, "fenced admission refused");
        // The title splash is ended only on request, once, from the splash, when nothing else is unmet.
        var splash = All(); splash.MainMenu = false;
        Check(LatchedTrial.AdvanceTitle(true, false, true, splash), "requested title advance refused");
        Check(!LatchedTrial.AdvanceTitle(false, false, true, splash) && !LatchedTrial.AdvanceTitle(true, true, true, splash) &&
            !LatchedTrial.AdvanceTitle(true, false, false, splash) && !LatchedTrial.AdvanceTitle(true, false, true, All()), "title advanced without request, twice, off the splash or on the menu");
        var unarmed = All(); unarmed.MainMenu = false; unarmed.NoForceArmed = false;
        var drivingSplash = All(); drivingSplash.MainMenu = false; drivingSplash.Driving = true;
        Check(!LatchedTrial.AdvanceTitle(true, false, true, unarmed) && !LatchedTrial.AdvanceTitle(true, false, true, drivingSplash), "title advanced with another fence unmet");
        var unfocusedSplash = All(); unfocusedSplash.MainMenu = false; unfocusedSplash.Focused = false;
        Check(!LatchedTrial.AdvanceTitle(true, false, true, unfocusedSplash), "title advanced while the game was not focused");
        // Trial runs only on the reviewed latched build.
        string Refusal(string shim)
        {
            try { new ProbeRequest { Schema = 1, Mode = "Trial", Nonce = Guid.NewGuid().ToString("N"), ShimSha256 = shim, ExpiresUtc = DateTime.UtcNow.AddMinutes(5).ToString("o") }.Validate(DateTime.UtcNow, "x", "y", "z", "w"); return ""; }
            catch (Exception e) { return e.Message; }
        }
        Check(Refusal(BridgeTests.OrdinaryShim).Contains("reviewed latched bridge build") && !Refusal(ProbeRequest.LatchedShimSha256).Contains("reviewed latched bridge build"), "Trial shim pin");
        string advanceApply;
        try { new ProbeRequest { Schema = 1, Mode = "Apply", Nonce = Guid.NewGuid().ToString("N"), AdvanceTitle = true, ExpiresUtc = DateTime.UtcNow.AddMinutes(5).ToString("o") }.Validate(DateTime.UtcNow, "x", "y", "z", "w"); advanceApply = ""; }
        catch (Exception e) { advanceApply = e.Message; }
        Check(advanceApply.Contains("only a Trial may advance the title"), "title advance accepted outside a Trial: " + advanceApply);
        // Whole-file replacement while a reader holds the old file the way Core does (read + delete sharing).
        string folder = Path.Combine(Path.GetTempPath(), "art-trial-replace-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string batch = Path.Combine(folder, "shim-batch.txt");
        ProbeRequest.Replace(batch, "one\n");
        using (var held = new FileStream(batch, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
            ProbeRequest.Replace(batch, "two\n");
            Check(new StreamReader(held).ReadToEnd() == "one\n", "held reader saw a torn or replaced object");
        }
        Check(File.ReadAllText(batch) == "two\n" && Directory.GetFiles(folder).Length == 1, "replacement incomplete or temp file left");
        // A Core-style reader opening the batch concurrently never meets a sharing violation or a torn file.
        int refused = 0, torn = 0, reads = 0; bool stop = false;
        var reader = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                try { using (var r = new FileStream(batch, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) { reads++; var t = new StreamReader(r).ReadToEnd(); if (!t.StartsWith("batch ") || !t.EndsWith(" end\n")) torn++; } }
                catch (FileNotFoundException) { refused++; }
                catch (IOException) { refused++; }
            }
        });
        // Background reader, always stopped: a failed replacement is reported, never a hung process.
        reader.IsBackground = true;
        string replaceError = null;
        ProbeRequest.Replace(batch, "batch 0 end\n"); reader.Start();
        try { for (int i = 1; i <= 400; i++) ProbeRequest.Replace(batch, "batch " + i + " " + new string('x', i % 97) + " end\n"); }
        catch (Exception e) { replaceError = e.GetType().Name + ": " + e.Message; }
        finally { Volatile.Write(ref stop, true); reader.Join(); }
        Check(replaceError == null, "replacement failed under a concurrent reader: " + replaceError);
        Check(refused == 0 && torn == 0 && reads > 0 && Directory.GetFiles(folder).Length == 1, "concurrent reader refused " + refused + " / torn " + torn + " of " + reads);
    }
    // Preserved repro (romi 29497): a Core-style reader (GENERIC_READ, FILE_SHARE_READ|DELETE, as
    // art-shim-bridge windows/host/control_snapshot.hpp SnapshotFile) against 400 whole-file
    // replacements by File.Replace (ReplaceFileW, as tests/shim_first_loop/run.py replace_file) and by
    // ProbeRequest.Replace (POSIX-semantics rename). Reports counts; asserts nothing about the first.
    private static string ReplaceRace(string writer, Action<string, string> replace)
    {
        string folder = Path.Combine(Path.GetTempPath(), "art-replace-race-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string batch = Path.Combine(folder, "inject.txt");
        File.WriteAllText(batch, "batch 0 end\n");
        int refused = 0, torn = 0, reads = 0; bool stop = false;
        var reader = new Thread(() =>
        {
            while (!Volatile.Read(ref stop))
            {
                try { using (var r = new FileStream(batch, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) { reads++; var t = new StreamReader(r).ReadToEnd(); if (!t.StartsWith("batch ") || !t.EndsWith(" end\n")) torn++; } }
                catch (IOException) { refused++; }
            }
        });
        reader.IsBackground = true; reader.Start();
        string failed = null;
        try { for (int i = 1; i <= 400; i++) replace(batch, "batch " + i + " " + new string('x', i % 97) + " end\n"); }
        catch (Exception e) { failed = e.GetType().Name + ": " + e.Message; }
        finally { Volatile.Write(ref stop, true); reader.Join(); }
        if (failed != null) return "\"" + writer + "\":{\"writerFailed\":\"" + failed.Replace("\"", "'") + "\",\"refused\":" + refused + ",\"reads\":" + reads + "}";
        return "\"" + writer + "\":{\"refused\":" + refused + ",\"torn\":" + torn + ",\"reads\":" + reads + "}";
    }
    private static void FileReplace(string path, string text)
    {
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp"; File.WriteAllText(temp, text); File.Replace(temp, path, null);
    }
    // A scripted latched Core and car: batches inject the normalized values their raw objects encode, the real
    // consumer composes, and the car stores what GetInput returned (unless told to differ).
    private static DriveTrial SimulateDrive(bool automatic = false, bool carDiffers = false, bool invertSteer = false, bool injects = true, int stopAt = -1, int underwayFrom = 1,
        bool gearStuck = false, bool sticky = false, bool composeOff = false, bool shortHold = false, bool slowShift = false, bool steerDiffers = false,
        Func<int, bool> releaseBad = null, bool companion = false)
    {
        const ShimStatus L = ShimStatus.Live, N = ShimStatus.TestNeutral; string why;
        var b = DriveTrial.Bindings(BridgeTests.DriveProfile);
        var sink = new ShimAxisConsumer { AcceptLatched = true }; sink.DeliverOnly(DriveTrial.Delivered);
        var held = new float[7]; var until = new int[7]; byte run = LatchedTrial.Waiting; string pending = null;
        int gear = 2, changing = 0, slowTarget = 0, batchIndex = 0, firstThrottleEnd = -1; bool upWas = false, downWas = false, batchSteer = false;
        var trial = new DriveTrial(sink, BridgeTests.DriveProfile, text => pending = text, "n0nce1234");
        for (int frame = 1; frame <= 6000 && !trial.Finished; frame++)
        {
            if (pending != null)
            {
                batchIndex++; batchSteer = pending.Contains("inject raw axis 0 ");
                foreach (var line in pending.Split('\n'))
                {
                    if (line == "end") { run = LatchedTrial.Completed; continue; }
                    if (!line.StartsWith("inject raw") || !injects) continue;
                    var t = line.Split(' '); int index = int.Parse(t[3]); string dev = t[4].Substring(4);
                    long raw = long.Parse(t[5].Substring(6)); int ms = int.Parse(t[6].Substring(3));
                    foreach (var pair in b)
                    {
                        if (pair.Value.Index != index || pair.Value.Device != dev || pair.Value.Kind != t[2]) continue;
                        int action = pair.Key == "steer" ? 0 : pair.Key == "throttle" ? 1 : pair.Key == "brake" ? 2 : pair.Key == "handbrake" ? 4 : pair.Key == "shiftUp" ? 5 : 6;
                        var bind = pair.Value; float value;
                        if (bind.Kind == "button") value = 1f;
                        // Core's normalize: steer centred over the range, pedals from rest to the travel end.
                        else if (pair.Key == "steer") value = (float)((raw - (bind.Min + bind.Max) / 2.0) / ((bind.Max - bind.Min) / 2.0));
                        else value = (float)Math.Max(0, Math.Min(1, (raw - bind.Rest) / (double)((bind.Travel > 0 ? bind.Max : bind.Min) - bind.Rest)));
                        if (action == 0 && invertSteer) value = -value;
                        held[action] = value; until[action] = frame + (shortHold ? 2 : ms / 10); run = 3;
                    }
                }
                pending = null;
                if (batchIndex == 1) firstThrottleEnd = until[1];
            }
            var frameValues = new System.Collections.Generic.List<(int, ShimStatus, float)>();
            var injected = new bool[7];
            foreach (int a in DriveTrial.Delivered)
            {
                bool on = frame < until[a];
                injected[a] = on;
                // sticky: the throttle stays applied (Live, not injected) after its injection ends.
                bool stuck = sticky && a == 1 && !on && until[a] > 0;
                // 30996 reproductions: (flicker) after the throttle step 19 bad release frames, one clean,
                // then bad again; (companion) the throttle stays applied after each steer step.
                // releaseBad(offset) decides, after the throttle step's injection ends, which frames stay applied.
                if (releaseBad != null && a == 1 && batchIndex == 1 && !on && frame >= firstThrottleEnd && releaseBad(frame - firstThrottleEnd)) stuck = true;
                if (companion && a == 1 && batchSteer && !on && frame >= until[a]) stuck = true;
                frameValues.Add((a, on || stuck ? L : N, on ? held[a] : stuck ? .4f : 0f));
            }
            ulong down = (injected[5] ? 1UL << 5 : 0) | (injected[6] ? 1UL << 6 : 0);
            var bytes = Actions((ulong)frame, down, frameValues.ToArray()); bytes[40] = run; bytes[41] = 1; bytes[42] = 1;
            foreach (int a in DriveTrial.Delivered) if (injected[a]) bytes[48 + a * 8 + 5] = 1;
            if (frame == stopAt) sink.Stop(); else Check(sink.Publish(bytes, frame, out why), why);
            // The car: GetInput composes, Update stores it; a manual box shifts on Core's press edges.
            var values = new DrivingValues(); const float alignment = .02f;
            sink.Compose(frame, true, alignment, ref values);
            int pendingGear = 0;
            if (!automatic && !gearStuck)
            {
                if (injected[5] && !upWas) { if (slowShift) pendingGear = 1; else gear++; changing = slowShift ? 80 : 6; }
                if (injected[6] && !downWas) { if (slowShift) pendingGear = -1; else gear--; changing = slowShift ? 80 : 6; }
            }
            if (pendingGear != 0) slowTarget = pendingGear;
            upWas = injected[5]; downWas = injected[6];
            if (changing > 0 && --changing == 0 && slowTarget != 0) { gear += slowTarget; slowTarget = 0; }
            var s = new DriveTrial.Sample
            {
                Frame = frame, Underway = frame >= underwayFrom, Composed = true, SteerLive = sink.Live(0, frame),
                AppliedSteer = sink.Applied(0, frame), AppliedThrottle = sink.Applied(1, frame), AppliedBrake = sink.Applied(2, frame), AppliedHandbrake = sink.Applied(4, frame), Alignment = alignment,
                OutSteer = values.Steer, OutThrottle = values.Throttle + (composeOff && frame > 200 ? .05f : 0f), OutBrake = values.Brake, OutHandbrake = values.Handbrake,
                EngineSteer = values.Steer + (steerDiffers && frame > 200 ? .05f : 0f), EngineThrottle = values.Throttle + (carDiffers && frame > 200 ? .1f : 0f) + (composeOff && frame > 200 ? .05f : 0f),
                EngineBrake = values.Brake, EngineHandbrake = values.Handbrake,
                Gear = gear, Automatic = automatic, ChangingGear = changing > 0, Speed = 3f,
            };
            foreach (int a in DriveTrial.Delivered) s.Injected[a] = sink.Injected(a, frame);
            trial.Tick(s);
        }
        return trial;
    }
    // The profile's physical controls, for refusing Art fallbacks that would make one control drive two actions.
    private static void ProfileControls()
    {
        var objects = ProfileObjects.Parse(BridgeTests.DriveProfile + "navDown = hat 0 18000 dev={d71b8350-61b7-11f1-8001-444553540000}\n" + "; comment = axis 9 dev={x}\nProfile = Drive\n");
        var r12 = new Guid("d71b8350-61b7-11f1-8001-444553540000"); var ds8x = new Guid("d9b1d890-6f3f-11f1-8002-444553540000");
        Check(objects.Count == 7, "profile controls parsed: " + objects.Count);
        Check(ProfileObjects.BoundBy(objects, "axis", 2, 0, r12) == "throttle" && ProfileObjects.BoundBy(objects, "button", 9, 0, ds8x) == "shiftUp", "bound controls not found");
        Check(ProfileObjects.BoundBy(objects, "axis", 3, 0, r12) == null && ProfileObjects.BoundBy(objects, "button", 9, 0, r12) == null && ProfileObjects.BoundBy(objects, "axis", 2, 0, ds8x) == null, "free control, other device or kind reported bound");
        Check(ProfileObjects.BoundBy(objects, "hat", 0, 22500, r12) == "navDown" && ProfileObjects.BoundBy(objects, "hat", 0, 27000, r12) == null &&
            ProfileObjects.BoundBy(objects, "hat", 0, 9001, r12) == "navDown" && ProfileObjects.BoundBy(objects, "hat", 0, 0, r12) == null && ProfileObjects.BoundBy(objects, "hat", 1, 18000, r12) == null, "hat sectors");
        Check(ProfileObjects.Parse(null).Count == 0 && ProfileObjects.BoundBy(null, "axis", 0, 0, r12) == null, "empty profile");
    }
    private static void DriveSteps()
    {
        var b = DriveTrial.Bindings(BridgeTests.DriveProfile);
        // Core's denormalize: steer centred over 0..65535 (rest/travel do not apply), pedals rest..travel end.
        Check(DriveTrial.Raw(b["steer"], -.3f) == 22937 && DriveTrial.Raw(b["steer"], .3f) == 42598 && DriveTrial.Raw(b["throttle"], .4f) == 26214 &&
            DriveTrial.Raw(b["brake"], .6f) == 40018 && DriveTrial.Raw(b["handbrake"], 1f) == 65535 && DriveTrial.Raw(b["throttle"], 0f) == 0, "raw values from the profile objects");
        var inverted = DriveTrial.Bindings(BridgeTests.DriveProfile.Replace("rest=0 travel=+1\n", "rest=0 travel=+1 inverted\n"));
        Check(DriveTrial.Raw(inverted["throttle"], .4f) == 39321 && inverted["throttle"].Inverted, "explicit inversion not applied");
        Check(DriveTrial.Axis(b["throttle"], .4f, 1500) == "inject raw axis 2 dev={d71b8350-61b7-11f1-8001-444553540000} value=26214 ms=1500 range=0..65535" &&
            DriveTrial.Button(b["shiftUp"], 200) == "inject raw button 9 dev={d9b1d890-6f3f-11f1-8002-444553540000} value=1 ms=200", "raw command text");
        string Refused(string profile) { try { DriveTrial.Bindings(profile); return ""; } catch (FormatException e) { return e.Message; } }
        Check(Refused(BridgeTests.DriveProfile.Replace("handbrake = axis 7", "handbrakeX = axis 7")).Contains("does not bind handbrake") &&
            Refused(BridgeTests.DriveProfile.Replace("shiftUp = button 9", "shiftUp = axis 3")).Contains("shiftUp: unexpected axis"), "incomplete or mismatched profile accepted");
        var passed = SimulateDrive();
        Check(passed.Passed && passed.Steps.Count == 7 && passed.Steps.TrueForAll(s => s.Result == "passed" && (s.GearDelta != 0 || s.HoldSamples >= DriveTrial.MinHold) && s.ReleaseSamples >= DriveTrial.SettleFrames),
            "drive did not pass: " + passed.Failure + " | " + string.Join(" | ", passed.Steps.ConvertAll(s => s.Name + "=" + s.Result + " " + s.Detail + " h" + s.HoldSamples)));
        Check(passed.Steps[3].GearAfter - passed.Steps[3].GearBefore == 1 && passed.Steps[4].GearAfter - passed.Steps[4].GearBefore == -1, "gear rows not measured");
        var slow = SimulateDrive(slowShift: true);
        Check(slow.Passed && slow.Steps[3].GearAfter - slow.Steps[3].GearBefore == 1, "a slow gear change was read before it completed: " + slow.Failure);
        Check(passed.Samples.ToString().Split('\n').Length > 300, "per-frame samples not recorded");
        var automatic = SimulateDrive(automatic: true);
        Check(automatic.Finished && !automatic.Passed && automatic.Failure == null && automatic.Steps[3].Result == "open" && automatic.Steps[4].Result == "open" &&
            automatic.Steps[0].Result == "passed", "automatic gearbox gear rows not left open: " + automatic.Failure);
        foreach (var (name, trial, expected) in new[] {
            ("car differs", SimulateDrive(carDiffers: true), "car inputs differ from the composition"),
            ("wrong sign", SimulateDrive(invertSteer: true), "wrong sign"),
            ("never injected", SimulateDrive(injects: false), "no progress in Holding of throttle 40%"),
            ("stopped", SimulateDrive(stopAt: 120), "shared session stopped"),
            ("steer differs", SimulateDrive(steerDiffers: true), "car inputs differ from the composition"),
            ("gear stuck", SimulateDrive(gearStuck: true), "shiftUp with throttle: gear 2 -> 2"),
            ("sticky throttle", SimulateDrive(sticky: true), "throttle still applied"),
            ("composition off", SimulateDrive(composeOff: true), "composed throttle"),
            ("short hold", SimulateDrive(shortHold: true), "held samples"),
            // offset 0 is the hold->release transition, not judged: 19 judged bad frames, one clean, then bad again.
            ("one clean release frame after 19 bad", SimulateDrive(releaseBad: r => r != 20), "throttle 40% release: throttle still applied"),
            ("intermittent release (1 bad in 11)", SimulateDrive(releaseBad: r => r % 11 == 10), "throttle 40% release: throttle still applied"),
            ("pulsed release (15 bad, 1 clean)", SimulateDrive(releaseBad: r => r % 16 != 15), "throttle 40% release: throttle still applied"),
            ("companion throttle left applied", SimulateDrive(companion: true), "steer -30% with throttle release: throttle still applied") })
            Check(trial.Finished && !trial.Passed && trial.Failure != null && trial.Failure.Contains(expected), name + ": " + trial.Failure);
        var countdown = SimulateDrive(underwayFrom: 300);
        Check(countdown.Passed && countdown.Samples.ToString().Contains("\n-\t299\t0\t"), "drive began before the countdown ended: " + countdown.Failure);
        // Fences: only the stage start and focus may be waited for.
        DriveTrial.Fences All() => new DriveTrial.Fences { NoForceArmed = true, SettingsWritesSuppressed = true, TelemetryIdle = true, Compatible = true, Custom = true, StageReady = true, Focused = true };
        bool wait;
        Check(DriveTrial.Admission(All(), out wait) == null && !wait && DriveTrial.Unfenced(null) != null, "verified drive refused or missing fences accepted");
        var stage = All(); stage.StageReady = false; Check(DriveTrial.Admission(stage, out wait) != null && wait, "stage start not waited for");
        // In the menus the game mode is not set yet: CUSTOM is waited for with the stage (the first Drive run
        // refused here); at the stage a non-CUSTOM event refuses, focused or not.
        var menus = All(); menus.StageReady = false; menus.Custom = false;
        Check(DriveTrial.Admission(menus, out wait) != null && wait, "unset menu game mode refused instead of waited for");
        var career = All(); career.Custom = false;
        Check(DriveTrial.Admission(career, out wait).Contains("CUSTOM") && !wait, "non-CUSTOM stage waited for");
        career.Focused = false; Check(DriveTrial.Admission(career, out wait) != null && !wait, "non-CUSTOM stage waited for while unfocused");
        foreach (var (mutate, expected) in new (Action<DriveTrial.Fences>, string)[] {
            (f => f.NoForceArmed = false, "no-force"), (f => f.SettingsWritesSuppressed = false, "settings writes"), (f => f.TelemetryIdle = false, "telemetry"),
            (f => f.MissingHooks = "GetInput", "input hooks missing"), (f => f.MissingGuards = "UploadScore", "capture guards missing: UploadScore"),
            (f => f.Compatible = false, "game build") })
        {
            var f = All(); mutate(f); f.StageReady = false;
            string why = DriveTrial.Admission(f, out wait);
            Check(why != null && why.Contains(expected) && !wait, "drive fence waited for or not refused: " + expected + " -> " + why);
        }
    }
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--replace-race")
        {
            Console.WriteLine("{\"status\":\"measured\"," + ReplaceRace("replaceFileW", FileReplace) + "," + ReplaceRace("posixRename", ProbeRequest.Replace) + "}");
            return 0;
        }
        try
        {
            Decode(); Consumption(); StatusesAndFailures(); SharedActions(); SharedStatus(); TrialSteps(); DriveSteps(); ProfileControls();
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
