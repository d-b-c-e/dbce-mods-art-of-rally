using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ArtOfSimRally.Testing
{
    // carInput=native (romi 31616): at the stage the capture driver scripts the Rewired values Art's own
    // AxisCarController reads, standing in for a keyboard, while an ordinary (non-latched) shared session owns the
    // wheel at rest. native.tape holds, per player GetInput call, the script, Art's own result (before any mod
    // postfix), the final result (after the mod's shared composition) and that frame's shared state.
    // Scripted Rewired native-path evidence only: not physical keyboard, wheel or force feedback qualification.
    internal static class NativeEvidence
    {
        internal const string Header = "frame\tmarker\tfocused\tsSteer\tsThrottle\tsBrake\tsHandbrake\t" +
            "artSteer\tartThrottle\tartBrake\tartHandbrake\tartClutch\tartStart\tsteer\tthrottle\tbrake\thandbrake\tclutch\tstart\t" +
            "composed\tisolated\tsteerLive\tappliedSteer\tappliedThrottle\tappliedBrake\tappliedHandbrake\talignment";
        private static readonly string[] Columns = Header.Split('\t');
        internal const int MinRows = 300;
        private const int Script = 3, Art = 7, Final = 13, Composed = 19, Isolated = 20, SteerLive = 21, Applied = 22, Alignment = 26;

        // The stage script by seconds since UNDERWAY. Every value clears Art's largest deadzone (0.75, a
        // 0 setting); each action is held alone; the tail releases everything. The car stays near the start:
        // a wheel resting off centre steers it, and at 06:52 an 11 s throttle drove it off the road into two of
        // Art's out-of-bounds resets, which force the car's handbrake outside GetInput.
        internal static Dictionary<string, string> At(float seconds)
        {
            float steer = 0, throttle = 0, brake = 0, handbrake = 0;
            if (seconds < 1.5f) throttle = 0.8f;
            else if (seconds < 2.5f) brake = 0.8f;
            else if (seconds < 4f) handbrake = 1f;
            else if (seconds < 5.5f) steer = 0.8f;
            else if (seconds < 7f) steer = -0.8f;
            var values = new Dictionary<string, string>();
            void Put(string action, float value) { if (value != 0f) values["R0.GetAxisRaw.s" + action] = value.ToString("R", CultureInfo.InvariantCulture); }
            Put(AxisNames[0], steer); Put(AxisNames[1], throttle); Put(AxisNames[2], brake); Put(AxisNames[3], handbrake);
            return values;
        }
        // AxisCarController's Rewired actions for steer, throttle, brake and handbrake.
        internal static readonly string[] AxisNames = { "Horizontal", "Throttle", "Brake", "Handbrake" };

        // Passes only when, on every stage frame, the shared composition ran with Rewired isolation verified and
        // live steering; the wheel stayed at rest (owned pedals and handbrake neutral, steering constant); Art read
        // the script through its own processing; and the final result is the ordinary composition (ShimAxisConsumer
        // .Compose) of Art's result with the wheel's: steering is the wheel's plus Art's alignment term whenever the
        // wheel is off centre (without force a wheel rests where it was left: 0.1407 in the 02:11 Apply receipt) and
        // Art's at exact centre; throttle, brake and handbrake are the larger, so Art's with the pedals at rest;
        // clutch and start are Art's. The script must reach steering both ways, every pedal action and the release;
        // from the first matching tick on, the car's input fields carry composed results (automatic reverse may
        // swap throttle and brake).
        internal static string Evaluate(IEnumerable<string> nativeLines, IEnumerable<string> carLines, Func<int, bool> underway, out bool passed)
        {
            passed = false;
            var finals = new HashSet<string>(StringComparer.Ordinal);
            var unbraked = new HashSet<string>(StringComparer.Ordinal); // the same without handbrake, for naming Art's resets
            int rows = 0, right = 0, left = 0, throttle = 0, brake = 0, handbrake = 0, released = 0, wheelSteered = 0, artSteered = 0;
            float rest = 0f;
            bool header = true;
            foreach (var line in nativeLines)
            {
                if (header) { if (line != Header) return "native.tape header differs"; header = false; continue; }
                var f = line.Split('\t');
                if (f.Length != Columns.Length || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int marker)) return "malformed native row: " + line;
                if (!underway(marker)) continue;
                rows++;
                string at = " at frame " + f[0];
                if (f[2] != "1") return "game unfocused" + at;
                if (f[Composed] != "1") return "the shared composition did not run" + at;
                if (f[Isolated] != "1") return "Rewired isolation unverified" + at;
                if (f[SteerLive] != "1") return "shared steering not live" + at;
                float wheel = Value(f[Applied]);
                if (rows == 1) rest = wheel;
                else if (Math.Abs(wheel - rest) > 1e-3f) return "shared steering moved from " + rest.ToString("R", CultureInfo.InvariantCulture) + " to " + f[Applied] + " (wheel not at rest)" + at;
                for (int i = Applied + 1; i < Alignment; i++) if (Value(f[i]) != 0f) return "shared " + Columns[i] + " " + f[i] + " (wheel not at rest)" + at;
                float alignment = Value(f[Alignment]);
                if (!Near(Value(f[Art]), Value(f[Script]) + alignment) || !Near(Value(f[Art + 1]), Value(f[Script + 1])) ||
                    !Near(Value(f[Art + 2]), Value(f[Script + 2])) || !Near(Value(f[Art + 3]), Math.Abs(Value(f[Script + 3]))))
                    return "Art's GetInput result differs from the script" + at;
                if (wheel != 0f ? !Near(Value(f[Final]), wheel + alignment) : f[Final] != f[Art])
                    return "steer " + f[Final] + " is not the ordinary composition of Art's " + f[Art] + " and the wheel's " + f[Applied] + at;
                for (int i = 1; i < 6; i++) if (f[Art + i] != f[Final + i]) return "the shared path changed Art's " + Columns[Final + i] + ": " + f[Art + i] + " -> " + f[Final + i] + at;
                float steer = Value(f[Art]) - alignment;
                if (steer != 0f) { if (wheel != 0f) wheelSteered++; else artSteered++; }
                if (steer > 0f) right++; else if (steer < 0f) left++;
                if (Value(f[Art + 1]) > 0f) throttle++;
                if (Value(f[Art + 2]) > 0f) brake++;
                if (Value(f[Art + 3]) > 0f) handbrake++;
                if (Enumerable.Range(Script, 4).All(i => Value(f[i]) == 0f) && Value(f[Final + 1]) == 0f && Value(f[Final + 2]) == 0f && Value(f[Final + 3]) == 0f) released++;
                finals.Add(Key(f[Final], f[Final + 1], f[Final + 2], f[Final + 3]));
                finals.Add(Key(f[Final], f[Final + 2], f[Final + 1], f[Final + 3]));
                unbraked.Add(Key(f[Final], f[Final + 1], f[Final + 2], "")); unbraked.Add(Key(f[Final], f[Final + 2], f[Final + 1], ""));
            }
            if (header) return "native.tape is empty";
            if (rows < MinRows) return "only " + rows + " stage frames";
            if (right == 0 || left == 0 || throttle == 0 || brake == 0 || handbrake == 0 || released == 0)
                return "the script did not reach every action: right " + right + ", left " + left + ", throttle " + throttle + ", brake " + brake + ", handbrake " + handbrake + ", released " + released;
            int ticks = 0, matched = 0, leading = 0;
            header = true;
            foreach (var line in carLines)
            {
                if (header) { header = false; continue; }
                var c = line.Split('\t');
                if (c.Length < 6 || !int.TryParse(c[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int marker)) return "malformed car row: " + line;
                if (!underway(marker)) continue;
                ticks++;
                if (finals.Contains(Key(c[2], c[3], c[4], c[5]))) matched++;
                else if (matched > 0) return "car input at step " + c[0] + " is no composed result" +
                    (c[5] == "1" && unbraked.Contains(Key(c[2], c[3], c[4], "")) ? " (handbrake forced over a composed result: Art's out-of-bounds reset?)" : "");
                else leading++;
            }
            if (matched == 0) return "no car tick carried a composed result";
            passed = true;
            string steering = rest != 0f
                ? "the wheel at rest off centre (shared steer " + rest.ToString("R", CultureInfo.InvariantCulture) + ") held steering on " + wheelSteered + " scripted-steer frames"
                : "the wheel at centre passed Art's steering on " + artSteered + " scripted-steer frames";
            return "passed: " + rows + " stage frames composed with isolation and live steering; " + steering +
                "; pedals and handbrake at rest passed Art's result (right " + right + ", left " + left + ", throttle " + throttle + ", brake " + brake +
                ", handbrake " + handbrake + ", released " + released + "); car ticks " + matched + " matched, " + leading +
                " leading start-line ticks; scripted Rewired native-path evidence only";
        }
        private static float Value(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
        private static bool Near(float a, float b) => Math.Abs(a - b) <= 1e-5f;
        private static string Key(string steer, string throttle, string brake, string handbrake) => steer + "|" + throttle + "|" + brake + "|" + handbrake;
    }
}
