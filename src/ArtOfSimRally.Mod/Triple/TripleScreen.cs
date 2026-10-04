using System;
using System.IO;
using Dbce.TripleScreen.Protocol;
using UnityEngine;
using UnityModManagerNet;

namespace ArtOfSimRally.Mod.Triple;

/// <summary>
/// Triple-screen rendering, formerly the separate DbceTripleScreenArtOfRally mod
/// (0.3.12). <see cref="ArtOfSimRally.Mod.Main"/> drives it; it has no UMM entry
/// of its own. Settings stay in their own file (TripleScreen.xml) so the
/// accepted 0.3.12 values migrate unchanged.
/// </summary>
internal static class TripleScreen
{
    internal const string LegacyModId = "DbceTripleScreenArtOfRally";

    private static UnityModManager.ModEntry _modEntry;
    private static Settings _settings;
    private static LayoutSource Layout;
    private static readonly ResolvedLayoutSource EffectiveLayout = new();
    private static readonly ProjectionController Projection = new();
    private static StatusPublisher _status;
    private static RenderDiagnostics _renderDiagnostics;
    private static readonly MenuCenteringController MenuCentering = new();
    private static bool _enabled;
    private static bool _blockedByLegacyMod;
    private static int _frame;
    private static ProjectionOutcome _lastOutcome;
    private static bool _showAdvanced;

    internal static void Load(UnityModManager.ModEntry modEntry)
    {
        _modEntry = modEntry;
        MigrateFromLegacyMod(modEntry);
        try
        {
            _settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
        }
        catch (Exception exception)
        {
            modEntry.Logger.Warning("Triple screens: could not load settings; safe defaults are active: " + exception.Message);
            _settings = new Settings();
        }

        _status = new StatusPublisher(modEntry.Logger);
        _renderDiagnostics = new RenderDiagnostics(modEntry.Logger);
        Layout = new LayoutSource(Path.Combine(modEntry.Path, "desired-layout.json"));
        var layoutPath = AdapterConstants.DesiredLayoutPath;
        modEntry.Logger.Log("Triple screens: layout candidates: canonical=" + layoutPath +
                            " (visible=" + File.Exists(layoutPath) + "), staged=" +
                            Path.Combine(modEntry.Path, "desired-layout.json") +
                            " (visible=" + File.Exists(Path.Combine(modEntry.Path, "desired-layout.json")) + ").");
        Layout.Refresh(true);
        _enabled = true;
        _blockedByLegacyMod = LegacyModActive();

        var layout = EffectiveLayout.Resolve(Layout.Current, _settings);
        _lastOutcome = _settings.EnableThreeViewPrototype || _settings.EnableCenterPanelPreview || _settings.EnableSeparateDisplayPrototype
            ? ProjectionOutcome.Starting(layout, "ADAPTER_STARTING", "Adapter loaded; waiting to evaluate the stage camera.")
            : ProjectionOutcome.Inactive(layout, "FEATURE_DISABLED", "Triple-screen rendering is disabled; stock rendering is unchanged.");
        _status.Publish(_lastOutcome, Application.version, true);
        modEntry.Logger.Log("Triple screens: loaded v" + AdapterConstants.AdapterVersion + ".");
    }

    // The old standalone mod must not render alongside this one. The installer
    // retires it; a manual or drag-and-drop install may still leave it loaded.
    private static bool LegacyModActive()
    {
        var legacy = UnityModManager.FindMod(LegacyModId);
        return legacy != null && legacy.Active;
    }

    // First load after upgrading: copy the accepted settings and staged layout
    // from the old mod folder. Never overwrites, never deletes the originals.
    private static void MigrateFromLegacyMod(UnityModManager.ModEntry modEntry)
    {
        try
        {
            var modsDir = Path.GetDirectoryName(modEntry.Path.TrimEnd('\\', '/')) ?? "";
            foreach (var legacyDir in new[] { Path.Combine(modsDir, LegacyModId),
                                              Path.Combine(Path.GetDirectoryName(modsDir) ?? "", "Mods-retired", LegacyModId) })
            {
                if (!Directory.Exists(legacyDir)) continue;
                CopyIfMissing(Path.Combine(legacyDir, "Settings.xml"), new Settings().GetPath(modEntry), modEntry);
                CopyIfMissing(Path.Combine(legacyDir, "desired-layout.json"), Path.Combine(modEntry.Path, "desired-layout.json"), modEntry);
            }
        }
        catch (Exception exception)
        {
            modEntry.Logger.Warning("Triple screens: settings migration skipped: " + exception.Message);
        }
    }

    private static void CopyIfMissing(string from, string to, UnityModManager.ModEntry modEntry)
    {
        if (!File.Exists(from) || File.Exists(to)) return;
        File.Copy(from, to);
        modEntry.Logger.Log("Triple screens: migrated " + Path.GetFileName(from) + " from " + Path.GetDirectoryName(from) + ".");
    }

    internal static void Update()
    {
        if (_settings == null) return;
        _frame++;

        try
        {
            if (_frame % 120 == 0)
            {
                LogLayoutRefresh(Layout.Refresh(false));
                _blockedByLegacyMod = LegacyModActive();
            }
            var layout = EffectiveLayout.Resolve(Layout.Current, _settings);
            ProjectionOutcome outcome;
            if (!_enabled)
                outcome = ProjectionOutcome.Inactive(layout, "MOD_DISABLED", "The UMM mod is disabled; stock rendering is active.");
            else if (_blockedByLegacyMod)
            {
                Projection.Release();
                outcome = ProjectionOutcome.Inactive(layout, "LEGACY_MOD_ACTIVE",
                    "The old DBCE triple-screen mod is still installed. Disable or remove it in Unity Mod Manager.");
            }
            else
                outcome = Projection.Update(layout, _settings);
            _lastOutcome = outcome;
            _status.Publish(outcome, Application.version);
            _renderDiagnostics.Sample(outcome, Projection);
            if (_frame % 30 == 0) MenuCentering.Update(_enabled && !_blockedByLegacyMod && _settings.CenterMenusOnMiddleScreen, layout);
        }
        catch (Exception exception)
        {
            Projection.Release();
            _lastOutcome = ProjectionOutcome.Error(EffectiveLayout.Resolve(Layout.Current, _settings), "ADAPTER_UPDATE_ERROR", SafeMessage(exception.Message));
            _status.Publish(_lastOutcome, Application.version);
        }
    }

    /// <summary>Drawn inside the Cameras page of the wheel settings panel.</summary>
    internal static void DrawSettings()
    {
        if (_settings == null) return;
        if (_blockedByLegacyMod)
            GUILayout.Label("The old \"DBCE triple-screen for art of rally\" mod is still installed. Disable or remove it in Unity Mod Manager; this mod now includes triple screens.");
        if (float.IsNaN(_settings.ViewWidthScale) || float.IsInfinity(_settings.ViewWidthScale))
            _settings.ViewWidthScale = 1f;
        var mode = _settings.EnableSeparateDisplayPrototype ? 2 : _settings.EnableThreeViewPrototype ? 1 : 0;
        GUILayout.BeginHorizontal();
        if (GUILayout.Toggle(mode == 0, "Off", GUI.skin.button)) mode = 0;
        if (GUILayout.Toggle(mode == 1, "Single wide display", GUI.skin.button)) mode = 1;
        if (GUILayout.Toggle(mode == 2, "Three separate displays", GUI.skin.button)) mode = 2;
        GUILayout.EndHorizontal();
        _settings.EnableThreeViewPrototype = mode == 1;
        _settings.EnableSeparateDisplayPrototype = mode == 2;
        _settings.EnableCenterPanelPreview = false;

        var layout = EffectiveLayout.Resolve(Layout.Current, _settings);
        var panel = layout.Document?.Panel;
        var geometry = layout.Document?.Geometry;
        if (mode != 0)
            _settings.OverrideFieldOfView = GUILayout.Toggle(_settings.OverrideFieldOfView,
                "Override field of view");
        if (mode != 0 && !_settings.OverrideFieldOfView)
        {
            GUILayout.Label("Following the game's camera field of view on all three screens.");
            if (_lastOutcome != null && Projection.CenterVerticalFovDegrees.HasValue)
                GUILayout.Label($"Current center field of view: {Projection.CenterVerticalFovDegrees.Value:0}°");
        }
        else if (mode != 0 && panel != null && geometry != null)
        {
            var minimum = FovAtScale(layout, FovPreference.MinimumSliderScale);
            var maximum = FovAtScale(layout, FovPreference.MaximumSliderScale);
            var current = FovAtScale(layout, Mathf.Clamp(_settings.ViewWidthScale,
                (float)FovPreference.MinimumSliderScale, (float)FovPreference.MaximumSliderScale));
            GUILayout.Label($"Field of view: {current:0}°");
            var chosen = GUILayout.HorizontalSlider((float)current, (float)minimum, (float)maximum);
            _settings.ViewWidthScale = ScaleForFov(layout, chosen);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Narrower", GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            GUILayout.Label("Wider", GUILayout.Width(80));
            GUILayout.EndHorizontal();
            if (GUILayout.Button("Reset field of view")) _settings.ViewWidthScale = 1f;
        }
        else if (mode != 0)
            GUILayout.Label("Set up your screens in Advanced setup to enable field of view.");

        if (_lastOutcome != null && mode != 0)
            GUILayout.Label("Status: " + _lastOutcome.Diagnostic.Message);
        _showAdvanced = GUILayout.Toggle(_showAdvanced, "Advanced setup and diagnostics");
        if (!_showAdvanced) return;

        GUILayout.Label("Screen measurements");
        var imported = Layout.Current.IsSuccess;
        if (imported)
        {
            var local = GUILayout.Toggle(_settings.UseLocalLayout, "Use measurements entered here");
            _settings.UseLocalLayout = local;
            if (!_settings.UseLocalLayout) GUILayout.Label("Using imported measurements. The optimizer is optional.");
        }
        else
        {
            _settings.UseLocalLayout = true;
            GUILayout.Label("No imported measurements found. Enter yours here; no optimizer is required.");
        }
        if (_settings.UseLocalLayout)
        {
            GUILayout.Label("Enter the visible size of one panel and your distance from its center. The example values below are inactive until accepted.");
            _settings.PanelWidthPx = IntField("Panel width (pixels)", _settings.PanelWidthPx);
            _settings.PanelHeightPx = IntField("Panel height (pixels)", _settings.PanelHeightPx);
            _settings.PanelWidthMm = FloatField("Visible panel width (mm)", _settings.PanelWidthMm);
            _settings.PanelHeightMm = FloatField("Visible panel height (mm)", _settings.PanelHeightMm);
            _settings.EyeDistanceMm = FloatField("Eye distance (mm)", _settings.EyeDistanceMm);
            _settings.LeftYawDegrees = FloatField("Left screen angle (degrees)", _settings.LeftYawDegrees);
            _settings.RightYawDegrees = FloatField("Right screen angle (degrees)", _settings.RightYawDegrees);
            if (GUILayout.Button("Use these measurements")) _settings.ManualSetupConfirmed = true;
            GUILayout.Label(_settings.ManualSetupConfirmed ? "Local setup active" : "Local setup not yet active");
        }
        if (mode == 2)
        {
            GUILayout.Label("Center must be Windows primary. Exit the game after changing display mode; activated displays release on exit.");
            _settings.LeftDisplayIndex = IntField("Left display index", _settings.LeftDisplayIndex);
            _settings.RightDisplayIndex = IntField("Right display index", _settings.RightDisplayIndex);
        }
        _settings.CenterMenusOnMiddleScreen = GUILayout.Toggle(_settings.CenterMenusOnMiddleScreen,
            "Center title and main menu on middle screen");
        _settings.AllowOutputResolutionMismatch = GUILayout.Toggle(_settings.AllowOutputResolutionMismatch,
            "Developer: allow output resolution mismatch");
        if (GUILayout.Button("Reload imported layout")) LogLayoutRefresh(Layout.Refresh(true));
        GUILayout.Label(layout.IsSuccess ? "Effective layout: " + layout.Sha256.Substring(0, 12) + "…"
            : "Setup: " + layout.ErrorMessage);
        GUILayout.Label(_renderDiagnostics.PresentationSummary);
    }

    private static double FovAtScale(LayoutLoadResult layout, double scale)
    {
        var panel = layout.Document.Panel;
        var geometry = layout.Document.Geometry;
        return FovPreference.VerticalDegrees(panel.PhysicalHeightMm, geometry.EyeDistanceMm,
            geometry.EyeHeightAbovePanelCenterMm, scale);
    }

    private static float ScaleForFov(LayoutLoadResult layout, float degrees)
    {
        return (float)FovPreference.ScaleForSliderDegrees(layout.Document.Panel.PhysicalHeightMm,
            layout.Document.Geometry.EyeDistanceMm,
            layout.Document.Geometry.EyeHeightAbovePanelCenterMm, degrees);
    }

    private static int IntField(string label, int value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(210));
        var changed = GUILayout.TextField(value.ToString(), GUILayout.Width(80));
        GUILayout.EndHorizontal();
        return int.TryParse(changed, out var parsed) ? parsed : value;
    }

    private static float FloatField(string label, float value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(210));
        var changed = GUILayout.TextField(value.ToString("0.##"), GUILayout.Width(80));
        GUILayout.EndHorizontal();
        return float.TryParse(changed, out var parsed) ? parsed : value;
    }

    internal static void Save()
    {
        if (_settings == null || _modEntry == null) return;
        try { _settings.Save(_modEntry); }
        catch (Exception exception) { _modEntry.Logger.Warning("Triple screens: could not save settings: " + exception.Message); }
    }

    internal static void Toggle(bool value)
    {
        if (_settings == null) return;
        _enabled = value;
        if (!value)
        {
            Projection.Release();
            MenuCentering.Release();
            _lastOutcome = ProjectionOutcome.Inactive(Layout.Current, "MOD_DISABLED", "The UMM mod is disabled; stock rendering is active.");
        }
        else
        {
            _lastOutcome = ProjectionOutcome.Starting(Layout.Current, "ADAPTER_STARTING", "Adapter enabled; waiting to evaluate the stage camera.");
        }

        _status?.Publish(_lastOutcome, Application.version, true);
    }

    internal static void Unload()
    {
        if (_settings == null) return;
        _enabled = false;
        Projection.Release();
        MenuCentering.Release();
        var outcome = ProjectionOutcome.Inactive(Layout.Current, "ADAPTER_UNLOADED", "The adapter unloaded and restored stock camera state.");
        _status?.Publish(outcome, Application.version, true);
    }

    private static void LogLayoutRefresh(bool changed)
    {
        if (!changed || _modEntry == null) return;
        var layout = Layout.Current;
        if (layout.IsSuccess) _modEntry.Logger.Log("Accepted optimizer layout " + layout.Sha256.Substring(0, 12) +
                                                   "… from " + Layout.CurrentPath);
        else _modEntry.Logger.Warning((layout.ErrorCode ?? "LAYOUT_INVALID") + ": " + layout.ErrorMessage);
    }

    private static string SafeMessage(string message)
    {
        var singleLine = (message ?? "Unknown adapter error.").Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= 400 ? singleLine : singleLine.Substring(0, 400);
    }
}
