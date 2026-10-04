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
using Dbce.Wheel.Playback;

namespace ArtOfSimRally.Testing
{
    /// <summary>
    /// Whole-session recording and replay, from game launch: menus, stage
    /// choice and the drive. Developer probe only; armed from outside the game
    /// by a request file that the probe reads (and consumes) when it loads.
    /// See docs/SESSION-REPLAY.md.
    /// </summary>
    /// <remarks>
    /// Menus and game buttons are taped per rendered frame from Rewired action
    /// getters and the game's global <c>Input</c> wrapper. The car is taped per
    /// physics tick: the <c>CarController</c> input fields at the start of
    /// <c>FixedUpdate</c>, plus every <c>Drivetrain.Shift</c> call with the tick
    /// the drivetrain first acts on it. Replay feeds the same values on the same
    /// ticks in input-diagnostic mode. Default trajectory playback owns the
    /// kinematic player body and applies recorded poses; it is not physics resimulation.
    ///
    /// Frames carry a state marker (scene | top menu panel | event status);
    /// replay plays one marker segment at a time and waits for the live game to
    /// reach the next one. Force output and telemetry stay muted from arming
    /// until the process exits, whatever the replay result.
    /// </remarks>
    internal static class SessionTape
    {
        private enum Mode { Off, Record, Replay }

        internal const int Format = 3;
        private const string HarmonyId = "ArtOfSimRally.DevRecorder.SessionTape";
        private static readonly Harmony OutputHarmony = new Harmony(HarmonyId + ".Outputs");
        private static readonly VehiclePlayback Vehicle = new VehiclePlayback();
        private static bool _trajectory;
        private static bool _semanticMenus;
        private static readonly ScenePlayback Scenes = new ScenePlayback();
        private static bool _sceneSelections;
        private static int _clicksInFrame;
        private static CaptureDriver _captureDriver;

        internal static string RequestPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArtOfSimRally", "session-request.txt");

        private static Mode _mode;
        private static string _dir, _out;
        private static Action<string> _log;
        private static Harmony _harmony;
        // Set when a replay is armed and never cleared in this process.
        private static bool _muteOutputs;

        // Recording.
        private static readonly Dictionary<string, string> _frameValues = new Dictionary<string, string>();
        private static TextWriter _inputWriter, _carWriter, _shiftWriter;
        private static StreamWriter _eventWriter;
        private static string _incomplete;
        private static int _drivetrainTick = int.MinValue;

        private static int _frame, _fixedStep;
        private static readonly Dictionary<string, int> _markerIds = new Dictionary<string, int>();
        private static int _lastMarker = -1, _lastCarMarker = -1;
        private static int _lastObservedMarker = -1;

        // Replay.
        private static SessionAligner<Dictionary<string, string>> _frames;
        private static SessionAligner<CarRow> _car;
        private static readonly Dictionary<string, string> Empty = new Dictionary<string, string>();
        private static Dictionary<string, string> _playing = Empty;
        private static CarRow _carPlaying;
        private static bool _carActive, _legacy, _replayShifting, _scenarioChecked;
        private static int _liveTick = int.MinValue;
        private static int _finishedStageSegment = -1;
        private static Dictionary<int, List<KeyValuePair<int, bool>>> _shiftsByTick = new Dictionary<int, List<KeyValuePair<int, bool>>>();
        private static float _poseThreshold;
        private static int _shiftsReplayed, _shiftsBlocked, _gearMismatchSteps, _firstDivergenceRow = -1;
        private static float _maxPoseError, _lastPoseError;
        private static string _result, _expectedScenario;
        private static StreamWriter _divergenceWriter;

        private static PanelManager _panels;
        private static int _panelsCheckedFrame = -1000;
        private static readonly FieldInfo PanelStackField = typeof(PanelManager).GetField("panelStack", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo EventManagerField = typeof(GameEntryPoint).GetField("eventManager", BindingFlags.Static | BindingFlags.NonPublic);

        internal struct CarRow
        {
            public int Marker, Tick;
            public float Steer, Throttle, Brake, Handbrake, Clutch;
            public bool StartEngine;
            public int Gear;
            public Vector3 Position, Velocity, AngularVelocity;
            public Quaternion Rotation;
            public float ResolvedSteer, ResolvedThrottle, ResolvedBrake, Rpm;
        }

        internal static bool Replaying => _mode == Mode.Replay;
        internal static string Describe => _mode == Mode.Off && _result == null ? "session tape off" :
            (_result != null ? "Replay result=" + _result + " " : _mode + " ") + "frame=" + _frame + " step=" + _fixedStep +
            (_frames != null
                ? " segment=" + _frames.Segment + "/" + _frames.SegmentCount + " carSegment=" + _car.Segment + "/" + _car.SegmentCount +
                  " frameRows=" + _frames.Consumed + "/" + _frames.Total + " (skipped " + _frames.Skipped + ")" +
                  " carRows=" + _car.Consumed + "/" + _car.Total + " (skipped " + _car.Skipped + ")" +
                  " shifts=" + _shiftsReplayed + " blockedShifts=" + _shiftsBlocked + " gearMismatchSteps=" + _gearMismatchSteps +
                  " maxPoseError=" + F2(_maxPoseError) + " lastPoseError=" + F2(_lastPoseError) + " firstDivergenceRow=" + _firstDivergenceRow +
                  " mode=" + (_trajectory ? "trajectory" : "input-diagnostic") +
                  " posesApplied=" + Vehicle.Applied + " maxApplicationError=" + F2(Vehicle.MaxApplicationError) + (_legacy ? " legacyTape" : "")
                : "") + (_incomplete != null ? " INCOMPLETE: " + _incomplete : "");

        /// <summary>Reads and consumes a request written before launch.</summary>
        internal static void TryArm(Action<string> log)
        {
            _log = log;
            Dictionary<string, string> request;
            try
            {
                if (!File.Exists(RequestPath)) return;
                request = File.ReadAllLines(RequestPath)
                    .Select(l => l.Split(new[] { '=' }, 2)).Where(p => p.Length == 2)
                    .ToDictionary(p => p[0].Trim(), p => p[1].Trim(), StringComparer.OrdinalIgnoreCase);
                File.Move(RequestPath, RequestPath + "." + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".taken");
            }
            catch (Exception ex) { _log?.Invoke("Session tape request unreadable: " + ex.Message); return; }

            request.TryGetValue("mode", out var mode);
            bool replay = string.Equals(mode, "replay", StringComparison.OrdinalIgnoreCase);
            // Outputs stay muted for this whole launch once a replay was asked for,
            // even if arming below fails.
            if (replay || request.ContainsKey("captureDriver") || (request.TryGetValue("muteOutputs", out var mute) && mute == "1")) _muteOutputs = true;
            try
            {
                if (_muteOutputs) PatchOutputs();
                if (!request.TryGetValue("expiresUtc", out var expires) ||
                    !DateTime.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var until) ||
                    DateTime.UtcNow > until)
                    throw new InvalidDataException("request expired at " + expires);
                request.TryGetValue("tape", out _dir);
                if (string.IsNullOrEmpty(_dir)) throw new InvalidDataException("tape= is required");
                if (request.TryGetValue("poseThreshold", out var threshold))
                    float.TryParse(threshold, NumberStyles.Float, CultureInfo.InvariantCulture, out _poseThreshold);
                if (_poseThreshold != 0) throw new InvalidDataException("Assisted physics playback was replaced by trajectory playback.");
                _trajectory = !request.TryGetValue("playback", out var playback) || playback == "trajectory";
                if (!_trajectory && playback != "input-diagnostic") throw new InvalidDataException("unknown playback mode");
                _harmony = new Harmony(HarmonyId);
                if (request.TryGetValue("captureDriver", out var driverSource))
                {
                    if (replay) throw new InvalidDataException("capture driver is only valid for recording");
                    PatchOutputs();
                    _captureDriver = new CaptureDriver(driverSource, 12f);
                }
                if (replay) { PatchOutputs(); LoadReplay(request.TryGetValue("out", out var o) && o.Length > 0 ? o : Path.Combine(_dir, "replay-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"))); }
                else if (string.Equals(mode, "record", StringComparison.OrdinalIgnoreCase)) OpenRecording();
                else throw new InvalidDataException("mode must be record or replay");
                PatchInputs();
                _mode = replay ? Mode.Replay : Mode.Record;
                Event(replay ? "replay started: " + _frames.Total + " frames, " + _car.Total + " car rows, " + _frames.SegmentCount +
                               " segments, " + _shiftsByTick.Values.Sum(l => l.Count) + " shifts; force and telemetry muted; " +
                               (_trajectory ? "trajectory playback (recorded poses)" : "input diagnostic (physics divergence checked)")
                             : "record started (format " + Format + ")");
            }
            catch (Exception ex)
            {
                // Remove input hooks; keep the output mutes if they went in.
                try { _harmony?.UnpatchAll(HarmonyId); if (_muteOutputs) PatchOutputs(); } catch (Exception muteError) { _log?.Invoke("OUTPUT MUTE FAILURE: " + muteError); }
                _mode = Mode.Off;
                _inputWriter?.Dispose(); _carWriter?.Dispose(); _shiftWriter?.Dispose();
                if (replay && request.TryGetValue("out", out var failedOut))
                {
                    Directory.CreateDirectory(failedOut);
                    File.WriteAllText(Path.Combine(failedOut, "result.txt"), "failed: arm: " + ex.Message + "\n");
                }
                _log?.Invoke("Session tape not armed: " + ex.Message + (replay ? " (force and telemetry stay muted this launch)" : ""));
            }
        }

        private static StreamWriter Create(string path) =>
            new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false));

        private static void OpenRecording()
        {
            Directory.CreateDirectory(Path.Combine(_dir, "shots"));
            if (File.Exists(Path.Combine(_dir, "input.tape"))) throw new IOException("tape already exists: " + _dir);
            _inputWriter = new BufferedTapeWriter(Path.Combine(_dir, "input.tape"));
            _carWriter = new BufferedTapeWriter(Path.Combine(_dir, "car.tape"));
            _shiftWriter = new BufferedTapeWriter(Path.Combine(_dir, "shifts.tape"));
            _eventWriter = Create(Path.Combine(_dir, "events.log"));
            _carWriter.WriteLine("step\tmarker\tsteer\tthrottle\tbrake\thandbrake\tclutch\tstart\tgear\tpx\tpy\tpz\tqx\tqy\tqz\tqw\tvx\tvy\tvz\tavx\tavy\tavz\ttick\tresolvedSteer\tresolvedThrottle\tresolvedBrake\trpm");
            _shiftWriter.WriteLine("tick\tgear\tchangeTarget\tphase");
            File.WriteAllText(Path.Combine(_dir, "session.txt"),
                "format=" + Format + "\ngame=" + Application.version + "\nunity=" + Application.unityVersion +
                "\ngameAssemblySha256=" + ArtifactHash.FileHash(typeof(CarDynamics).Assembly.Location) +
                "\nfixedDeltaTime=" + N(Time.fixedDeltaTime) + "\nstartedUtc=" + DateTime.UtcNow.ToString("o") +
                "\nsceneSelections=1\ncaptureSource=" + (_captureDriver == null ? "player" : "synthetic-integration") + "\nphysicalOutput=" + (!_muteOutputs).ToString().ToLowerInvariant() + "\n");
            RecorderMain.StartSessionSignals(_dir, !_muteOutputs, _captureDriver != null);
        }

        private static void LoadReplay(string output)
        {
            var session = File.ReadAllLines(Path.Combine(_dir, "session.txt")).Select(l => l.Split(new[] { '=' }, 2)).Where(p => p.Length == 2)
                .ToDictionary(p => p[0], p => p[1]);
            if (!session.TryGetValue("format", out var format) || !int.TryParse(format, out var version) || version < 1 || version > Format)
                throw new InvalidDataException("unsupported session format");
            if (session.TryGetValue("gameAssemblySha256", out var gameHash) &&
                !string.Equals(gameHash, ArtifactHash.FileHash(typeof(CarDynamics).Assembly.Location), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("recorded game assembly differs from this installation");
            _legacy = version < 2;
            _semanticMenus = version >= 3;
            _sceneSelections = session.TryGetValue("sceneSelections", out var selections) && selections == "1";
            if (version >= 3)
            {
                var required = new List<string> { "session.txt", "markers.tsv", "input.tape", "car.tape", "shifts.tape", "end.txt" };
                if (_sceneSelections) required.AddRange(Directory.GetFiles(_dir, "load-*.json").Select(Path.GetFileName));
                ArtifactSeal.Verify(_dir, required.ToArray());
            }
            if (!session.TryGetValue("fixedDeltaTime", out var fixedDelta) ||
                Math.Abs(float.Parse(fixedDelta, CultureInfo.InvariantCulture) - Time.fixedDeltaTime) > 0.000001f)
                throw new InvalidDataException("physics timestep differs from recording");
            if (session.TryGetValue("game", out var game) && game != Application.version)
                throw new InvalidDataException("tape is from game " + game + ", running " + Application.version);
            if (File.Exists(Path.Combine(_dir, "incomplete.txt")))
                throw new InvalidDataException("tape is marked incomplete: " + File.ReadAllText(Path.Combine(_dir, "incomplete.txt")).Trim());
            session.TryGetValue("scenario", out _expectedScenario);

            var markers = File.ReadAllLines(Path.Combine(_dir, "markers.tsv")).Where(l => l.Length > 0).Select(l => l.Split('\t'))
                .ToDictionary(p => int.Parse(p[0], CultureInfo.InvariantCulture), p => p[1]);
            foreach (var pair in markers) _markerIds[pair.Value] = pair.Key;

            var frames = new List<KeyValuePair<int, Dictionary<string, string>>>();
            foreach (var line in File.ReadLines(Path.Combine(_dir, "input.tape")))
            {
                var parts = line.Split('\t');
                if (parts.Length < 2 || int.Parse(parts[0], CultureInfo.InvariantCulture) != frames.Count)
                    throw new InvalidDataException("input frame sequence is truncated or out of order");
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
                if (p.Length != (version >= 3 ? 27 : version == 2 ? 23 : 22) ||
                    int.Parse(p[0], CultureInfo.InvariantCulture) != car.Count + 1)
                    throw new InvalidDataException("car row has wrong shape or sequence");
                float F(int i)
                {
                    float v = float.Parse(p[i], CultureInfo.InvariantCulture);
                    if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("non-finite value in car.tape");
                    return v;
                }
                car.Add(new KeyValuePair<int, CarRow>(int.Parse(p[1], CultureInfo.InvariantCulture), new CarRow
                {
                    Marker = int.Parse(p[1], CultureInfo.InvariantCulture), Steer = F(2), Throttle = F(3), Brake = F(4), Handbrake = F(5), Clutch = F(6),
                    StartEngine = p[7] == "1", Gear = int.Parse(p[8], CultureInfo.InvariantCulture),
                    Position = new Vector3(F(9), F(10), F(11)), Rotation = new Quaternion(F(12), F(13), F(14), F(15)),
                    Velocity = new Vector3(F(16), F(17), F(18)), AngularVelocity = new Vector3(F(19), F(20), F(21)),
                    Tick = p.Length > 22 ? int.Parse(p[22], CultureInfo.InvariantCulture) : int.MinValue,
                    ResolvedSteer = p.Length > 23 ? F(23) : F(2),
                    ResolvedThrottle = p.Length > 24 ? F(24) : F(3),
                    ResolvedBrake = p.Length > 25 ? F(25) : F(4), Rpm = p.Length > 26 ? F(26) : -1
                }));
            }
            if (frames.Count == 0 || car.Count == 0 || markers.Count == 0) throw new InvalidDataException("tape has an empty stream");
            if (version >= 3)
            {
                var end = File.ReadAllLines(Path.Combine(_dir, "end.txt")).Select(l => l.Split(new[] { '=' }, 2)).ToDictionary(p => p[0], p => p[1]);
                if (int.Parse(end["frames"], CultureInfo.InvariantCulture) != frames.Count || int.Parse(end["steps"], CultureInfo.InvariantCulture) != car.Count)
                    throw new InvalidDataException("completion counts differ from streams");
            }
            var shifts = Path.Combine(_dir, "shifts.tape");
            if (File.Exists(shifts))
                foreach (var line in File.ReadLines(shifts).Skip(1))
                {
                    var p = line.Split('\t');
                    int tick = int.Parse(p[0], CultureInfo.InvariantCulture);
                    if (!_shiftsByTick.TryGetValue(tick, out var list)) _shiftsByTick[tick] = list = new List<KeyValuePair<int, bool>>();
                    list.Add(new KeyValuePair<int, bool>(int.Parse(p[1], CultureInfo.InvariantCulture), p[2] == "1"));
                }
            _frames = new SessionAligner<Dictionary<string, string>>(frames, markers, "frames", () => Time.realtimeSinceStartup);
            _car = new SessionAligner<CarRow>(car, markers, "car", () => Time.realtimeSinceStartup, earlyLimit: 0,
                required: m => m.EndsWith("|UNDERWAY"));
            _out = output;
            Directory.CreateDirectory(Path.Combine(_out, "shots"));
            _eventWriter = Create(Path.Combine(_out, "replay.log"));
            _divergenceWriter = Create(Path.Combine(_out, "divergence.tsv"));
            _divergenceWriter.WriteLine("row\tmarker\tpose_error_m\tlive_gear\ttape_gear");
        }

        // ---- patches -------------------------------------------------------

        private static bool _outputsPatched;
        private static void PatchOutputs()
        {
            if (_outputsPatched) return;
            var native = AccessTools.TypeByName("Dbce.Wheel.Ffb.WheelFfbNative") ?? throw new MissingMemberException("WheelFfbNative");
            Mute(native, "SetForce", nameof(MuteInt0));
            Mute(native, "UpdatePeriodic", nameof(MuteFloat1));
            Mute(native, "PlayConstantBurst", nameof(MuteFloat1));
            Mute(native, "PlayPeriodicBurst", nameof(MuteFloat1));
            Mute(native, "PlayShapedPeriodicBurst", nameof(MuteFloat1));
            Mute(native, "UpdateCondition", nameof(MuteFloat1));
            // SimHub-driven shakers would react to replayed telemetry.
            var pump = AccessTools.TypeByName("ArtOfSimRally.Mod.TelemetryPump") ?? throw new MissingMemberException("TelemetryPump");
            OutputHarmony.Patch(AccessTools.Method(pump, "SendFrame"), prefix: new HarmonyMethod(typeof(SessionTape), nameof(SkipWhenMuted)));
            // Playback must not submit a simulated result, achievement or career save.
            foreach (var name in new[] { "UploadScore", "UnlockAchievement", "IncrementStat", "ForceStatUpload",
                "SaveLocalSaveGame", "SaveLocalCareer", "SaveLocalLeaderboards", "SaveLocalGhosts", "SaveCustomCollectables" })
                foreach (var method in typeof(PlatformSteam).GetMethods().Where(m => m.Name == name))
                    OutputHarmony.Patch(method, prefix: new HarmonyMethod(typeof(SessionTape), nameof(SkipWhenMuted)));
            OutputHarmony.Patch(AccessTools.Method(typeof(PlatformSteam), "GetOnlineLeaderboardsActive"),
                prefix: new HarmonyMethod(typeof(SessionTape), nameof(OfflineReplay)));
            _outputsPatched = true;
        }

        private static void PatchInputs()
        {
            var player = typeof(Rewired.Player);
            foreach (var name in new[] { "GetButton", "GetButtonDown", "GetButtonUp", "GetNegativeButton", "GetNegativeButtonDown", "GetNegativeButtonUp" })
            {
                Patch(player.GetMethod(name, new[] { typeof(int) }), postfix: nameof(BoolRewiredBoolInt));
                Patch(player.GetMethod(name, new[] { typeof(string) }), postfix: nameof(BoolRewiredBoolString));
            }
            foreach (var name in new[] { "GetAxis", "GetAxisRaw" })
            {
                Patch(player.GetMethod(name, new[] { typeof(int) }), postfix: nameof(FloatRewiredFloatInt));
                Patch(player.GetMethod(name, new[] { typeof(string) }), postfix: nameof(FloatRewiredFloatString));
            }
            Patch(player.GetMethod("GetAnyButton", Type.EmptyTypes), postfix: nameof(BoolRewiredBoolNone));
            Patch(player.GetMethod("GetAnyButtonDown", Type.EmptyTypes), postfix: nameof(BoolRewiredBoolNone));

            // The game's global Input wrapper (not UnityEngine.Input).
            var input = typeof(global::Input);
            Patch(input.GetProperty("anyKey").GetGetMethod(), postfix: nameof(BoolInputBoolNone));
            Patch(input.GetProperty("anyKeyDown").GetGetMethod(), postfix: nameof(BoolInputBoolNone));
            foreach (var name in new[] { "GetKey", "GetKeyDown", "GetKeyUp" })
            {
                Patch(input.GetMethod(name, new[] { typeof(KeyCode) }), postfix: nameof(BoolInputBoolKey));
                Patch(input.GetMethod(name, new[] { typeof(string) }), postfix: nameof(BoolInputBoolString));
            }
            foreach (var name in new[] { "GetMouseButton", "GetMouseButtonDown", "GetMouseButtonUp" })
            {
                var method = input.GetMethod(name, new[] { typeof(int) });
                if (method != null) Patch(method, postfix: nameof(BoolInputBoolInt));
            }

            Patch(AccessTools.Method(typeof(AxisCarController), "GetInput"), postfix: nameof(AfterCarInput), last: true);
            // CarController.Update reads input once per rendered frame; FixedUpdate
            // smooths the *Input fields each tick. Tape and replay those fields
            // at the start of every tick.
            Patch(AccessTools.Method(typeof(CarController), "FixedUpdate"), prefix: nameof(BeforeCarStep), last: true);
            // Shift() only queues a change; Drivetrain.FixedUpdate acts on it.
            Patch(AccessTools.Method(typeof(Drivetrain), "Shift"), prefix: nameof(BeforeShift));
            Patch(AccessTools.Method(typeof(Drivetrain), "FixedUpdate"), prefix: nameof(BeforeDrivetrainStep), postfix: nameof(AfterDrivetrainStep));
            // The game's only Rewired input-event consumer; getters can't reach it.
            Patch(AccessTools.Method(typeof(SplashScreenControl), "EndSplashScreen"), prefix: nameof(BeforeSplashEnd));
            Patch(AccessTools.Method(typeof(PlayerManager), "DisableKinematic"), prefix: nameof(BeforeDisableKinematic));
            Patch(AccessTools.Method(typeof(OutOfBoundsManager), "FixedUpdate"), prefix: nameof(BeforeOutOfBounds));
            Patch(AccessTools.Method(typeof(PlayerManager), "Update"), prefix: nameof(BeforePlayerUpdate));
            Patch(AccessTools.Method(typeof(CarDynamics), "FixedUpdate"), prefix: nameof(BeforeDynamicsStep), last: true);
            Patch(AccessTools.Method(typeof(Wheel), "FixedUpdate"), prefix: nameof(BeforeWheelStep), last: true);
            Patch(AccessTools.Method(typeof(UnityEngine.UI.Button), "Press"), prefix: nameof(BeforeButtonPress));
            Patch(AccessTools.Method(typeof(SceneLoader), "LoadLevel"), prefix: nameof(BeforeSceneLoad));
        }

        private static bool BeforeSceneLoad(ref int aLevelIndex)
        {
            try
            {
                if (_mode == Mode.Record || (_mode == Mode.Replay && _sceneSelections))
                    Scenes.BeforeLoad(_dir, _mode == Mode.Record, ref aLevelIndex);
                return true;
            }
            catch (Exception ex) { Fail("scene selection: " + ex.Message); return false; }
        }

        private static void Patch(MethodBase target, string prefix = null, string postfix = null, bool last = false)
        {
            if (target == null) throw new MissingMethodException("hook target for " + (prefix ?? postfix));
            _harmony.Patch(target,
                prefix: prefix == null ? null : new HarmonyMethod(typeof(SessionTape), prefix) { priority = last ? Priority.Last : Priority.Normal },
                postfix: postfix == null ? null : new HarmonyMethod(typeof(SessionTape), postfix) { priority = last ? Priority.Last : Priority.Normal });
        }

        private static void Mute(Type type, string name, string prefix)
        {
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == name).ToList();
            if (methods.Count == 0) throw new MissingMethodException(type.Name + "." + name);
            foreach (var method in methods) OutputHarmony.Patch(method, prefix: new HarmonyMethod(typeof(SessionTape), prefix));
        }
        private static void MuteInt0(ref int __0) { if (_muteOutputs) __0 = 0; }
        private static void MuteFloat1(ref float __1) { if (_muteOutputs) __1 = 0f; }
        private static bool SkipWhenMuted() => !_muteOutputs;
        private static bool OfflineReplay(ref bool __result) { if (!_muteOutputs) return true; __result = false; return false; }

        // ---- input layer -----------------------------------------------------
        private static bool BeforeButtonPress(UnityEngine.UI.Button __instance)
        {
            if (_mode == Mode.Record && __instance.IsActive() && __instance.IsInteractable())
                _frameValues["E.Click." + _clicksInFrame++] = MenuPlayback.Path(__instance.transform);
            return !(_mode == Mode.Replay && _semanticMenus);
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

        // The last value read in a frame is the one taped.
        private static void Bool(string key, ref bool result)
        {
            if (_mode == Mode.Record && _captureDriver != null) result = _captureDriver.Values.TryGetValue(key, out var scripted) && scripted == "1";
            if (_mode == Mode.Record) { if (result) _frameValues[key] = "1"; else _frameValues.Remove(key); }
            else if (_mode == Mode.Replay) result = _playing.TryGetValue(key, out var v) && v == "1";
        }
        private static void Float(string key, ref float result)
        {
            if (_mode == Mode.Record && _captureDriver != null) result = _captureDriver.Values.TryGetValue(key, out var scripted) ? float.Parse(scripted, CultureInfo.InvariantCulture) : 0;
            if (_mode == Mode.Record) { if (result != 0f) _frameValues[key] = N(result); else _frameValues.Remove(key); }
            else if (_mode == Mode.Replay)
                result = _playing.TryGetValue(key, out var v) ? float.Parse(v, CultureInfo.InvariantCulture) : 0f;
        }

        // StageIntroCinematic skips on any press, but only while it's running;
        // tape when it starts finishing and trigger the same finish on replay.
        private static readonly FieldInfo CinematicFinishing = typeof(StageIntroCinematic).GetField("isFinishingCinematic", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CinematicFinish = typeof(StageIntroCinematic).GetMethod("FinishCinematic", BindingFlags.Instance | BindingFlags.NonPublic);
        private static StageIntroCinematic _cinematic;
        private static bool _cinematicWasFinishing;

        private static StageIntroCinematic Cinematic(string liveMarker)
        {
            if (!liveMarker.EndsWith("|INTRO_CINEMATIC")) { _cinematic = null; _cinematicWasFinishing = false; return null; }
            if (_cinematic == null) _cinematic = UnityEngine.Object.FindObjectOfType<StageIntroCinematic>();
            return _cinematic;
        }

        private static void TapeCinematicSkip(string liveMarker)
        {
            var c = Cinematic(liveMarker);
            if (c == null || CinematicFinishing == null) return;
            bool finishing = (bool)CinematicFinishing.GetValue(c);
            if (finishing && !_cinematicWasFinishing) _frameValues["E.CinematicSkip"] = "1";
            _cinematicWasFinishing = finishing;
        }

        private static void ReplayCinematicSkip(string liveMarker)
        {
            var c = Cinematic(liveMarker);
            if (c == null || CinematicFinishing == null || CinematicFinish == null || (bool)CinematicFinishing.GetValue(c)) return;
            if (!(_playing.ContainsKey("E.CinematicSkip") || (_frames.PlayedOut && MarkerText(_frames.CurrentMarker).EndsWith("|INTRO_CINEMATIC")))) return;
            Event("skipping the intro cinematic as taped");
            CinematicFinishing.SetValue(c, true);
            c.StopAllCoroutines();
            LeanTween.cancelAll();
            c.StartCoroutine((System.Collections.IEnumerator)CinematicFinish.Invoke(c, null));
        }

        private static bool _splashEnded, _endingSplash;
        private static bool BeforeSplashEnd()
        {
            if ((_mode == Mode.Replay || _captureDriver != null) && !_endingSplash) return false;
            if (_mode == Mode.Record) _frameValues["E.SplashEnd"] = "1";
            _splashEnded = true;
            return true;
        }

        private static void EndSplashIfTaped(string liveMarker)
        {
            if (_splashEnded || !liveMarker.Contains("|IntroSplashScreen|")) return;
            if (!(_playing.ContainsKey("E.SplashEnd") || _frames?.PlayedOut == true)) return;
            var splash = UnityEngine.Object.FindObjectOfType<SplashScreenControl>();
            if (splash == null) return;
            Event("ending splash screen as taped");
            _endingSplash = true;
            try { splash.EndSplashScreen(); } finally { _endingSplash = false; }
        }

        // ---- car layer -------------------------------------------------------

        private static int LiveTick => Mathf.RoundToInt(Time.fixedTime / Time.fixedDeltaTime);
        private static bool BeforeDisableKinematic() => !(_mode == Mode.Replay && _trajectory && Vehicle.Active);
        private static bool BeforeDynamicsStep(CarDynamics __instance)
        {
            if (!(_mode == Mode.Replay && _trajectory && Vehicle.Active && IsPlayer(__instance))) return true;
            __instance.velo = _carPlaying.Velocity.magnitude;
            return false;
        }
        private static bool BeforeWheelStep(Wheel __instance)
        {
            if (!(_mode == Mode.Replay && _trajectory && Vehicle.Active)) return true;
            var car = __instance.GetComponentInParent<CarDynamics>();
            if (!IsPlayer(car)) return true;
            __instance.angularVelocity = _carPlaying.Velocity.magnitude / Mathf.Max(0.05f, __instance.radius);
            __instance.steering = _carPlaying.ResolvedSteer;
            return false;
        }
        private static void BeforePlayerUpdate(ref bool isReplay)
        { if (_mode == Mode.Replay && _trajectory && Vehicle.Active) isReplay = true; }
        private static readonly MethodInfo ClosestWaypoint = AccessTools.Method(typeof(OutOfBoundsManager), "FindClosestWaypointToCar");
        private static readonly FieldInfo CurrentWaypoint = AccessTools.Field(typeof(OutOfBoundsManager), "CurrentWaypointIndex");
        private static readonly MethodInfo Progress = AccessTools.Method(typeof(OutOfBoundsManager), "UpdateStageProgress");
        private static bool BeforeOutOfBounds(OutOfBoundsManager __instance)
        {
            if (!(_mode == Mode.Replay && _trajectory && Vehicle.Active)) return true;
            // Preserve presentation; reset animations/teleports are already represented by the tape.
            try { CurrentWaypoint.SetValue(__instance, ClosestWaypoint.Invoke(__instance, null)); Progress.Invoke(__instance, null); }
            catch (Exception ex) { Fail("playback progress: " + ex.Message); }
            return false;
        }

        // Replay only: keep the per-frame read consistent with the taped tick.
        private static void AfterCarInput(ref float throttleInput, ref float brakeInput, ref float steerInput,
            ref float handbrakeInput, ref float clutchInput, ref bool startEngineInput)
        {
            if (_mode == Mode.Record && _captureDriver != null)
            { _captureDriver.Input(ref throttleInput, ref brakeInput, ref steerInput, ref handbrakeInput, ref clutchInput, ref startEngineInput); return; }
            if (_mode != Mode.Replay) return;
            if (!_carActive) { throttleInput = brakeInput = steerInput = handbrakeInput = clutchInput = 0; startEngineInput = false; return; }
            steerInput = _carPlaying.Steer; throttleInput = _carPlaying.Throttle; brakeInput = _carPlaying.Brake;
            handbrakeInput = _carPlaying.Handbrake; clutchInput = _carPlaying.Clutch; startEngineInput = _carPlaying.StartEngine;
        }

        private static bool IsPlayer(Component component)
        {
            var manager = EventManagerField?.GetValue(null) as EventManager;
            var player = manager?.playerManager;
            return player != null && player.carDynamics != null && component != null && component.gameObject == player.carDynamics.gameObject;
        }

        // Replay: the first player-car callback of a tick takes that tick's row,
        // whichever of CarController/Drivetrain Unity runs first.
        private static void BeginReplayTick()
        {
            int tick = LiveTick;
            if (tick == _liveTick) return;
            _liveTick = tick;
            _fixedStep++;
            if (_trajectory && _car.PlayedOut && _finishedStageSegment != _car.Segment &&
                MarkerText(_car.CurrentMarker).EndsWith("|UNDERWAY") && MarkerText(_car.NextMarker).EndsWith("|FINISHING_STAGE_ANIMATION"))
            {
                _finishedStageSegment = _car.Segment;
                var manager = EventManagerField?.GetValue(null) as EventManager;
                if (manager != null && manager.status == EventStatusEnums.EventStatus.UNDERWAY)
                { Event("recorded finish boundary reached; invoking the game's finish gate"); manager.GiveControlToPlayer(true, true); }
            }
            int before = _car.Consumed;
            _carActive = _car.Next(MarkerId(Marker()), out _carPlaying);
            if (!_carActive || _car.Consumed == before) return;
            if (!_scenarioChecked) CheckScenario();
        }

        private static void BeforeCarStep(CarController __instance)
        {
            try
            {
                if (_mode == Mode.Off || !IsPlayer(__instance)) return;
                var body = __instance.GetComponent<Rigidbody>();
                var drivetrain = __instance.GetComponent<Drivetrain>();
                if (body == null || drivetrain == null) return;
                if (_mode == Mode.Record)
                {
                    int marker = MarkerId(Marker());
                    _fixedStep++;
                    if (!_startStateDone && MarkerText(marker).EndsWith("|UNDERWAY"))
                    {
                        _startStateDone = true;
                        try { StartState.Save(__instance.gameObject, Path.Combine(_dir, "start-state.tsv")); Event("start state saved at the first stage tick"); }
                        catch (Exception ex) { MarkIncomplete("start state: " + ex.Message); }
                    }
                    var c = __instance;
                    var p = body.position; var q = body.rotation; var v = body.velocity; var w = body.angularVelocity;
                    _carWriter.WriteLine(string.Join("\t", new[] { _fixedStep.ToString(CultureInfo.InvariantCulture), marker.ToString(CultureInfo.InvariantCulture),
                        N(c.steerInput), N(c.throttleInput), N(c.brakeInput), N(c.handbrakeInput), N(c.clutchInput), c.startEngineInput ? "1" : "0",
                        drivetrain.gear.ToString(CultureInfo.InvariantCulture), N(p.x), N(p.y), N(p.z), N(q.x), N(q.y), N(q.z), N(q.w),
                        N(v.x), N(v.y), N(v.z), N(w.x), N(w.y), N(w.z), LiveTick.ToString(CultureInfo.InvariantCulture),
                        N(c.steering), N(c.throttle), N(c.brake), N(drivetrain.rpm) }));
                    if (marker != _lastCarMarker)
                    {
                        _lastCarMarker = marker;
                        Event("car segment " + MarkerText(marker));
                        if (MarkerText(marker).EndsWith("|WAITING_TO_BEGIN") && !_scenarioChecked)
                        {
                            _scenarioChecked = true;
                            File.AppendAllText(Path.Combine(_dir, "session.txt"), "scenario=" + Scenario() + "\n");
                            Event("scenario " + Scenario());
                        }
                    }
                    return;
                }
                BeginReplayTick();
                if (!_carActive) return;
                var r = _carPlaying;
                if (r.Gear < 0 || r.Gear >= drivetrain.gearRatios.Length) throw new InvalidDataException("taped gear out of range");
                // One-time starting state at the first stage tick, at the same hook
                // the recording saved it from; strict from there on.
                if (!_trajectory && !_startStateDone && MarkerText(r.Marker).EndsWith("|UNDERWAY"))
                {
                    _startStateDone = true;
                    var path = Path.Combine(_dir, "start-state.tsv");
                    if (File.Exists(path)) Event("start state restored: " + StartState.Restore(__instance.gameObject, path, m => Event(m)) + " values");
                    else Event("no start state on this tape; the launch will differ");
                }
                __instance.steerInput = r.Steer; __instance.throttleInput = r.Throttle; __instance.brakeInput = r.Brake;
                __instance.handbrakeInput = r.Handbrake; __instance.clutchInput = r.Clutch; __instance.startEngineInput = r.StartEngine;
                if (_legacy && drivetrain.gear != r.Gear && !drivetrain.changingGear) Shift(drivetrain, r.Gear, true);
                if (_trajectory)
                {
                    if (MarkerText(r.Marker).EndsWith("|UNDERWAY") || MarkerText(r.Marker).EndsWith("|FINISHING_STAGE_ANIMATION"))
                        Vehicle.Apply(__instance, r);
                    else Vehicle.Release();
                }
                else Measure(body, drivetrain, r);
            }
            catch (Exception ex) { Fail("car step: " + ex.Message); }
        }

        private static void Measure(Rigidbody body, Drivetrain drivetrain, CarRow r)
        {
            float error = Vector3.Distance(body.position, r.Position);
            bool driving = (EventManagerField?.GetValue(null) as EventManager)?.status == EventStatusEnums.EventStatus.UNDERWAY;
            if (!driving) return;
            _lastPoseError = error;
            if (error > _maxPoseError) _maxPoseError = error;
            if (error > 0.05f && _firstDivergenceRow < 0) { _firstDivergenceRow = _car.Consumed; Event("first divergence > 5 cm at car row " + _car.Consumed); }
            if (drivetrain.gear != r.Gear) _gearMismatchSteps++;
            if (_car.Consumed % 30 == 0)
                _divergenceWriter?.WriteLine(_car.Consumed + "\t" + r.Marker + "\t" + N(error) + "\t" + drivetrain.gear + "\t" + r.Gear);
            if (error > 2f) { Fail("input replay diverged by " + F2(error) + " m at car row " + _car.Consumed); return; }
        }
        private static bool _startStateDone;

        // Record: tape each player shift with the tick Drivetrain.FixedUpdate first
        // sees it. Replay: only the replayer may shift the player car.
        private static bool BeforeShift(Drivetrain __instance, int m_gear, bool ChangeTargetGear)
        {
            if (_mode == Mode.Off || !IsPlayer(__instance)) return true;
            if (_mode == Mode.Replay)
            {
                if (_replayShifting) return true;
                if (!_legacy) { _shiftsBlocked++; return false; }
                return true;
            }
            int tick = LiveTick;
            bool inStep = Time.inFixedTimeStep;
            int effective = inStep && _drivetrainTick != tick ? tick : tick + 1;
            _shiftWriter.WriteLine(effective.ToString(CultureInfo.InvariantCulture) + "\t" + m_gear.ToString(CultureInfo.InvariantCulture) + "\t" +
                                   (ChangeTargetGear ? "1" : "0") + "\t" + (inStep ? "fixed" : "update"));
            return true;
        }

        private static bool BeforeDrivetrainStep(Drivetrain __instance)
        {
            try
            {
                if (_mode != Mode.Replay || !IsPlayer(__instance)) return true;
                BeginReplayTick();
                if (!_carActive) return true;
                if (_trajectory && (MarkerText(_carPlaying.Marker).EndsWith("|UNDERWAY") || MarkerText(_carPlaying.Marker).EndsWith("|FINISHING_STAGE_ANIMATION")))
                {
                    VehiclePlayback.ApplyDrivetrain(__instance, _carPlaying);
                    return false;
                }
                if (_legacy) return true;
                if (_shiftsByTick.TryGetValue(_carPlaying.Tick, out var shifts))
                    foreach (var s in shifts) Shift(__instance, s.Key, s.Value);
            }
            catch (Exception ex) { Fail("drivetrain step: " + ex.Message); }
            return true;
        }
        private static void AfterDrivetrainStep(Drivetrain __instance)
        {
            if (_mode == Mode.Record && IsPlayer(__instance)) _drivetrainTick = LiveTick;
        }

        private static void Shift(Drivetrain drivetrain, int gear, bool changeTarget)
        {
            _replayShifting = true;
            try { drivetrain.Shift(gear, changeTarget); _shiftsReplayed++; }
            finally { _replayShifting = false; }
        }

        private static string Scenario()
        {
            var manager = EventManagerField?.GetValue(null) as EventManager;
            string car = manager?.playerManager?.carDynamics != null ? manager.playerManager.carDynamics.gameObject.name : "?";
            string weather = manager?.sceneryManager != null ? manager.sceneryManager.Weather.ToString() : "?";
            return SceneManager.GetActiveScene().name + "|" + car + "|" + weather;
        }

        private static void CheckScenario()
        {
            if (!MarkerText(_carPlaying.Marker).EndsWith("|WAITING_TO_BEGIN")) return;
            _scenarioChecked = true;
            string live = Scenario();
            Event("scenario " + live + (_expectedScenario != null ? " (taped " + _expectedScenario + ")" : " (not taped)"));
            if (_expectedScenario != null && live != _expectedScenario) Fail("scenario differs: live " + live + ", taped " + _expectedScenario);
        }

        // ---- frame tick --------------------------------------------------------

        /// <summary>Once per rendered frame, from the probe's OnUpdate.</summary>
        internal static void Tick()
        {
            if (_mode == Mode.Off) return;
            try
            {
                int marker = MarkerId(Marker());
                if (_mode == Mode.Replay && marker != _lastObservedMarker)
                { _lastObservedMarker = marker; Event("live state " + MarkerText(marker)); }
                if (_mode == Mode.Record)
                {
                    if (_captureDriver != null)
                    {
                        if (_captureDriver.Tick(MarkerText(marker))) { Stop("synthetic integration capture completed"); return; }
                        _playing = _captureDriver.Values;
                        var driverUmm = UnityModManagerNet.UnityModManager.UI.Instance;
                        if (driverUmm != null && driverUmm.Opened != _playing.ContainsKey("U.Open")) driverUmm.ToggleWindow(_playing.ContainsKey("U.Open"));
                        EndSplashIfTaped(MarkerText(marker));
                        if (_playing.ContainsKey("E.CinematicSkip")) ReplayCinematicSkip(MarkerText(marker));
                    }
                    // UMM's window opens at startup and blocks stock menus while open;
                    // it's often closed with the mouse, which isn't taped. Tape its state.
                    if (UnityModManagerNet.UnityModManager.UI.Instance?.Opened == true) _frameValues["U.Open"] = "1";
                    TapeCinematicSkip(MarkerText(marker));
                    var line = new StringBuilder();
                    line.Append(_frame.ToString(CultureInfo.InvariantCulture)).Append('\t').Append(marker.ToString(CultureInfo.InvariantCulture));
                    foreach (var pair in _frameValues) line.Append('\t').Append(pair.Key).Append('=').Append(pair.Value);
                    _inputWriter.WriteLine(line.ToString());
                    _frameValues.Clear();
                    _clicksInFrame = 0;
                    if (marker != _lastMarker) { _lastMarker = marker; Reseed(marker); Event("segment " + MarkerText(marker)); Shot(_dir, marker); }
                    if (_frame % 60 == 0) { _inputWriter.Flush(); _carWriter.Flush(); _shiftWriter.Flush(); _eventWriter.Flush(); }
                }
                else
                {
                    // Owner takeover: raw Unity input isn't replayed, so F12 still works.
                    if (UnityEngine.Input.GetKeyDown(KeyCode.F12)) { Finish("aborted: owner took over (F12)"); return; }
                    int before = _frames.Segment;
                    if (!_frames.Next(marker, out var playing)) playing = Empty;
                    _playing = playing;
                    if (_semanticMenus)
                        foreach (var click in _playing.Where(p => p.Key.StartsWith("E.Click.")).OrderBy(p => int.Parse(p.Key.Substring(8), CultureInfo.InvariantCulture)))
                            MenuPlayback.Click(click.Value);
                    var umm = UnityModManagerNet.UnityModManager.UI.Instance;
                    bool wantOpen = _playing.ContainsKey("U.Open");
                    if (umm != null && umm.Opened != wantOpen) { umm.ToggleWindow(wantOpen); Event((wantOpen ? "opened" : "closed") + " the mod manager window as taped"); }
                    EndSplashIfTaped(MarkerText(marker));
                    ReplayCinematicSkip(MarkerText(marker));
                    if (marker != _lastMarker) { _lastMarker = marker; Reseed(marker); }
                    if (_frames.Segment != before) { Event("segment " + _frames.Segment + " " + MarkerText(marker)); Shot(_out, marker); }
                    if (_frames.Failed != null) Fail(_frames.Failed);
                    else if (_car.Failed != null) Fail(_car.Failed);
                    else if (_frames.Finished && _car.SkippedWhere(m => m.EndsWith("|UNDERWAY")) == 0)
                        Finish(Verdict("final menu state reached and all driving rows played; unused non-driving rows=" + _car.SkippedWhere(m => !m.EndsWith("|UNDERWAY"))));
                    if (_frame % 60 == 0) { _eventWriter?.Flush(); _divergenceWriter?.Flush(); }
                }
                _frame++;
            }
            catch (Exception ex) { Fail("tick: " + ex.Message); }
        }

        internal static string Command(string command)
        {
            if (command == "SESSION-STATUS") return "OK " + Describe;
            if (command == "SESSION-UI") return "OK " + MenuPlayback.Describe();
            if (command == "SESSION-STOP") { Stop("stopped by command"); return "OK " + Describe; }
            return null;
        }

        internal static void Stop(string why)
        {
            if (_mode == Mode.Record)
            {
                Event("record stopped: " + why + (_incomplete != null ? "; INCOMPLETE: " + _incomplete : ""));
                try { RecorderMain.StopSessionSignals(_dir); }
                catch (Exception ex) { MarkIncomplete("signal capture: " + ex.Message); }
                try { _inputWriter?.Dispose(); _carWriter?.Dispose(); _shiftWriter?.Dispose(); _eventWriter?.Dispose(); }
                catch (Exception ex) { MarkIncomplete("closing files: " + ex.Message); }
                _inputWriter = _carWriter = _shiftWriter = null; _eventWriter = null;
                if (_incomplete == null)
                {
                    try
                    {
                        File.WriteAllText(Path.Combine(_dir, "end.txt"), "reason=" + why + "\nframes=" + _frame + "\nsteps=" + _fixedStep + "\n");
                        var sealedFiles = Directory.GetFiles(_dir).Select(Path.GetFileName).Where(n => n != "complete.tsv" && !n.EndsWith(".tmp")).ToList();
                        var environment = Path.Combine(_dir, "environment-at-start");
                        if (Directory.Exists(environment)) sealedFiles.AddRange(Directory.GetFiles(environment).Select(p => "environment-at-start/" + Path.GetFileName(p)));
                        ArtifactSeal.Complete(_dir, sealedFiles.ToArray());
                    }
                    catch (Exception ex) { MarkIncomplete("completion seal: " + ex.Message); }
                }
                _mode = Mode.Off;
            }
            else if (_mode == Mode.Replay && _result == null)
                Finish(_frames.Finished && _car.SkippedWhere(m => m.EndsWith("|UNDERWAY")) == 0 ? Verdict(why) : "aborted: " + why);
        }

        /// <summary>Probe unload: stop, then remove every session hook except the output mutes.</summary>
        internal static void Unload()
        {
            Stop("probe unloaded");
            try { _harmony?.UnpatchAll(HarmonyId); if (_muteOutputs) PatchOutputs(); }
            catch (Exception ex) { _log?.Invoke("Session tape unload: " + ex.Message); }
        }

        // Strict: no taped driving rows may be skipped. Menu frames skipped at a
        // transition, and the car's post-stage tail, are reported, not failed.
        private static string Verdict(string how)
        {
            int driving = _car.SkippedWhere(m => m.EndsWith("|UNDERWAY"));
            if (driving > 0) return "failed: " + driving + " taped driving rows skipped (" + how + ")";
            if (!_trajectory && (_maxPoseError > 0.5f || _gearMismatchSteps > 0))
                return "failed: input replay diverged; max error " + F2(_maxPoseError) + " m, gear mismatches " + _gearMismatchSteps;
            if (_trajectory && (Vehicle.Applied == 0 || Vehicle.MaxApplicationError > 0.01f)) return "failed: trajectory application";
            return "passed (" + how + (_trajectory ? ", trajectory playback" : ", input diagnostic") + ")";
        }

        private static void Finish(string result)
        {
            _result = result;
            Vehicle.Release();
            Event("replay " + result + "; " + Describe);
            Shot(_out, -1);
            try
            {
                _divergenceWriter?.Dispose(); _divergenceWriter = null;
                var tmp = Path.Combine(_out, "result.txt.tmp");
                File.WriteAllText(tmp, result + "\n" + Describe + "\n");
                File.Move(tmp, Path.Combine(_out, "result.txt"));
            }
            catch (Exception ex) { _log?.Invoke("Session tape result not written: " + ex.Message); }
            _eventWriter?.Dispose(); _eventWriter = null;
            // Inputs return to the player; force and telemetry stay muted.
            _mode = Mode.Off;
        }

        private static void MarkIncomplete(string why)
        {
            if (_incomplete != null) return;
            _incomplete = why;
            try { File.WriteAllText(Path.Combine(_dir, "incomplete.txt"), why + "\n"); } catch { }
            _log?.Invoke("Session tape recording INCOMPLETE: " + why);
        }
        internal static void CaptureError(string why) => MarkIncomplete(why);

        private static void Fail(string why)
        {
            if (_mode == Mode.Record) MarkIncomplete(why);
            else if (_mode == Mode.Replay && _result == null) Finish("failed: " + why);
        }

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

        // Deliberate, in recording and replay alike: the game rolls custom-event
        // stage, weather and car with Random.Range (StageGenerator). Re-seeding
        // Unity's generator from the screen name at each screen change makes the
        // same presses roll the same stage. The resolved scenario is taped and
        // checked before driving.
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
            try { _eventWriter?.WriteLine(line); } catch (Exception ex) { if (_mode == Mode.Record) MarkIncomplete("event log: " + ex.Message); }
            _log?.Invoke("Session tape: " + text);
        }

        private static string N(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string F2(float value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
