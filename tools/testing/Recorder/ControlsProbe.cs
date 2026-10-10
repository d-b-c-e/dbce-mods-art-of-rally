using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Dbce.Wheel.Input;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace ArtOfSimRally.Testing
{
    // Optional developer probe only. Not in the player mod/package. Arm once
    // before a cold launch; never bypass the mod's binding or focus guards.
    internal static class ControlsProbe
    {
        private const BindingFlags Flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private const string PatchId = "ArtOfSimRally.DevRecorder.RawControls";
        internal static string RequestPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArtOfSimRally", "controls-request.txt");
        internal static string SwitchPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dbce", "art-of-rally", "inject.on");
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static NativeTestInjectionClient _native;
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr module, string name);
        private static readonly HashSet<string> Seen = new HashSet<string>();
        private static readonly HashSet<int> WatchedActions = new HashSet<int>();
        private static Harmony _patches;
        private static StreamWriter _trace;
        private static FieldInfo _devices, _slot, _guid;
        private static MethodInfo _value;
        private static MethodInfo _closeInputs;
        private static PropertyInfo _inputStatus;
        private static FieldInfo[] _menuActions;
        private static Array _channels;
        private static string _nonce, _directory, _pendingStop, _nativePath, _nativeHash;
        private static double _deadline, _nextSample, _observeUntil, _nextModuleCheck;
        private static IntPtr _nativeModule;
        private static int _commands, _rows, _seenFrame = -1;
        private static bool _armed, _requested, _closed;
        private static readonly FieldInfo CameraAngles = typeof(CarCameras).GetField("CameraAnglesList", BindingFlags.Instance | BindingFlags.NonPublic);
        private static Action<string> _log;
        private static double Now => Clock.Elapsed.TotalSeconds;
        private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        internal static void TryArm(Assembly mod, Action<string> log)
        {
            _log = log;
            if (!File.Exists(RequestPath) && !File.Exists(SwitchPath)) return;
            _requested = true;
            // Reuse the already-reviewed whole-process force/telemetry/score
            // mute. It remains latched even if the request or hook is refused.
            try
            {
                SessionTape.MuteForControls();
                if (SessionTape.Active) throw new InvalidOperationException("Raw controls require a separate run from tape recording/playback.");
                Attach(mod); // assignment/force-open guards latch even if validation subsequently fails
                if (!File.Exists(SwitchPath) || !File.Exists(RequestPath)) throw new InvalidDataException("Both cold-start inject.on and controls request are required.");
                if (new FileInfo(RequestPath).Length > 16384) throw new InvalidDataException("Oversize controls request.");
                var request = File.ReadAllLines(RequestPath).Select(l => l.Split(new[] { '=' }, 2)).Where(p => p.Length == 2)
                    .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.Ordinal);
                File.Move(RequestPath, RequestPath + "." + Process.GetCurrentProcess().Id + ".taken");
                if (!DateTime.TryParse(request["expiresUtc"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var expires) ||
                    expires <= DateTime.UtcNow || expires > DateTime.UtcNow.AddMinutes(5)) throw new InvalidDataException("Invalid request expiry.");
                if (!Guid.TryParseExact(request["nonce"], "N", out var nonce) || nonce == Guid.Empty) throw new InvalidDataException("Invalid run nonce.");
                if (!string.Equals(request["modSha256"], ArtifactHash.FileHash(mod.Location), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Mod differs from prepared run.");
                var settings = Path.Combine(Path.GetDirectoryName(mod.Location), "Settings.xml");
                if (!string.Equals(request["settingsSha256"], ArtifactHash.FileHash(settings), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Settings differ from production Apply.");
                string directory = Path.GetFullPath(request["out"]);
                if (!Path.IsPathRooted(request["out"]) || Directory.Exists(directory) || File.Exists(directory)) throw new InvalidDataException("Output must be a new absolute directory.");
                var native = mod.GetType("ArtOfSimRally.Mod.FfbNative", true);
                _nativePath = Path.GetFullPath((string)native.GetProperty("RequestedPath", Flags).GetValue(null));
                _nativeHash = request["nativeSha256"];
                _nativeModule = VerifiedNativeModule();
                _native = NativeTestInjectionClient.FromResidentExports(name => GetProcAddress(_nativeModule, name));
                if (!_native.Arm()) throw new InvalidOperationException(_native.Status);
                Directory.CreateDirectory(directory);
                _directory = directory; // only our newly created output can receive a failure result
                File.WriteAllText(Path.Combine(_directory, "identity.txt"), "modSha256=" + ArtifactHash.FileHash(mod.Location) + "\nsettingsSha256=" + ArtifactHash.FileHash(settings) +
                    "\nprobeSha256=" + ArtifactHash.FileHash(typeof(ControlsProbe).Assembly.Location) + "\nnativePath=" + _nativePath + "\nnativeSha256=" + _nativeHash +
                    "\nnativeStatus=" + _native.Status + "\nphysicalOutput=false\nkind=native-raw-before-binding\n", new UTF8Encoding(false));
                _trace = new StreamWriter(new FileStream(Path.Combine(_directory, "observations.tsv"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));
                _trace.WriteLine("time_s\tframe\tkind\tdata");
                _nonce = nonce.ToString("N"); _deadline = Now + 300; _armed = true;
                Row("armed", _native.Status + "; raw reader, downstream values and actual Rewired calls; normal guards retained");
                _log?.Invoke("Raw controls probe armed for five minutes; force/telemetry delivery and score output muted.");
            }
            catch (Exception e) { Close("arm failed: " + e.Message); _log?.Invoke("Raw controls probe refused: " + e.Message); }
        }

        private static IntPtr VerifiedNativeModule()
        {
            using (var process = Process.GetCurrentProcess())
            {
                var modules = process.Modules.Cast<ProcessModule>().Where(m =>
                    string.Equals(m.ModuleName, "UnityForceFeedback.dll", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.ModuleName, "WheelFfb.dll", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (modules.Length != 1 || !string.Equals(Path.GetFullPath(modules[0].FileName), _nativePath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(ArtifactHash.FileHash(_nativePath), _nativeHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Expected exactly the hash-pinned reader module; missing, changed or multiple native copies refused.");
                return modules[0].BaseAddress;
            }
        }

        // Separately exercised against the actual game/mod metadata by ProbeHooks.
        internal static void Attach(Assembly mod)
        {
            var input = mod.GetType("ArtOfSimRally.Mod.WheelInput", true);
            _devices = input.GetField("_devices", Flags) ?? throw new MissingFieldException("WheelInput._devices");
            var device = input.GetNestedType("Device", BindingFlags.NonPublic) ?? throw new MissingMemberException("WheelInput.Device");
            _slot = device.GetField("Slot"); _guid = device.GetField("InstanceGuid");
            _value = input.GetMethod("Value", Flags);
            _closeInputs = input.GetMethod("Close", Flags);
            _inputStatus = input.GetProperty("Status", Flags) ?? throw new MissingMemberException("WheelInput.Status");
            _channels = Enum.GetValues(_value.GetParameters()[0].ParameterType);
            var gameButtons = mod.GetType("ArtOfSimRally.Mod.GameButtonInput", true);
            _menuActions = new[] { "_horizontal", "_vertical", "_submit", "_cancel" }.Select(name => gameButtons.GetField(name, Flags) ?? throw new MissingFieldException(name)).ToArray();
            var read = AccessTools.Method(mod.GetType("ArtOfSimRally.Mod.WheelPovInput", true), "Read", new[] { typeof(int), typeof(int[]), typeof(byte[]), typeof(int[]) });
            if (_slot == null || _guid == null || read == null || read.ReturnType != typeof(bool)) throw new MissingMemberException("Raw reader contract changed.");
            _patches = new Harmony(PatchId);
            foreach (var method in AccessTools.TypeByName("Dbce.Wheel.Ffb.WheelFfbNative").GetMethods().Where(m => m.Name == "Initialise"))
                _patches.Patch(method, prefix: new HarmonyMethod(typeof(ControlsProbe), nameof(RefuseForceOpen)) { priority = Priority.First });
            foreach (var name in new[] { "BeginAssign", "BeginCalibration" })
                _patches.Patch(AccessTools.Method(input, name), prefix: new HarmonyMethod(typeof(ControlsProbe), nameof(RefuseAssignment)) { priority = Priority.First });
            _patches.Patch(AccessTools.Method(input, "SaveCalibration"), prefix: new HarmonyMethod(typeof(ControlsProbe), nameof(RefuseAssignmentBool)) { priority = Priority.First });
            _patches.Patch(AccessTools.Method(mod.GetType("ArtOfSimRally.Mod.Shifter", true), "PollForBinding"), prefix: new HarmonyMethod(typeof(ControlsProbe), nameof(RefuseAssignment)) { priority = Priority.First });
            _patches.Patch(read, postfix: new HarmonyMethod(typeof(ControlsProbe), nameof(AfterRead)) { priority = Priority.Last });
            foreach (var name in new[] { "GetButton", "GetButtonDown", "GetNegativeButton", "GetNegativeButtonDown" })
                _patches.Patch(AccessTools.Method(typeof(Player), name, new[] { typeof(int) }), postfix: new HarmonyMethod(typeof(ControlsProbe), nameof(ObserveButton)) { priority = Priority.Last });
            _patches.Patch(AccessTools.Method(typeof(Player), "GetAxis", new[] { typeof(int) }), postfix: new HarmonyMethod(typeof(ControlsProbe), nameof(ObserveAxis)) { priority = Priority.Last });
            _patches.Patch(AccessTools.Method(typeof(AxisCarController), "GetInput"), postfix: new HarmonyMethod(typeof(ControlsProbe), nameof(ObserveCarInput)) { priority = Priority.Last });
        }

        private static bool RefuseAssignment()
        {
            if (!_requested) return true;
            _inputStatus?.SetValue(null, "Binding/calibration disabled during a controls test. Restart for normal configuration.");
            return false;
        }
        private static bool RefuseAssignmentBool(ref bool __result) { if (!_requested) return true; __result = false; RefuseAssignment(); return false; }
        private static bool RefuseForceOpen(ref bool __result) { if (!_requested) return true; __result = false; return false; }

        internal static string Command(string command)
        {
            if (!command.StartsWith("CONTROLS ", StringComparison.Ordinal)) return null;
            try
            {
                var parts = command.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                Tick();
                if (!_armed || parts.Length < 3 || parts[2] != _nonce) return "ERROR controls probe not armed for this run";
                if (parts[1] == "STATUS" && parts.Length == 3) return "OK native=" + _native.Status + " commands=" + _commands;
                if (parts[1] == "STOP" && parts.Length == 3) { Close("stopped by command"); return "OK controls test stopped; native force fence remains latched"; }
                if (parts[1] != "RAW" || parts.Length < 5) return "ERROR use CONTROLS RAW <nonce> inject raw ...";
                if (++_commands > 256) { Close("command limit"); return "ERROR command limit"; }
                string raw = string.Join(" ", parts.Skip(3));
                bool accepted = _native.SubmitRaw(raw, out string reason);
                if (!_native.Armed) { Close("native status uncertain: " + _native.Status); return "ERROR native test lost confirmation"; }
                _observeUntil = Now + 16; // native grammar bounds the longest pulse at 15 s, plus release
                Row(accepted ? "request-accepted" : "request-refused", raw + "; " + reason);
                return accepted ? "OK raw command accepted; delivery and game observation are separate" : "ERROR " + reason;
            }
            catch (Exception e) { return "ERROR " + e.Message; }
        }

        internal static void Tick()
        {
            if (_pendingStop != null) { string stop = _pendingStop; _pendingStop = null; Close(stop); }
            if (!_armed) return;
            try
            {
                double now = Now;
                if (now >= _deadline) { Close("duration ended"); return; }
                if (now < _nextSample) return;
                _nextSample = now + .1;
                if (!_native.RefreshStatus()) { Close(_native.Status); return; }
                if (now >= _nextModuleCheck)
                {
                    _nextModuleCheck = now + 1;
                    if (VerifiedNativeModule() != _nativeModule) { Close("native reader module changed"); return; }
                }
                WatchedActions.Clear();
                foreach (int fixedId in new[] { 14, 17, 19, 61 }) WatchedActions.Add(fixedId);
                foreach (var field in _menuActions) { int id = (int)field.GetValue(null); if (id >= 0) WatchedActions.Add(id); }
                Row("binding-values", "focused=" + Application.isFocused + " " + string.Join(" ", _channels.Cast<object>().Select(c => c + "=" + Convert.ToString(_value.Invoke(null, new[] { c }), CultureInfo.InvariantCulture))));
                Row("native-status", _native.Status);
                if (now <= _observeUntil)
                {
                    var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
                    Row("menu-selection", selected == null ? "none" : MenuPlayback.Path(selected.transform));
                    foreach (var camera in UnityEngine.Object.FindObjectsOfType<CarCameras>())
                    {
                        var angles = CameraAngles?.GetValue(camera) as IList;
                        Row("camera-selection", "rig=" + camera.GetInstanceID() + " enabled=" + camera.enabled + " index=" + (angles == null ? -1 : angles.IndexOf(camera.CurrentCameraAngle)));
                    }
                }
                _trace.Flush();
            }
            catch (Exception e) { Close("sampling failed: " + e.Message); }
        }

        private static void AfterRead(int __0, int[] __1, byte[] __2, int[] __3, bool __result)
        {
            if (!_armed || Now > _observeUntil) return;
            try
            {
                Guid identity = Guid.Empty; int matches = 0;
                foreach (var device in (IEnumerable)_devices.GetValue(null))
                    if ((int)_slot.GetValue(device) == __0) { matches++; if (_guid.GetValue(device) is Guid guid) identity = guid; }
                if (matches != 1 || identity == Guid.Empty) { _armed = false; _pendingStop = "reader identity unavailable"; return; }
                bool injected = _native.LastReadInjected(__0);
                if (!_native.Armed) { _armed = false; _pendingStop = _native.Status; return; }
                Row("raw-read", "slot=" + __0 + " guid=" + identity + " valid=" + __result + " injected=" + injected + " axes=" + string.Join(",", __1) +
                    " hats=" + string.Join(",", __3) + " buttons=" + string.Join(",", __2.Select((v,i) => v != 0 ? i.ToString(CultureInfo.InvariantCulture) : "").Where(v => v.Length > 0)));
            }
            catch (Exception e) { _armed = false; _pendingStop = "raw hook failed: " + e.Message; }
        }

        private static void ObserveButton(Player __instance, int __0, MethodBase __originalMethod, bool __result) => Observe(__instance, __0, __originalMethod.Name, __result ? "1" : "0");
        private static void ObserveAxis(Player __instance, int __0, float __result) => Observe(__instance, __0, "GetAxis", N(__result));
        private static void ObserveCarInput(AxisCarController __instance, float throttleInput, float brakeInput, float steerInput, float handbrakeInput, float clutchInput)
        {
            if (!_armed || Now > _observeUntil) return;
            try { Row("car-input", "id=" + __instance.GetInstanceID() + " steer=" + N(steerInput) + " throttle=" + N(throttleInput) + " brake=" + N(brakeInput) + " handbrake=" + N(handbrakeInput) + " clutch=" + N(clutchInput)); }
            catch (Exception e) { _armed = false; _pendingStop = "car observer failed: " + e.Message; }
        }
        private static void Observe(Player player, int action, string method, string value)
        {
            if (!_armed || Now > _observeUntil || !WatchedActions.Contains(action)) return;
            try
            {
                if (_seenFrame != Time.frameCount) { Seen.Clear(); _seenFrame = Time.frameCount; }
                string data = "player=" + player.id + " action=" + action + " " + method + "=" + value;
                if (Seen.Count < 256 && Seen.Add(data)) Row("game-read", data);
            }
            catch (Exception e) { _armed = false; _pendingStop = "observer failed: " + e.Message; }
        }
        private static void Row(string kind, string data)
        {
            if (_trace == null) return;
            if (++_rows > 200000) { _armed = false; _pendingStop = "row limit"; return; }
            _trace.WriteLine(N(Now) + "\t" + Time.frameCount + "\t" + kind + "\t" + data.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' '));
        }
        internal static void Close(string reason)
        {
            if (_closed) return;
            _closed = true; // retain the first terminal cause through shutdown/unload
            _armed = false;
            // Own the reader lifecycle, rather than injecting a managed release.
            // Native CloseReadDevices clears pending injections; its no-force latch stays.
            if (_native != null) try { _closeInputs?.Invoke(null, null); } catch (Exception e) { reason += "; reader cleanup failed: " + e.Message; }
            try { _trace?.Dispose(); } catch (Exception e) { _log?.Invoke("Raw controls trace close failed: " + e.Message); }
            finally { _trace = null; }
            try
            {
                if (_directory != null && Directory.Exists(_directory)) File.WriteAllText(Path.Combine(_directory, "result.txt"), reason + "\nrows=" + _rows + "\ncommands=" + _commands + "\ninputVerdict=unclassified\n");
            }
            catch (Exception e) { _log?.Invoke("Raw controls result write failed: " + e.Message); }
            // Leave raw/observer hooks inert. Unpatching a method from inside
            // its own callback is unnecessary; output mutes stay process-wide.
        }
    }
}
