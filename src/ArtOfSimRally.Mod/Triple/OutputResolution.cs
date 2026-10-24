namespace ArtOfSimRally.Mod.Triple;

// The game applies its saved resolution at startup, before mods load, and again only from its display options. A size
// saved for one triple layout (2560 wide for separate monitors) is wrong for the other (a 7680-wide Surround span), so
// the mod asks Unity for the size the selected mode needs. It never writes the game's saved resolution setting.
internal sealed class OutputResolution
{
    internal const float RetrySeconds = 3f;
    internal const int MaxAttempts = 3;
    private int _attempts;
    private float _lastRequest = float.NegativeInfinity;

    internal bool GaveUp => _attempts >= MaxAttempts;

    /// <summary>
    /// Whether to request <paramref name="wantWidth"/>x<paramref name="wantHeight"/> now: only when the desktop display
    /// already is that size (Surround on for the span, the center monitor for separate displays), the game output is
    /// not, and fewer than <see cref="MaxAttempts"/> requests were made since the output last matched.
    /// </summary>
    internal bool ShouldRequest(int desktopWidth, int desktopHeight, int screenWidth, int screenHeight,
        int wantWidth, int wantHeight, float now)
    {
        if (screenWidth == wantWidth && screenHeight == wantHeight) { _attempts = 0; return false; }
        if (desktopWidth != wantWidth || desktopHeight != wantHeight) return false;
        if (GaveUp || now - _lastRequest < RetrySeconds) return false;
        _attempts++;
        _lastRequest = now;
        return true;
    }
}
