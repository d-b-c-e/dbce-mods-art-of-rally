namespace CrashFeel;

enum StepKind { Constant, Periodic, Stream }

// One command in a kick. Constant uses a pre-created finite slot of SlotMs;
// Periodic a pre-created sine slot (Hz, SlotMs); Stream adds Magnitude to the
// steering force for ForMs, sent at the game's ~60 Hz update rate.
readonly record struct Step(int AtMs, StepKind Kind, float Magnitude, int SlotMs = 0, int Hz = 0,
    int PhaseHundredths = 0, int FadeMs = 0, int ForMs = 0)
{
    public int EndMs => AtMs + (Kind == StepKind.Stream ? ForMs : SlotMs);
}

sealed record Candidate(char Key, string Name, string Detail, Func<float, int, Step[]> Build);

static class Candidates
{
    // The native pool has four constant slots, so these are all the durations on offer.
    public static readonly int[] ConstantSlots = { 60, 120, 250, 400 };
    public static readonly (int Hz, int Ms)[] PeriodicSlots = { (12, 300), (25, 250) };
    public static readonly int[] Strengths = { 25, 50, 75, 100 };
    public const float LoadMagnitude = 0.2f;

    static Step Push(int at, int ms, float signed) => new(at, StepKind.Constant, signed, SlotMs: ms);
    static Step Sine(int at, int hz, int ms, float magnitude, int phase, int fade) =>
        new(at, StepKind.Periodic, magnitude, SlotMs: ms, Hz: hz, PhaseHundredths: phase, FadeMs: fade);
    static Step InSteering(int at, int ms, float signed) => new(at, StepKind.Stream, signed, ForMs: ms);

    // m is the requested magnitude 0..1 (a full-intensity crash at that Crash
    // strength); sign is +1 or -1 and only picks the direction.
    public static readonly Candidate[] All =
    {
        new('A', "120 ms push (current)", "What the mod plays today: one 120 ms constant pulse.",
            (m, s) => new[] { Push(0, 120, s * m) }),
        new('B', "250 ms push", "Same pulse, twice as long.",
            (m, s) => new[] { Push(0, 250, s * m) }),
        new('C', "400 ms push", "Same pulse, more than three times as long.",
            (m, s) => new[] { Push(0, 400, s * m) }),
        new('D', "Knock 60 + 60 ms", "60 ms one way, then 60 ms the other way.",
            (m, s) => new[] { Push(0, 60, s * m), Push(60, 60, -s * m) }),
        new('E', "Crunch 12 Hz", "Starts at a full push, shakes at 12 Hz and fades over 300 ms.",
            (m, s) => new[] { Sine(0, 12, 300, m, s > 0 ? 9000 : 27000, 200) }),
        new('F', "Push + rattle", "The 120 ms push with a 25 Hz rattle at half strength on top.",
            (m, s) => new[] { Push(0, 120, s * m), Sine(0, 25, 250, m / 2, 0, 150) }),
        new('G', "250 ms push in steering", "B's push added to the steering force instead of a separate effect.",
            (m, s) => new[] { InSteering(0, 250, s * m) }),
    };

    public static int LengthMs(Step[] steps) => steps.Max(step => step.EndMs);

    // Bounds every plan against the slots the engine creates; no hardware needed.
    public static string? SelfTest()
    {
        if (All.Select(c => c.Key).Distinct().Count() != All.Length) return "duplicate candidate key";
        foreach (var candidate in All)
        foreach (int strength in Strengths)
        foreach (int sign in new[] { 1, -1 })
        {
            float m = strength / 100f;
            var steps = candidate.Build(m, sign);
            string where = candidate.Key + " at " + strength + "% sign " + sign;
            if (steps.Length == 0) return where + ": empty";
            for (int i = 1; i < steps.Length; i++) if (steps[i].AtMs < steps[i - 1].AtMs) return where + ": unsorted";
            if (LengthMs(steps) > 500) return where + ": longer than 500 ms";
            foreach (var step in steps)
            {
                if (Math.Abs(step.Magnitude) > 1f || step.Magnitude == 0f) return where + ": magnitude out of range";
                if (step.Kind == StepKind.Constant && !ConstantSlots.Contains(step.SlotMs)) return where + ": no constant slot";
                if (step.Kind == StepKind.Periodic && (!PeriodicSlots.Contains((step.Hz, step.SlotMs)) || step.Magnitude < 0))
                    return where + ": no periodic slot";
                if (step.Kind == StepKind.Stream && step.ForMs > 500) return where + ": stream kick too long";
            }
            if (candidate.Key == 'A' && (steps.Length != 1 || steps[0].SlotMs != 120 || steps[0].Magnitude != sign * m))
                return "A no longer matches the mod's crash pulse";
        }
        return null;
    }
}
