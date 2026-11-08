// MIT. Shared cached span selection, extracted from Woden a58f8d9. No engine or Windows calls here.
#nullable enable
using System;

namespace Dbce.Wheel.Display;

/// <summary>
/// The cached span decision over the toolkit racing row. The saved
/// opt-out and the triple-mode gate come first. Then the policy selects the row from the monitors Windows reports: the
/// primary display as the center, with one display of the same size, top and bottom directly on each side. Displays
/// wholly above or below that row, such as a diagnostic screen, are ignored. A fourth display in the row, a missing,
/// offset or different-size neighbor, or two candidates for a side select no row.
/// Only a Separate row is spanned. The wide-primary path and no row keep the game's own window, as the former
/// virtual-desktop test did for one wide display. A failed monitor query is no row.
/// The monitor observation is cached for <see cref="RefreshSeconds"/>, so callers that check every frame do not
/// enumerate the displays each time. Engine-free, so fixtures drive it with fixed rectangles and a fixed clock.
/// </summary>
public sealed class SpanTarget
{
    /// <summary>How long one observation of the displays is reused before Windows is asked again.</summary>
    public const double RefreshSeconds = 2;

    private readonly Func<RacingRow> _read;
    private readonly Func<double> _now;
    private RacingRow? _row;
    private string? _readFailure, _last;
    private double _observedAt = double.NegativeInfinity;

    /// <param name="read">The racing row now: <c>WindowsMonitors.SelectRacingRow</c> in the game.</param>
    /// <param name="now">Seconds on a monotonic clock (default: the high-resolution performance counter).</param>
    public SpanTarget(Func<RacingRow> read, Func<double>? now = null)
    {
        _read = read;
        _now = now ?? (() => System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency);
    }

    /// <summary>Why the last decision was made, in one sentence.</summary>
    public string Reason { get; private set; } = "";

    /// <summary>The rectangle to span, or null. <paramref name="changed"/> is true when the reason differs from the
    /// previous decision, so the caller logs it once rather than on every check.</summary>
    public (int X, int Y, int W, int H)? Decide(bool enabled, bool triplesAllowed, out bool changed)
    {
        (int X, int Y, int W, int H)? span = null;
        string reason;
        if (!enabled) reason = "the separate-monitor span is off (saved setting)";
        else if (!triplesAllowed) reason = "triple views are off";
        else
        {
            Observe();
            if (_row is { Kind: RacingRowKind.Separate } row) span = (row.X, row.Y, row.Width, row.Height);
            reason = _readFailure ?? _row!.Reason;
        }
        changed = reason != _last;
        _last = reason; Reason = reason;
        return span;
    }

    private void Observe()
    {
        double now = _now();
        if (now - _observedAt < RefreshSeconds && now >= _observedAt) return;
        _observedAt = now;
        try { _row = _read(); _readFailure = null; }
        catch (Exception ex) { _row = null; _readFailure = "the displays could not be read: " + ex.Message; }
    }
}
