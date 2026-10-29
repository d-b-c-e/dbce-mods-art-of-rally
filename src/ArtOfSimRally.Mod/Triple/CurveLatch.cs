using System;
using Dbce.TripleScreen;

namespace ArtOfSimRally.Mod.Triple;

/// <summary>
/// Curve-correction state: warps are built once per rig key, and a failure (invalid geometry, a render target or
/// shader pass the driver could not use) latches flat output with its reason until the key changes, so nothing is
/// retried or allocated every frame.
/// </summary>
internal sealed class CurveLatch
{
    private string _key;

    internal CurvedPanelWarp[] Warps { get; private set; }
    internal string Failure { get; private set; }
    internal int Builds { get; private set; }
    internal string Note => Failure != null ? "Curve correction is off: " + Failure : Warps != null ? "Curved screens are corrected." : null;

    /// <summary>The warps for <paramref name="key"/> (null key: correction off), or null while flat.</summary>
    internal CurvedPanelWarp[] Resolve(string key, Func<CurvedPanelWarp[]> build)
    {
        if (key == null)
        {
            _key = null; Warps = null; Failure = null;
            return null;
        }
        if (key == _key) return Warps;
        _key = key; Warps = null; Failure = null;
        Builds++;
        try { Warps = build(); }
        catch (Exception exception) { Failure = exception.Message; }
        return Warps;
    }

    /// <summary>Drops the current warps until the key changes.</summary>
    internal void Fail(string reason)
    {
        if (_key == null || Failure != null) return;
        Warps = null;
        Failure = reason;
    }
}
