// Vendorable capture logic. No clocks, devices, OS input, file I/O, UI or force calls.
// Hosts provide snapshots from their existing reader and own cancellation/timeouts.
#nullable enable
using System;
using System.Collections.Generic;

namespace Dbce.Wheel.Input
{
    public sealed class BindingCaptureSnapshot
    {
        public int[] Axes { get; }
        public bool[] Buttons { get; }
        public int[] Hats { get; }
        public BindingCaptureSnapshot(int[] axes, bool[] buttons, int[] hats)
        {
            Axes = (int[])axes.Clone(); Buttons = (bool[])buttons.Clone(); Hats = (int[])hats.Clone();
        }
    }

    public sealed class BindingCaptureChange
    {
        public string Kind { get; }
        public int Index { get; }
        public int Direction { get; }
        public int Rest { get; }
        public BindingCaptureChange(string kind, int index, int direction, int rest)
        { Kind = kind; Index = index; Direction = direction; Rest = rest; }
    }

    /// <summary>Fixed axis rest; button/hat edges advance every sample, so held inputs need release/repress.</summary>
    public sealed class SharedBindingCaptureSession
    {
        private readonly int[] restAxes;
        private BindingCaptureSnapshot previous;
        private readonly bool analog;
        private readonly int[]? minimum, maximum;
        public SharedBindingCaptureSession(BindingCaptureSnapshot initial, bool analog, int[]? minimum = null, int[]? maximum = null)
        { restAxes = (int[])initial.Axes.Clone(); previous = initial; this.analog = analog;
            this.minimum = minimum == null ? null : (int[])minimum.Clone(); this.maximum = maximum == null ? null : (int[])maximum.Clone(); }
        public BindingCaptureChange? Read(BindingCaptureSnapshot now)
        {
            var baseline = new BindingCaptureSnapshot(restAxes, previous.Buttons, previous.Hats);
            previous = now;
            return SharedBindingCapture.Detect(baseline, now, analog, minimum, maximum);
        }
    }

    public static class SharedBindingCapture
    {
        /// <summary>One invalid known-range axis rejects the whole observation, including simultaneous buttons.</summary>
        public static bool RangesValid(int[] axes, int[] minimum, int[] maximum)
        {
            if (minimum.Length < axes.Length || maximum.Length < axes.Length) return false;
            for (int i = 0; i < axes.Length; i++)
                if (maximum[i] > minimum[i] && (axes[i] < minimum[i] || axes[i] > maximum[i])) return false;
            return true;
        }
        public static BindingCaptureChange? Detect(BindingCaptureSnapshot rest, BindingCaptureSnapshot now, bool analog,
            int[]? minimum = null, int[]? maximum = null)
        {
            if ((minimum != null || maximum != null) && (minimum == null || maximum == null ||
                !RangesValid(rest.Axes, minimum, maximum) || !RangesValid(now.Axes, minimum, maximum))) return null;
            var changes = new List<BindingCaptureChange>();
            if (analog) for (int i = 0; i < Math.Min(rest.Axes.Length, now.Axes.Length); i++)
            {
                long threshold = 12000;
                if (minimum != null || maximum != null)
                {
                    if (minimum == null || maximum == null || i >= minimum.Length || i >= maximum.Length || maximum[i] <= minimum[i]) continue;
                    threshold = Math.Max(1L, ((long)maximum[i] - minimum[i]) * 12000 / 65535);
                }
                long delta = (long)now.Axes[i] - rest.Axes[i];
                if (Math.Abs(delta) > threshold) changes.Add(new BindingCaptureChange("Axis", i, Math.Sign(delta), rest.Axes[i]));
            }
            for (int i = 0; i < Math.Min(rest.Buttons.Length, now.Buttons.Length); i++)
                if (!rest.Buttons[i] && now.Buttons[i]) changes.Add(new BindingCaptureChange("Button", i, 1, 0));
            for (int i = 0; i < Math.Min(rest.Hats.Length, now.Hats.Length); i++)
                if ((rest.Hats[i] < 0 || (rest.Hats[i] & 0xffff) == 0xffff) && now.Hats[i] >= 0 && now.Hats[i] < 36000)
                    changes.Add(new BindingCaptureChange("Hat", i, now.Hats[i], -1));
            return changes.Count == 1 ? changes[0] : null;
        }
    }
}
