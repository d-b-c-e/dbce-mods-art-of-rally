namespace ArtOfSimRally.Mod.Triple;

// F7 comparison steps (toolkit docs/camera-framing-and-comparison-policy.md): the scene correction changes, the saved
// framing (game or measured FOV) does not.
internal enum ComparisonStep { Original = 1, Angle = 2, AngleCurve = 3 }

internal static class F7Comparison
{
    /// <summary>What shows now: the session step, else the saved choice (curved when the curve is working).</summary>
    internal static ComparisonStep Current(ComparisonStep? session, bool curveActive) =>
        session ?? (curveActive ? ComparisonStep.AngleCurve : ComparisonStep.Angle);

    /// <summary>Original -> Angle -> Angle + curve (only when a curve is available) -> Original.</summary>
    internal static ComparisonStep Next(ComparisonStep current, bool curveAvailable) =>
        current == ComparisonStep.Original ? ComparisonStep.Angle
        : current == ComparisonStep.Angle && curveAvailable ? ComparisonStep.AngleCurve
        : ComparisonStep.Original;

    /// <summary>The saved F7 pick; anything but 1..3 (a missing key reads 0) means none.</summary>
    internal static ComparisonStep? FromSaved(int saved) =>
        saved >= 1 && saved <= 3 ? (ComparisonStep)saved : (ComparisonStep?)null;

    internal static string Label(ComparisonStep step, bool measured, bool curveAvailable, bool saved = true)
    {
        var name = step == ComparisonStep.Original ? "Original game view"
            : step == ComparisonStep.Angle ? "Angle correction" : "Angle + curve correction";
        var framing = step == ComparisonStep.Original ? "" : measured ? " · Measured FOV" : " · Game FOV";
        return name + framing + " · " + (int)step + "/" + (curveAvailable ? 3 : 2) + (saved ? "" : " · Could not save");
    }
}
