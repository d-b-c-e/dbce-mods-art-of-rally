// MIT. P/Invoke wrapper of dbce_ui.dll (native/ui-host/dbce_ui.h), the shared F6 panel core drawn inside the game by
// its own present hooks (UI-2). One source for Unity Mono 2.0 (net35), Mono 4.x and IL2CPP (BepInEx 6 on .NET 6):
// C# 7.3, no engine types, no callbacks (the core never calls managed code, so nothing runs on the render thread).
// Strings cross as UTF-8 byte arrays, so no marshaling feature newer than .NET 3.5 is needed.
//
// Use, on the mod's own thread (Update):
//   DbceUi.Load(Path.Combine(pluginDir, IntPtr.Size == 8 ? "x64/dbce_ui.dll" : "x86/dbce_ui.dll"), out error);
//   DbceUi.Configure(descriptorJson, iniPath, ffbStop: true, out error);   // then push the saved values
//   each frame: while (DbceUi.TryPollEvent(out e)) { apply and save e }; forward wheel D-pad presses with Navigate.
// A D3D9 game also calls AttachD3D9(texture.GetNativeTexturePtr()) and GL.IssuePluginEvent(DbceUi.RenderEventFunc, 1).
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Dbce.Wheel.Ui
{
    /// <summary>Navigation commands: the same path as the keyboard's arrows, Enter/Space and Esc.</summary>
    public enum DbceUiNav { Up = 0, Down = 1, Left = 2, Right = 3, Select = 4, Back = 5, Open = 6, Close = 7 }

    public enum DbceUiEventType { None = 0, Changed = 1, Action = 2, Capture = 3, Clear = 4 }

    public enum DbceUiTone { Normal = 0, Good = 1, Warning = 2, Error = 3 }

    /// <summary>The standard pages, as <see cref="DbceUi.GetState"/> reports them.</summary>
    public enum DbceUiPage { Controls = 0, Ffb = 1, Display = 2, Cameras = 3, Telemetry = 4, Help = 5, Developer = 6 }

    /// <summary>A player change or command. Changed carries the value for the row's kind; Action "panel.stop-ffb" is F8.</summary>
    public struct DbceUiEvent
    {
        public DbceUiEventType Type;
        public string Id;
        public bool Toggle;
        public int Choice;
        public double Number;
    }

    [Flags]
    public enum DbceUiStatus { None = 0, Hooked = 1, Drawing = 2, Configured = 4, D3D9 = 8 }

    public static class DbceUi
    {
        public const int ApiVersion = 1;
        public const string StopFfbAction = "panel.stop-ffb";
        const string Dll = "dbce_ui";
        const int FfbStopFlag = 1;

        /// <summary>True once <see cref="Load"/> found the DLL and its API version; every other call is a no-op before.</summary>
        public static bool Loaded { get; private set; }

        /// <summary>Loads dbce_ui.dll from a full path (the build that matches the process: x86 or x64). Loading starts
        /// the panel's present and input hooks. False with the reason when the file is missing or of another API.</summary>
        public static bool Load(string path, out string error)
        {
            error = null;
            if (Loaded) return true;
            if (string.IsNullOrEmpty(path)) { error = "no path"; return false; }
            if (LoadLibraryW(path) == IntPtr.Zero) { error = "LoadLibrary failed (" + Marshal.GetLastWin32Error() + "): " + path; return false; }
            int version;
            try { version = dbce_ui_version(); }
            catch (Exception e) { error = e.GetType().Name + ": " + e.Message; return false; }
            if (version != ApiVersion) { error = "dbce_ui API " + version + ", expected " + ApiVersion; return false; }
            Loaded = true;
            return true;
        }

        /// <summary>The settings descriptor (dbce-mod.json "settings"), read strictly; iniPath is the mod's config, where
        /// the panel keeps [Panel] View, Scale, Page and ShowFrameRate. ffbStop: F8 sends <see cref="StopFfbAction"/>.</summary>
        public static bool Configure(string descriptorJson, string iniPath, bool ffbStop, out string error)
        {
            error = null;
            if (!Loaded) { error = "dbce_ui not loaded"; return false; }
            byte[] json = Utf8(descriptorJson);
            if (json == null) { error = "no descriptor"; return false; }
            var buffer = new byte[256];
            if (dbce_ui_configure(json, json.Length - 1, Utf8(iniPath), ffbStop ? FfbStopFlag : 0, buffer, buffer.Length) != 0) return true;
            error = FromUtf8(buffer);
            return false;
        }

        public static bool SetToggle(string id, bool value) { return Loaded && dbce_ui_set_toggle(Utf8(id), value ? 1 : 0) != 0; }
        public static bool SetChoice(string id, int index) { return Loaded && dbce_ui_set_choice(Utf8(id), index) != 0; }
        public static bool SetNumber(string id, double value) { return Loaded && dbce_ui_set_number(Utf8(id), value) != 0; }
        public static bool SetText(string id, string text, DbceUiTone tone = DbceUiTone.Normal) { return Loaded && dbce_ui_set_text(Utf8(id), Utf8(text ?? ""), (int)tone) != 0; }
        public static bool SetRowVisible(string id, bool visible) { return Loaded && dbce_ui_set_row_visible(Utf8(id), visible ? 1 : 0) != 0; }
        public static void SetFfbOn(bool on) { if (Loaded) dbce_ui_set_ffb_on(on ? 1 : 0); }

        static readonly byte[] s_id = new byte[128];

        /// <summary>The oldest change or command; false when there is none.</summary>
        public static bool TryPollEvent(out DbceUiEvent e)
        {
            e = default(DbceUiEvent);
            if (!Loaded) return false;
            int type, toggle, choice;
            double number;
            lock (s_id) {
                if (dbce_ui_poll_event(out type, s_id, s_id.Length, out toggle, out choice, out number) == 0) return false;
                e.Id = FromUtf8(s_id);
            }
            e.Type = (DbceUiEventType)type;
            e.Toggle = toggle != 0;
            e.Choice = choice;
            e.Number = number;
            return true;
        }

        /// <summary>A navigation command, for the wheel's D-pad and buttons; false when it does not apply now.</summary>
        public static bool Navigate(DbceUiNav command) { return Loaded && dbce_ui_navigate((int)command) != 0; }

        /// <summary>True while F6 is open: the mod blocks the game's own input.</summary>
        public static bool IsOpen { get { return Loaded && dbce_ui_is_open() != 0; } }

        /// <summary>The page shown, whether More options is open, and the focused control's id ("" on the tab row).</summary>
        public static bool GetState(out DbceUiPage page, out bool moreOptions, out string focus)
        {
            page = DbceUiPage.Help; moreOptions = false; focus = "";
            if (!Loaded) return false;
            int p, more;
            var buffer = new byte[128];
            bool open = dbce_ui_state(out p, out more, buffer, buffer.Length) != 0;
            page = (DbceUiPage)p; moreOptions = more != 0; focus = FromUtf8(buffer);
            return open;
        }

        /// <summary>Up to two lines at the bottom of the centre screen (camera name, notices); 0 ms is 3 s.</summary>
        public static void ShowToast(string line1, string line2 = null, uint milliseconds = 0) { if (Loaded) dbce_ui_show_toast(Utf8(line1), Utf8(line2), milliseconds); }

        /// <summary>After the mod saved a change: "Saved" for two seconds, or the failure.</summary>
        public static void NoteSaved(bool ok, string detail = null) { if (Loaded) dbce_ui_note_saved(ok ? 1 : 0, Utf8(detail)); }

        /// <summary>D3D9 games: the device, or a resource of it (Texture.GetNativeTexturePtr()). Taken on the render thread
        /// at the next GL.IssuePluginEvent(RenderEventFunc, 1).</summary>
        public static bool AttachD3D9(IntPtr deviceOrResource) { return Loaded && deviceOrResource != IntPtr.Zero && dbce_ui_attach_d3d9(deviceOrResource) != 0; }

        /// <summary>GL.IssuePluginEvent ids: take the device AttachD3D9 handed over; let go of it before the game releases
        /// it (a device recreated for a mode change: detach, then attach the new one).</summary>
        public const int RenderAttachD3D9 = 1, RenderDetachD3D9 = 2;

        /// <summary>The native render-event function for GL.IssuePluginEvent; IntPtr.Zero before Load.</summary>
        public static IntPtr RenderEventFunc { get { return Loaded ? dbce_ui_render_event_func() : IntPtr.Zero; } }

        public static DbceUiStatus Status { get { return Loaded ? (DbceUiStatus)dbce_ui_status() : DbceUiStatus.None; } }

        static readonly byte[] s_log = new byte[512];

        /// <summary>The oldest unread log line of the core (hooks, swap chain, refusals), for the mod's own log.</summary>
        public static bool TryPollLog(out string line)
        {
            line = null;
            if (!Loaded) return false;
            lock (s_log) {
                if (dbce_ui_poll_log(s_log, s_log.Length) == 0) return false;
                line = FromUtf8(s_log);
            }
            return true;
        }

        static byte[] Utf8(string s)
        {
            if (s == null) return null;
            var bytes = new byte[Encoding.UTF8.GetByteCount(s) + 1];
            Encoding.UTF8.GetBytes(s, 0, s.Length, bytes, 0);
            return bytes;
        }

        static string FromUtf8(byte[] b)
        {
            int n = 0;
            while (n < b.Length && b[n] != 0) n++;
            return Encoding.UTF8.GetString(b, 0, n);
        }

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibraryW(string path);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_version();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_configure(byte[] json, int length, byte[] iniPath, int flags, byte[] error, int errorSize);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_set_toggle(byte[] id, int value);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_set_choice(byte[] id, int index);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_set_number(byte[] id, double value);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_set_text(byte[] id, byte[] text, int tone);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_set_row_visible(byte[] id, int visible);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void dbce_ui_set_ffb_on(int on);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_poll_event(out int type, byte[] id, int idSize, out int toggle, out int choice, out double number);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_navigate(int command);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_is_open();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_state(out int page, out int moreOptions, byte[] focus, int focusSize);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void dbce_ui_show_toast(byte[] line1, byte[] line2, uint milliseconds);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void dbce_ui_note_saved(int ok, byte[] detail);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_attach_d3d9(IntPtr deviceOrResource);
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern IntPtr dbce_ui_render_event_func();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_status();
        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern int dbce_ui_poll_log(byte[] line, int size);
    }
}
