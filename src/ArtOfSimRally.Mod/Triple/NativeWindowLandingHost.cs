using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Dbce.Wheel.Display;
using UnityEngine;

namespace ArtOfSimRally.Mod.Triple;

internal sealed class NativeWindowLandingHost : RacingWindowLanding.IHost
{
    private const string MonitorKey = "UnitySelectMonitor";
    private readonly LandingPersistence _persistence = new LandingPersistence(ReadPersistenceBlock);
    public IList<EngineDisplay> EngineDisplays => Display.displays
        .Select(d => new EngineDisplay(d.systemWidth, d.systemHeight)).ToList();
    public int? SavedMonitor => PlayerPrefs.HasKey(MonitorKey) ? PlayerPrefs.GetInt(MonitorKey) : (int?)null;
    public string PersistenceBlock => _persistence.Block;
    private static string ReadPersistenceBlock()
    {
        try
        {
            var root = Path.GetDirectoryName(Application.dataPath);
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(local)) return "launch classification unavailable";
            var recorder = LandingPersistence.MarkerPresent(Path.Combine(root, "Mods", "ArtOfSimRally.DevRecorder"), File.GetAttributes) ||
                AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "ArtOfSimRally.DevRecorder");
            var request = LandingPersistence.MarkerPresent(Path.Combine(local,
                "ArtOfSimRally", "session-request.txt"), File.GetAttributes);
            return LandingLaunchPolicy.Block(Environment.GetEnvironmentVariable("DBCE_FFB_MUTE"),
                Environment.GetEnvironmentVariable("DBCE_ART_SHIM_REQUEST"), recorder, request,
                Application.isEditor, Environment.GetCommandLineArgs());
        }
        catch { return "launch classification unavailable"; }
    }
    public bool SaveMonitor(int? expected, int index) => _persistence.Save(expected, index,
        () => SavedMonitor, value => PlayerPrefs.SetInt(MonitorKey, value),
        () => PlayerPrefs.DeleteKey(MonitorKey), () => PlayerPrefs.Save(), Log);
    public float Now => Time.realtimeSinceStartup;
    public IReadOnlyList<MonitorRect> Monitors => WindowsMonitors.Read();
    public void Resize(int width, int height) => Screen.SetResolution(width, height, FullScreenMode.Windowed);
    public void Log(string message) => ModLog.Info(message);
    public bool Observe(out RacingWindowLanding.Observation observation)
    {
        observation = default;
        var window = SecondaryDisplayWindows.LandingWindow();
        if (window == IntPtr.Zero || !GetWindowRect(window, out var outer) ||
            !GetClientRect(window, out var client)) return false;
        var origin = new Point { X = client.Left, Y = client.Top };
        if (!ClientToScreen(window, ref origin)) return false;
        // All native coordinates are sampled in the calling Unity thread's DPI context.
        // Translate the observed nonclient offset; never change global DPI awareness.
        observation = new RacingWindowLanding.Observation {
            Window = window.ToInt64(), X = origin.X, Y = origin.Y,
            Width = client.Right - client.Left, Height = client.Bottom - client.Top,
            OffsetX = origin.X - outer.Left, OffsetY = origin.Y - outer.Top,
            EngineWidth = Screen.width, EngineHeight = Screen.height,
            Windowed = Screen.fullScreenMode == FullScreenMode.Windowed
        };
        return true;
    }
    public bool Place(long window, int x, int y) => SetWindowPos(new IntPtr(window), IntPtr.Zero, x, y, 0, 0, 0x0015);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after,
        int x, int y, int width, int height, uint flags);
}
