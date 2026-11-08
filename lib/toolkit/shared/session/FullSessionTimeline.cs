// MIT. Source-vendored full-session timeline: a data-only JSON contract for one recorded launch.
// Extracted from DRIVE Rally's DriveRally.Core/FullSessionTimeline.cs (dbce-mods-drive-rally 9f1b85e), keeping its
// shape, field names and rules, with the schema string supplied by each game. Hosts use their existing serializer;
// there are no engine, device, input-injection, process or filesystem calls here.
// Builds for netstandard2.0 and later.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dbce.Wheel.Sessions
{
    public enum FullSessionPhase { Boot, Menu, Loading, Driving, Paused, Finish, Results }
    public enum FullSessionStepKind { State, Action, Poses }
    public enum FullSessionActionKind { Activate, Select, Back, Pause, Continue, FinishStage }
    public enum FullSessionOutcome { Stopped, CompletedStage, Failed }

    /// <summary>The recorded native stage choice. Field meanings are per game (DRIVE: biome; Woden: Arcade route).</summary>
    public sealed class FullSessionSelection
    {
        public string Scene { get; set; } = "";
        public string Biome { get; set; } = "";
        public string Track { get; set; } = "";
        // A stable catalog/asset fingerprint, never a transient runtime id.
        public string Car { get; set; } = "";
        public string Mode { get; set; } = "";
        public string Start { get; set; } = "";
        public double FixedDeltaTime { get; set; }
        public FullSessionSelection Copy() => (FullSessionSelection)MemberwiseClone();
        public bool Same(FullSessionSelection? other) => other != null && Scene == other.Scene && Biome == other.Biome &&
            Track == other.Track && Car == other.Car && Mode == other.Mode && Start == other.Start && FixedDeltaTime == other.FixedDeltaTime;
        public void Validate()
        {
            foreach (var s in new[] { Scene, Biome, Track, Car, Mode, Start }) FullSessionTimeline.Text(s);
            if (!FullSessionTimeline.Finite(FixedDeltaTime) || FixedDeltaTime < .001 || FixedDeltaTime > .1)
                throw new InvalidDataException("Invalid stage timestep.");
        }
    }

    public sealed class FullSessionState
    {
        public FullSessionPhase Phase { get; set; }
        // Scene + top native event loop + selected semantic menu target; no engine instance ids.
        public string Marker { get; set; } = "";
        public FullSessionSelection? Selection { get; set; }
        public FullSessionState Copy() => new FullSessionState { Phase = Phase, Marker = Marker, Selection = Selection?.Copy() };
        public bool Same(FullSessionState? other) => other != null && Phase == other.Phase && Marker == other.Marker &&
            (Selection == null ? other.Selection == null : Selection.Same(other.Selection));
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(FullSessionPhase), Phase)) throw new InvalidDataException("Unknown session phase.");
            FullSessionTimeline.Text(Marker); Selection?.Validate();
            if (Phase >= FullSessionPhase.Driving && Selection == null) throw new InvalidDataException("Missing selected stage identity.");
        }
    }

    public sealed class FullSessionAction
    {
        public FullSessionActionKind Kind { get; set; }
        // An adapter-owned semantic target; never an arbitrary method, event type or OS key.
        public string Target { get; set; } = "";
        public string Value { get; set; } = "";
        public FullSessionAction Copy() => (FullSessionAction)MemberwiseClone();
        public void Validate()
        {
            if (!Enum.IsDefined(typeof(FullSessionActionKind), Kind)) throw new InvalidDataException("Unknown menu action.");
            FullSessionTimeline.Text(Target); FullSessionTimeline.Text(Value, allowEmpty: true);
        }
    }

    public sealed class FullSessionStep
    {
        public FullSessionStepKind Kind { get; set; }
        public double AtSeconds { get; set; }
        public FullSessionState? State { get; set; }
        public FullSessionAction? Action { get; set; }
        public int FirstPose { get; set; }
        public int PoseCount { get; set; }
        public FullSessionStep Copy() => new FullSessionStep { Kind = Kind, AtSeconds = AtSeconds, State = State?.Copy(),
            Action = Action?.Copy(), FirstPose = FirstPose, PoseCount = PoseCount };
    }

    public sealed class FullSessionArtifact
    {
        // The game's existing sealed stage bundle (trajectory, signals, context) and its complete.tsv.
        public string Directory { get; set; } = "";
        public string SealSha256 { get; set; } = "";
        public int PoseCount { get; set; }
        public FullSessionArtifact Copy() => (FullSessionArtifact)MemberwiseClone();
    }

    public sealed class FullSessionTimeline
    {
        public const int MaxSteps = 10000, MaxSeconds = 1800, MaxPoses = 180000;
        public const int MaxDocumentBytes = 8 * 1024 * 1024; // Host checks before deserializing.
        public string Schema { get; set; } = "";
        public string GameBuild { get; set; } = "";
        public string AdapterVersion { get; set; } = "";
        public double DurationSeconds { get; set; }
        public FullSessionOutcome Outcome { get; set; }
        public FullSessionArtifact? Stage { get; set; }
        public FullSessionStep[] Steps { get; set; } = Array.Empty<FullSessionStep>();
        public FullSessionTimeline Copy() => new FullSessionTimeline { Schema = Schema, GameBuild = GameBuild, AdapterVersion = AdapterVersion,
            DurationSeconds = DurationSeconds, Outcome = Outcome, Stage = Stage?.Copy(), Steps = Steps.Select(s => s.Copy()).ToArray() };

        /// <summary>Checks one recorded launch against the expected game schema (for example "drive.full-session@1").</summary>
        public void Validate(string schema, bool forPlayback = false)
        {
            Text(schema);
            if (Schema != schema || !Hash(GameBuild)) throw new InvalidDataException("Unknown session schema or game build.");
            Text(AdapterVersion);
            if (!Finite(DurationSeconds) || DurationSeconds < 0 || DurationSeconds > MaxSeconds ||
                !Enum.IsDefined(typeof(FullSessionOutcome), Outcome) || Steps == null || Steps.Length == 0 || Steps.Length > MaxSteps)
                throw new InvalidDataException("Invalid session bounds or outcome.");
            FullSessionState? state = null; FullSessionSelection? selected = null;
            int poses = 0; double time = 0; bool finished = false, results = false;
            FullSessionStep? previous = null;
            foreach (var row in Steps)
            {
                if (row == null || !Finite(row.AtSeconds) || row.AtSeconds < time || row.AtSeconds > DurationSeconds)
                    throw new InvalidDataException("Session timeline is not monotonic.");
                time = row.AtSeconds;
                switch (row.Kind)
                {
                    case FullSessionStepKind.State:
                        if (row.State == null || row.Action != null || row.FirstPose != 0 || row.PoseCount != 0)
                            throw new InvalidDataException("Invalid state row.");
                        row.State.Validate();
                        if (state == null && row.State.Phase != FullSessionPhase.Boot) throw new InvalidDataException("Session does not start at boot.");
                        if (row.State.Phase >= FullSessionPhase.Driving)
                        {
                            if (selected == null && row.State.Phase != FullSessionPhase.Driving) throw new InvalidDataException("Stage lifecycle starts outside driving.");
                            selected ??= row.State.Selection;
                            if (!selected!.Same(row.State.Selection)) throw new InvalidDataException("Recorded stage identity changed.");
                        }
                        if (finished && (row.State.Phase == FullSessionPhase.Driving || row.State.Phase == FullSessionPhase.Paused))
                            throw new InvalidDataException("A second driving stage is outside this session format.");
                        if (row.State.Phase == FullSessionPhase.Results && !finished) throw new InvalidDataException("Results without a recorded finish.");
                        if (row.State.Phase == FullSessionPhase.Finish && (previous?.Action?.Kind != FullSessionActionKind.FinishStage || finished))
                            throw new InvalidDataException("Finish needs one recorded native finish action.");
                        finished |= row.State.Phase == FullSessionPhase.Finish;
                        results |= row.State.Phase == FullSessionPhase.Results;
                        state = row.State;
                        break;
                    case FullSessionStepKind.Action:
                        if (state == null || row.Action == null || row.State != null || row.FirstPose != 0 || row.PoseCount != 0)
                            throw new InvalidDataException("Invalid action row.");
                        row.Action.Validate();
                        if (row.Action.Kind == FullSessionActionKind.FinishStage &&
                            (state.Phase != FullSessionPhase.Driving || row.Action.Target != "stage.finish" || row.Action.Value.Length != 0 || poses < 2))
                            throw new InvalidDataException("Invalid native finish action.");
                        break;
                    case FullSessionStepKind.Poses:
                        if (state?.Phase != FullSessionPhase.Driving || row.State != null || row.Action != null ||
                            row.FirstPose != poses || row.PoseCount < 1 || row.PoseCount > MaxPoses || poses > MaxPoses - row.PoseCount)
                            throw new InvalidDataException("Missing, repeated or misplaced trajectory interval.");
                        poses += row.PoseCount;
                        break;
                    default: throw new InvalidDataException("Unknown session row.");
                }
                if (previous?.Action?.Kind == FullSessionActionKind.FinishStage && row.State?.Phase != FullSessionPhase.Finish)
                    throw new InvalidDataException("Recorded finish was not followed by its observed state.");
                previous = row;
            }
            if (previous?.Action?.Kind == FullSessionActionKind.FinishStage)
                throw new InvalidDataException("Recorded finish is missing its observed state.");
            if (Stage != null)
            {
                Text(Stage.Directory);
                if (!FullSessionPackageNames.SafeRelative(Stage.Directory) || !Hash(Stage.SealSha256) || Stage.PoseCount != poses || poses < 2)
                    throw new InvalidDataException("Invalid stage artifact reference.");
            }
            if (Outcome == FullSessionOutcome.CompletedStage && (!finished || !results || Stage == null))
                throw new InvalidDataException("A complete session requires the observed finish, results and sealed stage.");
            if (forPlayback && (Outcome == FullSessionOutcome.Failed || Stage == null))
                throw new InvalidDataException("Playback needs a clean stop and its complete sealed stage artifact.");
        }
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static bool Hash(string value) => value != null && value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
        public static void Text(string value, bool allowEmpty = false)
        {
            if (value == null || (!allowEmpty && string.IsNullOrWhiteSpace(value)) || value.Length > 2048 || value.Any(char.IsControl))
                throw new InvalidDataException("Invalid or oversized session text.");
        }
    }

    /// <summary>Observes one user-selected stage within a launch. Pause and finish close pose intervals, not the session.</summary>
    public sealed class FullSessionCapture
    {
        private readonly FullSessionTimeline tape;
        private readonly List<FullSessionStep> steps = new List<FullSessionStep>();
        private FullSessionState? state;
        private int lastPose, intervalStart;
        private double lastTime;
        private bool stopped;
        public FullSessionCapture(string schema, string gameBuild, string adapterVersion)
        {
            FullSessionTimeline.Text(schema);
            tape = new FullSessionTimeline { Schema = schema, GameBuild = gameBuild, AdapterVersion = adapterVersion };
        }
        public FullSessionPhase? Phase => state?.Phase;

        public void Observe(FullSessionState observed, int poseCount, double seconds)
        {
            observed.Validate(); Check(poseCount, seconds);
            if (state?.Same(observed) == true) return;
            CloseInterval(seconds);
            if (observed.Phase == FullSessionPhase.Finish)
                Add(new FullSessionStep { Kind = FullSessionStepKind.Action, AtSeconds = seconds,
                    Action = new FullSessionAction { Kind = FullSessionActionKind.FinishStage, Target = "stage.finish" } });
            state = observed.Copy(); Add(new FullSessionStep { Kind = FullSessionStepKind.State, AtSeconds = seconds, State = state.Copy() });
            intervalStart = poseCount;
        }
        // The adapter records a successful semantic invocation once, before the next observed state.
        // It observes ordinary keyboard/mouse/controller behavior; capture never replaces any input.
        public void Action(FullSessionAction action, int poseCount, double seconds)
        {
            action.Validate(); Check(poseCount, seconds);
            if (state == null) throw new InvalidOperationException("Observe boot before recording actions.");
            if (action.Kind == FullSessionActionKind.FinishStage) throw new InvalidDataException("Observe the actual finish to record it.");
            CloseInterval(seconds); Add(new FullSessionStep { Kind = FullSessionStepKind.Action, AtSeconds = seconds, Action = action.Copy() });
        }
        public FullSessionTimeline Stop(int poseCount, double seconds, FullSessionArtifact? artifact, bool failed = false)
        {
            Check(poseCount, seconds); CloseInterval(seconds);
            var result = tape.Copy(); result.DurationSeconds = seconds; result.Stage = artifact?.Copy(); result.Steps = steps.Select(s => s.Copy()).ToArray();
            result.Outcome = failed ? FullSessionOutcome.Failed : steps.Any(s => s.State?.Phase == FullSessionPhase.Results)
                ? FullSessionOutcome.CompletedStage : FullSessionOutcome.Stopped;
            result.Validate(tape.Schema); stopped = true; return result;
        }
        private void Check(int poseCount, double seconds)
        {
            if (stopped) throw new InvalidOperationException("Session already stopped.");
            if (!FullSessionTimeline.Finite(seconds) || seconds < lastTime || seconds > FullSessionTimeline.MaxSeconds ||
                poseCount < lastPose || poseCount > FullSessionTimeline.MaxPoses)
                throw new InvalidDataException("Session clock or pose counter is invalid.");
            if (poseCount != lastPose && state?.Phase != FullSessionPhase.Driving) throw new InvalidDataException("Poses advanced outside driving.");
            lastTime = seconds; lastPose = poseCount;
        }
        private void CloseInterval(double seconds)
        {
            if (state?.Phase == FullSessionPhase.Driving && lastPose > intervalStart)
                Add(new FullSessionStep { Kind = FullSessionStepKind.Poses, AtSeconds = seconds, FirstPose = intervalStart, PoseCount = lastPose - intervalStart });
            intervalStart = lastPose;
        }
        private void Add(FullSessionStep step)
        {
            if (steps.Count >= FullSessionTimeline.MaxSteps) throw new InvalidDataException("Session action/state limit reached.");
            steps.Add(step);
        }
    }

    /// <summary>DRIVE's package layout: payload names derive from the original signal file; the outer seal covers them.</summary>
    public static class FullSessionPackageNames
    {
        public const string Descriptor = "session.json";
        public const int MaxReasonLength = 512;
        public sealed class Names
        {
            public string Signals = "", Observation = "", Timeline = "", Stage = "";
            public string[] OuterSealFiles() => new[] { Descriptor, Signals, Observation, Timeline, Stage + "/complete.tsv" };
        }
        /// <summary>For "source.jsonl": source.session.jsonl, source.stage-session.json and source.stage.</summary>
        public static Names FromSignal(string signalName)
        {
            if (!SafeRelative(signalName) || signalName.IndexOf('/') >= 0 || !signalName.EndsWith(".jsonl", StringComparison.Ordinal) ||
                signalName.Length <= ".jsonl".Length)
                throw new InvalidDataException("Original signal filename required.");
            string stem = signalName.Substring(0, signalName.Length - ".jsonl".Length);
            return new Names { Signals = signalName, Observation = stem + ".session.jsonl", Timeline = stem + ".stage-session.json", Stage = stem + ".stage" };
        }
        /// <summary>Text-only check of a package-relative path: forward slashes, no root, drive, traversal, control or trailing dot/space.</summary>
        public static bool SafeRelative(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 260 || name.StartsWith("/", StringComparison.Ordinal) ||
                name.IndexOfAny(new[] { '\\', ':', '\r', '\n', '\t' }) >= 0 || name.Any(char.IsControl)) return false;
            return name.Split('/').All(part => part.Length > 0 && part != "." && part != ".." && !part.EndsWith(".", StringComparison.Ordinal) &&
                !part.EndsWith(" ", StringComparison.Ordinal));
        }
        /// <summary>Nonempty plain text: control characters become spaces, then trim and cut to the limit without splitting a surrogate pair.</summary>
        public static string UnavailableReason(string message)
        {
            string text = new string((message ?? "").Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
            if (text.Length == 0) text = "The native route compiler refused this recording.";
            if (text.Length <= MaxReasonLength) return text;
            int cut = char.IsHighSurrogate(text[MaxReasonLength - 1]) ? MaxReasonLength - 1 : MaxReasonLength;
            return text.Substring(0, cut).TrimEnd();
        }
    }
}
