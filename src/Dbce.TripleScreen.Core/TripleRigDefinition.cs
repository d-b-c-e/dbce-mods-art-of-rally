using System;

namespace Dbce.TripleScreen;

/// <summary>Measurements for three identical panels hinged at the center panel edges.</summary>
public sealed class TripleRigDefinition
{
    public TripleRigDefinition(
        double panelWidthMm,
        double panelHeightMm,
        double eyeDistanceMm,
        double sideAngleDegrees)
        : this(
            panelWidthMm,
            panelHeightMm,
            eyeDistanceMm,
            sideAngleDegrees,
            sideAngleDegrees,
            0d)
    {
    }

    public TripleRigDefinition(
        double panelWidthMm,
        double panelHeightMm,
        double eyeDistanceMm,
        double leftAngleDegrees,
        double rightAngleDegrees,
        double eyeHeightAbovePanelCenterMm = 0d)
        : this(panelWidthMm, panelHeightMm, eyeDistanceMm, leftAngleDegrees, rightAngleDegrees, eyeHeightAbovePanelCenterMm, 0d)
    {
    }

    /// <param name="panelWidthMm">Visible active-area width of one panel.</param>
    /// <param name="panelHeightMm">Visible active-area height of one panel.</param>
    /// <param name="eyeDistanceMm">Eye to the center panel, along its normal.</param>
    /// <param name="leftAngleDegrees">How far the left panel turns toward the viewer (0 = flat).</param>
    /// <param name="rightAngleDegrees">How far the right panel turns toward the viewer (0 = flat).</param>
    /// <param name="eyeHeightAbovePanelCenterMm">Eye height above the center panel's middle.</param>
    /// <param name="bezelGapMm">
    /// Gap between adjacent active areas at each seam, measured along the side panel's surface.
    /// The scene behind the bezels is skipped, as through a real window frame.
    /// </param>
    public TripleRigDefinition(
        double panelWidthMm,
        double panelHeightMm,
        double eyeDistanceMm,
        double leftAngleDegrees,
        double rightAngleDegrees,
        double eyeHeightAbovePanelCenterMm,
        double bezelGapMm)
        : this(panelWidthMm, panelHeightMm, eyeDistanceMm, leftAngleDegrees, rightAngleDegrees, eyeHeightAbovePanelCenterMm, bezelGapMm, 0d)
    {
    }

    /// <summary>
    /// As above, for curved panels: <c>eyeDistanceMm</c> is the eye to the center panel's glass at its middle, along its
    /// normal (what a tape measures), and <c>curveRadiusMm</c> is the monitor radius (0 = flat). A curved panel's glass
    /// middle sits one sagitta behind its chord, so the chord plane the builder places is that much closer; hinges stay
    /// at the chord ends.
    /// </summary>
    public TripleRigDefinition(
        double panelWidthMm,
        double panelHeightMm,
        double eyeDistanceMm,
        double leftAngleDegrees,
        double rightAngleDegrees,
        double eyeHeightAbovePanelCenterMm,
        double bezelGapMm,
        double curveRadiusMm)
    {
        if (!(panelWidthMm > 0d)) throw new ArgumentOutOfRangeException(nameof(panelWidthMm));
        if (!(panelHeightMm > 0d)) throw new ArgumentOutOfRangeException(nameof(panelHeightMm));
        if (!(eyeDistanceMm > 0d)) throw new ArgumentOutOfRangeException(nameof(eyeDistanceMm));
        if (leftAngleDegrees < 0d || leftAngleDegrees >= 90d) throw new ArgumentOutOfRangeException(nameof(leftAngleDegrees));
        if (rightAngleDegrees < 0d || rightAngleDegrees >= 90d) throw new ArgumentOutOfRangeException(nameof(rightAngleDegrees));
        if (!(bezelGapMm >= 0d) || double.IsInfinity(bezelGapMm)) throw new ArgumentOutOfRangeException(nameof(bezelGapMm));
        if (!(curveRadiusMm >= 0d) || double.IsInfinity(curveRadiusMm) || (curveRadiusMm > 0d && panelWidthMm > 2d * curveRadiusMm))
            throw new ArgumentOutOfRangeException(nameof(curveRadiusMm), "Curve radius must be 0 (flat) or at least half the panel width.");
        if (!(eyeDistanceMm > CurvedPanelWarp.Sagitta(panelWidthMm, curveRadiusMm)))
            throw new ArgumentOutOfRangeException(nameof(eyeDistanceMm), "The eye must be in front of the curved panel's chord.");

        PanelWidthMm = panelWidthMm;
        PanelHeightMm = panelHeightMm;
        EyeDistanceMm = eyeDistanceMm;
        LeftAngleDegrees = leftAngleDegrees;
        RightAngleDegrees = rightAngleDegrees;
        EyeHeightAbovePanelCenterMm = eyeHeightAbovePanelCenterMm;
        BezelGapMm = bezelGapMm;
        CurveRadiusMm = curveRadiusMm;
    }

    public double PanelWidthMm { get; }
    public double PanelHeightMm { get; }
    public double EyeDistanceMm { get; }
    public double LeftAngleDegrees { get; }
    public double RightAngleDegrees { get; }
    public double EyeHeightAbovePanelCenterMm { get; }
    public double BezelGapMm { get; }
    public double CurveRadiusMm { get; }

    /// <summary>Eye to the center panel's chord plane: the glass distance less the sagitta (equal when flat).</summary>
    public double ChordDistanceMm => EyeDistanceMm - CurvedPanelWarp.Sagitta(PanelWidthMm, CurveRadiusMm);

    /// <summary>
    /// Eye distance at which the center panel spans a game's vertical field of view. Use it to
    /// offer a "match the game's FOV" option: the whole rig is rebuilt for a virtual eye at that
    /// distance, so the side views still meet the center view at the seams.
    /// </summary>
    public static double EyeDistanceForVerticalFov(double panelHeightMm, double verticalFovDegrees)
    {
        if (!(panelHeightMm > 0d)) throw new ArgumentOutOfRangeException(nameof(panelHeightMm));
        if (!(verticalFovDegrees > 0d && verticalFovDegrees < 180d)) throw new ArgumentOutOfRangeException(nameof(verticalFovDegrees));
        return panelHeightMm / 2d / Math.Tan(verticalFovDegrees * Math.PI / 360d);
    }
}
