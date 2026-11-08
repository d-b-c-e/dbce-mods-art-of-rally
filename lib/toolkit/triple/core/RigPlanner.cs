using System;
using System.Collections.Generic;

namespace Dbce.TripleScreen;

/// <summary>A rectangle in the spanning window's client pixels (x from the left edge of the leftmost display, y from
/// the top). The window itself goes at the leftmost display's desktop origin; that placement is the caller's.</summary>
public sealed class PixelRect
{
    public PixelRect(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
    public int X { get; }
    public int Y { get; }
    public int Width { get; }
    public int Height { get; }
}

/// <summary>
/// One view of a rig plan: a flat display, or one slice of a curved display, drawn into <see cref="Pixels"/>.
/// A renderer projects for <see cref="CurvedPanelWarp.SourceSurface"/> (see <see cref="Projection"/>) and, for a curved
/// slice, redraws that image with <see cref="CurvedPanelWarp.SourceUvGrid"/>. Physical space is millimetres, eye at the
/// origin, +x right, +y up, the displays toward -z; matrices follow <see cref="ProjectionCalculator"/> (row-major,
/// right-handed, the camera looks down -z, applied as M * v).
/// </summary>
public sealed class RigSlice
{
    internal RigSlice(int displayIndex, int sliceIndex, DisplaySurface panel, CurvedPanelWarp warp, PixelRect pixels)
    {
        DisplayIndex = displayIndex; SliceIndex = sliceIndex; Panel = panel; Warp = warp; Pixels = pixels;
    }

    public int DisplayIndex { get; }
    public int SliceIndex { get; }
    /// <summary>The physical (chord) surface this slice covers.</summary>
    public DisplaySurface Panel { get; }
    public CurvedPanelWarp Warp { get; }
    public PixelRect Pixels { get; }

    /// <summary>The off-axis projection for this slice; near and far are in millimetres.</summary>
    public OffAxisProjection Projection(double nearMm, double farMm) =>
        ProjectionCalculator.Calculate(Warp.SourceSurface, new Vector3d(0d, 0d, 0d), nearMm, farMm);
}

/// <summary>Builds the surfaces and the view plan of any <see cref="RigDefinition"/>.</summary>
public static class RigPlanner
{
    /// <summary>
    /// The widest horizontal angle, seen from the eye, one view of a curved display may cover; wider displays are cut
    /// into equal slices. 80° keeps each of today's 1500R triple panels as one view (the sides span about 62°) and a
    /// 49" 32:9 at 1000R viewed from 800 mm (about 73°) too; tighter limits trade draw calls for edge resolution.
    /// </summary>
    public const double DefaultMaxSliceDegrees = 80d;

    /// <summary>Each display's chord surface, left to right, hinged like <see cref="TripleRigBuilder"/>.</summary>
    public static IReadOnlyList<DisplaySurface> Surfaces(RigDefinition rig)
    {
        if (rig is null) throw new ArgumentNullException(nameof(rig));
        var displays = rig.Displays;
        var surfaces = new DisplaySurface[displays.Count];
        var up = new Vector3d(0d, 1d, 0d);
        var centerY = -rig.EyeHeightAbovePanelCenterMm;
        var center = displays[rig.CenterIndex];
        surfaces[rig.CenterIndex] = Create(center.Id, new Vector3d(0d, centerY, -rig.EyeDistanceMm), new Vector3d(1d, 0d, 0d), up,
            center.WidthMm, center.HeightMm);

        var angle = 0d;
        for (var i = rig.CenterIndex + 1; i < displays.Count; i++)
        {
            angle += displays[i].FoldDegrees * Math.PI / 180d;
            var right = new Vector3d(Math.Cos(angle), 0d, Math.Sin(angle));
            var previous = surfaces[i - 1];
            var hinge = previous.Center + (previous.Right * (previous.Width / 2d));
            surfaces[i] = Create(displays[i].Id, hinge + (right * (rig.BezelGapMm + (displays[i].WidthMm / 2d))), right, up,
                displays[i].WidthMm, displays[i].HeightMm);
        }
        angle = 0d;
        for (var i = rig.CenterIndex - 1; i >= 0; i--)
        {
            angle += displays[i].FoldDegrees * Math.PI / 180d;
            var right = new Vector3d(Math.Cos(angle), 0d, -Math.Sin(angle));
            var next = surfaces[i + 1];
            var hinge = next.Center - (next.Right * (next.Width / 2d));
            surfaces[i] = Create(displays[i].Id, hinge - (right * (rig.BezelGapMm + (displays[i].WidthMm / 2d))), right, up,
                displays[i].WidthMm, displays[i].HeightMm);
        }
        return surfaces;
    }

    /// <summary>
    /// The views to draw, left to right: one per flat display, and one or more per curved display (equal slices of its
    /// arc sharing their edges, so a straight line stays continuous across them).
    /// </summary>
    public static IReadOnlyList<RigSlice> Plan(RigDefinition rig, double maxSliceDegrees = DefaultMaxSliceDegrees)
    {
        if (rig is null) throw new ArgumentNullException(nameof(rig));
        if (!(maxSliceDegrees >= 10d && maxSliceDegrees < 170d)) throw new ArgumentOutOfRangeException(nameof(maxSliceDegrees));
        var surfaces = Surfaces(rig);
        var eye = new Vector3d(0d, 0d, 0d);
        var slices = new List<RigSlice>();
        var x = 0;
        for (var d = 0; d < surfaces.Count; d++)
        {
            var spec = rig.Displays[d];
            var panel = surfaces[d];
            var radius = spec.CurveRadiusMm;
            var count = radius == 0d ? 1 : Math.Max(1, (int)Math.Ceiling(SpanDegrees(panel, eye, radius) / maxSliceDegrees - 1e-9));
            for (var s = 0; s < count; s++)
            {
                double u0 = (double)s / count, u1 = (double)(s + 1) / count;
                var piece = count == 1 ? panel : SubPanel(panel, radius, u0, u1, s);
                var x0 = x + (int)Math.Round(u0 * spec.WidthPx);
                var x1 = x + (int)Math.Round(u1 * spec.WidthPx);
                slices.Add(new RigSlice(d, s, piece, CurvedPanelWarp.Create(piece, eye, radius), new PixelRect(x0, 0, x1 - x0, spec.HeightPx)));
            }
            x += spec.WidthPx;
        }
        return slices;
    }

    /// <summary>The horizontal angle a curved display covers as seen from the eye, at its middle height.</summary>
    public static double SpanDegrees(DisplaySurface panel, Vector3d eye, double radiusMm)
    {
        double min = double.MaxValue, max = double.MinValue;
        for (var i = 0; i <= 64; i++)
        {
            var ray = CurvedPanelWarp.PanelPoint(panel, radiusMm, i / 64d, .5d) - eye;
            var a = Math.Atan2(Vector3d.Dot(ray, panel.Right), -Vector3d.Dot(ray, panel.NormalTowardViewer)) * 180d / Math.PI;
            min = Math.Min(min, a); max = Math.Max(max, a);
        }
        return max - min;
    }

    // The chord of arc fraction [u0, u1] of a curved panel; with the same radius it is the same curve.
    private static DisplaySurface SubPanel(DisplaySurface panel, double radiusMm, double u0, double u1, int index)
    {
        var start = CurvedPanelWarp.PanelPoint(panel, radiusMm, u0, 0d);
        var end = CurvedPanelWarp.PanelPoint(panel, radiusMm, u1, 0d);
        return new DisplaySurface(panel.Id + "-" + index, start, end, start + (panel.UpperLeft - panel.LowerLeft));
    }

    private static DisplaySurface Create(string id, Vector3d center, Vector3d right, Vector3d up, double width, double height)
    {
        var halfRight = right * (width / 2d);
        var halfUp = up * (height / 2d);
        return new DisplaySurface(id, center - halfRight - halfUp, center + halfRight - halfUp, center - halfRight + halfUp);
    }
}
