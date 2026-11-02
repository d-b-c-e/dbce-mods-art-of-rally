using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ArtOfSimRally.Mod.Triple;

/// <summary>Finds this process's Unity game window for the window landing and SpanWindow. Read-only; titles are read
/// with InternalGetWindowText, which sends no message to the window.</summary>
internal static class SecondaryDisplayWindows
{
    /// <summary>The class Unity gives its player windows.</summary>
    internal const string UnityWindowClass = "UnityWndClass";
    // Unity's title for an activated secondary display's window; such a window is never the game window.
    private const string SecondaryWindowTitle = "Unity Secondary Display";
    private static readonly uint OwnProcess = CurrentProcessId();

    internal static IntPtr LandingWindow()
    {
        var found = new List<IntPtr>();
        Enumerate(window =>
        {
            if (IsWindowVisible(window) && !IsIconic(window) &&
                ClassName(window) == UnityWindowClass && Title(window) != SecondaryWindowTitle)
                found.Add(window);
        });
        return found.Count == 1 ? found[0] : IntPtr.Zero;
    }

    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);

    private static void Enumerate(Action<IntPtr> visit)
    {
        EnumWindowsProc callback = (window, _) =>
        {
            GetWindowThreadProcessId(window, out var process);
            if (process == OwnProcess) visit(window);
            return true;
        };
        if (!EnumWindows(callback, IntPtr.Zero)) throw new InvalidOperationException("EnumWindows failed");
        GC.KeepAlive(callback);
    }

    private static string Title(IntPtr window)
    {
        var text = new StringBuilder(128);
        InternalGetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    private static string ClassName(IntPtr window)
    {
        var text = new StringBuilder(128);
        GetClassNameW(window, text, text.Capacity);
        return text.ToString();
    }

    private static uint CurrentProcessId()
    {
        using var process = Process.GetCurrentProcess();
        return (uint)process.Id;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int InternalGetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
}
