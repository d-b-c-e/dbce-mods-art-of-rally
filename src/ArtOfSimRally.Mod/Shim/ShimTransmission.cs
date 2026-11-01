namespace ArtOfSimRally.Mod.Shim
{
    // Shared gear actions mapped onto Art's gear-ratio indices: reverse 0, neutral 1,
    // first gear 2. Pure decisions only; the caller applies them with Drivetrain.Shift
    // after checking the current car's gear-ratio range. A gate or step the car
    // lacks is ignored, never clamped onto another gear.
    internal static class ShimTransmission
    {
        internal const int Reverse = 0, Neutral = 1, FirstGear = 2;
        internal const int NeutralGate = 7, ReverseGate = 8, Gear1 = 23, Gates = 8;

        internal static bool OwnsGates(ShimAxisConsumer consumer)
        {
            if (consumer.Owns(NeutralGate) || consumer.Owns(ReverseGate)) return true;
            for (int g = 0; g < Gates; g++) if (consumer.Owns(Gear1 + g)) return true;
            return false;
        }

        // The held H-pattern gate as a gear index; no gate held is neutral. Null when the
        // session owns no gate or this Unity frame's shared input is not current.
        internal static int? Gate(ShimAxisConsumer consumer, int unityFrame)
        {
            if (!OwnsGates(consumer) || !consumer.Fresh(unityFrame)) return null;
            // Reverse first: on many shifters reverse shares a gate with a forward gear.
            if (consumer.Value(ReverseGate, unityFrame) > .5f) return Reverse;
            for (int g = 0; g < Gates; g++) if (consumer.Value(Gear1 + g, unityFrame) > .5f) return FirstGear + g;
            return Neutral;
        }

        // One sequential step from the engaged gear, optionally stepping over neutral.
        // Null when there is no step or the target is outside this car's ratios.
        internal static int? Step(int engaged, int step, bool skipNeutral, int maxIndex)
        {
            if (step == 0) return null;
            int target = engaged + step;
            if (skipNeutral && target == Neutral) target += step;
            return Exists(target, maxIndex) ? target : (int?)null;
        }

        internal static bool Exists(int gear, int maxIndex) => gear >= 0 && gear <= maxIndex;

        // The shared profile's Transmission (Core's parse; contract words Automatic | Sequential | H-pattern).
        // Core does not enforce it, so the adapter does: exactly one gear input mode per session.
        internal enum Mode { None, Sequential, HPattern }
        internal static Mode ParseMode(string transmission) =>
            transmission == "Sequential" ? Mode.Sequential : transmission == "H-pattern" ? Mode.HPattern : Mode.None;

        // The single gear change for one physics step, or null. An automatic gearbox, an Automatic or unknown
        // profile, and the other mode's actions never shift. A new gate applies at once; a gear this car lacks is
        // recorded as applied (not retried) and never clamped onto another gear. An unchanged gate is applied again
        // while the gearbox is not in it and no change is under way: the game refuses a shift during a change or with
        // the clutch out, and resets to neutral at the start (owner 2026-10-31: 1st held at the start was ignored).
        internal static int? Next(Mode mode, bool automatic, int engaged, int? gate, ref int lastGate, int step, bool skipNeutral, int maxIndex,
            bool changing = false)
        {
            if (automatic || mode == Mode.None) { lastGate = int.MinValue; return null; }
            if (mode == Mode.HPattern)
            {
                if (!gate.HasValue) return null;
                if (gate.Value != lastGate)
                {
                    bool first = lastGate == int.MinValue;
                    lastGate = gate.Value;
                    // The shared provider reports a gear held since start-up as neutral until the lever moves (owner log
                    // 2026-11-01: lever 1st, read neutral, the game's own 1st was shifted out). A first neutral reading is
                    // not trusted; the game's gear stays until the lever is seen to move.
                    if (first && gate.Value == Neutral) return null;
                    return Exists(gate.Value, maxIndex) && engaged != gate.Value ? gate : null;
                }
                // A held gear the gearbox is not in is applied again; a held neutral never is (it may be that unread gear).
                return !changing && gate.Value != Neutral && engaged != gate.Value && Exists(gate.Value, maxIndex) ? gate : null;
            }
            lastGate = int.MinValue;
            return Step(engaged, step, skipNeutral, maxIndex);
        }
    }
}
