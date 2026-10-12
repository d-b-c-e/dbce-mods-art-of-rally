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
    }
}
