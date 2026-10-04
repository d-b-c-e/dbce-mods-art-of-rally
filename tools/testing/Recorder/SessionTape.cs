using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArtOfSimRally.Testing
{
    /// <summary>
    /// Whole-session recording and replay, from game launch: menus, stage
    /// choice and the drive. Developer probe only; armed from outside the game
    /// by a request file that the probe reads (and consumes) when it loads.
    /// </summary>
    /// <remarks>
    /// Three layers are taped. Menus and game buttons come from Rewired action
    /// getters and the game's global <c>Input</c> wrapper (intro "press any
    /// key"), keyed per rendered frame. The car is taped after the shipping
    /// mod's wheel override, as the final <c>AxisCarController.GetInput</c>
    /// values plus gear and pose per physics step. Replay overrides the same
    /// points, so the physical wheel, pedals and keyboard are ignored.
    ///
    /// Frames carry a state marker (scene | top menu panel | event status).
    /// Replay plays one marker segment at a time and waits for the live game to
    /// reach the next segment's marker, so load-time differences don't shift
    /// the inputs. A segment that never arrives fails the replay with a
    /// screenshot. All force output is muted during replay.
    /// </remarks>
    internal static class SessionTape
    {
        private enum Mode { Off, Record, Replay }

        internal static string RequestPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArtOfSimRally", "session-request.txt");

        private static Mode _mode;
        private static string _dir, _out;
        private static Action<string> _log;
        private static Harmony _harmony;

        // Frame layer.
        private static readonly Dictionary<string, string> _frameValues = new Dictionary<string, string>();
        private static StreamWriter _inputWriter, _carWriter, _eventWriter;
        private static int _frame, _fixedStep;
        private static readonly Dictionary<string, int> _markerIds = new Dictionary<string, int>();
        private static int _lastMarker = -1, _lastCarMarker = -1;

        // Replay state.
        private static Aligner<Dictionary<string, string>> _frames;
        private static Aligner<CarRow> _car;
        private static readonly Dictionary<string, string> Empty = new Dictionary<string, string>();
        private static Dictionary<string, string> _playing = Empty;
        private static CarRow _carPlaying;
        private static bool _carActive;
        private static float _poseThreshold = 2f;
        private static int _poseCorrections, _gearSets;
        private static float _maxPoseError;
        private static string _result;

        // Marker inputs, cached so a frame does not search the scene.
        private static PanelManager _panels;
        private static int _panelsCheckedFrame = -1000;
        private static readonly FieldInfo PanelStackField = typeof(PanelManager).GetField("panelStack", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo EventManagerField = typeof(GameEntryPoint).GetField("eventManager", BindingFlags.Static | BindingFlags.NonPublic);

        internal struct CarRow
        {
            public int Marker;
            public float Steer, Throttle, Brake, Handbrake, Clutch;
            public bool StartEngine;
            public int Gear;
            public Vector3 Position, Velocity, AngularVelocity;
            public Quaternion Rotation;
        }

        internal static bool Active => _mode != Mode.Off;
        internal static bool Replaying => _mode == Mode.Replay;
        internal static string Describe => _mode == Mode.Off ? "session tape off" :
            _mode + " frame=" + _frame + " step=" + _fixedStep + (_mode == Mode.Replay
                ? " segment=" + _frames.Segment + "/" + _frames.SegmentCount + " carSegment=" + _car.Segment + "/" + _car.SegmentCount +
                  " poseCorrections=" + _poseCorrections + " maxPoseError=" + _maxPoseError.ToString("0.00", CultureInfo.InvariantCulture) +
                  (_result != null ? " result=" + _result : "")
                : "");

        /// <summary>Reads and consumes a request written before launch.</summary>
        internal static void TryArm(Action<string> log)
        {
            _log = log;
            try
            {
                if (!File.Exists(RequestPath)) return;
                var request = File.ReadAllLines(RequestPath)
                    .Select(l => l.Split(new[] { '=' }, 2)).Where(p => p.Length == 2)
                    .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
                File.Move(RequestPath, RequestPath + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".taken");
                request.TryGetValue("mode", out var mode);
                request.TryGetValue("tape", out _dir);
                if (request.TryGetValue("poseThreshold", out var threshold))
                    float.TryParse(threshold, NumberStyles.Float, CultureInfo.InvariantCulture, out _poseThreshold);
                if (string.IsNullOrEmpty(_dir)) throw new InvalidDataException("tape= is required");
                if (string.Equals(mode, "record", StringComparison.OrdinalIgnoreCase)) StartRecording();
                else if (string.Equals(mode, "replay", StringComparison.OrdinalIgnoreCase))
                {
                    request.TryGetValue("out", out _out);
                    StartReplay(string.IsNullOrEmpty(_out) ? Path.Combine(_dir, "replay-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")) : _out);
                }
                else throw new InvalidDataException("mode must be record or replay");
                Patch();
            }
            catch (Exception ex)
            {
                _mode = Mode.Off;
                _log?.Invoke("Session tape not armed: " + ex.Message);
            }
        }

        private static void StartRecording()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "shots"));
            _inputWriter = new StreamWriter(Path.Combine(_dir, "input.tape"), false, new UTF8Encoding(false));
            _carWriter = new StreamWriter(Path.Combine(_dir, "car.tape"), false, new UTF8Encoding(false));
            _eventWriter = new StreamWriter(Path.Combine(_dir, "events.log"), false, new UTF8Encoding(false));
            _carWriter.WriteLine("step\tmarker\tsteer\tthrottle\tbrake\thandbrake\tclutch\tstart\tgear\tpx\tpy\tpz\tqx\tqy\tqz\tqw\tvx\tvy\tvz\tavx\tavy\tavz");
            File.WriteAllText(Path.Combine(_dir, "session.txt"),
                "game=" + Application.version + "\nunity=" + Application.unityVersion + "\nstartedUtc=" + DateTime.UtcNow.ToString("o") + "\n");
            _mode = Mode.Record;
            Event("record started");
        }

        private static void StartReplay(string output)
        {
            _out = output;
            Directory.CreateDirectory(Path.Combine(_out, "shots"));
            var markers = File.ReadAllLines(Path.Combine(_dir, "markers.tsv")).Select(l => l.Split('\t'))
                .ToDictionary(p => int.Parse(p[0], CultureInfo.InvariantCulture), p => p[1]);
            foreach (var pair in markers) _markerIds[pair.Value] = pair.Key;

            var frames = new List<KeyValuePair<int, Dictionary<string, string>>>();
            foreach (var line in File.ReadLines(Path.Combine(_dir, "input.tape")))
            {
                var parts = line.Split('\t');
                var values = new Dictionary<string, string>();
                for (int i = 2; i < parts.Length; i++)
                {
                    int eq = parts[i].LastIndexOf('=');
                    if (eq > 0) values[parts[i].Substring(0, eq)] = parts[i].Substring(eq + 1);
                }
                frames.Add(new KeyValuePair<int, Dictionary<string, string>>(int.Parse(parts[1], CultureInfo.InvariantCulture), values));
            }
            var car = new List<KeyValuePair<int, CarRow>>();
            foreach (var line in File.ReadLines(Path.Combine(_dir, "car.tape")).Skip(1))
            {
                var p = line.Split('\t');
                float F(int i) => float.Parse(p[i], CultureInfo.InvariantCulture);
                var row = new CarRow
                {
                    Marker = int.Parse(p[1], CultureInfo.InvariantCulture), Steer = F(2), Throttle = F(3), Brake = F(4), Handbrake = F(5), Clutch = F(6),
                    StartEngine = p[7] == "1", Gear = int.Parse(p[8], CultureInfo.InvariantCulture),
                    Position = new Vector3(F(9), F(10), F(11)), Rotation = new Quaternion(F(12), F(13), F(14), F(15)),
                    Velocity = new Vector3(F(16), F(17), F(18)), AngularVelocity = new Vector3(F(19), F(20), F(21))
                };
                car.Add(new KeyValuePair<int, CarRow>(row.Marker, row));
            }
            _frames = new Aligner<Dictionary<string, string>>(frames, markers, "frames");
            _car = new Aligner<CarRow>(car, markers, "car");
            _eventWriter = new StreamWriter(Path.Combine(_out, "replay.log"), false, new UTF8Encoding(false));
            _mode = Mode.Replay;
            Event("replay started: " + frames.Count + " frames, " + car.Count + " car steps, " + _frames.SegmentCount + " segments; force output muted");
        }

        // ---- patches -------------------------------------------------------

        private static void Patch()
        {
            _harmony = new Harmony("ArtOfSimRally.DevRecorder.SessionTape");
            var player = typeof(Rewired.Player);
            foreach (var name in new[] { "GetButton", "GetButtonDown", "GetButtonUp", "GetNegativeButton", "GetNegativeButtonDown", "GetNegativeButtonUp" })
            {
                PatchBool(player.GetMethod(name, new[] { typeof(int) }), "RewiredBoolInt");
                PatchBool(player.GetMethod(name, new[] { typeof(string) }), "RewiredBoolString");
            }
            foreach (var name in new[] { "GetAxis", "GetAxisRaw" })
            {
                PatchFloat(player.GetMethod(name, new[] { typeof(int) }), "RewiredFloatInt");
                PatchFloat(player.GetMethod(name, new[] { typeof(string) }), "RewiredFloatString");
            }
            PatchBool(player.GetMethod("GetAnyButton", Type.EmptyTypes), "RewiredBoolNone");
            PatchBool(player.GetMethod("GetAnyButtonDown", Type.EmptyTypes), "RewiredBoolNone");

            // The game's global Input wrapper (not UnityEngine.Input).
            var input = typeof(global::Input);
            PatchBool(input.GetProperty("anyKey").GetGetMethod(), "InputBoolNone");
            PatchBool(input.GetProperty("anyKeyDown").GetGetMethod(), "InputBoolNone");
            foreach (var name in new[] { "GetKey", "GetKeyDown", "GetKeyUp" })
            {
                PatchBool(input.GetMethod(name, new[] { typeof(KeyCode) }), "InputBoolKey");
                PatchBool(input.GetMethod(name, new[] { typeof(string) }), "InputBoolString");
            }
            foreach (var name in new[] { "GetMouseButton", "GetMouseButtonDown", "GetMouseButtonUp" })
            {
                var method = input.GetMethod(name, new[] { typeof(int) });
                if (method != null) PatchBool(method, "InputBoolInt");
            }

            var getInput = AccessTools.Method(typeof(AxisCarController), "GetInput");
            _harmony.Patch(getInput, postfix: new HarmonyMethod(typeof(SessionTape), nameof(AfterCarInput)) { priority = Priority.Last });
            _harmony.Patch(AccessTools.Method(typeof(CarDynamics), "FixedUpdate"),
                postfix: new HarmonyMethod(typeof(SessionTape), nameof(AfterCarStep)) { priority = Priority.Last });

            // SplashScreenControl is the game's only Rewired input-event delegate
            // (ButtonJustReleased); getter substitution can't reach it.
            _harmony.Patch(AccessTools.Method(typeof(SplashScreenControl), "EndSplashScreen"),
                prefix: new HarmonyMethod(typeof(SessionTape), nameof(BeforeSplashEnd)));

            if (_mode == Mode.Replay)
            {
                var native = AccessTools.TypeByName("Dbce.Wheel.Ffb.WheelFfbNative");
                Mute(native, "SetForce", nameof(MuteInt0));
                Mute(native, "UpdatePeriodic", nameof(MuteFloat1));
                Mute(native, "PlayConstantBurst", nameof(MuteFloat1));
                Mute(native, "PlayPeriodicBurst", nameof(MuteFloat1));
                Mute(native, "PlayShapedPeriodicBurst", nameof(MuteFloat1));
                Mute(native, "UpdateCondition", nameof(MuteFloat1));
            }
        }

        private static bool _splashEnded;
        private static void BeforeSplashEnd()
        {
            if (_mode == Mode.Record) _frameValues["E.SplashEnd"] = "1";
            _splashEnded = true;
        }

        private static void EndSplashIfTaped(string liveMarker)
        {
            if (_splashEnded || !liveMarker.Contains("|IntroSplashScreen|")) return;
            if (!(_playing.ContainsKey("E.SplashEnd") || _frames.PlayedOut)) return;
            var splash = UnityEngine.Object.FindObjectOfType<SplashScreenControl>();
            if (splash == null) return;
            Event("ending splash screen as taped");
            splash.EndSplashScreen();
        }

        private static void Mute(Type type, string name, string prefix)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == name))
                _harmony.Patch(method, prefix: new HarmonyMethod(typeof(SessionTape), prefix));
        }
        private static void MuteInt0(ref int __0) { if (Replaying) __0 = 0; }
        private static void MuteFloat1(ref float __1) { if (Replaying) __1 = 0f; }

        private static void PatchBool(MethodInfo method, string keyKind)
        {
            if (method == null) throw new MissingMethodException("input getter for " + keyKind);
            _harmony.Patch(method, postfix: new HarmonyMethod(typeof(SessionTape), "Bool" + keyKind));
        }
        private static void PatchFloat(MethodInfo method, string keyKind)
        {
            if (method == null) throw new MissingMethodException("input getter for " + keyKind);
            _harmony.Patch(method, postfix: new HarmonyMethod(typeof(SessionTape), "Float" + keyKind));
        }

        // Keys: R<player>.<method>.<action>, I.<member>[.<arg>].
        private static void BoolRewiredBoolInt(Rewired.Player __instance, MethodBase __originalMethod, int __0, ref bool __result)
            => Bool("R" + __instance.id + "." + __originalMethod.Name + "." + __0.ToString(CultureInfo.InvariantCulture), ref __result);
        private static void BoolRewiredBoolString(Rewired.Player __instance, MethodBase __originalMethod, string __0, ref bool __result)
            => Bool("R" + __instance.id + "." + __originalMethod.Name + ".s" + __0, ref __result);
        private static void BoolRewiredBoolNone(Rewired.Player __instance, MethodBase __originalMethod, ref bool __result)
            => Bool("R" + __instance.id + "." + __originalMethod.Name, ref __result);
        private static void FloatRewiredFloatInt(Rewired.Player __instance, MethodBase __originalMethod, int __0, ref float __result)
            => Float("R" + __instance.id + "." + __originalMethod.Name + "." + __0.ToString(CultureInfo.InvariantCulture), ref __result);
        private static void FloatRewiredFloatString(Rewired.Player __instance, MethodBase __originalMethod, string __0, ref float __result)
            => Float("R" + __instance.id + "." + __originalMethod.Name + ".s" + __0, ref __result);
        private static void BoolInputBoolNone(MethodBase __originalMethod, ref bool __result)
            => Bool("I." + __originalMethod.Name, ref __result);
        private static void BoolInputBoolKey(MethodBase __originalMethod, KeyCode __0, ref bool __result)
            => Bool("I." + __originalMethod.Name + "." + __0, ref __result);
        private static void BoolInputBoolString(MethodBase __originalMethod, string __0, ref bool __result)
            => Bool("I." + __originalMethod.Name + ".s" + __0, ref __result);
        private static void BoolInputBoolInt(MethodBase __originalMethod, int __0, ref bool __result)
            => Bool("I." + __originalMethod.Name + "." + __0.ToString(CultureInfo.InvariantCulture), ref __result);

        private static void Bool(string key, ref bool result)
        {
            if (_mode == Mode.Record) { if (result) _frameValues[key] = "1"; }
            else if (_mode == Mode.Replay) result = _playing.TryGetValue(key, out var v) && v == "1";
        }
        private static void Float(string key, ref float result)
        {
            if (_mode == Mode.Record) { if (result != 0f) _frameValues[key] = result.ToString("R", CultureInfo.InvariantCulture); }
            else if (_mode == Mode.Replay)
                result = _playing.TryGetValue(key, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : 0f;
        }

        // Last postfix, after the shipping mod's wheel override.
        private static void AfterCarInput(ref float throttleInput, ref float brakeInput, ref float steerInput,
            ref float handbrakeInput, ref float clutchInput, ref bool startEngineInput)
        {
            if (_mode == Mode.Record)
            {
                _carPlaying = new CarRow { Steer = steerInput, Throttle = throttleInput, Brake = brakeInput,
                    Handbrake = handbrakeInput, Clutch = clutchInput, StartEngine = startEngineInput };
            }
            else if (_mode == Mode.Replay)
            {
                if (!_carActive) { throttleInput = brakeInput = steerInput = handbrakeInput = clutchInput = 0; startEngineInput = false; return; }
                steerInput = _carPlaying.Steer; throttleInput = _carPlaying.Throttle; brakeInput = _carPlaying.Brake;
                handbrakeInput = _carPlaying.Handbrake; clutchInput = _carPlaying.Clutch; startEngineInput = _carPlaying.StartEngine;
            }
        }

        private static void AfterCarStep(CarDynamics __instance)
        {
            try
            {
                if (_mode == Mode.Off || !IsPlayerCar(__instance)) return;
                var body = __instance.GetComponent<Rigidbody>();
                var drivetrain = __instance.GetComponent<Drivetrain>();
                if (body == null || drivetrain == null) return;
                int marker = MarkerId(Marker());
                _fixedStep++;
                if (_mode == Mode.Record)
                {
                    var r = _carPlaying;
                    var p = body.position; var q = body.rotation; var v = body.velocity; var w = body.angularVelocity;
                    _carWriter.WriteLine(string.Join("\t", new[] { _fixedStep.ToString(CultureInfo.InvariantCulture), marker.ToString(CultureInfo.InvariantCulture),
                        N(r.Steer), N(r.Throttle), N(r.Brake), N(r.Handbrake), N(r.Clutch), r.StartEngine ? "1" : "0",
                        drivetrain.gear.ToString(CultureInfo.InvariantCulture), N(p.x), N(p.y), N(p.z), N(q.x), N(q.y), N(q.z), N(q.w),
                        N(v.x), N(v.y), N(v.z), N(w.x), N(w.y), N(w.z) }));
                    if (marker != _lastCarMarker) { _lastCarMarker = marker; Event("car segment " + MarkerText(marker) + " car=" + __instance.gameObject.name); }
                    return;
                }
                _carActive = _car.Next(marker, out _carPlaying);
                if (!_carActive) return;
                if (drivetrain.gear != _carPlaying.Gear) { drivetrain.Shift(_carPlaying.Gear, true); _gearSets++; }
                float error = Vector3.Distance(body.position, _carPlaying.Position);
                if (error > _maxPoseError) _maxPoseError = error;
                if (_poseThreshold > 0 && error > _poseThreshold)
                {
                    body.position = _carPlaying.Position; body.rotation = _carPlaying.Rotation;
                    body.velocity = _carPlaying.Velocity; body.angularVelocity = _carPlaying.AngularVelocity;
                    _poseCorrections++;
                }
            }
            catch (Exception ex) { Fail("car step: " + ex.Message); }
        }

        private static bool IsPlayerCar(CarDynamics car)
        {
            var manager = EventManagerField?.GetValue(null) as EventManager;
            var player = manager?.playerManager;
            return player != null && player.carDynamics == car;
        }

        /// <summary>Once per rendered frame, from the probe's OnUpdate.</summary>
        internal static void Tick()
        {
            if (_mode == Mode.Off) return;
            try
            {
                int marker = MarkerId(Marker());
                if (_mode == Mode.Record)
                {
                    // UMM's window opens at startup and blocks stock menus while open;
                    // it's often closed with the mouse, which isn't taped. Tape its state.
                    if (UnityModManagerNet.UnityModManager.UI.Instance?.Opened == true) _frameValues["U.Open"] = "1";
                    var line = new StringBuilder();
                    line.Append(_frame.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(marker.ToString(CultureInfo.InvariantCulture));
                    foreach (var pair in _frameValues) line.Append('\t').Append(pair.Key).Append('=').Append(pair.Value);
                    _inputWriter.WriteLine(line.ToString());
                    _frameValues.Clear();
                    if (marker != _lastMarker) { _lastMarker = marker; Reseed(marker); Event("segment " + MarkerText(marker)); Shot(_dir, marker); }
                    if (_frame % 60 == 0) { _inputWriter.Flush(); _carWriter.Flush(); _eventWriter.Flush(); }
                }
                else
                {
                    int before = _frames.Segment;
                    if (!_frames.Next(marker, out var playing)) playing = Empty;
                    _playing = playing;
                    var umm = UnityModManagerNet.UnityModManager.UI.Instance;
                    bool wantOpen = _playing.ContainsKey("U.Open");
                    if (umm != null && umm.Opened != wantOpen) { umm.ToggleWindow(wantOpen); Event((wantOpen ? "opened" : "closed") + " the mod manager window as taped"); }
                    EndSplashIfTaped(MarkerText(marker));
                    if (marker != _lastMarker) { _lastMarker = marker; Reseed(marker); }
                    if (_frames.Segment != before) { Event("segment " + _frames.Segment + " " + MarkerText(marker)); Shot(_out, marker); }
                    if (_frames.Failed != null) Fail(_frames.Failed);
                    else if (_car.Failed != null) Fail(_car.Failed);
                    else if (_frames.Finished && _result == null) Finish("passed");
                    if (_frame % 60 == 0) _eventWriter.Flush();
                }
                _frame++;
            }
            catch (Exception ex) { Fail("tick: " + ex.Message); }
        }

        internal static string Command(string command)
        {
            if (command == "SESSION-STATUS") return "OK " + Describe;
            if (command == "SESSION-STOP") { Stop("stopped by command"); return "OK " + Describe; }
            return null;
        }

        internal static void Stop(string why)
        {
            if (_mode == Mode.Record)
            {
                Event("record stopped: " + why);
                File.WriteAllLines(Path.Combine(_dir, "markers.tsv"),
                    _markerIds.OrderBy(p => p.Value).Select(p => p.Value.ToString(CultureInfo.InvariantCulture) + "\t" + p.Key));
                _inputWriter?.Dispose(); _carWriter?.Dispose(); _eventWriter?.Dispose();
                _inputWriter = _carWriter = _eventWriter = null;
                _mode = Mode.Off;
            }
            else if (_mode == Mode.Replay && _result == null)
                Finish(_frames.Segment == _frames.SegmentCount - 1 ? "passed (" + why + " in the final recorded segment)" : "stopped: " + why);
        }

        private static void Finish(string result)
        {
            _result = result;
            Event("replay " + result + "; poseCorrections=" + _poseCorrections + " maxPoseError=" + _maxPoseError.ToString("0.00", CultureInfo.InvariantCulture) +
                  " gearSets=" + _gearSets + " frames=" + _frame + " steps=" + _fixedStep);
            Shot(_out, -1);
            File.WriteAllText(Path.Combine(_out, "result.txt"), result + "\n" + Describe + "\n");
            _eventWriter?.Flush();
            _mode = Mode.Off;
            _eventWriter?.Dispose(); _eventWriter = null;
        }

        private static void Fail(string why) { if (_mode == Mode.Replay && _result == null) Finish("failed: " + why); }

        // ---- markers ---------------------------------------------------------

        private static string Marker()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (_panels == null && Time.frameCount - _panelsCheckedFrame > 30)
            { _panels = UnityEngine.Object.FindObjectOfType<PanelManager>(); _panelsCheckedFrame = Time.frameCount; }
            string panel = "-";
            if (_panels != null && PanelStackField?.GetValue(_panels) is Stack<global::Panel> stack && stack.Count > 0 && stack.Peek() != null)
                panel = stack.Peek().name;
            var manager = EventManagerField?.GetValue(null) as EventManager;
            string status = manager != null ? manager.status.ToString() : "-";
            return scene + "|" + panel + "|" + status;
        }

        private static int MarkerId(string marker)
        {
            if (_markerIds.TryGetValue(marker, out int id)) return id;
            id = _markerIds.Count;
            while (_markerIds.ContainsValue(id)) id++;
            _markerIds[marker] = id;
            if (_mode == Mode.Record)
                File.AppendAllText(Path.Combine(_dir, "markers.tsv"), id.ToString(CultureInfo.InvariantCulture) + "\t" + marker + "\n");
            return id;
        }
        private static string MarkerText(int id) => _markerIds.FirstOrDefault(p => p.Value == id).Key ?? id.ToString(CultureInfo.InvariantCulture);

        // The game randomizes custom-event stage, weather and car (StageGenerator,
        // Random.Range). Re-seed Unity's generator from the screen name whenever
        // the screen changes, in recording and replay alike, so the same presses
        // roll the same stage.
        private static void Reseed(int marker)
        {
            int seed = 17;
            foreach (char c in MarkerText(marker)) seed = unchecked(seed * 31 + c);
            UnityEngine.Random.InitState(seed);
        }

        private static void Shot(string dir, int marker)
        {
            try
            {
                string name = marker < 0 ? "final" : MarkerText(marker);
                foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, "shots", _frame.ToString("000000", CultureInfo.InvariantCulture) + "-" + name + ".png"));
            }
            catch (Exception ex) { Event("screenshot failed: " + ex.Message); }
        }

        private static void Event(string text)
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + " f" + _frame + " s" + _fixedStep + " " + text;
            _eventWriter?.WriteLine(line);
            _log?.Invoke("Session tape: " + text);
        }

        private static string N(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>Plays a tape segment by segment, gated on the live marker.</summary>
        private sealed class Aligner<T>
        {
            private readonly List<List<T>> _segments = new List<List<T>>();
            private readonly List<int> _markers = new List<int>();
            private readonly Dictionary<int, string> _names;
            private readonly string _label;
            private int _segment = -1, _index, _waiting, _early;
            private const int EarlyLimit = 10;
            private const int WaitLimit = 60 * 120;   // two minutes at 60 fps

            internal Aligner(List<KeyValuePair<int, T>> rows, Dictionary<int, string> names, string label)
            {
                _names = names; _label = label;
                foreach (var row in rows)
                {
                    if (_markers.Count == 0 || _markers[_markers.Count - 1] != row.Key) { _markers.Add(row.Key); _segments.Add(new List<T>()); }
                    _segments[_segments.Count - 1].Add(row.Value);
                }
            }
            internal int Segment => _segment;
            internal int SegmentCount => _segments.Count;
            internal bool PlayedOut => _segment >= 0 && _index >= _segments[_segment].Count;
            internal bool Finished => _segment == _segments.Count - 1 && _index >= _segments[_segment].Count;
            internal string Failed { get; private set; }

            internal bool Next(int liveMarker, out T value)
            {
                value = default;
                if (_segments.Count == 0 || Failed != null) return false;
                // The input that causes a transition is often taped in the first
                // frames of the next segment (the marker changes the same frame).
                // Once a segment has played out, play up to EarlyLimit frames of
                // the next one before the live game has arrived there.
                if (_segment >= 0 && _index < _segments[_segment].Count)
                {
                    if (_markers[_segment] == liveMarker) { _early = 0; _waiting = 0; value = _segments[_segment][_index++]; return true; }
                    if (_early > 0 && _early < EarlyLimit && _segment > 0 && _markers[_segment - 1] == liveMarker)
                    { _early++; value = _segments[_segment][_index++]; return true; }
                }
                if (_segment + 1 < _segments.Count && _markers[_segment + 1] == liveMarker)
                { _segment++; _index = 0; _early = 0; _waiting = 0; value = _segments[_segment][_index++]; return true; }
                if (_segment >= 0 && _index >= _segments[_segment].Count && _markers[_segment] == liveMarker && _segment + 1 < _segments.Count)
                { _segment++; _index = 0; _early = 1; value = _segments[_segment][_index++]; return true; }
                if (Finished) return false;
                if (++_waiting > WaitLimit)
                {
                    int next = _early > 0 ? _segment : _segment + 1;
                    string expected = next < _segments.Count && _names.TryGetValue(_markers[next], out var n) ? n : "?";
                    Failed = _label + " waited 2 min for '" + expected + "'";
                }
                return false;
            }
        }
    }
}
