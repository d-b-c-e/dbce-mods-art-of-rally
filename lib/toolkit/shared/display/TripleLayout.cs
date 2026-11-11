// MIT. Source-vendored C# 10/.NET 6+ helpers; no Unity, Windows, UI or configuration-store dependencies.
#nullable enable
using System;
using System.Globalization;
using Dbce.TripleScreen;


namespace Dbce.Wheel.Display;

// Pure triple-screen policy, tested without the game. Selector and span policy adapted from iRacing Arcade
// SpanPolicy.cs (TripleLayoutChoice, STD-015/022); projection from the shared Dbce.TripleScreen.Core.

/// <summary>[Triple] Mode, as the other Unity mods and Wheelkit write it.</summary>
internal enum TripleMode { Off, Auto, On }

/// <summary>The F6 selector (STD-022): Off, Surround, Separate monitors, stored as Mode plus SpanSeparateMonitors.</summary>
internal static class TripleLayoutChoice
{
    public static readonly string[] Labels = { "Off", "Surround", "Separate monitors" };
    public static int Current(TripleMode mode, bool span) => mode == TripleMode.Off ? 0 : span ? 2 : 1;
    /// <summary>On stays On when it was already chosen in the config file; otherwise views are Auto.</summary>
    public static (TripleMode Mode, bool Span) Apply(int choice, TripleMode previous) => choice switch
    {
        0 => (TripleMode.Off, false),
        1 => (previous == TripleMode.On ? TripleMode.On : TripleMode.Auto, false),
        2 => (previous == TripleMode.On ? TripleMode.On : TripleMode.Auto, true),
        _ => throw new ArgumentOutOfRangeException(nameof(choice))
    };
}

/// <summary>Saved rig measurements in millimeters and degrees ([Triple] geometry keys).</summary>
internal readonly record struct TripleRig(double PanelWidthMm, double PanelHeightMm, double EyeDistanceMm, double SideAngle, double BezelMm,
    double CurveRadiusMm = 0)
{
    public const double MinWidth = 100, MaxWidth = 3000, MinHeight = 100, MaxHeight = 2000, MinEye = 100, MaxEye = 3000,
        MinAngle = 0, MaxAngle = 89, MinBezel = 0, MaxBezel = 100, MaxRadius = 10000;

    /// <summary>How far the glass middle of a curved panel sits behind its chord (0 when flat). Valid rigs only.</summary>
    public double CurveDepth() => CurvedPanelWarp.Sagitta(PanelWidthMm, CurveRadiusMm);   // a method: record ToString must not evaluate it for invalid rigs

    public string? Validation()
    {
        if (!In(PanelWidthMm, MinWidth, MaxWidth)) return "Panel width must be 100 to 3000 mm.";
        if (!In(PanelHeightMm, MinHeight, MaxHeight)) return "Panel height must be 100 to 2000 mm.";
        if (!In(EyeDistanceMm, MinEye, MaxEye)) return "Eye distance must be 100 to 3000 mm.";
        if (!In(SideAngle, MinAngle, MaxAngle)) return "Side angle must be 0 to 89 degrees.";
        if (!In(BezelMm, MinBezel, MaxBezel)) return "Bezel gap must be 0 to 100 mm.";
        if (!(CurveRadiusMm == 0 || CurveRadiusMm >= PanelWidthMm / 2 && CurveRadiusMm <= MaxRadius))
            return "Curve radius must be 0 (flat) or between half the panel width and 10000 mm.";
        if (EyeDistanceMm <= CurveDepth()) return "Eye distance must be greater than the panel's curve depth.";
        return null;
    }

    private static bool In(double value, double min, double max) => !double.IsNaN(value) && value >= min && value <= max;

    public static bool TryParse(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsInfinity(value);
}

/// <summary>Startup triple-screen settings. The views and the span read them once per start.</summary>
internal readonly record struct TripleSettings(TripleMode Mode, bool Span, TripleRig Rig, bool MatchGameFov);

/// <summary>One panel's view: yaw from the center view and off-axis tangents at a near plane of 1 (Unity camera space).</summary>
internal readonly record struct PanelFrustum(double YawDegrees, double Left, double Right, double Bottom, double Top);

/// <summary>The non-zero elements of Unity's off-center perspective matrix (OpenGL convention, Camera.projectionMatrix).</summary>
internal readonly record struct OffCenterProjection(double M00, double M02, double M11, double M12, double M22, double M23)
{
    public static OffCenterProjection From(PanelFrustum view, double near, double far)
    {
        if (!(near > 0) || !(far > near)) throw new ArgumentOutOfRangeException(nameof(near), "Require 0 < near < far.");
        double l = view.Left * near, r = view.Right * near, b = view.Bottom * near, t = view.Top * near;
        return new(2 * near / (r - l), (r + l) / (r - l), 2 * near / (t - b), (t + b) / (t - b),
            -(far + near) / (far - near), -(2 * far * near) / (far - near));
    }
}

internal static class TriplePanels
{
    /// <summary>
    /// Left, center, right. With <paramref name="centerVerticalFov"/> the eye distance is chosen so the center panel keeps
    /// that vertical field of view (the game's chase framing); otherwise the saved eye distance is used.
    /// </summary>
    public static PanelFrustum[] Compute(TripleRig rig, double? centerVerticalFov = null) => Compute(rig, centerVerticalFov, out _, false);

    /// <summary>
    /// As <see cref="Compute(TripleRig, double?)"/> for a renderer that redraws each view onto its curved panel: the
    /// frustums cover each panel's chord source surface and <paramref name="warps"/> (left, center, right) give
    /// <see cref="CurvedPanelWarp.SourceUv"/> for the redraw. With <paramref name="centerVerticalFov"/> the game-derived
    /// virtual eye is taken to the glass (Art, Woden and DRIVE's contract), so the redrawn glass middle keeps that
    /// field of view and the chord frustum is a little wider. Flat rigs get the plain frustums and flat warps.
    /// </summary>
    public static PanelFrustum[] ComputeWarped(TripleRig rig, double? centerVerticalFov, out CurvedPanelWarp[] warps) =>
        Compute(rig, centerVerticalFov, out warps, true);

    private static PanelFrustum[] Compute(TripleRig rig, double? centerVerticalFov, out CurvedPanelWarp[] warps, bool warped)
    {
        if (rig.Validation() is { } invalid) throw new ArgumentOutOfRangeException(nameof(rig), invalid);
        double eye = rig.EyeDistanceMm;
        if (centerVerticalFov is double fov)
        {
            if (!(fov > 1 && fov < 179)) throw new ArgumentOutOfRangeException(nameof(centerVerticalFov));
            // Eye distances are to the plane through the panel edges (STD-047), where the builder puts each chord.
            // Without the redraw the picture is drawn on that plane, so it gets the game's field of view; with it, the
            // picture ends up on the glass, one curve depth further away, so the glass gets it.
            double framing = TripleRigDefinition.EyeDistanceForVerticalFov(rig.PanelHeightMm, fov);
            eye = warped ? framing - rig.CurveDepth() : framing;
        }
        var definition = new TripleRigDefinition(rig.PanelWidthMm, rig.PanelHeightMm, eye, rig.SideAngle, rig.SideAngle, 0, rig.BezelMm, rig.CurveRadiusMm);
        var result = new PanelFrustum[3];
        warps = new CurvedPanelWarp[3];
        var surfaces = TripleRigBuilder.Build(definition);
        var origin = new Vector3d(0, 0, 0);
        for (int i = 0; i < 3; i++)
        {
            warps[i] = CurvedPanelWarp.Create(surfaces[i], origin, rig.CurveRadiusMm);
            // The core works with the eye looking along -Z; Unity camera space looks along +Z with the same right and up.
            var projection = ProjectionCalculator.Calculate(warped ? warps[i].SourceSurface : surfaces[i], origin, 1, 2);
            var forward = projection.CameraForward;
            double yaw = Math.Atan2(forward.X, -forward.Z) * 180 / Math.PI;
            result[i] = new PanelFrustum(yaw, projection.Left, projection.Right, projection.Bottom, projection.Top);
        }
        return result;
    }

    /// <summary>The vertical field of view a frustum shows, as Unity's Camera.fieldOfView reports it.</summary>
    public static double VerticalFov(PanelFrustum frustum) =>
        (Math.Atan(frustum.Top) - Math.Atan(frustum.Bottom)) * 180 / Math.PI;
}

internal static class TripleGate
{
    /// <summary>Three screens wide: Surround or a borderless span over three panels.</summary>
    public const double WideAspect = RacingRowPolicy.SurroundAspect;

    /// <summary>Why the three views are not shown, or null when they should be.</summary>
    public static string? Inactive(TripleMode mode, int width, int height)
    {
        if (mode == TripleMode.Off) return "turned off";
        if (width <= 0 || height <= 0) return "window size unavailable";
        if (mode == TripleMode.Auto && width < height * WideAspect) return "window is not three screens wide";
        return null;
    }
}

/// <summary>The shared racing-row selection and the primary size used only when returning from a mod-owned span.</summary>
internal readonly record struct DesktopLayout(RacingRow Row, int PrimaryWidth, int PrimaryHeight)
{
    public bool SeparateTriple => Row.Kind == RacingRowKind.Separate;
    public int Monitors => Row.Displays.Count;
    public int X => Row.X;
    public int Y => Row.Y;
    public int Width => Row.Width;
    public int Height => Row.Height;
    public string Reason => Row.Reason;
}

internal enum SpanAction { None, ApplySpan, PlaceOnly, RestoreFullscreen, ForgetMarker }

/// <summary>
/// STD-015 startup policy for separate monitors. At most one apply per launch: a windowed apply at the span when it is
/// wanted and not already there, placement only when Unity already restored the span, and one return to borderless
/// fullscreen at the primary size when a span this mod made meets another layout or the option was turned off.
/// <paramref name="marker"/> is the span size this mod last applied ("" when none), so a size the player chose is kept.
/// </summary>
internal static class SpanPolicy
{
    public static SpanAction Decide(bool enabled, DesktopLayout layout, int width, int height, bool windowed, string marker)
    {
        if (enabled && layout.SeparateTriple)
            return windowed && width == layout.Width && height == layout.Height ? SpanAction.PlaceOnly : SpanAction.ApplySpan;
        if (string.IsNullOrEmpty(marker)) return SpanAction.None;
        return windowed && marker == Marker(width, height) ? SpanAction.RestoreFullscreen : SpanAction.ForgetMarker;
    }

    public static string Marker(int width, int height) => width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
}
