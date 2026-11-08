using System;

namespace Dbce.TripleScreen;

/// <summary>Maps normalized panel coordinates or pixel centers to physical eye rays.</summary>
public static class EyeRayCalculator
{
    /// <param name="surface">The physical planar display to sample.</param>
    /// <param name="eye">The measured eye position in the same coordinate system.</param>
    /// <param name="u">Horizontal coordinate, 0 at the panel's left edge and 1 at its right edge.</param>
    /// <param name="v">Vertical coordinate, 0 at the panel's bottom edge and 1 at its top edge.</param>
    public static EyeRay ThroughPanelUv(DisplaySurface surface, Vector3d eye, double u, double v)
    {
        if (surface is null) throw new ArgumentNullException(nameof(surface));
        if (double.IsNaN(u) || double.IsInfinity(u) || u < 0d || u > 1d)
            throw new ArgumentOutOfRangeException(nameof(u));
        if (double.IsNaN(v) || double.IsInfinity(v) || v < 0d || v > 1d)
            throw new ArgumentOutOfRangeException(nameof(v));

        var target = surface.LowerLeft
            + ((surface.LowerRight - surface.LowerLeft) * u)
            + ((surface.UpperLeft - surface.LowerLeft) * v);
        return new EyeRay(eye, target - eye);
    }

    /// <summary>Returns a ray through a pixel center; pixel Y is top-down.</summary>
    public static EyeRay ThroughPanelPixel(DisplaySurface surface, Vector3d eye,
        int pixelX, int pixelY, int widthPx, int heightPx)
    {
        if (widthPx <= 0) throw new ArgumentOutOfRangeException(nameof(widthPx));
        if (heightPx <= 0) throw new ArgumentOutOfRangeException(nameof(heightPx));
        if (pixelX < 0 || pixelX >= widthPx) throw new ArgumentOutOfRangeException(nameof(pixelX));
        if (pixelY < 0 || pixelY >= heightPx) throw new ArgumentOutOfRangeException(nameof(pixelY));

        return ThroughPanelUv(surface, eye,
            (pixelX + 0.5d) / widthPx,
            1d - ((pixelY + 0.5d) / heightPx));
    }
}
