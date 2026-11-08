// MIT. Source-vendored C# 10/.NET 6+ helper. Game adapters own storage, edit gates and presentation text.
#nullable enable
using System;
using System.Globalization;

namespace Dbce.Wheel.Display;

/// <summary>Consumer-owned page and Save wording; no game or page name is imposed by the draft.</summary>
internal readonly record struct TripleDraftText(string Initial, string Locked, string NoChanges, string Saved, string Discarded);

// Engine-free F6 draft for the triple-screen selector and rig (STD-022). Like telemetry, every change is saved for the
// next start: the span and the views are decided once at startup.
internal sealed class TripleSettingsDraft
{
    private readonly Func<TripleSettings, string?> save;
    private readonly Func<bool> canEdit;
    private readonly Func<string> status;
    private readonly TripleDraftText text;
    private readonly bool storeAsSinglePrecision;
    private bool closeWarned;
    internal TripleSettings Active { get; }
    internal TripleSettings Saved { get; private set; }
    internal int Choice { get; private set; }
    internal bool MatchGameFov { get; private set; }
    internal string WidthText { get; private set; } = "";
    internal string HeightText { get; private set; } = "";
    internal string EyeText { get; private set; } = "";
    internal string AngleText { get; private set; } = "";
    internal string BezelText { get; private set; } = "";
    internal string CurveText { get; private set; } = "";
    internal string SaveStatus { get; private set; }
    internal bool CloseWarning { get; private set; }
    internal bool Editable => canEdit();
    internal string Status => status();
    internal bool SavedPendingRestart => Saved != Active;
    internal bool HasDraft => Proposed() is not { } proposed || proposed != Saved;
    internal string? Validation => Proposed() is { } proposed ? proposed.Rig.Validation() : "Enter each measurement as a number.";

    /// <param name="storeAsSinglePrecision">True for a float store; false preserves double measurements and round-trip text.</param>
    internal TripleSettingsDraft(TripleSettings active, Func<TripleSettings, string?> save, Func<bool> canEdit, Func<string> status,
        TripleDraftText text, bool storeAsSinglePrecision)
    {
        Active = Saved = active; this.save = save; this.canEdit = canEdit; this.status = status;
        this.text = text; this.storeAsSinglePrecision = storeAsSinglePrecision; SaveStatus = text.Initial;
        Load(active);
    }

    internal void SetChoice(int value) { if (Editable && value is >= 0 and <= 2) Choice = value; }
    internal void SetMatchGameFov(bool value) { if (Editable) MatchGameFov = value; }
    internal void SetWidth(string value) { if (Editable) WidthText = value; }
    internal void SetHeight(string value) { if (Editable) HeightText = value; }
    internal void SetEye(string value) { if (Editable) EyeText = value; }
    internal void SetAngle(string value) { if (Editable) AngleText = value; }
    internal void SetBezel(string value) { if (Editable) BezelText = value; }
    internal void SetCurve(string value) { if (Editable) CurveText = value; }

    internal bool Save()
    {
        if (!Editable) { SaveStatus = text.Locked; return false; }
        if (Validation is { } invalid) { SaveStatus = invalid; return false; }
        if (!HasDraft) { SaveStatus = text.NoChanges; return false; }
        var proposed = Proposed()!.Value;
        string? failure = save(proposed);
        if (failure != null) { SaveStatus = failure + " The draft was kept."; return false; }
        Saved = proposed; Load(proposed);
        CloseWarning = closeWarned = false;
        SaveStatus = text.Saved;
        return true;
    }

    internal void Discard()
    {
        Load(Saved);
        CloseWarning = closeWarned = false; SaveStatus = text.Discarded;
    }

    internal bool HoldClose()
    {
        if (HasDraft && !closeWarned) { CloseWarning = closeWarned = true; return true; }
        return false;
    }

    internal void PanelHidden() { CloseWarning = closeWarned = false; }

    private TripleSettings? Proposed()
    {
        if (!TripleRig.TryParse(WidthText, out double width) || !TripleRig.TryParse(HeightText, out double height) ||
            !TripleRig.TryParse(EyeText, out double eye) || !TripleRig.TryParse(AngleText, out double angle) ||
            !TripleRig.TryParse(BezelText, out double bezel) || !TripleRig.TryParse(CurveText, out double curve)) return null;
        var (mode, span) = TripleLayoutChoice.Apply(Choice, Saved.Mode);
        // Compare/save the precision the consumer's store will retain.
        var rig = new TripleRig(Stored(width), Stored(height), Stored(eye), Stored(angle), Stored(bezel), Stored(curve));
        return new TripleSettings(mode, span, rig, MatchGameFov);
    }

    private void Load(TripleSettings value)
    {
        Choice = TripleLayoutChoice.Current(value.Mode, value.Span); MatchGameFov = value.MatchGameFov;
        WidthText = Text(value.Rig.PanelWidthMm); HeightText = Text(value.Rig.PanelHeightMm); EyeText = Text(value.Rig.EyeDistanceMm);
        AngleText = Text(value.Rig.SideAngle); BezelText = Text(value.Rig.BezelMm); CurveText = Text(value.Rig.CurveRadiusMm);
    }

    private double Stored(double value) => storeAsSinglePrecision ? (float)value : value;

    // Shortest round-trip text of the consumer's stored precision: opening the page is never an edit.
    private string Text(double value) => storeAsSinglePrecision
        ? ((float)value).ToString("R", CultureInfo.InvariantCulture)
        : value.ToString("R", CultureInfo.InvariantCulture);
}
