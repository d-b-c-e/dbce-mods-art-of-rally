// Shared value shaping for a game's declared extras (native/shim extension 2, dbce_shim_extras.h): a centred component
// from a digital pair, and a two-component stick with its declared range rule. The same arithmetic native Core applies
// after its status checks, for managed previews (Wheelkit, the setup helper, F6) so no view model re-derives it.
// Vendorable source beside ControlBinding.cs: no devices, OS input, file I/O, contexts or statuses; plain C#, no tuples.
// Unit neutrality (a configured part not Live leaves the whole destination neutral) needs statuses and stays native.
#nullable enable
using System;

namespace Dbce.Wheel.Input
{
    /// <summary>A declared stick's range rule ("stick circle" or "stick square").</summary>
    public enum StickRange { Circle, Square }

    /// <summary>A stick destination's value: each component -1..1, +X right, +Y up (the native [Extras] convention).</summary>
    public struct StickValue
    {
        public readonly double X, Y;
        public StickValue(double x, double y) { X = x; Y = y; }
    }

    public static class ExtraControlValue
    {
        /// <summary>
        /// A centred component from a digital pair: <paramref name="pos"/> minus <paramref name="neg"/>, each half that
        /// binding's normalized value (<see cref="ControlBinding.Normalize(ControlShape, int)"/> with
        /// <see cref="ControlShape.Digital"/>, 0 or 1). An unbound half passes 0, so a one-sided pair works; opposite halves
        /// together give 0. Inputs are clamped to 0..1; a non-finite input throws.
        /// </summary>
        public static double Pair(double neg, double pos)
        {
            return Clamp(Finite(pos, nameof(pos)), 0, 1) - Clamp(Finite(neg, nameof(neg)), 0, 1);
        }

        /// <summary>
        /// A stick destination from its components, each -1..1 (an axis's centred <see cref="ControlBinding.Normalize(ControlShape, int)"/>,
        /// or <see cref="Pair"/>). Each is clamped first. <see cref="StickRange.Circle"/> scales any vector longer than 1 back to
        /// length 1 and keeps shorter ones; <see cref="StickRange.Square"/> keeps each component. A non-finite input throws.
        /// </summary>
        public static StickValue Stick(double x, double y, StickRange range)
        {
            double cx = Clamp(Finite(x, nameof(x)), -1, 1), cy = Clamp(Finite(y, nameof(y)), -1, 1);
            if (range == StickRange.Circle)
            {
                double length = Math.Sqrt(cx * cx + cy * cy);
                if (length > 1) { cx /= length; cy /= length; }
            }
            else if (range != StickRange.Square) throw new ArgumentOutOfRangeException(nameof(range));
            return new StickValue(cx, cy);
        }

        /// <summary>The declaration grammar's range word: exactly "circle" or "square".</summary>
        public static bool TryParseRange(string? text, out StickRange range)
        {
            range = StickRange.Circle;
            if (text == "circle") return true;
            if (text == "square") { range = StickRange.Square; return true; }
            return false;
        }

        static double Finite(double v, string name)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentOutOfRangeException(name);
            return v;
        }
        static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;
    }
}
