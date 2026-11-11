using System;
using System.Collections.Generic;
using System.Linq;
using Dbce.TripleScreen;
using Dbce.TripleScreen.Protocol;
using Dbce.Wheel.Display.Unity;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace ArtOfSimRally.Mod.Triple;

internal sealed class ProjectionController
{
    internal bool LandingOwnsOutputResize { get; set; }
    private Camera _camera;
    private CenterProjectionDriver _centerDriver;
    private SeparateDisplayRenderDriver _separateDriver;
    private readonly OutputResolution _outputResolution = new OutputResolution();
    internal double? CenterVerticalFovDegrees { get; private set; }
    internal long SuccessfulWideFrames => _separateDriver?.SuccessfulCenterFrames ?? 0;
    internal DateTime? LastSuccessfulWideFrameUtc => SuccessfulWideFrames == 0
        ? (DateTime?)null : _separateDriver.LastCenterFrameUtc;
    internal bool SeparateDisplaysRendered => _separateDriver != null && _separateDriver.AllDisplaysRendered;
    internal double? SeparateCameraCallbackSpanMs => _separateDriver?.LastCameraCallbackSpanMs;
    internal int ActiveSidePostProcessLayers => _separateDriver?.ActiveSidePostProcessLayers ?? 0;
    internal bool SideColorGradingEnabled => _separateDriver != null && _separateDriver.SideColorGradingEnabled;
    internal bool SideColorGradeCreated => _separateDriver != null && _separateDriver.SideColorGradeCreated;
    internal string SideColorGradeError => _separateDriver?.SideColorGradeError;
    internal string SideEffectSummary => _separateDriver?.SideEffectSummary;
    internal int ExpandedVegetationSystems => _separateDriver?.ExpandedVegetationSystems ?? 0;
    internal int SideOcclusionCount => _separateDriver?.SideOcclusionCount ?? 0;
    internal int SideBeautifyCount => _separateDriver?.SideBeautifyCount ?? 0;
    internal string SampleCurveLuminance() => _separateDriver?.SampleCurveLuminance();
    internal int SideVolumetricCount => _separateDriver?.SideVolumetricCount ?? 0;
    internal double ActiveViewWidthScale { get; private set; } = 1d;

    internal ProjectionOutcome Update(LayoutLoadResult layoutResult, Settings settings)
    {
        if (!settings.EnableCenterPanelPreview && !settings.EnableThreeViewPrototype)
        {
            Release();
            return ProjectionOutcome.Inactive(layoutResult, "FEATURE_DISABLED", "Triple-screen rendering is off; stock rendering is active.");
        }

        if (!layoutResult.IsSuccess || layoutResult.Document is null)
        {
            Release();
            return ProjectionOutcome.Rejected(layoutResult.ErrorCode ?? "LAYOUT_INVALID", layoutResult.ErrorMessage ?? "The desired layout is invalid.");
        }

        var layout = layoutResult.Document;
        var output = layout.Output;
        var panel = layout.Panel;
        var geometry = layout.Geometry;
        if (output is null || panel is null || geometry is null)
        {
            Release();
            return ProjectionOutcome.Rejected("LAYOUT_INVALID", "The desired layout is missing a required object.");
        }

        if (!output.CombinedWidthPx.HasValue || !output.CombinedHeightPx.HasValue)
        {
            Release();
            return ProjectionOutcome.Degraded(layoutResult, "OUTPUT_RESOLUTION_REQUIRED", "The selected rendering mode requires the optimizer's combined output resolution.");
        }

        if (output.CombinedHeightPx.Value != panel.NativeHeightPx)
        {
            Release();
            return ProjectionOutcome.Degraded(layoutResult, "OUTPUT_HEIGHT_UNSUPPORTED", "The combined output height must equal panel native height.");
        }

        if ((long)output.CombinedWidthPx.Value < (long)panel.NativeWidthPx * panel.Count)
        {
            Release();
            return ProjectionOutcome.Degraded(layoutResult, "OUTPUT_WIDTH_UNSUPPORTED", "The combined output must provide at least one native-width viewport per panel.");
        }

        // Separate monitors (no Surround): one borderless window across the row instead (SpanWindow).
        if (settings.EnableThreeViewPrototype && SpanWindow.Needed(output.CombinedWidthPx.Value))
        {
            var waiting = SpanWindow.Ensure(output.CombinedWidthPx.Value, output.CombinedHeightPx.Value);
            if (waiting != null)
            {
                Release();
                return ProjectionOutcome.Starting(layoutResult, "SPAN_WINDOW", waiting);
            }
        }
        else SpanWindow.Release();
        var switching = settings.AllowOutputResolutionMismatch || SpanWindow.Engaged ? null
            : RequestOutputSize(layoutResult, output.CombinedWidthPx.Value, output.CombinedHeightPx.Value, "the Surround span");
        if (!settings.AllowOutputResolutionMismatch &&
            (Screen.width != output.CombinedWidthPx.Value || Screen.height != output.CombinedHeightPx.Value))
        {
            Release();
            return switching ?? ProjectionOutcome.Degraded(
                layoutResult,
                "OUTPUT_RESOLUTION_MISMATCH",
                Display.main.systemWidth != output.CombinedWidthPx.Value
                    ? $"Game output is {Screen.width}x{Screen.height}; the span needs {output.CombinedWidthPx.Value}x{output.CombinedHeightPx.Value}. Turn on NVIDIA Surround, or arrange the three monitors side by side in Windows."
                    : $"Game output is {Screen.width}x{Screen.height}; the span needs {output.CombinedWidthPx.Value}x{output.CombinedHeightPx.Value}. Choose that resolution in the game's display options.");
        }

        var camera = Camera.main;
        var rig = camera == null || camera.transform.parent == null
            ? null
            : camera.transform.parent.GetComponent<CarCameras>();
        var continuingSpanCamera = camera != null && camera == _camera && _separateDriver != null;
        if (camera == null || rig == null || (!rig.enabled && !continuingSpanCamera) || camera.name != "Camera Main")
        {
            Release();
            return ProjectionOutcome.Starting(layoutResult, "STAGE_CAMERA_WAIT", "Waiting for the active gameplay Stage Camera; menus and cinematics use stock rendering.");
        }

        var postProcessing = camera.GetComponent<PostProcessLayer>();
        if (postProcessing != null && postProcessing.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing)
        {
            Release();
            return ProjectionOutcome.Degraded(layoutResult, "TAA_UNSUPPORTED", "Temporal anti-aliasing can reset custom projection matrices; select another AA mode for this experiment.");
        }

        try
        {
            var viewWidthScale = ResolveViewWidthScale(panel, geometry, camera, settings);
            var definition = Rig(panel, geometry, viewWidthScale, settings);
            var surfaces = TripleRigBuilder.Build(definition);
            ActiveViewWidthScale = viewWidthScale;

            if (settings.EnableThreeViewPrototype)
            {
                if ((long)output.CombinedWidthPx.Value != (long)panel.NativeWidthPx * 3)
                {
                    Release();
                    return ProjectionOutcome.Degraded(
                        layoutResult,
                        "BEZEL_CORRECTED_OUTPUT_UNSUPPORTED",
                        "The first three-view prototype requires exactly three native panel widths; disable driver bezel correction for this experiment.");
                }

                EnsureSeparateDriver(camera);
                ConfigureViews(surfaces, definition, settings, $"span|{panel.NativeWidthPx}x{panel.NativeHeightPx}", warps =>
                {
                    var (projections, rotations) = Views(surfaces, warps, camera);
                    _separateDriver.ConfigureSpan(layoutResult.Sha256, projections, rotations, warps, panel.NativeWidthPx, panel.NativeHeightPx);
                });
                if (_separateDriver.RenderError != null)
                    return ProjectionOutcome.Error(layoutResult, "THREE_VIEW_RENDER_ERROR", _separateDriver.RenderError);
                if (!_separateDriver.AllDisplaysRendered)
                    return ProjectionOutcome.Starting(layoutResult, "THREE_VIEW_ARMED", "Three off-axis viewports are armed; waiting for all cameras to render.");

                return ProjectionOutcome.ThreeViewActive(
                    layoutResult,
                    _separateDriver.LastThreeViewFrameUtc,
                    "THREE_VIEW_EXPERIMENTAL",
                    $"Three camera viewports cover the wide display with {FovDescription(settings, camera)}.{CurveDescription} HUD routing, replay, and photo mode still need visual verification.");
            }

            EnsureCenterDriver(camera);
            var center = surfaces[1];

            // Core physical geometry is millimetres. Calculate frustum edges at
            // Unity clip distances expressed in millimetres, then convert those
            // four edges back to Unity units before constructing the matrix.
            var physical = CalculateView(center, camera);
            CenterVerticalFovDegrees = (Math.Atan(physical.Top / physical.Near) - Math.Atan(physical.Bottom / physical.Near)) * 180d / Math.PI;

            var viewportWidth = settings.AllowOutputResolutionMismatch ? Screen.width : output.CombinedWidthPx.Value;
            if (viewportWidth <= 0)
            {
                Release();
                return ProjectionOutcome.Degraded(layoutResult, "OUTPUT_RESOLUTION_UNAVAILABLE", "Unity has not reported a usable output width.");
            }

            var panelFraction = (float)panel.NativeWidthPx / viewportWidth;
            var targetRect = new Rect((1f - panelFraction) / 2f, 0f, panelFraction, 1f);
            _centerDriver.Configure(layoutResult.Sha256, ToUnityProjection(physical, camera), targetRect);

            if (_centerDriver.SuccessfulFrames == 0)
            {
                return ProjectionOutcome.Starting(layoutResult, "CENTER_PREVIEW_ARMED", "Center-panel projection is armed and waiting for its first rendered frame.");
            }

            return ProjectionOutcome.CenterPreviewActive(
                layoutResult,
                _centerDriver.LastSuccessfulFrameUtc,
                "CENTER_PREVIEW_ONLY",
                "Only the center physical viewport is rendered; side projections and final compositor are not implemented.");
        }
        catch (Exception exception)
        {
            Release();
            return ProjectionOutcome.Error(layoutResult, "PROJECTION_ERROR", SafeMessage(exception.Message));
        }
    }

    /// <summary>The switching outcome when this frame asked Unity for the mode's output size, otherwise null.</summary>
    private ProjectionOutcome RequestOutputSize(LayoutLoadResult layoutResult, int width, int height, string purpose)
    {
        if (LandingOwnsOutputResize) return ProjectionOutcome.Starting(layoutResult, "WINDOW_LANDING", "Waiting for the game window to finish resizing.");
        if (!_outputResolution.ShouldRequest(Display.main.systemWidth, Display.main.systemHeight, Screen.width, Screen.height,
                width, height, Time.realtimeSinceStartup))
            return null;
        var from = $"{Screen.width}x{Screen.height}";
        Screen.SetResolution(width, height, Screen.fullScreenMode);
        return ProjectionOutcome.Starting(layoutResult, "OUTPUT_RESOLUTION_SWITCHING",
            $"Switching the game output from {from} to {width}x{height} for {purpose}.");
    }

    internal void Release()
    {
        if (_centerDriver != null)
        {
            _centerDriver.Release();
            UnityEngine.Object.Destroy(_centerDriver);
        }

        if (_separateDriver != null)
        {
            _separateDriver.Release();
            UnityEngine.Object.Destroy(_separateDriver);
        }

        _centerDriver = null;
        _separateDriver = null;
        _camera = null;
        CenterVerticalFovDegrees = null;
        ActiveViewWidthScale = 1d;
    }

    private void EnsureCenterDriver(Camera camera)
    {
        if (_camera == camera && _centerDriver != null) return;
        Release();
        _camera = camera;
        _centerDriver = camera.GetComponent<CenterProjectionDriver>();
        if (_centerDriver == null) _centerDriver = camera.gameObject.AddComponent<CenterProjectionDriver>();
    }

    private void EnsureSeparateDriver(Camera camera)
    {
        if (_camera == camera && _separateDriver != null) return;
        Release();
        _camera = camera;
        _separateDriver = camera.GetComponent<SeparateDisplayRenderDriver>();
        if (_separateDriver == null) _separateDriver = camera.gameObject.AddComponent<SeparateDisplayRenderDriver>();
    }

    // Curve correction is opt-in: radius 0 keeps the direct flat path. Any failure latches flat output, with the original
    // flat frusta, until the output (mode, displays, pixels), rig or radius changes; the reason shows in the status.
    private readonly CurveLatch _curve = new();
    private string CurveDescription => _curve.Note != null ? " " + SafeMessage(_curve.Note) : "";

    // Bezel gap and curve come only from the local measurements (set in Advanced setup or by Wheelkit), so an imported
    // layout renders as before. The eye distance is to the plane through the center panel's edges (STD-047, toolkit
    // TripleRigDefinition); curvature changes only the warp. An unusable curve keeps the flat rig and is reported by ConfigureViews.
    private static double SavedCurve(Settings settings) =>
        settings.UseLocalLayout && settings.CurveRadiusMm > 0f && !float.IsInfinity(settings.CurveRadiusMm) ? settings.CurveRadiusMm : 0d;

    // The F7 "Angle correction" step keeps the angled views and drops only the warp.
    private static double RequestedCurve(Settings settings) =>
        TripleScreen.SessionStep == ComparisonStep.Angle ? 0d : SavedCurve(settings);

    // Shift+F7 "Original game view": TripleScreen releases to stock rendering for it.
    private static bool Original => TripleScreen.SessionStep == ComparisonStep.Original;

    internal bool CurveActive => _curve.Warps != null && _curve.Failure == null;
    internal string CurveFailure => _curve.Failure;
    internal static bool CurveAvailable(Settings settings) => SavedCurve(settings) > 0d && CurvedPanelPresenter.Available;

    /// <summary>The gameplay stage camera is live (menus, maps and cinematics are not).</summary>
    internal static bool InStage()
    {
        var camera = Camera.main;
        var rig = camera == null || camera.transform.parent == null ? null : camera.transform.parent.GetComponent<CarCameras>();
        return rig != null && camera.name == "Camera Main";
    }

    private static TripleRigDefinition Rig(PanelDocument panel, GeometryDocument geometry, double viewWidthScale, Settings settings)
    {
        var bezel = settings.UseLocalLayout ? panel.BezelWidthMm : 0d;
        var radius = RequestedCurve(settings);
        TripleRigDefinition Build(double curve) => new(panel.PhysicalWidthMm, panel.PhysicalHeightMm,
            geometry.EyeDistanceMm / viewWidthScale, geometry.LeftYawDegrees, geometry.RightYawDegrees,
            geometry.EyeHeightAbovePanelCenterMm, bezel, curve);
        try { return Build(radius); }
        catch (ArgumentOutOfRangeException) when (radius > 0d) { return Build(0d); }
    }

    private void ConfigureViews(IReadOnlyList<DisplaySurface> surfaces, TripleRigDefinition definition, Settings settings,
        string output, Action<CurvedPanelWarp[]> configure)
    {
        var radius = RequestedCurve(settings);
        var key = radius > 0d
            ? string.Join("|", output, radius, definition.PanelWidthMm, definition.PanelHeightMm, definition.EyeDistanceMm,
                definition.LeftAngleDegrees, definition.RightAngleDegrees, definition.EyeHeightAbovePanelCenterMm, definition.BezelGapMm)
            : null;
        var warps = _curve.Resolve(key, () =>
        {
            if (definition.CurveRadiusMm != radius)
                throw new InvalidOperationException("the radius must be at least half the panel width, with the eyes in front of the curve.");
            if (!CurvedPanelPresenter.Available) throw new InvalidOperationException("Unity's copy shader is unavailable.");
            // The warp is computed for the real eye (the measured rig); the views may render from a virtual eye for the
            // game's field of view (ActiveViewWidthScale). A warp for the virtual eye bowed straight edges on 1500R.
            var seat = ActiveViewWidthScale == 1d ? surfaces : TripleRigBuilder.Build(new TripleRigDefinition(
                definition.PanelWidthMm, definition.PanelHeightMm, definition.EyeDistanceMm * ActiveViewWidthScale,
                definition.LeftAngleDegrees, definition.RightAngleDegrees, definition.EyeHeightAbovePanelCenterMm,
                definition.BezelGapMm, definition.CurveRadiusMm));
            return surfaces.Select((s, i) => CurvedPanelWarp.Create(seat[i], new Vector3d(0d, 0d, 0d), radius).WithSourceOn(s)).ToArray();
        });
        configure(warps);
        if (warps == null || _separateDriver.CurveFailure == null) return;
        _curve.Fail(_separateDriver.CurveFailure);
        configure(null);
    }

    private (Matrix4x4[] Projections, Quaternion[] Rotations) Views(IReadOnlyList<DisplaySurface> surfaces,
        CurvedPanelWarp[] warps, Camera camera)
    {
        var projections = new Matrix4x4[3];
        var rotations = new Quaternion[3];
        for (var index = 0; index < 3; index++)
        {
            var view = CalculateView(warps?[index].SourceSurface ?? surfaces[index], camera);
            projections[index] = ToUnityProjection(view, camera);
            if (index == 1)
                CenterVerticalFovDegrees = (Math.Atan(view.Top / view.Near) - Math.Atan(view.Bottom / view.Near)) * 180d / Math.PI;
            var forward = view.CameraForward;
            rotations[index] = Quaternion.LookRotation(
                new Vector3((float)forward.X, (float)forward.Y, (float)-forward.Z), Vector3.up);
        }
        return (projections, rotations);
    }

    private static OffAxisProjection CalculateView(DisplaySurface surface, Camera camera)
    {
        return ProjectionCalculator.Calculate(
            surface,
            new Vector3d(0d, 0d, 0d),
            camera.nearClipPlane * 1000d,
            camera.farClipPlane * 1000d);
    }

    private static Matrix4x4 ToUnityProjection(OffAxisProjection physical, Camera camera)
    {
        var matrix = ProjectionCalculator.PerspectiveOffCenter(
            physical.Left / 1000d,
            physical.Right / 1000d,
            physical.Bottom / 1000d,
            physical.Top / 1000d,
            camera.nearClipPlane,
            camera.farClipPlane);
        return ToUnity(matrix);
    }

    private static Matrix4x4 ToUnity(Matrix4x4d source)
    {
        var matrix = new Matrix4x4();
        for (var row = 0; row < 4; row++)
        for (var column = 0; column < 4; column++)
            matrix[row, column] = (float)source[row, column];
        return matrix;
    }

    private static double SafeViewWidthScale(float requested)
    {
        if (float.IsNaN(requested) || float.IsInfinity(requested)) return 1d;
        return Math.Max(FovPreference.MinimumSliderScale,
            Math.Min(FovPreference.MaximumSliderScale, requested));
    }

    /// <summary>Whether the last projected view was a stock chase view; F7 and the status use it with the same policy.</summary>
    internal static bool LastChaseView { get; private set; }

    private static double ResolveViewWidthScale(PanelDocument panel, GeometryDocument geometry,
        Camera camera, Settings settings)
    {
        // Stock camera angles are chase views (the cars have no interiors); the mod's bonnet and bumper are mounted.
        var rig = camera.transform.parent != null ? camera.transform.parent.GetComponent<CarCameras>() : null;
        // A Custom Vehicles cockpit is a mounted view too (its own field of view, Realistic and F7 apply).
        var chase = rig != null && BonnetCamera.ActiveView(rig) == BonnetCamera.View.None && !TripleScreen.IsCustomCockpit(rig);
        LastChaseView = chase;
        // Every view follows the camera's own field of view. Realistic is a saved set per camera (CameraMountMemory) that
        // starts at the seat's true field of view (RealisticDegrees) and stays adjustable (owner 2026-10-31).
        return FovPreference.ScaleForGameDegrees(panel.PhysicalHeightMm, geometry.EyeDistanceMm,
            geometry.EyeHeightAbovePanelCenterMm, camera.fieldOfView);
    }

    /// <summary>The center screen's true vertical field of view from the measured seat (null without measurements).</summary>
    internal static double? RealisticDegrees(LayoutLoadResult layout)
    {
        var panel = layout?.Document?.Panel; var geometry = layout?.Document?.Geometry;
        if (panel == null || geometry == null) return null;
        try { return FovPreference.VerticalDegrees(panel.PhysicalHeightMm, geometry.EyeDistanceMm, geometry.EyeHeightAbovePanelCenterMm, 1d); }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    internal static bool Realistic(Settings settings) => settings.OverrideFieldOfView && !Original;

    private string FovDescription(Settings settings, Camera camera) =>
        $"a {camera.fieldOfView:0}° camera field of view" + (LastChaseView ? " (chase view, the game's)" : Realistic(settings) ? " (Realistic set)" : " (Game set)");

    private static string SafeMessage(string message)
    {
        var singleLine = (message ?? "Unknown projection error.").Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= 400 ? singleLine : singleLine.Substring(0, 400);
    }
}
