using System;

namespace ArtOfSimRally.Mod
{
    // Per-car bonnet/bumper placement (owner 2026-10-31: "it needs adjusting almost every time"). A car never tuned starts
    // from the last tuned car, moved to this car's size: the bonnet keeps its distance below the roof and behind the
    // front, the bumper its height above the ground and distance from the front; sideways offsets scale with width.
    // Values: bonnet side, height, forward, pitch, FOV, then the same five for the bumper. Extent: bottom, top, front,
    // width of the visible car in its camera-target space (metres).
    internal static class CameraMountFit
    {
        internal const int Values = 10, ExtentValues = 4;

        internal static float[] Fit(float[] tuned, float[] from, float[] to)
        {
            if (tuned == null || tuned.Length != Values || !Valid(from) || !Valid(to)) throw new ArgumentException("Invalid camera mount fit input.");
            float width = to[3] / from[3];
            return new[]
            {
                tuned[0] * width, to[1] - (from[1] - tuned[1]), to[2] - (from[2] - tuned[2]), tuned[3], tuned[4],
                tuned[5] * width, to[0] + (tuned[6] - from[0]), to[2] - (from[2] - tuned[7]), tuned[8], tuned[9]
            };
        }

        internal static bool Valid(float[] extent)
        {
            if (extent == null || extent.Length != ExtentValues) return false;
            foreach (var v in extent) if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            return extent[1] > extent[0] && extent[3] > 0.2f;
        }
    }
}
