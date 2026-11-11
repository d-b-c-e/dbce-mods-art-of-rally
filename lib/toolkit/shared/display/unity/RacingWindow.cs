// MIT. The family's game window on the racing row for Unity Mono plugins, extracted from art of rally (2026-11-10) and
// consumed by art of rally (Unity 2019.4, built-in pipeline, UMM) and #DRIVE Rally (Unity 6000.0, URP, BepInEx): the
// game window (GameWindow), one borderless window across a row of separate monitors (SpanWindow, STD-047), the window
// landing on the racing row's home display (UnityWindowLandingHost for shared/display/RacingWindowLanding.cs, STD-038)
// and menus kept on the middle screen of a three-screen window (MenuCentering, STD-019). Compile it inside the plugin
// with RacingRow.cs, WindowsMonitors.cs, WindowLanding.cs and RacingWindowLanding.cs. The game decides when each runs;
// nothing here reads a game type, a render pipeline or a mod loader.
#nullable disable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace Dbce.Wheel.Display.Unity
{
    /// <summary>This process's Unity game window. Read-only; titles are read with InternalGetWindowText, which sends no
    /// message to the window.</summary>
    internal static class GameWindow
    {
        /// <summary>The class Unity gives its player windows.</summary>
        internal const string UnityWindowClass = "UnityWndClass";
        // Unity's title for an activated secondary display's window; such a window is never the game window.
        private const string SecondaryWindowTitle = "Unity Secondary Display";
        private static readonly uint OwnProcess = CurrentProcessId();

        /// <summary>The one visible, not minimized Unity player window of this process, or zero when there is none or
        /// more than one.</summary>
        internal static IntPtr Find()
        {
            var found = new List<IntPtr>();
            EnumWindowsProc callback = (window, _) =>
            {
                GetWindowThreadProcessId(window, out var process);
                if (process == OwnProcess && IsWindowVisible(window) && !IsIconic(window) &&
                    ClassName(window) == UnityWindowClass && Title(window) != SecondaryWindowTitle)
                    found.Add(window);
                return true;
            };
            if (!EnumWindows(callback, IntPtr.Zero)) throw new InvalidOperationException("EnumWindows failed");
            GC.KeepAlive(callback);
            return found.Count == 1 ? found[0] : IntPtr.Zero;
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
            using (var process = Process.GetCurrentProcess()) return (uint)process.Id;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int InternalGetWindowText(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window, StringBuilder text, int length);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    }

    /// <summary>
    /// One borderless game window across a row of separate monitors, as Assetto Corsa/CSP does (STD-047, owner
    /// 2026-11-01): the three views then draw into it exactly as under Surround. Unity's fullscreen cannot cover more
    /// than one monitor, so the window is windowed at the row's size, its border removed and placed at the row's origin.
    /// Call <see cref="Ensure"/> every frame while the span is wanted and <see cref="Release"/> when it is not.
    /// </summary>
    internal static class SpanWindow
    {
        /// <summary>The plugin's log.</summary>
        internal static Action<string> Log = _ => { };
        private static float _nextResize;
        private static string _placed;

        /// <summary>True while the span window is in use (the home-display landing must not move it).</summary>
        internal static bool Engaged { get; private set; }

        /// <summary>Whether the span needs this window: no single display (Surround) is that wide.</summary>
        internal static bool Needed(int width) => UnityEngine.Display.main.systemWidth < width;

        internal static void Release() { Engaged = false; _placed = null; }

        /// <summary>Null when the window covers the row at <paramref name="width"/> x <paramref name="height"/>; else why
        /// not yet. Leaving fullscreen takes one windowed resolution request every 2 s until Unity reports it.</summary>
        internal static string Ensure(int width, int height)
        {
            var row = RacingRowPolicy.Select(WindowsMonitors.Read());
            if (row.Kind != RacingRowKind.Separate || row.Width != width || row.Height != height)
            {
                Release();
                return "No row of separate monitors " + width + "x" + height + " (" + row + ").";
            }
            Engaged = true;
            if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                if (Time.realtimeSinceStartup >= _nextResize)
                {
                    _nextResize = Time.realtimeSinceStartup + 2f;
                    Screen.SetResolution(width, height, FullScreenMode.Windowed);
                    Log("Span window: switching to a " + width + "x" + height + " window across " + row + ".");
                }
                return "Switching to a window across the monitors.";
            }
            var window = GameWindow.Find();
            if (window == IntPtr.Zero || !GetWindowRect(window, out var rect)) return "Waiting for the game window.";
            var style = GetWindowLongPtr(window, GwlStyle).ToInt64();
            var wanted = (style & ~(WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox)) | WsPopup;
            if (style != wanted || rect.Left != row.X || rect.Top != row.Y || rect.Right - rect.Left != width || rect.Bottom - rect.Top != height)
            {
                if (style != wanted) SetWindowLongPtr(window, GwlStyle, new IntPtr(wanted));
                SetWindowPos(window, IntPtr.Zero, row.X, row.Y, width, height, SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
                var placed = row.ToString();
                if (_placed != placed) { _placed = placed; Log("Span window: borderless " + width + "x" + height + " at " + row.X + "," + row.Y + "."); }
            }
            return Screen.width == width && Screen.height == height ? null
                : "Waiting for the game to take the " + width + "x" + height + " window (now " + Screen.width + "x" + Screen.height + ").";
        }

        private const int GwlStyle = -16;
        private const long WsCaption = 0x00C00000, WsThickFrame = 0x00040000, WsSysMenu = 0x00080000, WsMinimizeBox = 0x00020000,
            WsMaximizeBox = 0x00010000, WsPopup = 0x80000000;
        private const uint SwpNoZOrder = 0x4, SwpNoActivate = 0x10, SwpFrameChanged = 0x20, SwpShowWindow = 0x40;
        [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }

    /// <summary>
    /// <see cref="RacingWindowLanding"/>'s host on Unity: the window from <see cref="GameWindow"/>, Unity's display list,
    /// its start-monitor preference (PlayerPrefs UnitySelectMonitor) and a windowed resolution request. The game gives its
    /// log and its own developer or test-launch marker (null for none); a muted launch (DBCE_FFB_MUTE), the editor and
    /// batch or test launches never save the start monitor.
    /// </summary>
    internal sealed class UnityWindowLandingHost : RacingWindowLanding.IHost
    {
        private const string MonitorKey = "UnitySelectMonitor";
        private readonly Action<string> _log;
        private readonly LandingPersistence _persistence;

        internal UnityWindowLandingHost(Action<string> log, Func<string> gameMarker)
        {
            _log = log;
            _persistence = new LandingPersistence(() => LandingLaunchPolicy.Block(Environment.GetEnvironmentVariable("DBCE_FFB_MUTE"),
                gameMarker(), Application.isEditor, Environment.GetCommandLineArgs()));
        }

        public IList<EngineDisplay> EngineDisplays => UnityEngine.Display.displays.Select(d => new EngineDisplay(d.systemWidth, d.systemHeight)).ToList();
        public int? SavedMonitor => PlayerPrefs.HasKey(MonitorKey) ? PlayerPrefs.GetInt(MonitorKey) : (int?)null;
        public string PersistenceBlock => _persistence.Block;
        public bool SaveMonitor(int? expected, int index) => _persistence.Save(expected, index,
            () => SavedMonitor, value => PlayerPrefs.SetInt(MonitorKey, value),
            () => PlayerPrefs.DeleteKey(MonitorKey), () => PlayerPrefs.Save(), Log);
        public float Now => Time.realtimeSinceStartup;
        public IReadOnlyList<MonitorRect> Monitors => WindowsMonitors.Read();
        public void Resize(int width, int height) => Screen.SetResolution(width, height, FullScreenMode.Windowed);
        public void Log(string message) => _log(message);

        public bool Observe(out RacingWindowLanding.Observation observation)
        {
            observation = default(RacingWindowLanding.Observation);
            var window = GameWindow.Find();
            if (window == IntPtr.Zero || !GetWindowRect(window, out var outer) || !GetClientRect(window, out var client)) return false;
            var origin = new Point { X = client.Left, Y = client.Top };
            if (!ClientToScreen(window, ref origin)) return false;
            // All native coordinates are sampled in the calling Unity thread's DPI context.
            // Translate the observed nonclient offset; never change global DPI awareness.
            observation = new RacingWindowLanding.Observation
            {
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
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }

    /// <summary>
    /// On a three-panel window, keeps the game's screen-space UI (menus, backgrounds, HUD) on the middle panel (STD-019):
    /// every root canvas covering the whole window has its top-level elements' horizontal anchors mapped into the center
    /// third, and plain-color camera backgrounds go black (a splash screen's grey filled the side panels once its
    /// background was moved to the middle, owner 2026-11-01). Canvases already on a camera viewport narrower than the
    /// window (a triple view's HUD) are left alone. Everything is restored on <see cref="Release"/>.
    /// Call <see cref="Update"/> about twice a second with whether it applies now (the game's setting and output).
    /// </summary>
    internal sealed class MenuCentering
    {
        private readonly Dictionary<RectTransform, KeyValuePair<Vector2, Vector2>> _original = new Dictionary<RectTransform, KeyValuePair<Vector2, Vector2>>();
        private readonly Dictionary<Camera, Color> _background = new Dictionary<Camera, Color>();

        internal bool Active => _original.Count > 0 || _background.Count > 0;

        internal void Update(bool active)
        {
            if (!active)
            {
                Release();
                return;
            }

            var gone = new List<RectTransform>();
            foreach (var rect in _original.Keys) if (rect == null) gone.Add(rect);
            foreach (var rect in gone) _original.Remove(rect);   // destroyed with their scene

            // FindObjectsOfType is obsolete from Unity 2023.1 but present in every engine this family uses (2019.4, 6000.0).
#pragma warning disable CS0618
            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>();
#pragma warning restore CS0618
            foreach (var canvas in canvases)
            {
                if (!canvas.isRootCanvas || canvas.renderMode == RenderMode.WorldSpace) continue;
                // A camera canvas already limited to a camera rect is not on the whole window.
                if (Mathf.Abs(canvas.pixelRect.width - Screen.width) > 1f) continue;
                foreach (Transform child in canvas.transform)
                    if (child is RectTransform rect && !_original.ContainsKey(rect))
                    {
                        _original.Add(rect, new KeyValuePair<Vector2, Vector2>(rect.anchorMin, rect.anchorMax));
                        rect.anchorMin = new Vector2(CenterThird(rect.anchorMin.x), rect.anchorMin.y);
                        rect.anchorMax = new Vector2(CenterThird(rect.anchorMax.x), rect.anchorMax.y);
                    }
            }
            foreach (var camera in Camera.allCameras)
                if (camera.clearFlags == CameraClearFlags.SolidColor && camera.backgroundColor != Color.black && !_background.ContainsKey(camera))
                {
                    _background.Add(camera, camera.backgroundColor);
                    camera.backgroundColor = Color.black;
                }
            // An element the game re-anchors after it was moved stays where the game put it.
        }

        internal static float CenterThird(float anchor) => (1f + anchor) / 3f;

        internal void Release()
        {
            foreach (var item in _original)
                if (item.Key != null)
                {
                    item.Key.anchorMin = item.Value.Key;
                    item.Key.anchorMax = item.Value.Value;
                }
            _original.Clear();
            foreach (var item in _background)
                if (item.Key != null) item.Key.backgroundColor = item.Value;
            _background.Clear();
        }
    }
}
