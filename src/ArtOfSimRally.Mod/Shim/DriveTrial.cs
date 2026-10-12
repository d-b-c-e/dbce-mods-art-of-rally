using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ArtOfSimRally.Mod.Shim
{
    // Slice C (romi 30328, plan a73b4fe): one bounded latched Core drive at a stage start. Stimuli are raw
    // objects built from the applied profile (Core reserves action-level injection for navigation). Each step
    // is held, then released; every Unity frame's sample pairs the shared applied values, the composition the
    // car's GetInput returned and the car's final input fields after CarController.Update. Pure: the probe
    // supplies samples and an atomic batch writer.
    internal sealed class DriveTrial
    {
        internal const int Steer = 0, Throttle = 1, Brake = 2, Clutch = 3, Handbrake = 4, ShiftUp = 5, ShiftDown = 6, ReverseGate = 8, Gear1 = 23;
        internal static readonly int[] Delivered = { Steer, Throttle, Brake, Handbrake, ShiftUp, ShiftDown };
        // Steps=gears (romi 33299): clutch and H-pattern gates from the owner's saved bindings. The driving axes stay
        // delivered so every release is judged neutral on them too.
        internal static readonly int[] GearsDelivered = { Steer, Throttle, Brake, Clutch, Handbrake, ReverseGate, Gear1, Gear1 + 1, Gear1 + 2, Gear1 + 3, Gear1 + 4, Gear1 + 5, Gear1 + 6, Gear1 + 7 };
        private static readonly string[] DriveRequired = { "steer", "throttle", "brake", "handbrake", "shiftUp", "shiftDown" };
        private static readonly string[] GearsRequired = { "clutch", "reverse", "gear1", "gear3" };
        private static readonly string[] Known = { "steer", "throttle", "brake", "clutch", "handbrake", "shiftUp", "shiftDown", "reverse",
            "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8" };
        internal const int StepFrames = 900, SettleFrames = 20, MinHold = 5;
        internal const float Tolerance = 1e-4f;

        internal sealed class Binding
        {
            internal string Name, Kind, Device;
            internal int Index, Min = 0, Max = 65535, Rest, Travel = 1;
            internal bool Inverted;
        }

        // The applied profile's raw objects for the driving actions, from Core's line grammar
        // `name = axis N dev={..} prod={..} range=a..b rest=r travel=+1` or `name = button N dev={..}`.
        internal static Dictionary<string, Binding> Bindings(string profile) => Bindings(profile, DriveRequired);
        internal static Dictionary<string, Binding> Bindings(string profile, string[] required)
        {
            var found = new Dictionary<string, Binding>(StringComparer.Ordinal);
            foreach (var raw in profile.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.StartsWith("[", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal)) continue;
                string name = line.Substring(0, eq).Trim();
                if (Array.IndexOf(Known, name) < 0) continue;
                var t = line.Substring(eq + 1).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length < 3 || (t[0] != "axis" && t[0] != "button")) throw new FormatException(name + ": expected axis or button");
                var b = new Binding { Name = name, Kind = t[0], Index = int.Parse(t[1], CultureInfo.InvariantCulture) };
                foreach (var token in t)
                {
                    if (token == "inverted") b.Inverted = true;
                    if (token.StartsWith("dev=", StringComparison.Ordinal)) b.Device = token.Substring(4);
                    else if (token.StartsWith("rest=", StringComparison.Ordinal)) b.Rest = int.Parse(token.Substring(5), CultureInfo.InvariantCulture);
                    else if (token.StartsWith("travel=", StringComparison.Ordinal)) b.Travel = token.Substring(7) == "-1" ? -1 : 1;
                    else if (token.StartsWith("range=", StringComparison.Ordinal))
                    {
                        var r = token.Substring(6).Split(new[] { ".." }, StringSplitOptions.None);
                        b.Min = int.Parse(r[0], CultureInfo.InvariantCulture); b.Max = int.Parse(r[1], CultureInfo.InvariantCulture);
                    }
                }
                if (String.IsNullOrEmpty(b.Device)) throw new FormatException(name + ": no dev=");
                found[name] = b;
            }
            foreach (var name in required)
                if (!found.TryGetValue(name, out _)) throw new FormatException("profile does not bind " + name);
            foreach (var pair in found)
            {
                bool button = pair.Key.StartsWith("shift", StringComparison.Ordinal) || pair.Key == "reverse" || pair.Key.StartsWith("gear", StringComparison.Ordinal);
                if (button != (pair.Value.Kind == "button")) throw new FormatException(pair.Key + ": unexpected " + pair.Value.Kind);
            }
            return found;
        }

        // A normalized value as the raw sample Core normalizes back to it: Core's own denormalize (vendor
        // controls dbce_controls.hpp). Steer is centred over the whole range (right positive; rest and travel
        // do not apply); a pedal runs from rest to the travel's end; then the explicit inversion; half away
        // from zero, clamped to the range.
        internal static int Raw(Binding b, float value)
        {
            double lo = b.Min, hi = b.Max, n = value, v;
            if (b.Name == "steer")
            {
                n = Math.Max(-1.0, Math.Min(1.0, b.Inverted ? -n : n));
                v = (lo + hi) / 2.0 + n * ((hi - lo) / 2.0);
            }
            else
            {
                double full = b.Travel > 0 ? hi : lo, rest = b.Rest;
                n = Math.Max(0.0, Math.Min(1.0, n));
                v = rest + (b.Inverted ? 1.0 - n : n) * (full - rest);
            }
            if (v <= lo) return b.Min;
            if (v >= hi) return b.Max;
            return (int)Math.Round(v, MidpointRounding.AwayFromZero);
        }
        internal static string Axis(Binding b, float value, int ms) =>
            "inject raw axis " + b.Index + " dev=" + b.Device + " value=" + Raw(b, value) + " ms=" + ms + " range=" + b.Min + ".." + b.Max;
        internal static string Button(Binding b, int ms) => "inject raw button " + b.Index + " dev=" + b.Device + " value=1 ms=" + ms;

        internal sealed class Sample
        {
            internal int Frame;
            internal bool Underway;   // the event is UNDERWAY (no game override of the inputs)
            internal bool Composed;   // the shared composition ran in this frame's GetInput
            internal bool SteerLive;
            internal float AppliedSteer, AppliedThrottle, AppliedBrake, AppliedHandbrake, Alignment;
            internal float OutSteer, OutThrottle, OutBrake, OutHandbrake;
            internal float EngineSteer, EngineThrottle, EngineBrake, EngineHandbrake;
            internal float AppliedClutch, OutClutch, EngineClutch;
            internal bool[] Injected = new bool[ShimFrame.Count];
            internal int Gear, MaxGear = int.MaxValue;   // MaxGear: this car's highest gear-ratio index
            internal bool HPattern;                      // the session's profile transmission is H-pattern
            internal bool Automatic, ChangingGear;
            internal float Speed;
        }

        // What the latched open's fence byte asserts for a drive, each fact observed in the running game.
        internal sealed class Fences
        {
            internal bool NoForceArmed { get; set; }
            internal bool SettingsWritesSuppressed { get; set; }
            internal bool TelemetryIdle { get; set; }
            internal string MissingHooks { get; set; }
            internal string MissingGuards { get; set; }   // the recorder's upload/achievement/save/telemetry guards
            internal bool Compatible { get; set; }
            internal bool Custom { get; set; }            // a CUSTOM event: no career, daily or weekly progress
            internal bool StageReady { get; set; }        // the car held at the start or stationary
            internal bool Focused { get; set; }
        }
        internal static string Unfenced(Fences f)
        {
            if (f == null) return "fences not observed";
            if (!f.NoForceArmed) return "native no-force latch not armed";
            if (!f.SettingsWritesSuppressed) return "settings writes are not suppressed";
            if (!f.TelemetryIdle) return "telemetry is sending";
            if (!String.IsNullOrEmpty(f.MissingHooks)) return "input hooks missing: " + f.MissingHooks;
            if (!String.IsNullOrEmpty(f.MissingGuards)) return "capture guards missing: " + f.MissingGuards;
            if (!f.Compatible) return "game build differs from the inspected action map";
            if (!f.Custom) return "not a CUSTOM event";
            if (!f.StageReady) return "car not held at the start or stationary";
            if (!f.Focused) return "game window not focused";
            return null;
        }
        // Null to open now. Otherwise the unmet fence, with wait set when only the stage start or focus is
        // still pending (the caller bounds the wait); any other unmet fence refuses. Before the stage the
        // event's mode is not set yet (the menus run with no game mode), so CUSTOM is waited for with the
        // stage start; at the stage a non-CUSTOM event refuses (first Drive run, 05:38).
        internal static string Admission(Fences f, out bool wait)
        {
            string unmet = Unfenced(f);
            wait = false;
            if (unmet == null || f == null || (f.StageReady && f.Focused)) return unmet;
            var later = new Fences { NoForceArmed = f.NoForceArmed, SettingsWritesSuppressed = f.SettingsWritesSuppressed, TelemetryIdle = f.TelemetryIdle,
                MissingHooks = f.MissingHooks, MissingGuards = f.MissingGuards, Compatible = f.Compatible, Custom = f.StageReady ? f.Custom : true, StageReady = true, Focused = true };
            wait = Unfenced(later) == null;
            return unmet;
        }

        internal sealed class Step
        {
            internal string Name;
            internal string[] Commands;
            internal int Primary, Sign, GearDelta;
            internal int HoldSamples, ReleaseSamples, GearBefore = int.MinValue, GearAfter = int.MinValue;
            // Gate steps: the gear expected while held (an out-of-range gate: the gear it started in, never clamped)
            // and after release (neutral: no gate held).
            internal int? GateGear, ReleaseGear;
            internal bool OutOfRange;
            internal int GateFrames;
            internal string Result, Detail;
        }

        internal enum State { Admission, Holding, Releasing, Ending, Done }
        private readonly ShimAxisConsumer consumer;
        private readonly Action<string> writeBatch;
        private readonly string nonce;
        private int batch, frames, settled, cleanRun, badRelease, lastFrame = -1;
        private bool seen;
        internal readonly List<Step> Steps = new List<Step>();
        internal readonly bool Gears;
        internal readonly int[] DeliveredActions;
        private readonly Dictionary<string, Binding> gates = new Dictionary<string, Binding>(StringComparer.Ordinal);
        internal State Current { get; private set; } = State.Admission;
        internal int StepIndex { get; private set; }
        internal string Failure { get; private set; }
        internal readonly StringBuilder Samples = new StringBuilder("step\tframe\tunderway\tcomposed\tappliedSteer\tappliedThrottle\tappliedBrake\tappliedHandbrake\talignment\toutSteer\toutThrottle\toutBrake\toutHandbrake\tengineSteer\tengineThrottle\tengineBrake\tengineHandbrake\tinjected\tgear\tautomatic\tspeed\tappliedClutch\toutClutch\tengineClutch\tmaxGear\n");

        internal DriveTrial(ShimAxisConsumer consumer, string profile, Action<string> writeBatch, string nonce, bool gears = false)
        {
            this.consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            this.writeBatch = writeBatch ?? throw new ArgumentNullException(nameof(writeBatch));
            this.nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));
            Gears = gears;
            DeliveredActions = gears ? GearsDelivered : Delivered;
            if (gears)
            {
                // Clutch held alone; each gate held then released (no gate held is neutral); then the lowest bound gate
                // this car lacks, chosen from the live gear range when its step starts.
                var g = Bindings(profile, GearsRequired);
                foreach (var pair in g) if (pair.Key.StartsWith("gear", StringComparison.Ordinal)) gates[pair.Key] = pair.Value;
                Steps.Add(new Step { Name = "clutch 60%", Commands = new[] { Axis(g["clutch"], .6f, 1500) }, Primary = Clutch, Sign = 1 });
                Steps.Add(Gate("gear1 gate", g["gear1"], Gear1, ShimTransmission.FirstGear));
                Steps.Add(Gate("gear3 gate", g["gear3"], Gear1 + 2, ShimTransmission.FirstGear + 2));
                Steps.Add(Gate("reverse gate", g["reverse"], ReverseGate, ShimTransmission.Reverse));
                Steps.Add(new Step { Name = "out-of-range gate", OutOfRange = true, Sign = 1, ReleaseGear = ShimTransmission.Neutral });
                return;
            }
            var b = Bindings(profile);
            string Throttle40(int ms) => Axis(b["throttle"], .4f, ms);
            Steps.Add(new Step { Name = "throttle 40%", Commands = new[] { Throttle40(1500) }, Primary = Throttle, Sign = 1 });
            Steps.Add(new Step { Name = "steer -30% with throttle", Commands = new[] { Throttle40(900), Axis(b["steer"], -.3f, 800) }, Primary = Steer, Sign = -1 });
            Steps.Add(new Step { Name = "steer +30% with throttle", Commands = new[] { Throttle40(900), Axis(b["steer"], .3f, 800) }, Primary = Steer, Sign = 1 });
            Steps.Add(new Step { Name = "shiftUp with throttle", Commands = new[] { Throttle40(900), Button(b["shiftUp"], 200) }, Primary = ShiftUp, Sign = 1, GearDelta = 1 });
            Steps.Add(new Step { Name = "shiftDown", Commands = new[] { Button(b["shiftDown"], 200) }, Primary = ShiftDown, Sign = 1, GearDelta = -1 });
            Steps.Add(new Step { Name = "brake 60%", Commands = new[] { Axis(b["brake"], .6f, 800) }, Primary = Brake, Sign = 1 });
            Steps.Add(new Step { Name = "handbrake 100%", Commands = new[] { Axis(b["handbrake"], 1f, 800) }, Primary = Handbrake, Sign = 1 });
        }

        internal bool Finished => Current == State.Done;
        // Passed only when every row passed; an automatic gearbox leaves the gear rows open, not passed.
        internal bool Passed => Finished && Failure == null && Steps.TrueForAll(s => s.Result == "passed");

        // Once per Unity frame, after CarController.Update, with that frame's sample.
        internal void Tick(Sample s)
        {
            if (Current == State.Done || s.Frame == lastFrame) return;
            lastFrame = s.Frame;
            if (consumer.Terminal) { Fail("shared session stopped: " + consumer.Status); return; }
            if (++frames > StepFrames) { Fail("no progress in " + Current + (Current == State.Holding || Current == State.Releasing ? " of " + Steps[StepIndex].Name : "") + " within " + StepFrames + " frames"); return; }
            var step = StepIndex < Steps.Count ? Steps[StepIndex] : null;
            Record(Current == State.Admission ? null : step, s);
            switch (Current)
            {
                case State.Admission:
                    // Core admitted, the countdown is over and no game override applies.
                    if (!consumer.Started || consumer.OriginalFrame.Run != LatchedTrial.Waiting || !s.Underway) return;
                    Begin(s);
                    return;
                case State.Holding:
                    if (!s.Injected[step.Primary]) { if (seen) { Current = State.Releasing; settled = 0; } return; }
                    seen = true;
                    string hold = Check(step, s, true) ?? GateHold(step, s);
                    if (hold != null) { step.Result = "failed"; step.Detail = hold; Fail(step.Name + ": " + hold); return; }
                    step.HoldSamples++;
                    return;
                case State.Releasing:
                    // Judged only once every injection of the step has ended (and a gear change completed).
                    bool anyInjected = false; foreach (var i in DeliveredActions) anyInjected |= s.Injected[i];
                    if (anyInjected || ((step.GearDelta != 0 || step.GateGear.HasValue) && s.ChangingGear)) { cleanRun = 0; return; }
                    // romi 30996: SettleFrames consecutive clean samples; a bad sample restarts the run, and
                    // bad samples are counted for the whole release (never reset), so flicker fails.
                    string release = Check(step, s, false);
                    if (release != null)
                    {
                        cleanRun = 0;
                        if (++badRelease > SettleFrames) { step.Result = "failed"; step.Detail = release; Fail(step.Name + " release: " + release); }
                        return;
                    }
                    step.ReleaseSamples = ++cleanRun;
                    if (cleanRun < SettleFrames) return;
                    Close(step, s);
                    return;
                case State.Ending:
                    if (consumer.OriginalFrame.Run == LatchedTrial.Completed && ++settled >= 2 * SettleFrames) Current = State.Done;
                    return;
            }
        }

        private static Step Gate(string name, Binding binding, int action, int gear) =>
            new Step { Name = name, Commands = new[] { Button(binding, 1000) }, Primary = action, Sign = 1, GateGear = gear, ReleaseGear = ShimTransmission.Neutral };
        private void Begin(Sample s)
        {
            if (Gears && !s.HPattern) { Fail("the profile's transmission is not H-pattern: gates are not applied"); return; }
            if (Gears && s.Automatic) { Fail("automatic gearbox: shared gates are ignored by policy"); return; }
            Start(s);
        }
        // Starts the current step, or ends the drive. An out-of-range gate is resolved now from the car's live gear range:
        // the lowest bound gearN whose index (first gear + N - 1) this car lacks. None bound: the step stays open.
        private void Start(Sample s)
        {
            while (StepIndex < Steps.Count && Steps[StepIndex].OutOfRange && Steps[StepIndex].Commands == null)
            {
                var step = Steps[StepIndex];
                for (int n = 1; n <= ShimTransmission.Gates && step.Commands == null; n++)
                    if (gates.TryGetValue("gear" + n, out var binding) && ShimTransmission.FirstGear + n - 1 > s.MaxGear)
                    { step.Name = "gear" + n + " gate beyond this car"; step.Commands = new[] { Button(binding, 1000) }; step.Primary = Gear1 + n - 1; }
                if (step.Commands != null) break;
                step.Result = "open"; step.Detail = "every bound gate exists on this car (max gear index " + s.MaxGear + ")";
                StepIndex++;
            }
            if (StepIndex >= Steps.Count) { Send("end", State.Ending); return; }
            var current = Steps[StepIndex];
            current.GearBefore = s.Gear;
            if (current.OutOfRange) current.GateGear = s.Gear;
            // A gate step only shows the gate if the car is not already in that gear.
            else if (current.GateGear.HasValue && s.Gear == current.GateGear) { current.Result = "failed"; current.Detail = "already in gear " + s.Gear + " before the gate"; Fail(current.Name + ": " + current.Detail); return; }
            Send(String.Join("\n", current.Commands), State.Holding);
        }
        private void Close(Step step, Sample s)
        {
            step.GearAfter = s.Gear;
            if (step.HoldSamples < MinHold) { step.Result = "failed"; step.Detail = "only " + step.HoldSamples + " held samples"; Fail(step.Name + ": " + step.Detail); return; }
            if (step.GateGear.HasValue)
            {
                if (step.GateFrames < MinHold) { step.Result = "failed"; step.Detail = "gear " + step.GateGear + " held on only " + step.GateFrames + " settled frames"; Fail(step.Name + ": " + step.Detail); return; }
                if (s.Gear != step.ReleaseGear) { step.Result = "failed"; step.Detail = "after release gear " + s.Gear + ", expected neutral " + step.ReleaseGear; Fail(step.Name + ": " + step.Detail); return; }
                step.Result = "passed";
            }
            else if (step.GearDelta != 0)
            {
                if (s.Automatic) { step.Result = "open"; step.Detail = "automatic gearbox: shared gear actions are ignored by policy"; }
                else if (step.GearAfter - step.GearBefore != step.GearDelta) { step.Result = "failed"; step.Detail = "gear " + step.GearBefore + " -> " + step.GearAfter; Fail(step.Name + ": " + step.Detail); return; }
                else step.Result = "passed";
            }
            else step.Result = "passed";
            StepIndex++;
            Start(s);
        }

        // Hold: the primary action is applied with the step's sign. Always: the composition is exactly
        // Art's for these applied values, and the car kept it.
        private static string Check(Step step, Sample s, bool holding)
        {
            if (!s.Underway) return "event left UNDERWAY";
            if (!s.Composed) return "no shared composition in this frame";
            if (holding && step.GearDelta == 0 && !step.GateGear.HasValue && !step.OutOfRange)
            {
                float applied = step.Primary == Steer ? s.AppliedSteer : step.Primary == Throttle ? s.AppliedThrottle : step.Primary == Brake ? s.AppliedBrake : step.Primary == Clutch ? s.AppliedClutch : s.AppliedHandbrake;
                if (applied == 0f || Math.Sign(applied) != step.Sign) return "applied " + Name(step.Primary) + " " + applied.ToString("R", CultureInfo.InvariantCulture) + " has the wrong sign";
            }
            if (!holding)
            {
                // Every driving axis, not only the step's primary: a companion (the throttle under a steer or
                // shift step) must be neutral after its injection too.
                if (s.AppliedSteer != 0f) return "steer still applied";
                if (s.AppliedThrottle != 0f) return "throttle still applied";
                if (s.AppliedBrake != 0f) return "brake still applied";
                if (s.AppliedHandbrake != 0f) return "handbrake still applied";
                if (s.AppliedClutch != 0f) return "clutch still applied";
            }
            if (Math.Abs(s.OutClutch - Math.Max(0f, s.AppliedClutch)) > Tolerance) return "composed clutch " + s.OutClutch + " applied " + s.AppliedClutch;
            if (Math.Abs(s.EngineClutch - s.OutClutch) > Tolerance) return "car clutch " + s.EngineClutch + " differs from the composition " + s.OutClutch;
            float steer = s.AppliedSteer != 0f ? s.AppliedSteer + s.Alignment : s.SteerLive ? s.Alignment : 0f;
            if (Math.Abs(s.OutSteer - steer) > Tolerance) return "composed steer " + s.OutSteer + " expected " + steer;
            if (Math.Abs(s.OutThrottle - Math.Max(0f, s.AppliedThrottle)) > Tolerance) return "composed throttle " + s.OutThrottle + " applied " + s.AppliedThrottle;
            if (Math.Abs(s.OutBrake - Math.Max(0f, s.AppliedBrake)) > Tolerance) return "composed brake " + s.OutBrake + " applied " + s.AppliedBrake;
            if (Math.Abs(s.OutHandbrake - Math.Max(0f, s.AppliedHandbrake)) > Tolerance) return "composed handbrake " + s.OutHandbrake + " applied " + s.AppliedHandbrake;
            if (Math.Abs(s.EngineSteer - s.OutSteer) > Tolerance || Math.Abs(s.EngineThrottle - s.OutThrottle) > Tolerance ||
                Math.Abs(s.EngineBrake - s.OutBrake) > Tolerance || Math.Abs(s.EngineHandbrake - s.OutHandbrake) > Tolerance)
                return "car inputs differ from the composition" + (s.Automatic ? " (automatic gearbox: Art swaps throttle and brake when nearly stopped)" : "");
            return null;
        }
        // A held gate reaches its gear once the change settles, and stays there; an out-of-range gate never moves the
        // gear (no clamping onto another gear).
        private static string GateHold(Step step, Sample s)
        {
            if (!step.GateGear.HasValue) return null;
            if (step.OutOfRange && (s.ChangingGear || s.Gear != step.GateGear)) return "a gate this car lacks moved the gear " + step.GateGear + " -> " + s.Gear;
            if (s.ChangingGear) return null;
            if (s.Gear == step.GateGear) { step.GateFrames++; return null; }
            return step.GateFrames > 0 ? "gear left " + step.GateGear + " for " + s.Gear + " while the gate was held" : null;
        }
        private static string Name(int action) => ShimFrame.Names[action];

        private void Record(Step step, Sample s)
        {
            var injected = new StringBuilder();
            for (int i = 0; i < s.Injected.Length; i++) if (s.Injected[i]) injected.Append(injected.Length == 0 ? "" : ",").Append(Name(i));
            string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
            Samples.Append(step == null ? "-" : step.Name).Append('\t').Append(s.Frame).Append('\t').Append(s.Underway ? 1 : 0).Append('\t').Append(s.Composed ? 1 : 0)
                .Append('\t').Append(F(s.AppliedSteer)).Append('\t').Append(F(s.AppliedThrottle)).Append('\t').Append(F(s.AppliedBrake)).Append('\t').Append(F(s.AppliedHandbrake))
                .Append('\t').Append(F(s.Alignment)).Append('\t').Append(F(s.OutSteer)).Append('\t').Append(F(s.OutThrottle)).Append('\t').Append(F(s.OutBrake)).Append('\t').Append(F(s.OutHandbrake))
                .Append('\t').Append(F(s.EngineSteer)).Append('\t').Append(F(s.EngineThrottle)).Append('\t').Append(F(s.EngineBrake)).Append('\t').Append(F(s.EngineHandbrake))
                .Append('\t').Append(injected.Length == 0 ? "-" : injected.ToString()).Append('\t').Append(s.Gear).Append('\t').Append(s.Automatic ? 1 : 0).Append('\t').Append(F(s.Speed))
                .Append('\t').Append(F(s.AppliedClutch)).Append('\t').Append(F(s.OutClutch)).Append('\t').Append(F(s.EngineClutch)).Append('\t').Append(s.MaxGear).Append('\n');
        }
        private void Send(string commands, State next)
        {
            writeBatch("nonce=" + nonce + "\nbatch=" + (++batch) + "\n" + commands + "\n");
            Current = next; frames = 0; settled = 0; cleanRun = 0; badRelease = 0; seen = false;
        }
        private void Fail(string why) { if (Failure == null) Failure = why; Current = State.Done; }
    }
}
