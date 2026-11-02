using System;

namespace ArtOfSimRally.Mod
{
    // Bonnet/bumper starting placement calculated from each car's own shape (owner 2026-11-01): the bonnet camera a few
    // inches ahead of the windscreen base and just above the bonnet, the bumper camera on the centre line just ahead of
    // the front, halfway up the front face. The player's tweaks are offsets on top; reset returns to this.
    // Values: bonnet side, height, forward, pitch, FOV, then the same five for the bumper. Extent: bottom, top, front,
    // width of the visible car in its camera-target space (metres), plus bonnet height and windscreen base when found.
    internal static class CameraMountFit
    {
        internal const int Values = 10, ExtentValues = 4;
        internal const int ShapeExtentValues = 6;

        internal const float BonnetAhead = .10f, BonnetAbove = .08f, BumperAhead = .05f;
        // Clearance kept by KeepOutside so the calculated start is never inside the body (tweaks are not limited).
        internal const float Clearance = .03f;

        /// <summary>The calculated placement, or null without a usable measurement (the caller uses the defaults).</summary>
        internal static float[] Base(float[] extent, float bonnetFov, float bumperFov, float bonnetPitch, float bumperPitch)
        {
            if (!Valid(extent)) return null;
            float bottom = extent[0], top = extent[1], front = extent[2];
            bool shape = extent.Length == ShapeExtentValues;
            // shortcut: without the bonnet shape the box gives typical proportions; upgrade if box-only cars start badly.
            float hood = shape ? extent[4] : bottom + .58f * (top - bottom);
            float glass = shape ? extent[5] : front - 1.2f;
            return new[]
            {
                0f, hood + BonnetAbove, glass + BonnetAhead, bonnetPitch, bonnetFov,
                0f, (bottom + hood) / 2f, front + BumperAhead, bumperPitch, bumperFov
            };
        }

        /// <summary>Moves a placement to the nearest point outside the body: the bonnet camera stays ahead of the windscreen
        /// base and above the bonnet, the bumper camera ahead of the front while below the bonnet line.</summary>
        internal static bool KeepOutside(float[] v, float[] extent)
        {
            if (v == null || v.Length != Values || !Valid(extent)) return false;
            bool moved = false;
            if (extent.Length == ShapeExtentValues)
            {
                float hood = extent[4], glass = extent[5];
                moved |= AtLeast(ref v[2], glass + Clearance);
                if (v[2] <= extent[2]) moved |= AtLeast(ref v[1], hood + Clearance);
                if (v[6] < hood) moved |= AtLeast(ref v[7], extent[2] + Clearance);
            }
            else moved |= AtLeast(ref v[7], extent[2] + Clearance);
            return moved;
        }

        private static bool AtLeast(ref float value, float min)
        {
            if (value >= min) return false;
            value = min;
            return true;
        }

        internal static bool Valid(float[] extent)
        {
            if (extent == null || (extent.Length != ExtentValues && extent.Length != ShapeExtentValues)) return false;
            foreach (var v in extent) if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            if (!(extent[1] > extent[0] && extent[3] > 0.2f)) return false;
            return extent.Length == ExtentValues || (extent[4] > extent[0] && extent[4] < extent[1] && extent[5] < extent[2]);
        }

        // A measurement that can be a car body: 1.2-3.2 m wide, 0.8-3.6 m tall, a bonnet at least 25 cm above the bottom. A
        // helper collider made the owner's Eibenhammer measure 5.71 m wide with the bonnet below the car (2026-11-01), and
        // the bonnet camera was fitted onto the ground; such a measurement is not used.
        internal static bool Plausible(float[] extent)
        {
            if (!Valid(extent)) return false;
            float height = extent[1] - extent[0];
            // 4.5 m: the Turtle Van with its roof turret is about 4.1 m (owner 2026-11-01); width remains the main guard.
            if (extent[3] < 1.2f || extent[3] > 3.2f || height < .8f || height > 4.5f) return false;
            return extent.Length == ExtentValues || extent[4] > extent[0] + .25f;
        }

        // Steeper than this (rise per metre, about 19°) is a front face or windscreen; a bonnet or a sloping nose is shallower.
        internal const float WindscreenSlope = .35f;
        // A front face rising more than this has no bonnet in front of the windscreen (a cab-over van).
        internal const float CabOverFace = .9f;

        // The bonnet from a top-surface profile along the centre line, sampled from the front backwards (z falling, NaN
        // where nothing was found). From the front: climb the front face while it is steep, follow the bonnet while it is
        // shallow (a gently sloping nose counts), and where the surface turns steep again is the windscreen base; its
        // height is the bonnet. Walking from the roof instead stopped on a roof turret (owner 2026-11-01), and a
        // front-quarter median took a sloping nose for the bonnet. A cab-over (the front face itself rises past
        // CabOverFace) gets its "bonnet" camera just ahead of the face at half its height. Null when no windscreen step
        // is found in the front 60% of the car.
        internal static float[] HoodProfile(float[] z, float[] height)
        {
            if (z == null || height == null || z.Length != height.Length || z.Length < 8) return null;
            var idx = new System.Collections.Generic.List<int>();
            for (int i = 0; i < z.Length; i++) if (!float.IsNaN(height[i])) idx.Add(i);
            if (idx.Count < 8) return null;
            float Slope(int a, int b) => (height[b] - height[a]) / (z[a] - z[b]);
            int k = 0;
            while (k + 1 < idx.Count && Slope(idx[k], idx[k + 1]) >= WindscreenSlope) k++;
            if (height[idx[k]] - height[idx[0]] > CabOverFace)
                return new[] { (height[idx[0]] + height[idx[k]]) / 2f - BonnetAbove, z[0] - .01f };
            while (k + 1 < idx.Count && Slope(idx[k], idx[k + 1]) < WindscreenSlope) k++;
            if (k + 1 >= idx.Count) return null;
            int b = idx[k];
            if (z[b] < z[0] - .6f * (z[0] - z[z.Length - 1])) return null;
            float after = float.MinValue;
            for (int i = k + 1; i < idx.Count; i++) after = Math.Max(after, height[idx[i]]);
            return after < height[b] + .12f ? null : new[] { height[b], z[b] };
        }

        /// <summary>Raises <paramref name="h"/> (the profile, sampled from <paramref name="front"/> backwards over
        /// <paramref name="length"/>) to the top of a mesh's cross-sections at the given x planes. Points are x,y,z in the
        /// camera target's space. Corner points alone missed the big flat panels of a low-poly body (the Turtle Van's
        /// roof read as a hole and its bonnet camera was placed inside the cab, owner 2026-11-01).</summary>
        internal static void Slice(float[] h, float front, float length, float[] xyz, int[] triangles, float[] planes)
        {
            int n = h.Length;
            var cut = new float[6];
            for (int t = 0; t + 2 < triangles.Length; t += 3)
                foreach (var x0 in planes)
                {
                    int found = 0;
                    for (int e = 0; e < 3 && found < 2; e++)
                    {
                        int a = triangles[t + e] * 3, b = triangles[t + (e + 1) % 3] * 3;
                        float da = xyz[a] - x0, db = xyz[b] - x0;
                        if (da * db > 0f || da == db) continue;
                        float f = da / (da - db);
                        cut[found * 3] = xyz[a + 1] + (xyz[b + 1] - xyz[a + 1]) * f;
                        cut[found * 3 + 1] = xyz[a + 2] + (xyz[b + 2] - xyz[a + 2]) * f;
                        found++;
                    }
                    if (found < 2) continue;
                    float ya = cut[0], za = cut[1], yb = cut[3], zb = cut[4];
                    int i0 = (int)Math.Ceiling((front - Math.Max(za, zb)) / length * (n - 1) - 1e-4);
                    int i1 = (int)Math.Floor((front - Math.Min(za, zb)) / length * (n - 1) + 1e-4);
                    for (int i = Math.Max(0, i0); i <= Math.Min(n - 1, i1); i++)
                    {
                        float zi = front - length * i / (n - 1);
                        float y = Math.Abs(zb - za) < 1e-6f ? Math.Max(ya, yb) : ya + (yb - ya) * (zi - za) / (zb - za);
                        if (float.IsNaN(h[i]) || y > h[i]) h[i] = y;
                    }
                }
        }
    }
}
