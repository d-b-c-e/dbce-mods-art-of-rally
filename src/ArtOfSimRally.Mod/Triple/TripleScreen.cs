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
    private static readonly RacingWindowLanding Landing = new(new NativeWindowLandingHost());
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
        SessionStep = F7Comparison.FromSaved(_settings.F7View);

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

    // F7 comparison, saved as Settings.F7View and restored at load: null (never pressed) follows the saved choice. Steps
    // change the scene correction (original / angle / angle + curve); framing, radius and geometry stay. A saved framing
    // or output change returns to the saved pick (clearing a renderer fallback, which is never saved).
    internal static ComparisonStep? SessionStep;
    private static string _sessionKey;
    private static string _notice;
    private static float _noticeUntil;
    private static GUIStyle _noticeStyle;

    private static string SessionKey() => string.Join("|", _settings.OverrideFieldOfView, _settings.ViewWidthScale,
        _settings.ChaseUsesGameFov, _settings.CurveRadiusMm, _settings.EnableThreeViewPrototype, _settings.EnableSeparateDisplayPrototype);

    private static void Notify(string text)
    {
        _notice = text;
        _noticeUntil = Time.unscaledTime + 2.5f;
    }

    private static void ToggleSessionView()
    {
        if (!ProjectionController.InStage()) { Notify("Comparison available while driving"); return; }
        var curveAvailable = ProjectionController.CurveAvailable(_settings);
        SessionStep = F7Comparison.Next(F7Comparison.Current(SessionStep, Projection.CurveActive), curveAvailable);
        _sessionKey = SessionKey();
        var saved = SaveF7View((int)SessionStep.Value);
        var measured = !FovPreference.ShowsGameFov(_settings.OverrideFieldOfView, ProjectionController.LastChaseView, _settings.ChaseUsesGameFov);
        Notify(F7Comparison.Label(SessionStep.Value, measured, curveAvailable, saved));
        var eye = EffectiveLayout.Resolve(Layout.Current, _settings).Document?.Geometry?.EyeDistanceMm / Projection.ActiveViewWidthScale;
        _modEntry.Logger.Log("F7 comparison: " + _notice + "; camera " + (ProjectionController.LastChaseView ? "chase" : "mounted") +
            ", framing " + (measured ? "measured" : "game") + ", effective eye " + (eye.HasValue ? eye.Value.ToString("0") + " mm" : "unknown") +
            ", radius " + (curveAvailable ? _settings.CurveRadiusMm.ToString("0") + " mm" : "none") + ".");
    }

    // A requested curve that falls back reports the actual view once.
    private static void ReportCurveFallback()
    {
        if (SessionStep != ComparisonStep.AngleCurve || Projection.CurveFailure == null) return;
        SessionStep = ComparisonStep.Angle;
        Notify("Angle correction (curve unavailable: " + Projection.CurveFailure + ")");
        _modEntry.Logger.Warning("F7 comparison: curve fell back to flat: " + Projection.CurveFailure);
    }

    /// <summary>The brief F7 label, centered on the center screen over a dark band.</summary>
    internal static void DrawNotice()
    {
        if (_notice == null || Time.unscaledTime > _noticeUntil || Event.current == null || Event.current.type != EventType.Repaint) return;
        if (_noticeStyle == null) _noticeStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(18, Screen.height / 40), alignment = TextAnchor.MiddleCenter };
        var center = Screen.width / 2f; // middle of a span is the center screen
        var height = _noticeStyle.fontSize * 2.4f;
        var rect = new Rect(center - 520f, (Screen.height - height) / 2f, 1040f, height);
        var old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f); GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white; GUI.Label(rect, _notice, _noticeStyle);
        GUI.color = old;
    }

    internal static void Update()
    {
        if (_settings == null) return;
        _frame++;
        if (_enabled && Application.isFocused && !SettingsPanel.Editing && !CameraKeys.ModifierHeld() && Input.GetKeyDown(KeyCode.F7)
            && (_settings.EnableThreeViewPrototype || _settings.EnableSeparateDisplayPrototype))
            ToggleSessionView();

        try
        {
            if (_frame % 120 == 0)
            {
                LogLayoutRefresh(Layout.Refresh(false));
                _blockedByLegacyMod = LegacyModActive();
            }
            if (_enabled && !_blockedByLegacyMod && _frame % 30 == 0) Landing.Update();
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
            {
                if (_sessionKey != SessionKey()) { SessionStep = F7Comparison.FromSaved(_settings.F7View); _sessionKey = SessionKey(); }
                Projection.LandingOwnsOutputResize = Landing.BlocksOutputResize;
                if (SessionStep == ComparisonStep.Original)
                {
                    Projection.Release();
                    outcome = ProjectionOutcome.Inactive(layout, "F7_ORIGINAL_VIEW", "Original game view (picked with F7); stock rendering.");
                }
                else
                {
                    outcome = Projection.Update(layout, _settings);
                    ReportCurveFallback();
                }
            }
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

    // STD-022's three-way selector in the owner's wording (toolkit STANDARDS.md); the stored flags are unchanged.
    private static readonly string[] Modes = { "Off", "Surround", "Separate monitors" };

    /// <summary>Drawn inside the Cameras page of the wheel settings panel, on the panel's shared rows
    /// (<see cref="Dbce.Wheel.Ui.Unity.F6Widgets"/>). Only the presentation is shared: the mode flags, the field-of-view
    /// math and the measurements are this mod's own, and nothing is written on a draw without input.</summary>
    internal static void DrawSettings(Dbce.Wheel.Ui.Unity.F6Widgets w)
    {
        if (_settings == null) return;
        if (_blockedByLegacyMod)
            w.StatusCard("triple-legacy", "The old triple-screen mod is still installed",
                "Disable or remove \"DBCE triple-screen for art of rally\" in Unity Mod Manager; this mod now includes triple screens.", true, null);
        if (float.IsNaN(_settings.ViewWidthScale) || float.IsInfinity(_settings.ViewWidthScale))
            _settings.ViewWidthScale = 1f;
        var mode = _settings.EnableSeparateDisplayPrototype ? 2 : _settings.EnableThreeViewPrototype ? 1 : 0;
        int picked = w.ChoiceRow("triple-mode", "Triple screens", null, Modes, mode, true);
        if (picked >= 0) mode = picked;
        _settings.EnableThreeViewPrototype = mode == 1;
        _settings.EnableSeparateDisplayPrototype = mode == 2;
        _settings.EnableCenterPanelPreview = false;

        var layout = EffectiveLayout.Resolve(Layout.Current, _settings);
        var panel = layout.Document?.Panel;
        var geometry = layout.Document?.Geometry;
        // The game's field of view is the default framing (owner policy, ISS-0005); the measured perspective is one
        // press away and one press back. Nothing is reset on load.
        if (mode != 0)
        {
            var seat = MatchesSeat(_settings);
            w.Help(!_settings.OverrideFieldOfView ? "View: the game's field of view on all three screens."
                : seat ? "View: measured perspective from your seat" +
                    (_settings.ChaseUsesGameFov ? " in bonnet and bumper views; chase views keep the game's framing." : ".")
                : "View: widened measured perspective (Advanced).");
            if (SessionStep.HasValue)
                w.Help("F7 comparison is showing: " + (SessionStep == ComparisonStep.Original ? "original game view"
                    : SessionStep == ComparisonStep.Angle ? "angle correction" : "angle + curve correction") +
                    (F7Comparison.FromSaved(_settings.F7View) == SessionStep
                        ? " (picked with F7, kept next time; press F7 while driving to change it)."
                        : " for now; next time starts from your saved choice."));
            if (!seat && w.Command("triple-match-seat", "Use measured perspective")) MatchSeat(_settings);
            if (_settings.OverrideFieldOfView && w.Command("triple-game-view", "Use the game's field of view"))
                _settings.OverrideFieldOfView = false;
        }

        if (_lastOutcome != null && mode != 0)
            w.Help("Status: " + _lastOutcome.Diagnostic.Message);
        if (w.Command("triple-advanced", (_showAdvanced ? "Hide" : "Show") + " advanced setup and diagnostics")) _showAdvanced = !_showAdvanced;
        if (!_showAdvanced) return;

        if (mode != 0)
        {
            w.Caption("Field of view");
            _settings.OverrideFieldOfView = !w.ToggleRow("triple-game-fov", "Use the game's field of view",
                "Wider, but not matched to your seat.", !_settings.OverrideFieldOfView, true);
            _settings.ChaseUsesGameFov = w.ToggleRow("triple-chase-game-fov", "Chase views use the game's framing",
                "Bonnet and bumper views stay matched to your seat.", _settings.ChaseUsesGameFov, true);
        }
        if (mode != 0 && !_settings.OverrideFieldOfView)
        {
            w.Help("Following the game's camera field of view on all three screens.");
            if (_lastOutcome != null && Projection.CenterVerticalFovDegrees.HasValue)
                w.Help($"Current center field of view: {Projection.CenterVerticalFovDegrees.Value:0}°");
        }
        else if (mode != 0 && panel != null && geometry != null)
        {
            var minimum = FovAtScale(layout, FovPreference.MinimumSliderScale);
            var maximum = FovAtScale(layout, FovPreference.MaximumSliderScale);
            var current = FovAtScale(layout, Mathf.Clamp(_settings.ViewWidthScale,
                (float)FovPreference.MinimumSliderScale, (float)FovPreference.MaximumSliderScale));
            // Default is the unscaled view (ViewWidthScale 1, the former "Reset field of view"). Only an edit writes the
            // scale; a draw without input leaves the saved value exactly as it is.
            var standard = FovAtScale(layout, 1);
            var proposed = w.NumberRow("triple-fov", "Field of view", "Lower is narrower, higher is wider.", current, minimum, maximum,
                1, "0", "°", true, standard);
            if (proposed == standard && proposed != current) _settings.ViewWidthScale = 1f;
            else if (proposed != current && !double.IsNaN(proposed) && !double.IsInfinity(proposed))
                _settings.ViewWidthScale = ScaleForFov(layout, (float)proposed);
        }
        else if (mode != 0)
            w.Help("Enter your screen measurements below to enable field of view.");

        w.Caption("Screen measurements");
        var imported = Layout.Current.IsSuccess;
        if (imported)
        {
            _settings.UseLocalLayout = w.ToggleRow("triple-local-layout", "Use measurements entered here", null, _settings.UseLocalLayout, true);
            if (!_settings.UseLocalLayout) w.Help("Using imported measurements. The optimizer is optional.");
        }
        else
        {
            _settings.UseLocalLayout = true;
            w.Help("No imported measurements found. Enter yours here; no optimizer is required.");
        }
        if (_settings.UseLocalLayout)
        {
            w.Help("Enter the visible size of one panel and your distance from your eyes to the middle of the center screen's glass. The example values below are inactive until accepted.");
            _settings.PanelWidthPx = IntField(w, "triple-panel-width-px", "Panel width (pixels)", _settings.PanelWidthPx);
            _settings.PanelHeightPx = IntField(w, "triple-panel-height-px", "Panel height (pixels)", _settings.PanelHeightPx);
            _settings.PanelWidthMm = FloatField(w, "triple-panel-width-mm", "Visible panel width (mm)", _settings.PanelWidthMm);
            _settings.PanelHeightMm = FloatField(w, "triple-panel-height-mm", "Visible panel height (mm)", _settings.PanelHeightMm);
            _settings.EyeDistanceMm = FloatField(w, "triple-eye-distance", "Eye distance (mm)", _settings.EyeDistanceMm);
            _settings.LeftYawDegrees = FloatField(w, "triple-left-yaw", "Left screen angle (degrees)", _settings.LeftYawDegrees);
            _settings.RightYawDegrees = FloatField(w, "triple-right-yaw", "Right screen angle (degrees)", _settings.RightYawDegrees);
            _settings.BezelGapMm = FloatField(w, "triple-bezel-gap", "Gap between screens' lit areas (mm, both bezels)", _settings.BezelGapMm);
            _settings.CurveRadiusMm = FloatField(w, "triple-curve-radius", "Screen curve radius (mm, 0 = flat)", _settings.CurveRadiusMm);
            if (w.Command("triple-use-measurements", "Use these measurements")) _settings.ManualSetupConfirmed = true;
            w.Help(_settings.ManualSetupConfirmed ? "Local setup active" : "Local setup not yet active");
        }
        if (mode == 2)
        {
            w.Help("Center must be Windows primary. Exit the game after changing display mode; activated displays release on exit.");
            // The side displays are found automatically (the racing row beside the primary); the saved index fields are
            // no longer used, so they are not shown. Their stored values are left untouched.
            w.Help("Side displays are found automatically: the equal-size displays directly left and right of the primary. Other displays, such as a dashboard, are ignored.");
        }
        _settings.CenterMenusOnMiddleScreen = w.ToggleRow("triple-center-menus", "Keep menus and HUD on the middle screen", null,
            _settings.CenterMenusOnMiddleScreen, true);
        _settings.AllowOutputResolutionMismatch = w.ToggleRow("triple-allow-mismatch", "Developer: allow output resolution mismatch", null,
            _settings.AllowOutputResolutionMismatch, true);
        if (w.Command("triple-reload-layout", "Reload imported layout")) LogLayoutRefresh(Layout.Refresh(true));
        w.Help(layout.IsSuccess ? "Effective layout: " + layout.Sha256.Substring(0, 12) + "…"
            : "Setup: " + layout.ErrorMessage);
        w.Help(_renderDiagnostics.PresentationSummary);
    }

    // Seat-matched: the projection uses the measured eye distance unscaled (ProjectionController.ResolveViewWidthScale).
    internal static bool MatchesSeat(Settings s) => s.OverrideFieldOfView && s.ViewWidthScale == 1f;

    internal static void MatchSeat(Settings s)
    {
        s.OverrideFieldOfView = true;
        s.ViewWidthScale = 1f;
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

    private static int IntField(Dbce.Wheel.Ui.Unity.F6Widgets w, string id, string label, int value)
    {
        var shown = value.ToString();
        var changed = TextRow(w, id, label, shown);
        return changed != shown && int.TryParse(changed, out var parsed) ? parsed : value;
    }

    // Typed in place, as before; a draw without an edit keeps the stored value unrounded (the field shows two decimals).
    private static float FloatField(Dbce.Wheel.Ui.Unity.F6Widgets w, string id, string label, float value)
    {
        var shown = value.ToString("0.##");
        var changed = TextRow(w, id, label, shown);
        return changed != shown && float.TryParse(changed, out var parsed) ? parsed : value;
    }

    // A shared row with the label column and a short text field in the editor column, focusable like the panel's fields.
    private static string TextRow(Dbce.Wheel.Ui.Unity.F6Widgets w, string id, string label, string shown)
    {
        var row = w.BeginRow(label, null, 120);
        if (!row.Stacked) GUILayout.FlexibleSpace();
        GUI.SetNextControlName(id);
        w.Register(id);
        var changed = GUILayout.TextField(shown, GUILayout.Width(120 * w.Scale));
        w.Reveal(id);
        w.EndRow(row);
        return changed;
    }

    internal static void Save()
    {
        if (_settings == null || _modEntry == null) return;
        try { _settings.Save(_modEntry); }
        catch (Exception exception) { _modEntry.Logger.Warning("Triple screens: could not save settings: " + exception.Message); }
    }

    // F7 changes only <F7View> in TripleScreen.xml (the rest of the file is kept); memory follows only a saved pick.
    private static bool SaveF7View(int view)
    {
        try
        {
            SettingsPersistence.WriteElement(_settings.GetPath(_modEntry), "F7View", view.ToString());
            _settings.F7View = view;
            return true;
        }
        catch (Exception exception)
        {
            _modEntry.Logger.Warning("F7 comparison: could not save the view: " + exception.Message);
            return false;
        }
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
