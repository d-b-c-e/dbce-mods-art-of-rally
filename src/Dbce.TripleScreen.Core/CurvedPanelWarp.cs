using System;

namespace Dbce.TripleScreen;

/// <summary>
/// Within-panel correction for a monitor curved about a vertical axis, with the native compositor's convention
/// (<c>panel_point</c>): the surface's width is the chord, pixels are spaced evenly along the arc, the panel bows away
/// from the viewer, and radius 0 is flat. A renderer draws its view for <see cref="SourceSurface"/> (the chord plane,
/// widened to cover every ray to the curved panel) and redraws it onto the panel with <see cref="SourceUv"/>.
/// </summary>
public sealed class CurvedPanelWarp
{
    private const int EdgeSamples = 256;
    private readonly double _minX, _minY, _spanX, _spanY;

    private CurvedPanelWarp(DisplaySurface panel, Vector3d eye, double radiusMm, DisplaySurface source,
        double minX, double minY, double spanX, double spanY)
    {
        Panel = panel;
        Eye = eye;
        RadiusMm = radiusMm;
        SourceSurface = source;
        _minX = minX;
        _minY = minY;
        _spanX = spanX;
        _spanY = spanY;
    }

    public DisplaySurface Panel { get; }
    public Vector3d Eye { get; }
    public double RadiusMm { get; }
    public bool IsFlat => RadiusMm == 0d;

    /// <summary>The planar frustum surface a renderer projects for; the panel itself when flat.</summary>
    public DisplaySurface SourceSurface { get; }

    public static CurvedPanelWarp Create(DisplaySurface panel, Vector3d eye, double radiusMm)
    {
        if (panel is null) throw new ArgumentNullException(nameof(panel));
        if (!Finite(eye)) throw new ArgumentException("The eye position must be finite.", nameof(eye));
        ValidateRadius(panel, radiusMm);
        if (Vector3d.Dot(eye - panel.LowerLeft, panel.NormalTowardViewer) <= 1e-9)
            throw new ArgumentException("The eye must be in front of the panel.", nameof(eye));
        if (radiusMm == 0d) return new CurvedPanelWarp(panel, eye, 0d, panel, 0d, 0d, panel.Width, panel.Height);

        // The map from panel UV to the chord plane is one-to-one for an eye in front, so its extremes lie on the edges.
        double minX = 0d, maxX = panel.Width, minY = 0d, maxY = panel.Height;
        for (var i = 0; i <= EdgeSamples; i++)
        {
            var t = (double)i / EdgeSamples;
            foreach (var (u, v) in new[] { (t, 0d), (t, 1d), (0d, t), (1d, t) })
            {
                OnChordPlane(panel, eye, PanelPoint(panel, radiusMm, u, v), out var x, out var y);
                minX = Math.Min(minX, x); maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y); maxY = Math.Max(maxY, y);
            }
        }

        var origin = panel.LowerLeft + (panel.Right * minX) + (panel.Up * minY);
        var source = new DisplaySurface(panel.Id + "-source", origin, origin + (panel.Right * (maxX - minX)),
            origin + (panel.Up * (maxY - minY)));
        return new CurvedPanelWarp(panel, eye, radiusMm, source, minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>The physical point at panel UV (0,0 lower-left), as the native <c>panel_point</c>.</summary>
    public static Vector3d PanelPoint(DisplaySurface panel, double radiusMm, double u, double v)
    {
        if (panel is null) throw new ArgumentNullException(nameof(panel));
        ValidateRadius(panel, radiusMm);
        if (double.IsNaN(u) || u < 0d || u > 1d) throw new ArgumentOutOfRangeException(nameof(u));
        if (double.IsNaN(v) || v < 0d || v > 1d) throw new ArgumentOutOfRangeException(nameof(v));
        var chord = panel.Width;
        var lift = (panel.UpperLeft - panel.LowerLeft) * v;
        if (radiusMm == 0d) return panel.LowerLeft + (panel.Right * (chord * u)) + lift;
        var half = Math.Asin(chord / (2d * radiusMm));
        var phi = ((2d * u) - 1d) * half;
        // cos(half) - cos(phi) without cancellation for flat-ish panels.
        var sag = -2d * Math.Sin((half + phi) / 2d) * Math.Sin((half - phi) / 2d) * radiusMm;
        return panel.LowerLeft + (panel.Right * (chord / 2d)) + (panel.Right * (radiusMm * Math.Sin(phi)))
            + (panel.NormalTowardViewer * sag) + lift;
    }

    /// <summary>How far a curved panel's middle sits behind its chord (0 when flat).</summary>
    public static double Sagitta(double chordMm, double radiusMm)
    {
        if (!(chordMm > 0d) || double.IsInfinity(chordMm)) throw new ArgumentOutOfRangeException(nameof(chordMm));
        if (double.IsNaN(radiusMm) || double.IsInfinity(radiusMm) || radiusMm < 0d || (radiusMm > 0d && chordMm > 2d * radiusMm))
            throw new ArgumentOutOfRangeException(nameof(radiusMm), "Curve radius must be 0 (flat) or at least half the chord.");
        if (radiusMm == 0d) return 0d;
        var half = chordMm / 2d;
        // R - sqrt(R^2 - c^2/4) without cancellation for flat-ish panels.
        return half * half / (radiusMm + Math.Sqrt((radiusMm * radiusMm) - (half * half)));
    }

    /// <summary>Where panel UV (0,0 lower-left) samples the source view, in source UV (0,0 lower-left).</summary>
    public void SourceUv(double u, double v, out double sourceU, out double sourceV)
    {
        if (IsFlat) { sourceU = u; sourceV = v; return; }
        OnChordPlane(Panel, Eye, PanelPoint(Panel, RadiusMm, u, v), out var x, out var y);
        sourceU = (x - _minX) / _spanX;
        sourceV = (y - _minY) / _spanY;
    }

    // The eye ray through a physical point, met with the chord plane, in panel-local millimeters from the lower-left.
    private static void OnChordPlane(DisplaySurface panel, Vector3d eye, Vector3d point, out double x, out double y)
    {
        var normal = panel.NormalTowardViewer;
        var ray = point - eye;
        var hit = eye + (ray * (Vector3d.Dot(panel.LowerLeft - eye, normal) / Vector3d.Dot(ray, normal)));
        x = Vector3d.Dot(hit - panel.LowerLeft, panel.Right);
        y = Vector3d.Dot(hit - panel.LowerLeft, panel.Up);
    }

    private static void ValidateRadius(DisplaySurface panel, double radiusMm)
    {
        if (double.IsNaN(radiusMm) || double.IsInfinity(radiusMm) || radiusMm < 0d)
            throw new ArgumentOutOfRangeException(nameof(radiusMm), "Curve radius must be 0 (flat) or a positive number of millimeters.");
        if (radiusMm > 0d && panel.Width > 2d * radiusMm)
            throw new ArgumentOutOfRangeException(nameof(radiusMm), "Curve radius must be at least half the panel width.");
    }

    private static bool Finite(Vector3d p) =>
        !double.IsNaN(p.X) && !double.IsInfinity(p.X) && !double.IsNaN(p.Y) && !double.IsInfinity(p.Y)
        && !double.IsNaN(p.Z) && !double.IsInfinity(p.Z);
}
