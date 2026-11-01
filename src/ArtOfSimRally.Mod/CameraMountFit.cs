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
        // Clearance kept by KeepOutside so a tweak cannot put the camera inside the body.
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
            if (extent[3] < 1.2f || extent[3] > 3.2f || height < .8f || height > 3.6f) return false;
            return extent.Length == ExtentValues || extent[4] > extent[0] + .25f;
        }

        // Steeper than this (rise per metre, about 19°) is windscreen; a bonnet or a sloping nose is shallower.
        internal const float WindscreenSlope = .35f;

        // The bonnet from a top-surface profile along the centre line, sampled from the front backwards (z falling,
        // NaN where nothing was found). From where the profile first reaches roof height (the front two thirds' highest,
        // so a rear wing is ignored) it walks forward while the surface falls steeply: where that stops is the windscreen
        // base, and its height is the bonnet. A median of the front quarter took the Eibenhammer's low sloping nose for
        // the bonnet and the bonnet's rise for the windscreen, and put the camera inside the car (owner 2026-11-01).
        // Null when there is no windscreen step or no bonnet ahead of it.
        internal static float[] HoodProfile(float[] z, float[] height)
        {
            if (z == null || height == null || z.Length != height.Length || z.Length < 8) return null;
            float roof = float.NaN;
            for (int i = 0; i < z.Length * 2 / 3; i++) if (!float.IsNaN(height[i]) && !(height[i] <= roof)) roof = height[i];
            if (float.IsNaN(roof)) return null;
            int j = Array.FindIndex(height, h => !float.IsNaN(h) && h >= roof - .08f);
            while (true)
            {
                int prev = j - 1;
                while (prev >= 0 && float.IsNaN(height[prev])) prev--;
                if (prev < 0) return null;   // steep all the way to the front: no bonnet
                if ((height[j] - height[prev]) / (z[prev] - z[j]) < WindscreenSlope) break;
                j = prev;
            }
            return roof - height[j] < .12f ? null : new[] { height[j], z[j] };
        }
    }
}
