using System;
using System.Collections.Generic;
using System.Linq;

namespace Dbce.TripleScreen;

/// <summary>
/// One physical display of a rig. <see cref="WidthMm"/> is the chord of a curved display (the same convention as
/// <see cref="CurvedPanelWarp"/>: pixels are spaced evenly along the arc). <see cref="FoldDegrees"/> turns the display
/// toward the viewer relative to its inner neighbour (0 = continuing flat, 70 = the owner's triple sides); it is ignored
/// for the center display.
/// </summary>
public sealed class DisplaySpec
{
    public DisplaySpec(string id, int widthPx, int heightPx, double widthMm, double heightMm, double curveRadiusMm = 0d,
        double foldDegrees = 0d)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A display id is required.", nameof(id));
        if (widthPx < 1 || heightPx < 1) throw new ArgumentOutOfRangeException(nameof(widthPx), "Pixel size must be positive.");
        if (!(widthMm > 0d) || !(heightMm > 0d) || double.IsInfinity(widthMm) || double.IsInfinity(heightMm))
            throw new ArgumentOutOfRangeException(nameof(widthMm), "Physical size must be positive and finite.");
        if (double.IsNaN(curveRadiusMm) || curveRadiusMm < 0d || (curveRadiusMm > 0d && widthMm > 2d * curveRadiusMm))
            throw new ArgumentOutOfRangeException(nameof(curveRadiusMm), "Curve radius must be 0 (flat) or at least half the width.");
        if (double.IsNaN(foldDegrees) || foldDegrees <= -90d || foldDegrees >= 180d)
            throw new ArgumentOutOfRangeException(nameof(foldDegrees));
        Id = id; WidthPx = widthPx; HeightPx = heightPx; WidthMm = widthMm; HeightMm = heightMm;
        CurveRadiusMm = curveRadiusMm; FoldDegrees = foldDegrees;
    }

    public string Id { get; }
    public int WidthPx { get; }
    public int HeightPx { get; }
    public double WidthMm { get; }
    public double HeightMm { get; }
    public double CurveRadiusMm { get; }
    public double FoldDegrees { get; }
}

/// <summary>
/// Any rig of displays in one horizontal row, left to right, hinged edge to edge: one display, triples, a curved
/// ultrawide, or more. The eye looks straight at <see cref="CenterIndex"/>. <see cref="EyeDistanceMm"/> is eye to the
/// plane through that display's edges, as Assetto Corsa and the FOV calculators take it. <see cref="BezelGapMm"/> is the
/// visible gap between two displays' images. Every display is drawn in one borderless window spanning the row, so all
/// displays share one pixel height.
/// </summary>
public sealed class RigDefinition
{
    public RigDefinition(IReadOnlyList<DisplaySpec> displays, int centerIndex, double eyeDistanceMm,
        double eyeHeightAbovePanelCenterMm = 0d, double bezelGapMm = 0d)
    {
        if (displays is null || displays.Count == 0) throw new ArgumentException("At least one display is required.", nameof(displays));
        if (displays.Any(d => d is null)) throw new ArgumentException("Displays must not be null.", nameof(displays));
        if (centerIndex < 0 || centerIndex >= displays.Count) throw new ArgumentOutOfRangeException(nameof(centerIndex));
        if (!(eyeDistanceMm > 0d) || double.IsInfinity(eyeDistanceMm)) throw new ArgumentOutOfRangeException(nameof(eyeDistanceMm));
        if (double.IsNaN(eyeHeightAbovePanelCenterMm) || double.IsInfinity(eyeHeightAbovePanelCenterMm))
            throw new ArgumentOutOfRangeException(nameof(eyeHeightAbovePanelCenterMm));
        if (double.IsNaN(bezelGapMm) || double.IsInfinity(bezelGapMm) || bezelGapMm < 0d) throw new ArgumentOutOfRangeException(nameof(bezelGapMm));
        if (displays.Any(d => d.HeightPx != displays[0].HeightPx))
            throw new ArgumentException("A spanning window needs every display at the same pixel height.", nameof(displays));
        Displays = displays.ToArray(); CenterIndex = centerIndex; EyeDistanceMm = eyeDistanceMm;
        EyeHeightAbovePanelCenterMm = eyeHeightAbovePanelCenterMm; BezelGapMm = bezelGapMm;
    }

    public IReadOnlyList<DisplaySpec> Displays { get; }
    public int CenterIndex { get; }
    public double EyeDistanceMm { get; }
    public double EyeHeightAbovePanelCenterMm { get; }
    public double BezelGapMm { get; }

    /// <summary>Three equal displays with the sides folded in: the same rig as <see cref="TripleRigBuilder"/>.</summary>
    public static RigDefinition Triples(int widthPx, int heightPx, double widthMm, double heightMm, double eyeDistanceMm,
        double leftFoldDegrees, double rightFoldDegrees, double eyeHeightAbovePanelCenterMm = 0d, double bezelGapMm = 0d,
        double curveRadiusMm = 0d) =>
        new RigDefinition(new[]
        {
            new DisplaySpec("left", widthPx, heightPx, widthMm, heightMm, curveRadiusMm, leftFoldDegrees),
            new DisplaySpec("center", widthPx, heightPx, widthMm, heightMm, curveRadiusMm),
            new DisplaySpec("right", widthPx, heightPx, widthMm, heightMm, curveRadiusMm, rightFoldDegrees),
        }, 1, eyeDistanceMm, eyeHeightAbovePanelCenterMm, bezelGapMm);
}
