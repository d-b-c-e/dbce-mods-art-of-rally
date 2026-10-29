using System.IO;
using UnityModManagerNet;

namespace ArtOfSimRally.Mod.Triple;

public sealed class Settings : UnityModManager.ModSettings
{
    // Own file beside the wheel's Settings.xml; the XML root stays <Settings>,
    // so the old standalone mod's Settings.xml loads unchanged.
    public override string GetPath(UnityModManager.ModEntry modEntry) =>
        Path.Combine(modEntry.Path, "TripleScreen.xml");

    // Deliberately off until the attended center-panel experiment is performed.
    public bool EnableCenterPanelPreview;

    // One wide output with three direct camera viewports. Never enabled
    // automatically by an imported layout or a package update.
    public bool EnableThreeViewPrototype;

    // Independent physical-display path. Requires a separate-displays layout,
    // three Windows monitors, the center as primary, and an attended restart.
    public bool EnableSeparateDisplayPrototype;
    public int LeftDisplayIndex = 1;
    public int RightDisplayIndex = 2;

    // Existing installs keep their current slider behavior. When disabled,
    // all three views follow the game's live camera FOV instead.
    public bool OverrideFieldOfView = true;

    // 1 uses the measured eye distance. Larger values widen all three views
    // together; this is a visual preference, not a new physical measurement.
    public float ViewWidthScale = 1f;

    // Stock chase views keep the game's framing (all three panels share that eye); bonnet and bumper stay measured.
    public bool ChaseUsesGameFov = true;

    // Monitor curve radius in millimeters (1500 for a 1500R panel); 0 is flat and keeps the direct render path.
    public float CurveRadiusMm;

    // Gap between adjacent lit areas at each seam (both bezels), in millimeters; the scene behind it is skipped.
    public float BezelGapMm;

    // A mod-local rig works without Triple Screen Optimizer. The example values
    // are inert until the player explicitly accepts them in Advanced setup.
    public bool UseLocalLayout;
    public bool ManualSetupConfirmed;
    public int PanelWidthPx = 2560;
    public int PanelHeightPx = 1440;
    public float PanelWidthMm = 598f;
    public float PanelHeightMm = 336f;
    public float EyeDistanceMm = 600f;
    public float LeftYawDegrees = 45f;
    public float RightYawDegrees = 45f;

    // Safe escape hatch for development only. The default refuses to touch the
    // camera unless Screen.width/height exactly match the optimizer contract.
    public bool AllowOutputResolutionMismatch;

    // UI-only feature, independent of the experimental projection renderer.
    public bool CenterMenusOnMiddleScreen;
}
