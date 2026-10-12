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
        internal const int Steer = 0, Throttle = 1, Brake = 2, Handbrake = 4, ShiftUp = 5, ShiftDown = 6;
        internal static readonly int[] Delivered = { Steer, Throttle, Brake, Handbrake, ShiftUp, ShiftDown };
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
        internal static Dictionary<string, Binding> Bindings(string profile)
        {
            var found = new Dictionary<string, Binding>(StringComparer.Ordinal);
            foreach (var raw in profile.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.StartsWith("[", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal)) continue;
                string name = line.Substring(0, eq).Trim();
                if (name != "steer" && name != "throttle" && name != "brake" && name != "handbrake" && name != "shiftUp" && name != "shiftDown") continue;
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
            foreach (var name in new[] { "steer", "throttle", "brake", "handbrake", "shiftUp", "shiftDown" })
            {
                if (!found.TryGetValue(name, out var b)) throw new FormatException("profile does not bind " + name);
                if ((name.StartsWith("shift", StringComparison.Ordinal)) != (b.Kind == "button")) throw new FormatException(name + ": unexpected " + b.Kind);
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
            internal bool[] Injected = new bool[7];
            internal int Gear;
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
        // still pending (the caller bounds the wait); any other unmet fence refuses.
        internal static string Admission(Fences f, out bool wait)
        {
            string unmet = Unfenced(f);
            wait = false;
            if (unmet == null || f == null || (f.StageReady && f.Focused)) return unmet;
            var later = new Fences { NoForceArmed = f.NoForceArmed, SettingsWritesSuppressed = f.SettingsWritesSuppressed, TelemetryIdle = f.TelemetryIdle,
                MissingHooks = f.MissingHooks, MissingGuards = f.MissingGuards, Compatible = f.Compatible, Custom = f.Custom, StageReady = true, Focused = true };
            wait = Unfenced(later) == null;
            return unmet;
        }

        internal sealed class Step
        {
            internal string Name;
            internal string[] Commands;
            internal int Primary, Sign, GearDelta;
            internal int HoldSamples, ReleaseSamples, GearBefore = int.MinValue, GearAfter = int.MinValue;
            internal string Result, Detail;
        }

        internal enum State { Admission, Holding, Releasing, Ending, Done }
        private readonly ShimAxisConsumer consumer;
        private readonly Action<string> writeBatch;
        private readonly string nonce;
        private int batch, frames, settled, lastFrame = -1;
        private bool seen;
        internal readonly List<Step> Steps = new List<Step>();
        internal State Current { get; private set; } = State.Admission;
        internal int StepIndex { get; private set; }
        internal string Failure { get; private set; }
        internal readonly StringBuilder Samples = new StringBuilder("step\tframe\tunderway\tcomposed\tappliedSteer\tappliedThrottle\tappliedBrake\tappliedHandbrake\talignment\toutSteer\toutThrottle\toutBrake\toutHandbrake\tengineSteer\tengineThrottle\tengineBrake\tengineHandbrake\tinjected\tgear\tautomatic\tspeed\n");

        internal DriveTrial(ShimAxisConsumer consumer, string profile, Action<string> writeBatch, string nonce)
        {
            this.consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            this.writeBatch = writeBatch ?? throw new ArgumentNullException(nameof(writeBatch));
            this.nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));
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
                    string hold = Check(step, s, true);
                    if (hold != null) { step.Result = "failed"; step.Detail = hold; Fail(step.Name + ": " + hold); return; }
                    step.HoldSamples++;
                    return;
                case State.Releasing:
                    // Every injection of the step has ended; then the menu-free release must read neutral.
                    bool anyInjected = false; foreach (var i in Delivered) anyInjected |= s.Injected[i];
                    if (anyInjected || (step.GearDelta != 0 && s.ChangingGear)) { settled = 0; return; }
                    string release = Check(step, s, false);
                    if (release != null) { if (++settled > SettleFrames) { step.Result = "failed"; step.Detail = release; Fail(step.Name + " release: " + release); } return; }
                    step.ReleaseSamples++;
                    if (++settled < SettleFrames) return;
                    Close(step, s);
                    return;
                case State.Ending:
                    if (consumer.OriginalFrame.Run == LatchedTrial.Completed && ++settled >= 2 * SettleFrames) Current = State.Done;
                    return;
            }
        }

        private void Begin(Sample s)
        {
            var step = Steps[StepIndex];
            step.GearBefore = s.Gear;
            Send(String.Join("\n", step.Commands), State.Holding);
        }
        private void Close(Step step, Sample s)
        {
            step.GearAfter = s.Gear;
            if (step.HoldSamples < MinHold) { step.Result = "failed"; step.Detail = "only " + step.HoldSamples + " held samples"; Fail(step.Name + ": " + step.Detail); return; }
            if (step.GearDelta != 0)
            {
                if (s.Automatic) { step.Result = "open"; step.Detail = "automatic gearbox: shared gear actions are ignored by policy"; }
                else if (step.GearAfter - step.GearBefore != step.GearDelta) { step.Result = "failed"; step.Detail = "gear " + step.GearBefore + " -> " + step.GearAfter; Fail(step.Name + ": " + step.Detail); return; }
                else step.Result = "passed";
            }
            else step.Result = "passed";
            if (++StepIndex < Steps.Count) { Steps[StepIndex].GearBefore = s.Gear; Send(String.Join("\n", Steps[StepIndex].Commands), State.Holding); }
            else Send("end", State.Ending);
        }

        // Hold: the primary action is applied with the step's sign. Always: the composition is exactly
        // Art's for these applied values, and the car kept it.
        private static string Check(Step step, Sample s, bool holding)
        {
            if (!s.Underway) return "event left UNDERWAY";
            if (!s.Composed) return "no shared composition in this frame";
            if (holding && step.GearDelta == 0)
            {
                float applied = step.Primary == Steer ? s.AppliedSteer : step.Primary == Throttle ? s.AppliedThrottle : step.Primary == Brake ? s.AppliedBrake : s.AppliedHandbrake;
                if (applied == 0f || Math.Sign(applied) != step.Sign) return "applied " + Name(step.Primary) + " " + applied.ToString("R", CultureInfo.InvariantCulture) + " has the wrong sign";
            }
            if (!holding)
            {
                if (step.Primary == Steer && s.AppliedSteer != 0f) return "steer still applied";
                if (step.Primary == Throttle && s.AppliedThrottle != 0f) return "throttle still applied";
                if (step.Primary == Brake && s.AppliedBrake != 0f) return "brake still applied";
                if (step.Primary == Handbrake && s.AppliedHandbrake != 0f) return "handbrake still applied";
            }
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
        private static string Name(int action) => ShimFrame.Names[action];

        private void Record(Step step, Sample s)
        {
            var injected = new StringBuilder();
            foreach (var i in Delivered) if (s.Injected[i]) injected.Append(injected.Length == 0 ? "" : ",").Append(Name(i));
            string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);
            Samples.Append(step == null ? "-" : step.Name).Append('\t').Append(s.Frame).Append('\t').Append(s.Underway ? 1 : 0).Append('\t').Append(s.Composed ? 1 : 0)
                .Append('\t').Append(F(s.AppliedSteer)).Append('\t').Append(F(s.AppliedThrottle)).Append('\t').Append(F(s.AppliedBrake)).Append('\t').Append(F(s.AppliedHandbrake))
                .Append('\t').Append(F(s.Alignment)).Append('\t').Append(F(s.OutSteer)).Append('\t').Append(F(s.OutThrottle)).Append('\t').Append(F(s.OutBrake)).Append('\t').Append(F(s.OutHandbrake))
                .Append('\t').Append(F(s.EngineSteer)).Append('\t').Append(F(s.EngineThrottle)).Append('\t').Append(F(s.EngineBrake)).Append('\t').Append(F(s.EngineHandbrake))
                .Append('\t').Append(injected.Length == 0 ? "-" : injected.ToString()).Append('\t').Append(s.Gear).Append('\t').Append(s.Automatic ? 1 : 0).Append('\t').Append(F(s.Speed)).Append('\n');
        }
        private void Send(string commands, State next)
        {
            writeBatch("nonce=" + nonce + "\nbatch=" + (++batch) + "\n" + commands + "\n");
            Current = next; frames = 0; settled = 0; seen = false;
        }
        private void Fail(string why) { if (Failure == null) Failure = why; Current = State.Done; }
    }
}
