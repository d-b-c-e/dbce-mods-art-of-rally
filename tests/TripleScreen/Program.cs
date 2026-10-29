using System;
using System.IO;
using System.Linq;
using System.Text;
using ArtOfSimRally.Mod.Triple;
using Dbce.TripleScreen;
using Dbce.TripleScreen.Protocol;

namespace Dbce.TripleScreen.Tests;

internal static class Program
{
    private static int _assertions;

    private static int Main()
    {
        try
        {
            WindowLandingChecks.Run(Check);
            LandingSequenceChecks.Run(Check);
            DimensionsFromDiagonal();
            HingesAreContinuous();
            CenterProjectionIsSymmetric();
            SideProjectionsAreMirroredAndOffAxis();
            SharedEdgesLandOnAdjacentViewportBorders();
            SpanViewportsCoverTheOutput();
            ViewWidthOverridePreservesHinges();
            GameFovPreservesAllThreeSeams();
            IndependentSideAnglesArePreserved();
            MatrixMatchesFrustum();
            BadEyeSideIsRejected();
            CanonicalLayoutParsesStrictly();
            SeparateDisplayLayoutParsesWithoutWideOutput();
            ObservedRacingRowIsEvidenceOnly();
            DisplayIdentityEvidenceIsHypothesisOnly();
            SideDisplaysComeFromTheRacingRow();
            SideDisplaysAreProvenOnActivation();
            OutputResolutionSwitchesOnlyToTheDesktopSize();
            InvalidLayoutsAreRejected();
            StagedLayoutFallbackAndReload();
            RuntimeStatusMatchesCanonicalShape();
            Console.WriteLine($"PASS: {_assertions} triple-screen geometry assertions");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
    }

    private static void DimensionsFromDiagonal()
    {
        var dimensions = PanelDimensions.FromDiagonal(32d, 16d, 9d);
        Near(dimensions.WidthMm, 708.4d, 0.1d, "32-inch 16:9 width");
        Near(dimensions.HeightMm, 398.5d, 0.1d, "32-inch 16:9 height");
    }

    private static void HingesAreContinuous()
    {
        var surfaces = Rig();
        Near(surfaces[0].LowerRight, surfaces[1].LowerLeft, 1e-9, "left/center lower hinge");
        Near(surfaces[0].UpperLeft + (surfaces[0].Right * surfaces[0].Width), surfaces[1].UpperLeft, 1e-9, "left/center upper hinge");
        Near(surfaces[1].LowerRight, surfaces[2].LowerLeft, 1e-9, "center/right lower hinge");
    }

    private static void CenterProjectionIsSymmetric()
    {
        var center = ProjectionCalculator.Calculate(Rig()[1], new Vector3d(0d, 0d, 0d), 0.1d, 1500d);
        Near(center.Left, -center.Right, 1e-12, "center horizontal symmetry");
        Near(center.Bottom, -center.Top, 1e-12, "center vertical symmetry");
        Near(center.CameraForward, new Vector3d(0d, 0d, -1d), 1e-12, "center forward basis");
    }

    private static void SideProjectionsAreMirroredAndOffAxis()
    {
        var surfaces = Rig();
        var left = ProjectionCalculator.Calculate(surfaces[0], new Vector3d(0d, 0d, 0d), 0.1d, 1500d);
        var right = ProjectionCalculator.Calculate(surfaces[2], new Vector3d(0d, 0d, 0d), 0.1d, 1500d);
        Near(left.Left, -right.Right, 1e-12, "mirrored outer frustum edge");
        Near(left.Right, -right.Left, 1e-12, "mirrored inner frustum edge");
        Check(Math.Abs(left.Left + left.Right) > 1e-6, "arbitrary side angle must produce an asymmetric side frustum");
    }

    private static void MatrixMatchesFrustum()
    {
        var projection = ProjectionCalculator.Calculate(Rig()[1], new Vector3d(0d, 0d, 0d), 0.1d, 1500d);
        var expectedM00 = (2d * projection.Near) / (projection.Right - projection.Left);
        Near(projection.ProjectionMatrix[0, 0], expectedM00, 1e-12, "projection m00");
        Near(projection.ProjectionMatrix[3, 2], -1d, 0d, "projection perspective row");
    }

    private static void SharedEdgesLandOnAdjacentViewportBorders()
    {
        var surfaces = Rig();
        var projections = new[]
        {
            ProjectionCalculator.Calculate(surfaces[0], new Vector3d(0d, 0d, 0d), 0.1d, 1500d),
            ProjectionCalculator.Calculate(surfaces[1], new Vector3d(0d, 0d, 0d), 0.1d, 1500d),
            ProjectionCalculator.Calculate(surfaces[2], new Vector3d(0d, 0d, 0d), 0.1d, 1500d)
        };

        var leftHinge = surfaces[1].LowerLeft + (surfaces[1].Up * (surfaces[1].Height / 2d));
        var rightHinge = surfaces[1].LowerRight + (surfaces[1].Up * (surfaces[1].Height / 2d));
        Near(ProjectHorizontal(projections[0], leftHinge), 1d, 1e-9, "left view inner edge");
        Near(ProjectHorizontal(projections[1], leftHinge), -1d, 1e-9, "center view left edge");
        Near(ProjectHorizontal(projections[1], rightHinge), 1d, 1e-9, "center view right edge");
        Near(ProjectHorizontal(projections[2], rightHinge), -1d, 1e-9, "right view inner edge");
    }

    private static double ProjectHorizontal(OffAxisProjection view, Vector3d point)
    {
        var ray = point - view.CameraPosition;
        var depth = Vector3d.Dot(ray, view.CameraForward);
        var nearX = Vector3d.Dot(ray, view.CameraRight) * view.Near / depth;
        return (2d * nearX - view.Left - view.Right) / (view.Right - view.Left);
    }

    private static void SpanViewportsCoverTheOutput()
    {
        var left = TripleViewportLayout.ForPanelIndex(0);
        var center = TripleViewportLayout.ForPanelIndex(1);
        var right = TripleViewportLayout.ForPanelIndex(2);
        Near(left.X, 0d, 0d, "span begins at left edge");
        Near(left.X + left.Width, center.X, 1e-12, "span left/center border");
        Near(center.X + center.Width, right.X, 1e-12, "span center/right border");
        Near(right.X + right.Width, 1d, 1e-12, "span ends at right edge");
        Near(left.Height, 1d, 0d, "span fills output height");
        Check(left.Width == center.Width && center.Width == right.Width,
            "span panels must have equal viewport widths");
    }

    private static void ViewWidthOverridePreservesHinges()
    {
        var dimensions = PanelDimensions.FromDiagonal(32d, 16d, 9d);
        var measured = TripleRigBuilder.Build(new TripleRigDefinition(dimensions.WidthMm, dimensions.HeightMm, 700d, 60d));
        var widened = TripleRigBuilder.Build(new TripleRigDefinition(dimensions.WidthMm, dimensions.HeightMm, 700d / 1.5d, 60d));
        var measuredCenter = ProjectionCalculator.Calculate(measured[1], new Vector3d(0d, 0d, 0d), 0.1d, 1500d);
        var widerCenter = ProjectionCalculator.Calculate(widened[1], new Vector3d(0d, 0d, 0d), 0.1d, 1500d);
        Check(widerCenter.Right - widerCenter.Left > measuredCenter.Right - measuredCenter.Left,
            "view-width preference should widen the center frustum");

        var left = ProjectionCalculator.Calculate(widened[0], new Vector3d(0d, 0d, 0d), 0.1d, 1500d);
        var right = ProjectionCalculator.Calculate(widened[2], new Vector3d(0d, 0d, 0d), 0.1d, 1500d);
        var leftHinge = widened[1].LowerLeft + (widened[1].Up * (widened[1].Height / 2d));
        var rightHinge = widened[1].LowerRight + (widened[1].Up * (widened[1].Height / 2d));
        Near(ProjectHorizontal(left, leftHinge), 1d, 1e-9, "wider left inner edge");
        Near(ProjectHorizontal(widerCenter, leftHinge), -1d, 1e-9, "wider center left edge");
        Near(ProjectHorizontal(widerCenter, rightHinge), 1d, 1e-9, "wider center right edge");
        Near(ProjectHorizontal(right, rightHinge), -1d, 1e-9, "wider right inner edge");
    }

    private static void GameFovPreservesAllThreeSeams()
    {
        // The observed game camera uses 75° while the measured center view is
        // much narrower. Game-follow must permit values beyond the slider's
        // old 2x scale and apply one virtual eye to every physical view.
        const double width = 708.4d;
        const double height = 398.5d;
        const double distance = 660d;
        var wideScale = FovPreference.ScaleForGameDegrees(height, distance, 0d, 75d);
        Check(wideScale > FovPreference.MaximumSliderScale,
            "game FOV was incorrectly limited to the manual slider range");
        Near(FovPreference.VerticalDegrees(height, distance, 0d, wideScale), 75d,
            1e-9, "game camera FOV match");
        var tighterScale = FovPreference.ScaleForGameDegrees(height, distance, 0d, 65d);
        Check(tighterScale < wideScale, "live game FOV changes should update the shared scale");
        var savedSliderDegrees = FovPreference.VerticalDegrees(height, distance, 0d, 1.33d);
        Near(FovPreference.ScaleForSliderDegrees(height, distance, 0d, savedSliderDegrees),
            1.33d, 1e-9, "saved slider FOV is preserved when override is enabled");

        var surfaces = TripleRigBuilder.Build(new TripleRigDefinition(
            width, height, distance / wideScale, 70d, 70d));
        var views = new[]
        {
            ProjectionCalculator.Calculate(surfaces[0], new Vector3d(0d, 0d, 0d), 0.1d, 1500d),
            ProjectionCalculator.Calculate(surfaces[1], new Vector3d(0d, 0d, 0d), 0.1d, 1500d),
            ProjectionCalculator.Calculate(surfaces[2], new Vector3d(0d, 0d, 0d), 0.1d, 1500d)
        };
        var leftHinge = surfaces[1].LowerLeft + (surfaces[1].Up * (height / 2d));
        var rightHinge = surfaces[1].LowerRight + (surfaces[1].Up * (height / 2d));
        Near(ProjectHorizontal(views[0], leftHinge), 1d, 1e-9, "game-FOV left seam");
        Near(ProjectHorizontal(views[1], leftHinge), -1d, 1e-9, "game-FOV center left seam");
        Near(ProjectHorizontal(views[1], rightHinge), 1d, 1e-9, "game-FOV center right seam");
        Near(ProjectHorizontal(views[2], rightHinge), -1d, 1e-9, "game-FOV right seam");
    }

    private static void IndependentSideAnglesArePreserved()
    {
        var dimensions = PanelDimensions.FromDiagonal(32d, 16d, 9d);
        var surfaces = TripleRigBuilder.Build(new TripleRigDefinition(
            dimensions.WidthMm,
            dimensions.HeightMm,
            700d,
            45d,
            65d));
        Near(surfaces[0].CameraYawDegrees(), -45d, 1e-10, "left independent yaw");
        Near(surfaces[2].CameraYawDegrees(), 65d, 1e-10, "right independent yaw");
    }

    private static void BadEyeSideIsRejected()
    {
        var threw = false;
        try
        {
            ProjectionCalculator.Calculate(Rig()[1], new Vector3d(0d, 0d, -1000d), 0.1d, 1500d);
        }
        catch (ArgumentException)
        {
            threw = true;
        }

        Check(threw, "eye behind display must be rejected");
    }

    private static void CanonicalLayoutParsesStrictly()
    {
        var result = LayoutContractParser.Parse(Utf8(ValidLayout));
        Check(result.IsSuccess, result.ErrorMessage ?? "valid layout rejected");
        Check(result.Document?.Geometry?.LeftYawDegrees == 55d, "left yaw lost during parse");
        Check(result.Document?.Geometry?.RightYawDegrees == 60d, "right yaw lost during parse");
        Check(result.Sha256?.Length == 64, "layout SHA-256 missing");
    }

    private static void OutputResolutionSwitchesOnlyToTheDesktopSize()
    {
        // Owner 2026-10-24: Surround on (7680x1440 desktop) but the game kept its saved 2560x1440, so only the center drew.
        var span = new OutputResolution();
        Check(span.ShouldRequest(7680, 1440, 2560, 1440, 7680, 1440, 10f), "Surround desktop must switch a 2560 output to the span");
        Check(!span.ShouldRequest(7680, 1440, 2560, 1440, 7680, 1440, 11f), "a second request must wait for the retry interval");
        Check(span.ShouldRequest(7680, 1440, 2560, 1440, 7680, 1440, 13.5f), "a failed switch is retried after the interval");
        Check(span.ShouldRequest(7680, 1440, 2560, 1440, 7680, 1440, 17f), "third attempt");
        Check(!span.ShouldRequest(7680, 1440, 2560, 1440, 7680, 1440, 30f) && span.GaveUp, "the mod stops after three attempts");
        Check(!span.ShouldRequest(7680, 1440, 7680, 1440, 7680, 1440, 31f) && !span.GaveUp, "a matching output resets the attempts");
        Check(span.ShouldRequest(7680, 1440, 2560, 1440, 7680, 1440, 40f), "the game re-applying 2560 later is corrected again");

        var surroundOff = new OutputResolution();
        Check(!surroundOff.ShouldRequest(2560, 1440, 2560, 1440, 7680, 1440, 10f), "no span request while the desktop is a single monitor");

        var separate = new OutputResolution();
        Check(separate.ShouldRequest(2560, 1440, 7680, 1440, 2560, 1440, 10f), "a stale span size is returned to the center monitor");
        Check(!separate.ShouldRequest(2560, 1440, 2560, 1440, 2560, 1440, 11f), "a matching center output needs nothing");
    }

    private static void InvalidLayoutsAreRejected()
    {
        var unknown = LayoutContractParser.Parse(Utf8(ValidLayout.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"invented\":true")));
        Check(!unknown.IsSuccess && unknown.ErrorCode == "LAYOUT_INVALID_JSON", "unknown property accepted");

        var duplicate = LayoutContractParser.Parse(Utf8(ValidLayout.Replace("\"schemaVersion\":1", "\"schemaVersion\":1,\"schemaVersion\":1")));
        Check(!duplicate.IsSuccess && duplicate.ErrorCode == "LAYOUT_INVALID_JSON", "duplicate property accepted");

        var future = LayoutContractParser.Parse(Utf8(ValidLayout.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
        Check(!future.IsSuccess && future.ErrorCode == "LAYOUT_VERSION_UNSUPPORTED", "future contract guessed at");

        var badAngle = LayoutContractParser.Parse(Utf8(ValidLayout.Replace("\"leftYawDegrees\":55", "\"leftYawDegrees\":90")));
        Check(!badAngle.IsSuccess && badAngle.ErrorCode == "LAYOUT_GEOMETRY_INVALID", "invalid side angle accepted");

        var comment = LayoutContractParser.Parse(Utf8(ValidLayout + "/* trailing comment */"));
        Check(!comment.IsSuccess && comment.ErrorCode == "LAYOUT_COMMENTS_UNSUPPORTED", "JSON comment accepted");

        var trailing = LayoutContractParser.Parse(Utf8(ValidLayout + "{}"));
        Check(!trailing.IsSuccess, "trailing JSON value accepted");

        var oversized = LayoutContractParser.Parse(new byte[LayoutContractParser.MaximumDocumentBytes + 1]);
        Check(!oversized.IsSuccess && oversized.ErrorCode == "LAYOUT_TOO_LARGE", "oversized layout accepted");
    }

    private static void SeparateDisplayLayoutParsesWithoutWideOutput()
    {
        var independent = ValidLayout.Replace(
            "\"output\":{\"mode\":\"nvidia-surround\",\"combinedWidthPx\":7680,\"combinedHeightPx\":1440}",
            "\"output\":{\"mode\":\"separate-displays\"}");
        var result = LayoutContractParser.Parse(Utf8(independent));
        Check(result.IsSuccess, result.ErrorMessage ?? "separate-display layout rejected");
        Check(result.Document?.Output?.Mode == "separate-displays", "independent display mode lost");
        Check(result.Document?.Output?.CombinedWidthPx is null, "separate layout unexpectedly requires a wide canvas");
    }

    private static void RuntimeStatusMatchesCanonicalShape()
    {
        var status = new RuntimeStatusDocument
        {
            AdapterId = "dbce-triple-mod-art-of-rally",
            AdapterVersion = "0.1.0",
            GameId = "art-of-rally",
            State = "active",
            AcceptedLayoutSha256 = new string('a', 64),
            LayoutContractVersion = 1,
            Topology = "nvidia-surround",
            ActiveCameraCount = 1,
            LastSuccessfulFrameUtc = "2026-09-21T12:00:00.0000000Z"
        };
        status.ActiveCapabilities.Add("asymmetric-frustum");
        status.Diagnostics.Add(new RuntimeDiagnostic("CENTER_PREVIEW_ACTIVE", "info", "Center projection preview is active."));
        var json = RuntimeStatusJson.Serialize(status);
        Check(json.Contains("\"schemaVersion\": 1"), "status schema version missing");
        Check(json.Contains("\"state\": \"active\""), "status state missing");
        Check(json.Contains("\"activeCapabilities\": ["), "status capabilities missing");
    }

    private static void StagedLayoutFallbackAndReload()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dbce-triple-layout-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var canonicalPath = Path.Combine(directory, "canonical.json");
            var stagedPath = Path.Combine(directory, "staged.json");
            var source = new LayoutSource(stagedPath, canonicalPath);
            Check(!source.Refresh(true) && source.Current.ErrorCode == "LAYOUT_NOT_FOUND", "missing layout should remain rejected");

            File.WriteAllText(stagedPath, ValidLayout);
            Check(source.Refresh(false) && source.Current.IsSuccess, "staged layout fallback was not accepted");
            Check(source.CurrentPath == stagedPath, "staged path was not selected");
            Check(!source.Refresh(false), "unchanged staged layout should not reload");

            File.WriteAllText(canonicalPath, ValidLayout.Replace("\"leftYawDegrees\":55", "\"leftYawDegrees\":45"));
            Check(source.Refresh(false) && source.Current.IsSuccess, "canonical layout was not accepted");
            Check(source.CurrentPath == canonicalPath, "canonical path should take precedence");
            Check(source.Current.Document?.Geometry?.LeftYawDegrees == 45d, "canonical layout changes were not loaded");

            File.Delete(canonicalPath);
            Check(source.Refresh(false) && source.CurrentPath == stagedPath, "staged fallback was not restored");
            Check(source.Current.Document?.Geometry?.LeftYawDegrees == 55d, "staged fallback loaded the wrong layout");

            File.WriteAllBytes(stagedPath, new byte[LayoutContractParser.MaximumDocumentBytes + 1]);
            Check(source.Refresh(false) && source.Current.ErrorCode == "LAYOUT_TOO_LARGE", "oversized staged layout was not rejected");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static System.Collections.Generic.IReadOnlyList<DisplaySurface> Rig()
    {
        var dimensions = PanelDimensions.FromDiagonal(32d, 16d, 9d);
        return TripleRigBuilder.Build(new TripleRigDefinition(dimensions.WidthMm, dimensions.HeightMm, 700d, 60d));
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private const string ValidLayout = "{" +
        "\"schemaVersion\":1," +
        "\"panel\":{\"count\":3,\"nativeWidthPx\":2560,\"nativeHeightPx\":1440,\"physicalWidthMm\":708.4,\"physicalHeightMm\":398.5,\"curveRadiusMm\":1500,\"bezelWidthMm\":8}," +
        "\"geometry\":{\"eyeDistanceMm\":700,\"eyeHeightAbovePanelCenterMm\":10,\"leftYawDegrees\":55,\"rightYawDegrees\":60}," +
        "\"output\":{\"mode\":\"nvidia-surround\",\"combinedWidthPx\":7680,\"combinedHeightPx\":1440}" +
        "}";

    private static void Near(double actual, double expected, double tolerance, string name)
    {
        Check(Math.Abs(actual - expected) <= tolerance, $"{name}: expected {expected}, got {actual}");
    }

    private static void Near(Vector3d actual, Vector3d expected, double tolerance, string name)
    {
        Check((actual - expected).Length <= tolerance, $"{name}: expected {expected}, got {actual}");
    }

    // The diagnostic snapshot from the shared selector: the racing row and the displays outside it, with the saved
    // Unity indices stated as unused. Fixture: three 2560x1440 displays plus a 1920x1080 dashboard above.
    private static void ObservedRacingRowIsEvidenceOnly()
    {
        var left = new Dbce.Wheel.Display.MonitorRect(-2560, 0, 2560, 1440, false);
        var center = new Dbce.Wheel.Display.MonitorRect(0, 0, 2560, 1440, true);
        var right = new Dbce.Wheel.Display.MonitorRect(2560, 0, 2560, 1440, false);
        var dashboard = new Dbce.Wheel.Display.MonitorRect(314, -1080, 1920, 1080, false);

        var row = ObservedRacingRow.Describe(new[] { dashboard, right, center, left });
        Check(row.Kind == "Separate" && row.X == -2560 && row.Y == 0 && row.Width == 7680 && row.Height == 1440, "observed row: " + row.Summary);
        Check(row.RowDisplays.Length == 3 && row.RowDisplays[0].X == -2560 && row.RowDisplays[1].Primary && row.RowDisplays[2].X == 2560,
            "row displays left to right");
        Check(row.AuxiliaryDisplays.Length == 1 && row.AuxiliaryDisplays[0].Width == 1920 && row.AuxiliaryDisplays[0].Y == -1080,
            "the dashboard is reported outside the row");
        Check(row.WindowsDisplays.Length == 4 && row.Error == null, "every Windows display is listed");
        Check(row.IndexCorrelation.StartsWith("side views use this row", StringComparison.Ordinal) && row.IndexCorrelation.Contains("LeftDisplayIndex/RightDisplayIndex are not used"),
            "the saved Unity indices are stated as unused");
        Check(row.Summary == "Separate 7680x1440 at -2560,0; 1 other display(s)", "summary line: " + row.Summary);

        var three = ObservedRacingRow.Describe(new[] { left, center, right });
        Check(three.Kind == "Separate" && three.AuxiliaryDisplays.Length == 0, "the plain three-display row is unchanged");

        var refused = ObservedRacingRow.Describe(new[] { new Dbce.Wheel.Display.MonitorRect(0, 0, 2560, 1440, true), right,
            new Dbce.Wheel.Display.MonitorRect(5120, 0, 2560, 1440, false) });
        Check(refused.Kind == "None" && refused.RowDisplays.Length == 0 && refused.AuxiliaryDisplays.Length == 0 &&
            refused.WindowsDisplays.Length == 3 && refused.Summary.StartsWith("no racing row", StringComparison.Ordinal),
            "no row: nothing is called auxiliary, every display is still listed");
    }

    // The identity diagnostic's pure parts: the X-order hypothesis and the two probe conventions. Fixture: this rig's
    // Windows enumeration order (right, center, left, then a 1920x1080 DisplayLink dashboard above).
    private static void DisplayIdentityEvidenceIsHypothesisOnly()
    {
        var left = new Dbce.Wheel.Display.MonitorRect(-2560, 0, 2560, 1440, false);
        var center = new Dbce.Wheel.Display.MonitorRect(0, 0, 2560, 1440, true);
        var right = new Dbce.Wheel.Display.MonitorRect(2560, 0, 2560, 1440, false);
        var dashboard = new Dbce.Wheel.Display.MonitorRect(314, -1080, 1920, 1080, false);

        var rig = ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PredictXOrder(new[] { right, center, left, dashboard });
        Check(rig.SequenceEqual(new[] { center, left, dashboard, right }), "rig: primary, then left, dashboard, right by X");
        var three = ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PredictXOrder(new[] { right, left, center });
        Check(three.SequenceEqual(new[] { center, left, right }), "three displays: experiment 008's left=1, right=2");
        var sameSize = new Dbce.Wheel.Display.MonitorRect(0, -1440, 2560, 1440, false);
        var stacked = ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PredictXOrder(new[] { sameSize, right, center, left });
        Check(stacked.SequenceEqual(new[] { center, left, sameSize, right }), "a same-size dashboard above sorts by X between the sides");
        var low = new Dbce.Wheel.Display.MonitorRect(2560, 1440, 1920, 1080, false);
        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PredictXOrder(new[] { low, center, right })
            .SequenceEqual(new[] { center, right, low }), "equal X: Y breaks the tie");
        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PredictXOrder(new[] { left, right }).Count == 0, "no primary: no prediction");
        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PredictXOrder(new[] { center, new Dbce.Wheel.Display.MonitorRect(2560, 0, 2560, 1440, true) }).Count == 0,
            "two primaries: no prediction");

        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.DesktopCenter(left) == (-1280, 720), "desktop center, left");
        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.DesktopCenter(dashboard) == (1274, -540), "desktop center, dashboard");
        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PrimaryBottomUpCenter(left, center) == (-1280, 720), "bottom-up center, left");
        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PrimaryBottomUpCenter(dashboard, center) == (1274, 1980), "bottom-up center, dashboard above");
        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.PrimaryBottomUpCenter(right, center) == (3840, 720), "bottom-up center, right");

        var rect = (Func<Dbce.Wheel.Display.MonitorRect, ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.Rect>)ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.ToRect;
        var snapshot = new ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.Snapshot
        {
            Displays = new[]
            {
                new ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.UnityDisplay { Index = 0, Hmonitor = rect(center), XOrderPrediction = rect(center), XOrderSizeAgrees = true },
                new ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.UnityDisplay { Index = 1, XOrderPrediction = rect(left), XOrderSizeAgrees = true },
                new ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.UnityDisplay { Index = 2, XOrderPrediction = rect(dashboard), XOrderSizeAgrees = false },
            },
            Probes = new[]
            {
                new ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.Probe { Target = rect(left), Convention = ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.DesktopConvention, ResultIndex = 1 },
                new ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.Probe { Target = rect(dashboard), Convention = ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.BottomUpConvention, Error = "x" },
            },
        };
        var summary = ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.Summarize(snapshot);
        Check(summary == "nativeDisplay is an HMONITOR for 1/3 [0=0,0 2560x1440 primary] | RelativeMouseAt desktop [-2560,0->1] | " +
            "RelativeMouseAt primaryBottomUp [314,-1080->error] | X-order hypothesis [0=0,0; 1=-2560,0; 2=314,-1080 size differs]", "summary: " + summary);
        Check(ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.Summarize(new ArtOfSimRally.Mod.Triple.DisplayIdentityEvidence.Snapshot { Error = "E: no" })
            == "identity read failed: E: no", "failed read summary");
        Check(snapshot.Scope.StartsWith("diagnostic only", StringComparison.Ordinal) && snapshot.Conventions.Length == 2, "scope and conventions stated");
    }

    // The side displays come from the racing row and Unity's position-sorted order, never from the saved indices or a
    // dashboard; every refusal names why. Fixture: this rig (Windows order right, center, left, dashboard).
    private static void SideDisplaysComeFromTheRacingRow()
    {
        var left = new Dbce.Wheel.Display.MonitorRect(-2560, 0, 2560, 1440, false);
        var center = new Dbce.Wheel.Display.MonitorRect(0, 0, 2560, 1440, true);
        var right = new Dbce.Wheel.Display.MonitorRect(2560, 0, 2560, 1440, false);
        var dashboard = new Dbce.Wheel.Display.MonitorRect(314, -1080, 1920, 1080, false);
        var rig = new[] { right, center, left, dashboard };
        (int, int) big = (2560, 1440), small = (1920, 1080);
        var none = Array.Empty<string>();

        var selected = SideDisplaySelector.Select(rig, new[] { big, big, small, big }, none, 2560, 1440);
        Check(selected.Ok && selected.LeftIndex == 1 && selected.RightIndex == 3 && selected.Left.Equals(left) && selected.Right.Equals(right),
            "rig: left = Unity 1, right = Unity 3, the dashboard (2) is not chosen: " + selected.Message);
        var three = SideDisplaySelector.Select(new[] { left, right, center }, new[] { big, big, big }, none, 2560, 1440);
        Check(three.Ok && three.LeftIndex == 1 && three.RightIndex == 2, "three displays: experiment 008's left = 1, right = 2");
        var sameSize = new Dbce.Wheel.Display.MonitorRect(0, -1440, 2560, 1440, false);
        var stacked = SideDisplaySelector.Select(new[] { sameSize, right, center, left }, new[] { big, big, big, big }, none, 2560, 1440);
        Check(stacked.Ok && stacked.LeftIndex == 1 && stacked.RightIndex == 3, "a same-size dashboard above is never a side");

        Check(SideDisplaySelector.Select(rig, new[] { big, big, small, big }, new[] { "artofrally.exe", "-MultiDisplay" }, 2560, 1440).Code == "DISPLAY_ORDER_UNSORTED",
            "-multidisplay (any case) disables Unity's sort, so nothing is predicted");
        Check(SideDisplaySelector.Select(rig, new[] { big, big, big }, none, 2560, 1440).Code == "DISPLAY_IDENTITY_UNPROVEN", "count mismatch refuses");
        Check(SideDisplaySelector.Select(rig, new[] { big, small, big, big }, none, 2560, 1440).Code == "DISPLAY_IDENTITY_UNPROVEN", "size disagreement refuses");
        var offCenter = new Dbce.Wheel.Display.MonitorRect(-2560, 0, 2560, 1440, true);
        Check(SideDisplaySelector.Select(new[] { offCenter, new Dbce.Wheel.Display.MonitorRect(0, 0, 2560, 1440, false), right }, new[] { big, big, big }, none, 2560, 1440).Code == "RACING_ROW_UNAVAILABLE",
            "a primary that is not the center refuses");
        Check(SideDisplaySelector.Select(rig, new[] { big, big, small, big }, none, 1920, 1080).Code == "RACING_ROW_SIZE_MISMATCH", "panel size differs from the row");
    }

    // Each side is activated alone and must open exactly one new window that fills its expected monitor; any other
    // result fails for the process without trying another monitor.
    private static void SideDisplaysAreProvenOnActivation()
    {
        var left = new Dbce.Wheel.Display.MonitorRect(-2560, 0, 2560, 1440, false);
        var center = new Dbce.Wheel.Display.MonitorRect(0, 0, 2560, 1440, true);
        var onCenter = new SideActivationWitness.MainWindow(1, 0, 0, 2560, 1440, "fixture");
        var right = new Dbce.Wheel.Display.MonitorRect(2560, 0, 2560, 1440, false);
        var dashboard = new Dbce.Wheel.Display.MonitorRect(314, -1080, 1920, 1080, false);
        SideActivationWitness.SecondaryWindow W(long handle, Dbce.Wheel.Display.MonitorRect? monitor) => new(handle, monitor, "fixture");
        var none = Array.Empty<SideActivationWitness.SecondaryWindow>();

        var activated = new System.Collections.Generic.List<int>();
        var witness = new SideActivationWitness(1, 3, left, center, right);
        Check(witness.Step(0, onCenter, none, activated.Add) == SideActivationWitness.Phase.ProvingLeft && activated.SequenceEqual(new[] { 1 }), "left activated alone first");
        Check(witness.Step(0.1, onCenter, new[] { W(10, null) }, activated.Add) == SideActivationWitness.Phase.ProvingLeft, "an unplaced window is waited for");
        Check(witness.Step(0.2, onCenter, new[] { W(10, left) }, activated.Add) == SideActivationWitness.Phase.ProvingRight && activated.SequenceEqual(new[] { 1, 3 }),
            "left proven, then right activated");
        Check(witness.Step(0.3, onCenter, new[] { W(10, left), W(30, right) }, activated.Add) == SideActivationWitness.Phase.Proven &&
              witness.Code == "SIDE_DISPLAYS_PROVEN" && activated.Count == 2, "both sides proven: " + witness.Message);
        Check(witness.Step(0.4, onCenter, none, activated.Add) == SideActivationWitness.Phase.Proven && activated.Count == 2, "proven stays proven");

        activated.Clear();
        var existing = new SideActivationWitness(1, 3, left, center, right);
        Check(existing.Step(0, onCenter, new[] { W(7, left) }, activated.Add) == SideActivationWitness.Phase.Failed &&
              existing.Code == "SECONDARY_DISPLAY_STATE_UNKNOWN" && activated.Count == 0, "a pre-existing window cannot be attributed");

        var swapped = new SideActivationWitness(1, 3, left, center, right);
        swapped.Step(0, onCenter, none, activated.Add);
        Check(swapped.Step(0.1, onCenter, new[] { W(10, right) }, activated.Add) == SideActivationWitness.Phase.Failed &&
              swapped.Code == "SECONDARY_DISPLAY_IDENTITY_MISMATCH" && activated.SequenceEqual(new[] { 1 }), "left on the right monitor fails before the right is activated");
        Check(swapped.Step(0.2, onCenter, new[] { W(10, right) }, activated.Add) == SideActivationWitness.Phase.Failed && activated.Count == 1, "a failure is final");

        activated.Clear();
        var onDashboard = new SideActivationWitness(1, 3, left, center, right);
        onDashboard.Step(0, onCenter, none, activated.Add);
        Check(onDashboard.Step(0.1, onCenter, new[] { W(10, dashboard) }, activated.Add) == SideActivationWitness.Phase.Failed &&
              onDashboard.Code == "SECONDARY_DISPLAY_IDENTITY_MISMATCH", "a window on the dashboard fails");

        var ambiguous = new SideActivationWitness(1, 3, left, center, right);
        ambiguous.Step(0, onCenter, none, _ => { });
        Check(ambiguous.Step(0.1, onCenter, new[] { W(10, left), W(11, right) }, _ => { }) == SideActivationWitness.Phase.Failed &&
              ambiguous.Code == "SECONDARY_DISPLAY_IDENTITY_AMBIGUOUS", "two new windows are ambiguous");

        var slow = new SideActivationWitness(1, 3, left, center, right, 5);
        slow.Step(10, onCenter, none, _ => { });
        Check(slow.Step(14.9, onCenter, none, _ => { }) == SideActivationWitness.Phase.ProvingLeft, "within the time limit it waits");
        Check(slow.Step(15.1, onCenter, new[] { W(10, null) }, _ => { }) == SideActivationWitness.Phase.Failed && slow.Code == "SECONDARY_DISPLAY_WINDOW_TIMEOUT",
            "no placed window in time fails");

        activated.Clear();
        var rightWrong = new SideActivationWitness(1, 3, left, center, right);
        rightWrong.Step(0, onCenter, none, activated.Add);
        rightWrong.Step(0.1, onCenter, new[] { W(10, left) }, activated.Add);
        Check(rightWrong.Step(0.2, onCenter, new[] { W(10, left), W(30, dashboard) }, activated.Add) == SideActivationWitness.Phase.Failed &&
              rightWrong.Code == "SECONDARY_DISPLAY_IDENTITY_MISMATCH" && activated.SequenceEqual(new[] { 1, 3 }), "a wrong right side fails");
        Check(rightWrong.Observations.Count >= 4, "observations are kept as evidence");

        // The center first (sana 81671): nothing is activated until the game's own main window fills the row's center
        // display, and that refusal is not final.
        activated.Clear();
        var placed = new SideActivationWitness(1, 3, left, center, right);
        var onLeft = new SideActivationWitness.MainWindow(1, -2560, 0, 2560, 1440, "fixture");
        Check(placed.Step(0, onLeft, none, activated.Add) == SideActivationWitness.Phase.NotStarted &&
              placed.Code == "MAIN_WINDOW_NOT_ROW_CENTER" && activated.Count == 0, "a game window on another display activates nothing");
        var windowed = new SideActivationWitness.MainWindow(1, 8, 31, 2544, 1401, "fixture");
        Check(placed.Step(0.5, windowed, none, activated.Add) == SideActivationWitness.Phase.NotStarted && activated.Count == 0,
            "a windowed picture that does not fill the center activates nothing");
        Check(placed.Step(1, new SideActivationWitness.MainWindow(2, 0, 0, 0, 0, "two"), none, activated.Add) == SideActivationWitness.Phase.NotStarted &&
              placed.Code == "MAIN_WINDOW_UNKNOWN" && activated.Count == 0, "two main windows cannot confirm the center");
        Check(placed.Step(2, onCenter, none, activated.Add) == SideActivationWitness.Phase.ProvingLeft && activated.SequenceEqual(new[] { 1 }),
            "once the game fills the center, the left side is activated");

        // A failed activation is final, its side is called exactly once, and nothing is activated afterwards.
        var calls = new System.Collections.Generic.List<int>();
        var throwsRight = new SideActivationWitness(1, 3, left, center, right);
        Action<int> activateThrowingRight = index => { calls.Add(index); if (index == 3) throw new InvalidOperationException("display\nunavailable"); };
        throwsRight.Step(0, onCenter, none, activateThrowingRight);
        Check(throwsRight.Step(0.1, onCenter, new[] { W(10, left) }, activateThrowingRight) == SideActivationWitness.Phase.Failed &&
              throwsRight.Code == "SECONDARY_DISPLAY_ACTIVATION_FAILED" && calls.SequenceEqual(new[] { 1, 3 }) && !throwsRight.Message.Contains("\n"),
            "a throwing right activation fails finally: " + throwsRight.Message);
        Check(throwsRight.Step(0.2, onCenter, new[] { W(10, left), W(30, right) }, activateThrowingRight) == SideActivationWitness.Phase.Failed &&
              calls.Count == 2, "nothing is activated or proven after the failure");
        Check(!throwsRight.Observations.Any(o => o.StartsWith("activated Unity 3", StringComparison.Ordinal)), "a failed activation is not recorded as activated");
        var throwsLeft = new SideActivationWitness(1, 3, left, center, right);
        Check(throwsLeft.Step(0, onCenter, none, _ => throw new InvalidOperationException("no")) == SideActivationWitness.Phase.Failed &&
              throwsLeft.Code == "SECONDARY_DISPLAY_ACTIVATION_FAILED", "a throwing left activation fails at once");
        var threw = false;
        try { _ = new SideActivationWitness(0, 3, left, center, right); } catch (ArgumentException) { threw = true; }
        Check(threw, "index 0 (the primary) is never a side");
    }

    private static void Check(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}

internal static class SurfaceTestExtensions
{
    internal static double CameraYawDegrees(this DisplaySurface surface) =>
        Math.Atan2(surface.CameraForward().X, -surface.CameraForward().Z) * 180d / Math.PI;

    private static Vector3d CameraForward(this DisplaySurface surface) => -surface.NormalTowardViewer;
}
