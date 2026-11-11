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
        Same(frame[0].Value, -.4f, "steer wire value");
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
    // sana 35373: keyboard steering reported for this frame keeps the game's steer value (alignment already in
    // it) over a resting off-centre wheel, only with this frame's isolation proof and never in a latched session.
    private static void KeyboardSteerRule()
    {
        const ShimStatus L = ShimStatus.Live; string why;
        var sink = new ShimAxisConsumer();
        Check(sink.Publish(Frame(1, L, .25f), 1, out why), why);
        foreach (float key in new[] { 1.02f, -.98f })
        {
            var game = new DrivingValues { Steer = key, Throttle = .4f };
            Check(sink.Compose(1, true, .02f, ref game, KeyboardSteer.Present) && sink.KeyboardSteered, "keyboard steer not kept " + key);
            Same(game.Steer, key, "keyboard steer changed " + key); Same(game.Throttle, .6f, "throttle no longer composed with max");
        }
        var zero = new DrivingValues(); sink.Compose(1, true, 0f, ref zero, KeyboardSteer.Present);
        Same(zero.Steer, 0f, "zero keyboard steer replaced by the wheel");
        foreach (var state in new[] { KeyboardSteer.NotQueried, KeyboardSteer.Unknown, KeyboardSteer.Absent })
        {
            var game = new DrivingValues { Steer = 1.02f }; sink.Compose(1, true, .02f, ref game, state);
            Same(game.Steer, .27f, "wheel lost without a keyboard source " + state); Check(!sink.KeyboardSteered, "keyboard flag kept " + state);
        }
        var stale = new DrivingValues { Steer = 1.02f, Throttle = .9f }; sink.Compose(1, false, .02f, ref stale, KeyboardSteer.Present);
        Same(stale.Steer, .27f, "unisolated keyboard steer kept"); Same(stale.Throttle, .6f, "unisolated native throttle kept");
        Check(!sink.KeyboardSteered, "keyboard flag without isolation");
        var old = new DrivingValues { Steer = 1.02f }; sink.Compose(2, false, .02f, ref old, KeyboardSteer.Present);
        Same(old.Steer, 0f, "stale frame kept native or wheel steer"); Check(!sink.KeyboardSteered, "keyboard flag on a stale frame");
        // The query gate is this frame's isolation proof, not delivery freshness (sana 35766).
        Check(sink.AsksKeyboard(true) && !sink.AsksKeyboard(false), "ordinary query gate");
        sink.Stop(); Check(sink.Terminal && sink.AsksKeyboard(true), "stopped ordinary session lost its keyboard query");
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
        Check(ShimAxisConsumer.UnsupportedActions == "select=Art has no select action\n", "capability wrong: " + ShimAxisConsumer.UnsupportedActions);
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
        int[] unsupported = { 12 };
        for (int i = 0; i < ShimFrame.Count; i++) Check(ShimAxisConsumer.Supported(i) == Array.IndexOf(unsupported, i) < 0, "supported set " + ShimFrame.Names[i]);
        foreach (int action in unsupported)
        { var s = new ShimAxisConsumer(); Check(!s.Publish(Actions(1, 0, (action, L, 1f)), 1, out why) && s.Terminal, "unsupported action owned " + ShimFrame.Names[action]); }
        // Look back, reset (Art's recover car) and horn are owned and delivered like the menu buttons.
        var native = new ShimAxisConsumer();
        Check(native.Publish(Actions(1, 0, (18, L, 1f), (19, L, 0f), (20, L, 1f)), 1, out why), why);
        Check(native.Owns(18) && native.Value(18, 1) == 1f && native.Owns(19) && native.Live(19, 1) && native.Value(19, 1) == 0f &&
            native.Owns(20) && native.Value(20, 1) == 1f, "look back, reset or horn not delivered");

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

        // Core's latched test sessions (run state, latched, native suppressed, injected) are never accepted.
        foreach (int flag in new[] { 40, 41, 42 })
        {
            var b = Actions(1, 0, (9, L, 1f)); b[flag] = flag == 40 ? (byte)3 : (byte)1;
            var refusing = new ShimAxisConsumer();
            Check(!refusing.Publish(b, 1, out why) && refusing.Terminal && !refusing.Started, "a latched test frame was accepted: byte " + flag);
        }
        var plainSession = new ShimAxisConsumer(); Check(plainSession.Publish(Actions(1, 0, (9, L, 1f)), 1, out why), why);
        Check(plainSession.Live(9, 1) && plainSession.Value(9, 1) == 1f, "ordinary session suppressed");

        // Transmission policy: the profile's own mode only, never an automatic gearbox, one change per step.
        var S = ShimTransmission.Mode.Sequential; var H = ShimTransmission.Mode.HPattern; var N = ShimTransmission.Mode.None;
        Check(ShimTransmission.ParseMode("Sequential") == S && ShimTransmission.ParseMode("H-pattern") == H && ShimTransmission.ParseMode("Automatic") == N &&
            ShimTransmission.ParseMode(null) == N && ShimTransmission.ParseMode("sequential") == N, "transmission contract words");
        int last = 4;
        Check(ShimTransmission.Next(H, true, 2, 4, ref last, 0, false, 6) == null && last == int.MinValue, "automatic gearbox shifted by a gate");
        Check(ShimTransmission.Next(S, true, 2, null, ref last, 1, false, 6) == null, "automatic gearbox shifted by a press");
        Check(ShimTransmission.Next(H, false, 2, 4, ref last, 1, false, 6) == 4 && last == 4, "gate not applied, or step mixed in");
        Check(ShimTransmission.Next(H, false, 4, 4, ref last, 1, false, 6) == null, "unchanged gate reapplied or step mixed in");
        // 1st held at the start: the game refused the first shift (clutch, change under way) or reset to neutral.
        Check(ShimTransmission.Next(H, false, 0, 4, ref last, 0, false, 6) == 4, "a held gate the gearbox is not in was not applied again");
        Check(ShimTransmission.Next(H, false, 0, 4, ref last, 0, false, 6, changing: true) == null, "a held gate interrupted a change under way");
        // Owner log 2026-11-01: at the start the game is in 1st and the provider reads the held 1st as neutral.
        last = int.MinValue;
        Check(ShimTransmission.Next(H, false, 2, 1, ref last, 0, false, 6) == null && last == 1, "a first neutral reading shifted the game out of gear");
        Check(ShimTransmission.Next(H, false, 2, 1, ref last, 0, false, 6) == null, "a held neutral was re-applied over the game's gear");
        Check(ShimTransmission.Next(H, false, 2, 2, ref last, 0, false, 6) == null && last == 2, "1st read after start-up shifted although engaged");
        Check(ShimTransmission.Next(H, false, 2, 1, ref last, 0, false, 6) == 1, "a real move to neutral was not applied");
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
        var off = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Off });
        Check(off.Length == 1 && off[0] == SharedInputStatus.NoProfile && off[0].Contains("game's own controls apply") && off[0].Contains("Wheelkit"), "no-profile text");
        var refused = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Refused, Reason = "dbce_shim.dll is missing beside the mod." });
        Check(refused.Length == 1 && refused[0].Contains("dbce_shim.dll is missing") && refused[0].Contains("game's own controls apply"), "refusal text");
        Check(!string.Join("|", refused).Contains("Art bindings"), "refusal names the deleted Art bindings");
        const string identityReason = "native isolation unqualified: Rewired joystick identity unavailable: cannot exclude a duplicate mapped wheel; no name/index fallback";
        var identityFacts = new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Refused, Reason = identityReason };
        var unidentified = Lines(identityFacts);
        Check(unidentified.Length == 3 && unidentified[0].Contains("for this session") && unidentified[0].Contains("could not be identified") && unidentified[0].Contains("game's own controls apply"), "identity refusal does not explain the session fallback");
        Check(unidentified[1].Contains("with that controller disconnected") && unidentified[1].Contains("If you use Steam Input") && unidentified[1].Contains("Restart the game") && unidentified[1].Contains("may be the cause"), "identity workaround assumes Steam Input or omits restart");
        Check(unidentified[2] == "Details: " + identityReason, "identity diagnostic lost");
        Check(string.Join("|", SharedInputStatus.Lines(identityFacts, false)) == string.Join("|", unidentified), "compact F6 hides the refusal workaround");
        Check(!string.Join("|", refused).Contains("Steam Input"), "unrelated refusal gets controller workaround");
        var identityCleanup = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.CleanupFailed, Reason = identityReason, RestoreStatus = "restore pending", Retrying = true });
        Check(identityCleanup.Length == 1 && identityCleanup[0].Contains("still off") && !identityCleanup[0].Contains("controls apply") && !identityCleanup[0].Contains("Steam Input"), "identity cleanup failure claims fallback or offers premature workaround");
        Check(Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Waiting })[0].Contains("waiting") && Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Isolating })[0].Contains("isolating"), "pending text");
        var attached = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, ProfileName = "Moza KS Pro Triple", Transmission = "Sequential",
            ArtTransmissionMode = "Sequential", Devices = new[] { "MOZA R12 Base", "DS-8X Shifter" } });
        var compact = SharedInputStatus.Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, ProfileName = "Moza KS Pro Triple" }, false);
        Check(compact.Length == 1 && compact[0].Contains("Moza KS Pro Triple") && compact[0].Contains("Saved bindings apply after a restart"), "compact active state lost profile/restart meaning");
        Check(attached[0].Contains("Moza KS Pro Triple") && attached[0].Contains("Keyboard stays native") && attached[1].Contains("MOZA R12 Base, DS-8X Shifter"), "attached identity text");
        Check(attached[0].Contains("look back, recover car, horn") && !Array.Exists(attached, l => l.Contains("Art binding")), "scope text missing or names the deleted Art bindings");
        Check(!Array.Exists(attached, l => l.Contains("Not available") || l.Contains("select")), "attached text describes the hidden select action");
        Check(attached[attached.Length - 2].Contains("Gears follow the profile (Sequential)") && attached[attached.Length - 1] == SharedInputStatus.Editors, "manual gear/editor text");
        var onAutomatic = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, ProfileName = "p", Transmission = "Sequential", ArtTransmissionMode = "Sequential", CarAutomatic = true });
        Check(onAutomatic[onAutomatic.Length - 2].Contains("automatic now"), "attached text claimed gears on an automatic car");
        var stopped = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, Stopped = true, StopReason = "native input isolation lost" });
        Check(stopped.Length == 2 && stopped[0].Contains("native input isolation lost") && stopped[0].Contains("wheel and keyboard alike") && stopped[1] == SharedInputStatus.Editors, "stopped text");
        var compactStopped = SharedInputStatus.Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, Stopped = true, StopReason = "native input isolation lost" }, false);
        Check(compactStopped.Length == 1 && compactStopped[0] == stopped[0], "compact state hid stop cause or neutral/keyboard warning");
        var transport = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Attached, Stopped = true, StopReason = "shared provider refused physical batch", KeyboardRetained = true });
        Check(transport[0].Contains("refused physical batch") && transport[0].Contains("keyboard driving continues") && !transport[0].Contains("alike"), "transport stop text");
        Check(Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.Waiting, WaitingFor = "force feedback to finish connecting" })[0].Contains("for force feedback to finish connecting"), "waiting cause missing");
        var cleanup = Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.CleanupFailed, Reason = "x.", RestoreStatus = "one or more joystick flags could not be restored", Retrying = true });
        Check(cleanup.Length == 1 && cleanup[0].Contains("still off (one or more joystick flags") && cleanup[0].Contains("retrying") && !cleanup[0].Contains("controls apply"), "cleanup failure text claimed a handback");
        Check(Lines(new SharedInputStatus.Facts { Phase = SharedInputStatus.Phase.CleanupFailed, Reason = "x.", RestoreStatus = "s", Retrying = false })[0].Contains("restart the game"), "exhausted cleanup text");
        Check(SharedInputStatus.Gears("Sequential", "Automatic", null).Contains("ignored"), "Art Automatic not flagged");
        Check(SharedInputStatus.Gears("Sequential", "Follow game", null).Contains("only while the game's own transmission option is manual"), "Follow game not explained");
        Check(SharedInputStatus.Gears("H-pattern", "H-pattern", true).Contains("automatic now"), "automatic car not flagged");
        Check(SharedInputStatus.Gears("Automatic", "Sequential", false).Contains("not used") && SharedInputStatus.Gears(null, "Sequential", false).Contains("names no transmission"), "non-manual profile claimed gears");
        Check(SharedInputStatus.Gears("H-pattern", "Sequential", false).Contains("Gears follow the profile (H-pattern)"), "manual mismatch text");
    }
    // tools/package/shared-input-controls.json is the Wheelkit-facing contract build.json carries.
    // It must name exactly the actions this build delivers and the ones it refuses, with the same reasons.
    [System.Runtime.Serialization.DataContract]
    private sealed class ControlsContract
    {
        [System.Runtime.Serialization.DataMember(Name = "schema")] public int Schema = 0;
        [System.Runtime.Serialization.DataMember(Name = "adapter")] public string Adapter = null;
        [System.Runtime.Serialization.DataMember(Name = "delivered")] public string[] Delivered = null;
        [System.Runtime.Serialization.DataMember(Name = "unsupported")] public System.Collections.Generic.Dictionary<string, string> Unsupported = null;
        [System.Runtime.Serialization.DataMember(Name = "qualification")] public ControlsQualification Qualification = null;
    }
    [System.Runtime.Serialization.DataContract]
    private sealed class ControlsQualification
    {
        [System.Runtime.Serialization.DataMember(Name = "runtimeShown")] public ControlsShown[] RuntimeShown = null;
        [System.Runtime.Serialization.DataMember(Name = "fixtureOnly")] public string[] FixtureOnly = null;
        [System.Runtime.Serialization.DataMember(Name = "writerOmits")] public System.Collections.Generic.Dictionary<string, string> WriterOmits = null;
    }
    [System.Runtime.Serialization.DataContract]
    private sealed class ControlsShown
    {
        [System.Runtime.Serialization.DataMember(Name = "actions")] public string[] Actions = null;
    }
    private static void ControlsContractMatches()
    {
        string path = null;
        for (var d = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); d != null && path == null; d = d.Parent)
        {
            string candidate = Path.Combine(d.FullName, "tools", "package", "shared-input-controls.json");
            if (File.Exists(candidate)) path = candidate;
        }
        Check(path != null, "tools/package/shared-input-controls.json not found above the test binary");
        ControlsContract contract;
        var settings = new System.Runtime.Serialization.Json.DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
        using (var stream = File.OpenRead(path))
            contract = (ControlsContract)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(ControlsContract), settings).ReadObject(stream);
        var delivered = new System.Collections.Generic.List<string>();
        for (int i = 0; i < ShimFrame.Count; i++) if (ShimAxisConsumer.Supported(i)) delivered.Add(ShimFrame.Names[i]);
        var unsupported = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var line in ShimAxisConsumer.UnsupportedActions.Split('\n'))
            if (line.Length > 0) unsupported[line.Substring(0, line.IndexOf('='))] = line.Substring(line.IndexOf('=') + 1);
        // Schema 3 (sana 162623, Wheelkit zele a8583c5) adds look back, reset and horn to schema 2's delivered set.
        Check(contract.Schema == 3 && contract.Adapter == "art-shim-ini-controls-3", "controls contract identity changed");
        // Schema 2 (sana 34369) lets a writer write every delivered action, so each one must be labelled exactly once:
        // shown at runtime in some run, or fixture-only. Schema 3 keeps that rule.
        var q = contract.Qualification;
        Check(q != null && q.WriterOmits != null && q.WriterOmits.Count == 0, "schema 3 must not omit writer actions");
        var shown = new System.Collections.Generic.HashSet<string>();
        foreach (var run in q.RuntimeShown) foreach (var action in run.Actions) shown.Add(action);
        var fixture = new System.Collections.Generic.HashSet<string>(q.FixtureOnly);
        Check(fixture.Count == q.FixtureOnly.Length && !fixture.Overlaps(shown), "CONTROLS.json labels an action both shown and fixture-only");
        fixture.UnionWith(shown);
        Check(fixture.SetEquals(delivered), "CONTROLS.json qualification labels differ from delivered");
        Check(contract.Delivered != null && string.Join(",", contract.Delivered) == string.Join(",", delivered.ToArray()),
            "CONTROLS.json delivered differs from this build: " + string.Join(",", delivered.ToArray()));
        Check(contract.Unsupported != null && contract.Unsupported.Count == unsupported.Count, "CONTROLS.json unsupported count differs");
        foreach (var pair in unsupported)
            Check(contract.Unsupported.TryGetValue(pair.Key, out var reason) && reason == pair.Value, "CONTROLS.json unsupported reason differs: " + pair.Key);
    }
    // ShimBridge.AcceptedSnapshot (Hera 122724): the binding-capture host's view of the SAME physical read. The metadata
    // and owned path are the constructor's pinned copies (no second Describe or path query), and a sample is reported
    // only after the whole batch was ticked and published. A failed, refused, regressed or stopped frame reports nothing.
    private sealed class ObservedSource : IPhysicalSnapshotSource
    {
        internal int Descriptions, Reads, Result = 1; internal ulong Generation = 7; internal byte Valid = 1; internal int Axis0 = 32768;
        public int Describe(int slot, byte[] bytes)
        {
            Descriptions++; BitConverter.GetBytes(144).CopyTo(bytes, 0); bytes[4] = 1; BitConverter.GetBytes(Generation).CopyTo(bytes, 8);
            new Guid("0006346e-0000-0000-0000-504944564944").ToByteArray().CopyTo(bytes, 32);
            bytes[48] = bytes[52] = 1; BitConverter.GetBytes(65535).CopyTo(bytes, 112); bytes[64] = 1; return 1;
        }
        public int Read(int slot, byte[] bytes)
        {
            Reads++; BitConverter.GetBytes(232).CopyTo(bytes, 0); bytes[4] = 1; BitConverter.GetBytes(Generation).CopyTo(bytes, 8);
            BitConverter.GetBytes((ulong)Reads).CopyTo(bytes, 16); bytes[50] = Valid; BitConverter.GetBytes(Axis0).CopyTo(bytes, 52);
            for (int i = 212; i < 228; i++) bytes[i] = 0xff;
            return Result;
        }
    }
    private sealed class ObservedPathSource : IPhysicalSnapshotSource, IPhysicalSnapshotPathSource
    {
        internal readonly ObservedSource Inner = new ObservedSource(); internal int PathQueries;
        public int Describe(int slot, byte[] bytes) => Inner.Describe(slot, bytes);
        public int Read(int slot, byte[] bytes) => Inner.Read(slot, bytes);
        public bool TryGetPath(int slot, ulong generation, out string path)
        { PathQueries++; path = @"\\?\hid#vid_346e&pid_0006#owned-" + slot; return generation == Inner.Generation; }
    }
    private sealed class ObservedNative : IShimBridge, IShimPathBridge
    {
        internal int Ticks, Stops; internal bool Accept = true, BadFrame;
        public bool Devices(byte[] bytes, int count) => true;
        public bool DevicesWithPaths(byte[] bytes, int count, string[] paths) => true;
        public bool Tick(byte[] samples, int count, byte[] frame)
        {
            Ticks++; if (!Accept) return false;
            BitConverter.GetBytes(BadFrame ? 295 : 296).CopyTo(frame, 0); frame[4] = 2; BitConverter.GetBytes((ulong)Ticks).CopyTo(frame, 8);
            BitConverter.GetBytes((long)Ticks).CopyTo(frame, 16);
            for (int i = 0; i < 31; i++) frame[52 + i * 8] = (byte)(i < 5 ? ShimStatus.Live : ShimStatus.Unsupported);
            return true;
        }
        public void Stop() { Stops++; }
    }
    private struct Accepted { internal int Slot; internal byte[] Metadata, Sample; internal string Path; internal int CacheCallsBefore; }
    private static void AcceptedSnapshots()
    {
        // Without a path API: one event per slot per accepted frame, after the cache update, with the pinned metadata.
        var source = new ObservedSource(); var native = new ObservedNative();
        var bridge = new ShimBridge(source, native, new ShimAxisConsumer(), new[] { 2, 5 });
        var seen = new System.Collections.Generic.List<Accepted>(); int cacheCalls = 0;
        bridge.AcceptedSnapshot = (slot, metadata, sample, path) => seen.Add(new Accepted { Slot = slot, Metadata = (byte[])metadata.Clone(), Sample = (byte[])sample.Clone(), Path = path, CacheCallsBefore = cacheCalls });
        Check(seen.Count == 0 && source.Reads == 0 && source.Descriptions == 2, "accepted snapshots: construction described each slot once and read nothing");
        var expected = new byte[144]; source.Describe(0, expected); source.Descriptions--;
        Check(bridge.ReadFrame(1, (slot, bytes) => cacheCalls++), "accepted snapshots: first frame");
        Check(seen.Count == 2 && seen[0].Slot == 2 && seen[1].Slot == 5 && source.Reads == 2 && source.Descriptions == 2 && native.Ticks == 1,
            "accepted snapshots: one event per slot from the same single read, no second Describe");
        Check(seen[0].CacheCallsBefore == 1 && seen[1].CacheCallsBefore == 2, "accepted snapshots: each event follows that slot's accepted cache update");
        Check(Convert.ToBase64String(seen[0].Metadata) == Convert.ToBase64String(expected) && seen[0].Path == null,
            "accepted snapshots: metadata is the constructor's Describe bytes; no path without the path API");
        Check(BitConverter.ToUInt64(seen[1].Sample, 8) == 7 && seen[1].Sample[50] == 1 && BitConverter.ToUInt64(seen[1].Sample, 16) == 2,
            "accepted snapshots: the sample is this frame's read (generation, valid, sequence)");
        Check(bridge.ReadFrame(1, (slot, bytes) => cacheCalls++) && seen.Count == 2 && source.Reads == 2, "accepted snapshots: the same game frame reports nothing new");
        Check(bridge.ReadFrame(2, (slot, bytes) => cacheCalls++) && seen.Count == 4 && source.Descriptions == 2 &&
            Convert.ToBase64String(seen[2].Metadata) == Convert.ToBase64String(expected), "accepted snapshots: later frames reuse the pinned metadata");
        // An invalid observation is reported as exactly that (valid 0), never as a successful sample.
        source.Valid = 0;
        Check(bridge.ReadFrame(3, (slot, bytes) => cacheCalls++) && seen.Count == 6 && seen[4].Sample[50] == 0 && seen[5].Sample[50] == 0,
            "accepted snapshots: an invalid read keeps valid 0 for the host to refuse");
        source.Valid = 1;
        // A regressed frame stops the bridge before any read; nothing is reported, then or later.
        Check(!bridge.ReadFrame(2, (slot, bytes) => cacheCalls++) && bridge.Stopped && seen.Count == 6, "accepted snapshots: a regressed frame reports nothing");
        Check(!bridge.ReadFrame(4, (slot, bytes) => cacheCalls++) && seen.Count == 6, "accepted snapshots: a stopped bridge reports nothing");

        // A refused tick, an unpublishable frame, a failed read and a changed generation each report nothing.
        void NothingWhen(string what, Action<ObservedSource, ObservedNative> setup)
        {
            var s = new ObservedSource(); var n = new ObservedNative(); var b = new ShimBridge(s, n, new ShimAxisConsumer(), new[] { 0 }); int events = 0, cached = 0;
            b.AcceptedSnapshot = (slot, metadata, sample, path) => events++;
            Check(b.ReadFrame(1, (slot, bytes) => cached++) && events == 1, "accepted snapshots: baseline frame before " + what);
            setup(s, n);
            Check(!b.ReadFrame(2, (slot, bytes) => cached++) && b.Stopped && events == 1 && cached == 1, "accepted snapshots: " + what + " reported a sample");
        }
        NothingWhen("a refused tick", (s, n) => n.Accept = false);
        NothingWhen("an unpublishable frame", (s, n) => n.BadFrame = true);
        NothingWhen("a failed read", (s, n) => s.Result = -3);
        NothingWhen("a changed generation", (s, n) => s.Generation++);

        // An observer that throws is detached and recorded; the accepted driving session keeps running (Hera 122998).
        source = new ObservedSource(); native = new ObservedNative();
        bridge = new ShimBridge(source, native, new ShimAxisConsumer(), new[] { 2, 5 });
        int observerCalls = 0, cachedSlots = 0;
        bridge.AcceptedSnapshot = (slot, metadata, sample, path) => { observerCalls++; throw new InvalidOperationException("capture host bug"); };
        Check(bridge.ReadFrame(1, (slot, bytes) => cachedSlots++) && !bridge.Stopped && observerCalls == 1 && cachedSlots == 2 &&
            bridge.ObserverFailure == "capture host bug" && bridge.AcceptedSnapshot == null,
            "accepted snapshots: a throwing observer is detached and recorded, every slot is still cached and the session keeps running");
        Check(bridge.ReadFrame(2, (slot, bytes) => cachedSlots++) && !bridge.Stopped && observerCalls == 1 && cachedSlots == 4 && native.Ticks == 2,
            "accepted snapshots: later frames run without the failed observer");
        bridge.Stop("fixture");

        // With the path API: the owned path from the single constructor query rides along with every event.
        var pathSource = new ObservedPathSource(); var pathNative = new ObservedNative();
        bridge = new ShimBridge(pathSource, pathNative, new ShimAxisConsumer(), new[] { 3 });
        string reported = null; int pathEvents = 0;
        bridge.AcceptedSnapshot = (slot, metadata, sample, path) => { reported = path; pathEvents++; };
        Check(bridge.ReadFrame(1, (slot, bytes) => { }) && bridge.ReadFrame(2, (slot, bytes) => { }) && pathEvents == 2 &&
            reported == @"\\?\hid#vid_346e&pid_0006#owned-3" && pathSource.PathQueries == 1 && pathSource.Inner.Descriptions == 1,
            "accepted snapshots: the constructor's owned path, queried once, with every event");
        bridge.Stop("fixture");
    }
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--qualification-only")
            {
                assertions += AbiProbe.Check(args[1]);
                assertions += BridgeTests.Actual(args[1]);
                assertions += BridgeTests.ActualHeldGates(args[1]);
                assertions += ProfileInfoTests.Actual(args[1]);
                Console.WriteLine("{\"status\":\"passed\",\"assertions\":" + assertions + ",\"copiedProvider\":true,\"realDevice\":false}");
                return 0;
            }
            if (args.Length == 1 && args[0] == "--shared-status-only")
            {
                SharedStatus();
                Console.WriteLine("PASS " + assertions + " focused shared input status checks; no native, device, game or file writes.");
                return 0;
            }
            Decode(); Consumption(); KeyboardSteerRule(); StatusesAndFailures(); SharedActions(); SharedStatus(); ControlsContractMatches();
            AcceptedSnapshots();
            assertions += BridgeTests.Fake();
            assertions += IsolationTests.Run();
            assertions += NeutralAndJoinTests.Run();
            assertions += IdentityLeaseTests.Run();
            assertions += Ds4IdentityTests.Run();
            assertions += ProfileInfoTests.Decode();
            if (args.Length == 2 && args[0] == "--abi") assertions += AbiProbe.Check(args[1]);
            if (args.Length == 2 && args[0] == "--bridge") assertions += BridgeTests.Actual(args[1]);
            if (args.Length == 2 && args[0] == "--profile-info") assertions += ProfileInfoTests.Actual(args[1]);
            Console.WriteLine("{\"status\":\"passed\",\"assertions\":" + assertions + "}"); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
