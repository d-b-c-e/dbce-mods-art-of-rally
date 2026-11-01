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

        // With the shape (extent values 5 and 6: bonnet surface height and windscreen base, from HoodProfile) the bonnet
        // keeps its height above the bonnet and its distance from the windscreen base instead (owner 2026-10-31: fit from
        // the car's shape, not only its box). Either car without the shape falls back to the box.
        internal const int ShapeExtentValues = 6;

        internal static float[] Fit(float[] tuned, float[] from, float[] to)
        {
            if (tuned == null || tuned.Length != Values || !Valid(from) || !Valid(to)) throw new ArgumentException("Invalid camera mount fit input.");
            float width = to[3] / from[3];
            bool shape = from.Length == ShapeExtentValues && to.Length == ShapeExtentValues;
            return new[]
            {
                tuned[0] * width,
                shape ? to[4] + (tuned[1] - from[4]) : to[1] - (from[1] - tuned[1]),
                shape ? to[5] + (tuned[2] - from[5]) : to[2] - (from[2] - tuned[2]),
                tuned[3], tuned[4],
                tuned[5] * width, to[0] + (tuned[6] - from[0]), to[2] - (from[2] - tuned[7]), tuned[8], tuned[9]
            };
        }

        internal static bool Valid(float[] extent)
        {
            if (extent == null || (extent.Length != ExtentValues && extent.Length != ShapeExtentValues)) return false;
            foreach (var v in extent) if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            if (!(extent[1] > extent[0] && extent[3] > 0.2f)) return false;
            return extent.Length == ExtentValues || (extent[4] > extent[0] && extent[4] < extent[1] && extent[5] < extent[2]);
        }

        // The bonnet from a top-surface profile along the centre line, sampled from the front backwards (z falling,
        // NaN where nothing was found): the bonnet height is the median of the front quarter; the windscreen base is
        // the first sample past the front tenth that rises 12 cm above it. Null when the profile shows no such step.
        internal static float[] HoodProfile(float[] z, float[] height)
        {
            if (z == null || height == null || z.Length != height.Length || z.Length < 8) return null;
            int quarter = Math.Max(2, z.Length / 4), tenth = Math.Max(1, z.Length / 10);
            var front = new System.Collections.Generic.List<float>();
            for (int i = 0; i < quarter; i++) if (!float.IsNaN(height[i])) front.Add(height[i]);
            if (front.Count < 2) return null;
            front.Sort();
            float hood = front[front.Count / 2];
            for (int i = tenth; i < z.Length; i++)
                if (!float.IsNaN(height[i]) && height[i] > hood + .12f) return new[] { hood, z[i] };
            return null;
        }
    }
}
