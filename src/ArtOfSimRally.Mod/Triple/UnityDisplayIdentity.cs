using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dbce.Wheel.Display;
using UnityEngine;

namespace ArtOfSimRally.Mod.Triple;

// Reads the identity evidence described in DisplayIdentityEvidence. Read-only: it activates no display, writes no
// setting, moves no cursor and sends no input. nativeDisplay is passed only to GetMonitorInfo, which validates the
// handle and fails on anything that is not an HMONITOR. RelativeMouseAt receives plain numbers.
internal static class UnityDisplayIdentity
{
    private static readonly FieldInfo NativeDisplayField =
        typeof(Display).GetField("nativeDisplay", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>The evidence now. Never throws: a failed read is reported in the snapshot.</summary>
    internal static DisplayIdentityEvidence.Snapshot Observe()
    {
        var snapshot = new DisplayIdentityEvidence.Snapshot();
        try
        {
            var monitors = WindowsMonitors.Read();
            var displays = Display.displays;
            var predicted = DisplayIdentityEvidence.PredictXOrder(monitors);
            snapshot.WindowsDisplays = monitors.Select(DisplayIdentityEvidence.ToRect).ToArray();
            snapshot.Displays = displays.Select((display, index) =>
            {
                var native = NativeDisplayField?.GetValue(display) is IntPtr value ? value : (IntPtr?)null;
                var evidence = new DisplayIdentityEvidence.UnityDisplay
                {
                    Index = index, SystemWidth = display.systemWidth, SystemHeight = display.systemHeight,
                    NativeDisplay = native.HasValue ? "0x" + native.Value.ToInt64().ToString("X") : null,
                };
                if (native.HasValue && TryMonitor(native.Value, out var rect, out var device))
                {
                    evidence.Hmonitor = rect;
                    evidence.HmonitorDevice = device;
                }
                if (index < predicted.Count)
                {
                    evidence.XOrderPrediction = DisplayIdentityEvidence.ToRect(predicted[index]);
                    evidence.XOrderSizeAgrees = predicted[index].Width == display.systemWidth && predicted[index].Height == display.systemHeight;
                }
                return evidence;
            }).ToArray();
            var primaries = monitors.Where(m => m.Primary).ToArray();
            var probes = new List<DisplayIdentityEvidence.Probe>();
            foreach (var monitor in monitors)
                probes.Add(Ask(monitor, DisplayIdentityEvidence.DesktopConvention, DisplayIdentityEvidence.DesktopCenter(monitor)));
            if (primaries.Length == 1)
                foreach (var monitor in monitors)
                    probes.Add(Ask(monitor, DisplayIdentityEvidence.BottomUpConvention, DisplayIdentityEvidence.PrimaryBottomUpCenter(monitor, primaries[0])));
            snapshot.Probes = probes.ToArray();
        }
        catch (Exception exception)
        {
            snapshot.Error = exception.GetType().Name + ": " + exception.Message;
        }
        snapshot.Summary = DisplayIdentityEvidence.Summarize(snapshot);
        return snapshot;
    }

    private static DisplayIdentityEvidence.Probe Ask(MonitorRect target, string convention, (int X, int Y) point)
    {
        var probe = new DisplayIdentityEvidence.Probe
        {
            Target = DisplayIdentityEvidence.ToRect(target), Convention = convention, InputX = point.X, InputY = point.Y,
        };
        try
        {
            var result = Display.RelativeMouseAt(new Vector3(point.X, point.Y, 0f));
            probe.ResultX = result.x;
            probe.ResultY = result.y;
            probe.ResultIndex = (int)result.z;
        }
        catch (Exception exception)
        {
            probe.Error = exception.GetType().Name + ": " + exception.Message;
        }
        return probe;
    }

    private static bool TryMonitor(IntPtr handle, out DisplayIdentityEvidence.Rect rect, out string device)
    {
        rect = null;
        device = null;
        if (handle == IntPtr.Zero) return false;
        var info = new MonitorInfoEx { Size = Marshal.SizeOf(typeof(MonitorInfoEx)) };
        if (!GetMonitorInfoW(handle, ref info)) return false;
        rect = new DisplayIdentityEvidence.Rect
        {
            X = info.Monitor.Left, Y = info.Monitor.Top, Width = info.Monitor.Right - info.Monitor.Left,
            Height = info.Monitor.Bottom - info.Monitor.Top, Primary = (info.Flags & MonitorInfoPrimary) != 0,
        };
        device = info.Device;
        return true;
    }

    private const uint MonitorInfoPrimary = 1; // MONITORINFOF_PRIMARY
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfoEx info);
}
