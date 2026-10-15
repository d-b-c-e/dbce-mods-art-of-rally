using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace ArtOfSimRally.Testing
{
    /// <summary>
    /// Developer-only F6 visual and shared-binding messaging capture (recorder <c>mode=ui</c>; docs/F6-UI-CAPTURE-RECIPE.md,
    /// sana 124216/124479 with kiki 124440's freeze criteria). A frame-stepped script over the mod's own panel through
    /// reflection: it opens Wheel settings, walks every page in Simple and Advanced, both text-size modes and a narrow UMM
    /// width, and drives the shared-binding host's explicit calls (listen, cancel, draft, discard, save, the close intent).
    /// After each step it waits, takes one game-only <see cref="ScreenCapture"/> shot, logs the host state read in the same
    /// frame, and waits for the file to exist and stop growing before the next step.
    /// It sends no OS input, calls no device API and never claims a moved-control capture: the host-call scenes are messaging
    /// and state evidence only. Everything it changed (UMM width, text mode, page, view, scroll, panel visibility, an unsaved
    /// draft) is put back on completion, on failure and at expiry. Outputs stay muted (the request's muteOutputs=1).
    /// </summary>
    internal static class UiCapture
    {
        private const BindingFlags Static = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private const BindingFlags Instance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const int SettleFrames = 3;
        private const float SettleSeconds = .4f, ReadySeconds = 20f, SharedWaitSeconds = 60f;

        private sealed class Step
        {
            public string Name;
            public Action Act;
            /// <summary>Null when the expected state holds, otherwise why not (logged; the run continues).</summary>
            public Func<string> Expect;
            public bool Shot = true;
        }

        private static Action<string> _log;
        private static string _dir, _shots;
        private static DateTime _until;
        private static StreamWriter _events;
        private static readonly List<Step> Steps = new List<Step>();
        private static int _step = -1, _settle, _shotCount, _stableChecks, _shotFailures;
        private static float _armedAt, _stepAt, _sharedWaitUntil;
        private static string _pendingShot;
        private static long _lastSize = -1;
        private static bool _active, _restored, _waitShared, _cleanStart;
        private static readonly List<string> Notes = new List<string>();

        // Reflection targets (resolved once at arm).
        private static Type _main, _panel, _presentation, _guard, _stock, _hostType, _host;
        private static object _widgets;
        private static Func<bool> _sharedStarted;
        // Saved for restoration.
        private static bool _savedOpen, _savedFollowHost;
        private static int _savedPage;
        private static string _savedView;
        private static Vector2 _savedWindow, _savedScroll;
        private static bool _haveSaved;

        internal static bool Active => _active;

        /// <param name="set">The request's uiSet: null or "full" for the whole tour; "followup" for the bounded re-check
        /// after a fix (sana 126101): normal FFB, 640 Advanced Cameras at the larger scale, and the binding steps.</param>
        internal static void Arm(string dir, DateTime untilUtc, Action<string> log, string set = null)
        {
            bool followup = string.Equals(set, "followup", StringComparison.OrdinalIgnoreCase);
            if (!followup && set != null && !string.Equals(set, "full", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("uiSet must be full or followup, not " + set);
            _log = log; _dir = dir; _until = untilUtc;
            _shots = Path.Combine(dir, "shots");
            Directory.CreateDirectory(_shots);
            _events = new StreamWriter(new FileStream(Path.Combine(dir, "ui-events.log"), FileMode.CreateNew, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
            _main = Need("ArtOfSimRally.Mod.Main");
            _panel = Need("ArtOfSimRally.Mod.SettingsPanel");
            _presentation = Need("ArtOfSimRally.Mod.SettingsPresentation");
            _guard = Need("ArtOfSimRally.Mod.DraftCloseGuard");
            _stock = Need("ArtOfSimRally.Mod.StockUiInput");
            _hostType = Need("ArtOfSimRally.Mod.SharedBindingCaptureHost");
            _host = Need("Dbce.Wheel.Input.ISharedBindingHost");
            try { _sharedStarted = SharedReadiness.Bind(AccessTools.TypeByName); }
            catch (Exception ex) { Note("shared readiness unavailable: " + ex.Message); }
            Build(followup);
            _armedAt = Time.realtimeSinceStartup;
            _active = true;
            // An early stop (game closing) still restores and leaves incomplete evidence (kiki 123183); the recorder's
            // Unload calls Abort too.
            Application.quitting += () => Abort("game closing before the capture completed");
            Event("ui capture armed (" + (followup ? "followup" : "full") + "): " + Steps.Count + " steps, expires " + _until.ToString("o"));
        }

        internal static void Tick()
        {
            if (!_active) return;
            try
            {
                if (DateTime.UtcNow > _until) { Finish("failed: request expired before the capture completed"); return; }
                if (_step < 0)
                {
                    // Wait for the title: the mod loaded, UMM's UI present, not driving, and past the splash screens.
                    if (Time.realtimeSinceStartup - _armedAt < ReadySeconds || Value(_main, "Settings") == null || UnityModManager.UI.Instance == null) return;
                    // Asserted, not assumed: no change is made while the game reports driving (kiki 125216).
                    if (Value(Need("ArtOfSimRally.Mod.GameState"), "IsDriving") is bool driving && driving) return;
                    SaveState();
                    // Refuse an editor that is not clean before any change: no capture, draft or saved-pending state of
                    // someone else's is ever discarded by this script (kiki 125183).
                    string dirty = HostValue("CapturingAction") != null ? "a capture is active" : HostValue("HasDraft") is bool d && d ? "a draft exists" :
                        HostValue("SavedPendingRestart") is bool p && p ? "saved bindings already wait for a restart" : null;
                    if (dirty != null) { Finish("failed: binding editor not clean at start (" + dirty + "); nothing was changed"); return; }
                    _cleanStart = true;
                    Begin(0);
                    return;
                }
                if (_waitShared)
                {
                    // Before the binding steps: up to SharedWaitSeconds for the mod's shared session, then go on regardless.
                    if (SharedReady() || Time.realtimeSinceStartup >= _sharedWaitUntil) { _waitShared = false; Begin(_step + 1); }
                    return;
                }
                if (_pendingShot != null) { WaitForShot(); return; }
                if (_settle > 0) { _settle--; return; }
                if (Time.realtimeSinceStartup - _stepAt < SettleSeconds) return;
                var step = Steps[_step];
                string unmet = step.Expect?.Invoke();
                if (unmet != null) Note(step.Name + ": " + unmet);
                if (step.Shot) TakeShot(step.Name, unmet);
                else Next();
            }
            catch (Exception ex) { Finish("failed: " + ex.GetType().Name + ": " + ex.Message); }
        }

        // ---- script ----

        private static void Build(bool followup)
        {
            Steps.Clear();
            string[] views = { "Simple", "Advanced" };
            Add("open", () => { if (!Visible) Call(_main, "ToggleSettings"); Select("Simple", 0); Scroll(0); }, () => Visible ? null : "settings did not open");
            float scaleA = 0, scaleB = 0;
            Func<string> narrow = () => Mathf.Abs(Window.x - 640) < .5f ? null : "UMM width did not take 640";
            Action narrowAtLargerScale = () => { FollowHost(scaleB > scaleA ? !_savedFollowHost : _savedFollowHost); Width(640); };
            if (followup)
            {
                // Normal FFB at the starting text mode, then both text modes read without shots to find the larger scale.
                Add("simple-p1-top", () => { Select("Simple", 1); Scroll(0); }, null);
                Add("scale-current", () => { }, () => { scaleA = Scale; return null; }, false);
                Add("scale-other", () => FollowHost(!_savedFollowHost), () => { scaleB = Scale; return null; }, false);
                Add("narrow-advanced-p2-top", () => { narrowAtLargerScale(); Select("Advanced", 2); Scroll(0); }, narrow);
                Add("narrow-advanced-p2-lower", () => Scroll(100000), narrow);
            }
            else
            {
                foreach (var view in views)
                    for (int page = 0; page < 5; page++)
                    {
                        int p = page; string v = view;
                        Add(v.ToLowerInvariant() + "-p" + p + "-top", () => { Select(v, p); Scroll(0); }, null);
                        if (p == 0 || p == 2) Add(v.ToLowerInvariant() + "-p" + p + "-lower", () => Scroll(100000), null);
                    }
                // Text size: the other mode for Controls and FFB, then back; remember which mode gives the larger scale.
                Add("scale-current", () => { Select("Simple", 0); Scroll(0); }, () => { scaleA = Scale; return null; });
                Add("scale-other-p0", () => FollowHost(!_savedFollowHost), () => { scaleB = Scale; return null; });
                Add("scale-other-p1", () => Select("Simple", 1), null);
                // Narrow: UMM's in-memory window width 640 at the larger effective scale, header and footer together.
                Add("narrow-p0-top", () => { narrowAtLargerScale(); Select("Simple", 0); Scroll(0); }, narrow);
                Add("narrow-p0-lower", () => Scroll(100000), null);
                Add("narrow-p2-top", () => { Select("Simple", 2); Scroll(0); }, null);
                // Advanced Cameras lower content: the numeric rows with Default commands, most likely to overflow at 640.
                Add("narrow-advanced-p2-lower", () => { Select("Advanced", 2); Scroll(100000); }, null);
            }
            Add("narrow-restored", () => { Width(_savedWindow.x); FollowHost(_savedFollowHost); Select("Simple", 0); Scroll(0); }, null);
            // Shared-binding messaging through the host (explicit calls only; no moved control).
            Add("bindings-ready", () => { }, () => SharedReady() ? null : "shared session not started; binding steps show the host's actual state", false);
            Add("bindings-state", () => { Select("Simple", 0); Scroll(0); Focus("bind-" + (FirstBound(null) ?? "steer")); Event("binding editor access " + HostValue("Access") + ": " + HostValue("AccessReason")); }, null);
            string bound = null, other = null;
            Add("bindings-listening", () => { bound = FirstBound(null); if (Editable && bound != null) HostCall("BeginCapture", bound); },
                () => !Editable ? "not editable: " + HostValue("AccessReason") : bound == null ? "no bound action" :
                      (string)HostValue("CapturingAction") == bound ? null : "capture did not start: " + HostValue("CaptureMessage"));
            Add("bindings-canceled", () => { if (HostValue("CapturingAction") != null) HostCall("CancelCapture"); },
                () => HostValue("CapturingAction") == null ? null : "capture still active");
            Add("bindings-draft", () => { if (Editable && bound != null) HostCall("Clear", bound); Scroll(100000); },
                () => !Editable ? "not editable" : (bool)HostValue("HasDraft") ? null : "no draft after Clear");
            Add("bindings-discarded", () => { if ((bool)HostValue("HasDraft")) HostCall("Discard"); },
                () => (bool)HostValue("HasDraft") ? "draft still present" : null);
            // The saved draft clears a bound action other than steer: a profile without steering is refused by the
            // document ("A usable steering binding is required", ControlProfileDocument), as run-d4b7959-ffboff showed.
            string saveTarget = null;
            Add("bindings-saved", () =>
            {
                saveTarget = FirstBound("steer");
                if (!Editable || saveTarget == null) return;
                HostCall("Clear", saveTarget);
                var args = new object[] { null };
                bool saved = (bool)_host.GetMethod("Save").Invoke(Host, args);
                Event("save result " + saved + " (cleared " + saveTarget + ") reason '" + (args[0] as string ?? "") + "'");
            }, () => !Editable ? "not editable" : saveTarget == null ? "no bound action besides steer" :
                  (bool)HostValue("SavedPendingRestart") ? null : "save did not leave a pending restart");
            // A fresh, distinct draft on an action that is still bound, then the close intent twice.
            Add("close-draft", () => { other = FirstBound(saveTarget); if (Editable && other != null) HostCall("Clear", other); },
                () => !Editable ? "not editable" : other == null ? "no second bound action" : (bool)HostValue("HasDraft") ? null : "no fresh draft");
            Add("close-held", () => Call(_main, "CloseSettings"),
                () => !(bool)HostValue("HasDraft") ? "no draft to hold the close" : Visible && (bool)Value(_guard, "Warned") ? null : "first close was not held");
            Add("close-closed", () => Call(_main, "CloseSettings"), () => Visible ? "second close did not close" : null);
        }

        private static void Add(string name, Action act, Func<string> expect, bool shot = true) =>
            Steps.Add(new Step { Name = name, Act = act, Expect = expect, Shot = shot });

        private static void Begin(int index)
        {
            _step = index;
            if (_step >= Steps.Count) { Finish(_shotFailures == 0 ? "complete" : "incomplete: " + _shotFailures + " shot(s) not written"); return; }
            var step = Steps[_step];
            step.Act();
            _settle = SettleFrames; _stepAt = Time.realtimeSinceStartup;
        }

        private static void Next()
        {
            if (_step + 1 < Steps.Count && Steps[_step + 1].Name == "bindings-ready" && !SharedReady())
            { _waitShared = true; _sharedWaitUntil = Time.realtimeSinceStartup + SharedWaitSeconds; Event("waiting up to " + SharedWaitSeconds + " s for the shared session"); return; }
            Begin(_step + 1);
        }

        // ---- shots ----

        private static void TakeShot(string name, string unmet)
        {
            string file = Path.Combine(_shots, (++_shotCount).ToString("00", CultureInfo.InvariantCulture) + "-" + name + ".png");
            Event("shot " + Path.GetFileName(file) + " | " + State() + (unmet == null ? "" : " | UNMET: " + unmet));
            ScreenCapture.CaptureScreenshot(file);
            _pendingShot = file; _lastSize = -1; _stableChecks = 0; _stepAt = Time.realtimeSinceStartup;
        }

        // The file is written asynchronously at the end of a frame: wait until it exists and its size is unchanged over
        // three checks, at most five seconds.
        private static void WaitForShot()
        {
            if (Time.realtimeSinceStartup - _stepAt > 5f)
            {
                // A missing or unfinished shot makes the whole capture incomplete; the remaining steps still run.
                _shotFailures++; Note("shot not written within 5 s: " + Path.GetFileName(_pendingShot)); _pendingShot = null; Next(); return;
            }
            if (!File.Exists(_pendingShot)) return;
            long size = new FileInfo(_pendingShot).Length;
            if (size > 0 && size == _lastSize) _stableChecks++; else _stableChecks = 0;
            _lastSize = size;
            if (_stableChecks < 3) return;
            _pendingShot = null;
            Next();
        }

        private static string State()
        {
            var settings = Value(_main, "Settings");
            string page = settings == null ? "?" : Field(settings, "SettingsPage")?.ToString();
            string view = settings == null ? "?" : Field(settings, "SettingsView") as string;
            return "visible " + Visible + " page " + page + " view " + view + " scale " + Scale.ToString("0.00", CultureInfo.InvariantCulture) +
                   " width " + Window.x.ToString("0", CultureInfo.InvariantCulture) + " access " + HostValue("Access") + " capturing " + (HostValue("CapturingAction") ?? "-") +
                   " draft " + HostValue("HasDraft") + " savedPendingRestart " + HostValue("SavedPendingRestart") + " closeWarned " + Value(_guard, "Warned") +
                   " editing " + Value(_panel, "Editing") + " stockUiBlocked " + Value(_stock, "Blocked") + " sharedStarted " + SharedReady() +
                   " focused " + Application.isFocused + " osForeground " + OsForeground() + " body " + Px(_presentation, "BodyHeight") + " estimate " + Px(_presentation, "PageHeight") +
                   " viewport " + Px(_presentation, "_viewport") + " panelTop " + Px(_presentation, "_panelTop") + " fixedRows " + Px(_presentation, "_fixedRows");
        }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        // The OS foreground beside Unity's isFocused (sana 131300): True only while the foreground window belongs to this
        // game process, the same test as the mod's force connect (FfbNative.FocusedGameWindow). Read only; nothing is
        // activated.
        private static bool OsForeground()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint process);
            return process == (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
        }
        // A measured panel height for the state line ("?" when the mod has no such member).
        private static string Px(Type type, string member)
        {
            try { return Value(type, member) is float v ? v.ToString("0", CultureInfo.InvariantCulture) : "?"; }
            catch (MissingMemberException) { return "?"; }
        }

        // ---- finish and restore ----

        /// <summary>An early stop (the recorder unloading, the game closing): restore and record it as failed.</summary>
        internal static void Abort(string reason) { if (_active) Finish("failed: " + reason); }

        private static void Finish(string outcome)
        {
            if (!_active) return;
            _active = false;
            string restore = Restore();
            // A capture is complete only if everything it changed was put back (kiki 125458).
            if (restore != "ok" && outcome == "complete") outcome = "incomplete: restore failed";
            Event("ui capture " + (outcome == "complete" ? "complete" : "ended") + ": " + outcome + "; shots " + _shotCount + "; restore " + restore);
            try
            {
                File.WriteAllLines(Path.Combine(_dir, "notes.txt"), Notes);
                if (outcome != "complete" || restore != "ok") File.WriteAllText(Path.Combine(_dir, "incomplete.txt"), outcome + "; restore " + restore + "\n");
                File.WriteAllText(Path.Combine(_dir, "end.txt"), outcome + "; shots " + _shotCount + "; unmet " + Notes.Count + "; restore " + restore + "\n");
            }
            catch (Exception ex) { _log?.Invoke("UI capture: end files failed: " + ex.Message); }
            try { _events?.Dispose(); } catch { }
            _events = null;
        }

        private static string Restore()
        {
            if (_restored || !_haveSaved) return _haveSaved ? "ok" : "nothing saved";
            _restored = true;
            var problems = new List<string>();
            void Try(string what, Action action) { try { action(); } catch (Exception ex) { problems.Add(what + ": " + ex.Message); } }
            // Only this run's own capture and draft are dropped: the start was refused unless the editor was clean.
            if (_cleanStart)
            {
                Try("capture", () => { if (HostValue("CapturingAction") != null) HostCall("CancelCapture"); });
                Try("draft", () => { if (HostValue("HasDraft") is bool d && d) HostCall("Discard"); });
            }
            Try("width", () => Width(_savedWindow.x));
            Try("text mode", () => FollowHost(_savedFollowHost));
            Try("page and view", () => Select(_savedView, _savedPage));
            Try("scroll", () => SetValue(_panel, "_scroll", _savedScroll));
            Try("visibility", () => { if (Visible != _savedOpen) { if (_savedOpen) Call(_main, "ToggleSettings"); else Call(_main, "CloseSettings"); } });
            Try("close warning", () => Call(_guard, "Reset"));
            return problems.Count == 0 ? "ok" : string.Join("; ", problems);
        }

        private static void SaveState()
        {
            var settings = Value(_main, "Settings");
            _savedOpen = Visible;
            _savedFollowHost = (bool)Field(settings, "SettingsFollowHostScale");
            _savedView = Field(settings, "SettingsView") as string ?? "Simple";
            int stored = (int)Field(settings, "SettingsPage");
            _savedPage = stored >= 1 && stored <= 5 ? stored - 1 : 0;
            _savedWindow = Window;
            _savedScroll = (Vector2)Value(_panel, "_scroll");
            _haveSaved = true;
            Event("saved state: open " + _savedOpen + " view " + _savedView + " page " + _savedPage + " followHost " + _savedFollowHost +
                  " width " + _savedWindow.x.ToString("0", CultureInfo.InvariantCulture));
        }

        // ---- panel and host access (reflection only) ----

        private static bool Visible => (bool)Value(_main, "SettingsVisible");
        private static float Scale => (float)Value(_presentation, "Scale");
        private static readonly FieldInfo WindowSize = typeof(UnityModManager.UI).GetField("mWindowSize", Instance);
        private static Vector2 Window => WindowSize != null && UnityModManager.UI.Instance != null ? (Vector2)WindowSize.GetValue(UnityModManager.UI.Instance) : Vector2.zero;
        private static void Width(float x)
        {
            if (WindowSize == null || UnityModManager.UI.Instance == null) throw new MissingFieldException("UnityModManager.UI.mWindowSize");
            var size = Window; size.x = x; WindowSize.SetValue(UnityModManager.UI.Instance, size);
        }
        private static void Select(string view, int page)
        {
            var settings = Value(_main, "Settings");
            Need("ArtOfSimRally.Mod.SettingsViewPolicy").GetMethod("Select", Static).Invoke(null, new object[] { settings, view == "Advanced", page, false });
        }
        private static void Scroll(float y) => SetValue(_panel, "_scroll", new Vector2(0, y));
        private static void FollowHost(bool on)
        {
            var settings = Value(_main, "Settings");
            settings.GetType().GetField("SettingsFollowHostScale", Instance).SetValue(settings, on);
        }
        private static void Focus(string id)
        {
            _widgets = _widgets ?? Value(_panel, "W");
            _widgets?.GetType().GetMethod("Focus", Instance)?.Invoke(_widgets, new object[] { id });
        }
        private static bool SharedReady() { try { return _sharedStarted != null && _sharedStarted(); } catch { return false; } }

        private static object Host => Value(_hostType, "Current");
        private static bool Editable => HostValue("Access")?.ToString() == "Editable";
        private static object HostValue(string property)
        {
            try { var host = Host; return host == null ? null : _host.GetProperty(property)?.GetValue(host, null); }
            catch (TargetInvocationException ex) { return "error: " + ex.InnerException?.Message; }
        }
        private static void HostCall(string method, params object[] args)
        {
            var types = args.Select(a => a.GetType()).ToArray();
            (_host.GetMethod(method, types) ?? throw new MissingMethodException("ISharedBindingHost." + method)).Invoke(Host, args);
        }
        /// <summary>The first row whose effective value is bound (draft, then next start, then active), other than <paramref name="except"/>.</summary>
        private static string FirstBound(string except)
        {
            if (!(HostValue("Rows") is IEnumerable rows)) return null;
            foreach (var row in rows)
            {
                string action = Prop(row, "Action") as string;
                if (action == null || action == except) continue;
                object value = Prop(row, "Draft") ?? Prop(row, "NextStart") ?? Prop(row, "Active");
                if (value != null && Prop(value, "Bound") is bool b && b) return action;
            }
            return null;
        }

        private static Type Need(string name) => AccessTools.TypeByName(name) ?? throw new MissingMemberException(name);
        private static object Value(Type type, string member)
        {
            var p = type.GetProperty(member, Static); if (p != null) return p.GetValue(null, null);
            var f = type.GetField(member, Static); if (f != null) return f.GetValue(null);
            throw new MissingMemberException(type.Name + "." + member);
        }
        private static void SetValue(Type type, string member, object value)
        {
            var f = type.GetField(member, Static); if (f != null) { f.SetValue(null, value); return; }
            var p = type.GetProperty(member, Static); if (p != null && p.CanWrite) { p.SetValue(null, value, null); return; }
            throw new MissingMemberException(type.Name + "." + member);
        }
        private static object Call(Type type, string method) =>
            (type.GetMethod(method, Static, null, Type.EmptyTypes, null) ?? throw new MissingMethodException(type.Name + "." + method)).Invoke(null, null);
        private static object Field(object target, string name) => target.GetType().GetField(name, Instance)?.GetValue(target) ?? target.GetType().GetProperty(name, Instance)?.GetValue(target, null);
        private static object Prop(object target, string name) => target.GetType().GetProperty(name, Instance)?.GetValue(target, null);

        private static void Note(string text) { Notes.Add(text); Event("note: " + text); }
        private static void Event(string text)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " f" + Time.frameCount + " " + text;
            try { _events?.WriteLine(line); } catch { }
            _log?.Invoke("UI capture: " + text);
        }
    }
}
