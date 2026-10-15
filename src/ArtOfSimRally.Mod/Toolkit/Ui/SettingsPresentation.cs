// MIT. Source-vendored UI rules; no engine, device or persistence dependency.
using System;
using System.Globalization;

namespace Dbce.Wheel.Ui
{
    /// <summary>Shared player-facing control names. Stored hardware indices remain zero based.</summary>
    public static class ControlNames
    {
        private static readonly string[] Axes = { "X", "Y", "Z", "Rx", "Ry", "Rz", "Slider 1", "Slider 2" };
        public static string Axis(int index) => index >= 0 && index < Axes.Length ? Axes[index] : "Unknown axis";
        public static string Button(int index) => index >= 0 && index < 128 ? "Button " + (index + 1) : "Unknown button";
        public static string Hat(int index, int direction) => index >= 0 && index < 4 && direction >= 0 && direction < 36000
            ? "Hat " + (index + 1) + " · " + (direction / 100).ToString(CultureInfo.InvariantCulture) + "°" : "Unknown hat";
    }

    /// <summary>Numeric edits in WPF and in-game widgets use the same grid and finite-value rules.
    /// Call only for an explicit user edit; displaying a saved off-grid value must not rewrite it.</summary>
    public static class NumberEdit
    {
        public static bool TryValue(double requested, double min, double max, double step, out double value)
        {
            value = 0;
            if (!Finite(requested) || !Finite(min) || !Finite(max) || !Finite(step) || min > max || step <= 0) return false;
            double bounded = Math.Max(min, Math.Min(max, requested));
            double ticks = (bounded - min) / step;
            if (!Finite(ticks)) return false;
            value = Math.Max(min, Math.Min(max, min + Math.Round(ticks, MidpointRounding.AwayFromZero) * step));
            return Finite(value);
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
