using System.Globalization;
using ArtOfSimRally.Testing;
// The stand-ins below are read only through reflection.
#pragma warning disable CS0649, CS0414, CS0169

// Stand-ins with the mod's full type and member names: SharedEvidence binds to these by reflection exactly as it
// binds to the loaded mod (sana 36236). Only members the recorder reads exist here.
namespace ArtOfSimRally.Mod
{
    internal sealed class FixtureSettings { public bool SharedInput = true, WheelInputEnabled, ShifterEnabled, ForceFeedbackEnabled = true; }
    internal static class Main { internal static FixtureSettings Settings = new FixtureSettings(); }
    internal enum FixturePhase { Off, Waiting, Isolating, Attached, Refused, CleanupFailed }
    internal static class SharedInputStartup
    {
        internal static bool RequestedAtStartup { get; set; } = true;
        private static FixturePhase phase = FixturePhase.Waiting;
        private static string reason;
        internal static bool StoppedNow;
        private static bool Stopped => StoppedNow;
        private static string StopReason => "fixture stop";
        internal static void Set(FixturePhase next, string why = null) { phase = next; reason = why; }
    }
}
namespace ArtOfSimRally.Mod.Shim
{
    internal enum KeyboardSteer { NotQueried, Unknown, Absent, Present }
    internal static class ShimFrame { internal const int Count = 31; }
    internal sealed class FixtureConsumer
    {
        internal bool Started { get; set; }
        internal bool Terminal { get; set; }
        internal int Frame = -1;
        internal readonly float[] Values = new float[ShimFrame.Count];
        internal readonly bool[] Owned = new bool[ShimFrame.Count];
        internal bool Fresh(int unityFrame) => Started && !Terminal && unityFrame == Frame;
        internal float Value(int action, int unityFrame) => Fresh(unityFrame) && Owned[action] ? Values[action] : 0f;
        internal bool Owns(int action) => Started && Owned[action];
    }
    internal static class ShimInput
    {
        internal static readonly FixtureConsumer Consumer = new FixtureConsumer();
        internal static int IsolatedFrame = -1;
        internal struct Composition
        {
            internal int Frame;
            internal bool SteerLive;
            internal float AppliedSteer, AppliedThrottle, AppliedBrake, AppliedHandbrake, Alignment;
            internal float Steer, Throttle, Brake, Handbrake;
            internal float AppliedClutch, Clutch;
            internal KeyboardSteer Keyboard;
            internal bool KeyboardSteered;
        }
        internal static Composition Last;
    }
}

static class SharedEvidenceTests
{
    internal static int Run(string root, Action<bool, string> check)
    {
        int before = 0;
        void Check(bool ok, string why) { before++; check(ok, why); }
        string mod = Path.Combine(root, "mod"); Directory.CreateDirectory(mod);
        File.WriteAllText(Path.Combine(mod, "build.json"), "{\n  \"schema\": 1,\n  \"identity\": \"0.4.2-rc.17+fixture.clean\"\n}\n");
        File.WriteAllText(Path.Combine(mod, "dbce-controls.ini"), "; owner\n[Other]\nProfile = not this\n[Controls]\nSchema = 1\nProfile = Rig = Main\nRevision = abc123\nTransmission = H-pattern\nsteer = axis 0\n");
        File.WriteAllBytes(Path.Combine(mod, "dbce_shim.dll"), new byte[] { 1, 2, 3 });
        string native = Path.Combine(root, "UnityForceFeedback.dll"); File.WriteAllBytes(native, new byte[] { 4, 5 });

        var shared = SharedEvidence.Bind(out string why);
        Check(shared != null && why == null && shared.Requested, "binding to the mod's members: " + why);
        var identity = SharedEvidence.Identity(shared, null, mod, native).Split('\n').Where(l => l.Length > 0)
            .Select(l => l.Split(new[] { '=' }, 2)).ToDictionary(p => p[0], p => p[1]);
        Check(identity["inputModIdentity"] == "0.4.2-rc.17+fixture.clean" && identity["inputModSha256"] == "absent", "mod identity");
        Check(identity["settingSharedInput"] == "true" && identity["settingWheelInputEnabled"] == "false" && identity["settingForceFeedbackEnabled"] == "true", "input settings");
        Check(identity["sharedInputRequested"] == "true" && identity["sharedEvidence"] == "bound", "shared request");
        Check(identity["controlsProfile"] == "Rig = Main" && identity["controlsRevision"] == "abc123" && identity["controlsTransmission"] == "H-pattern", "controls header: " + identity["controlsProfile"]);
        Check(identity["controlsSha256"].Length == 64 && identity["shimSha256"] == "039058C6F2C0CB492C533B0A4D14EF77CC0F78ABCCCED5287D84A1A2011CFB81" &&
            identity["nativeSha256"].Length == 64, "payload hashes");
        var absent = SharedEvidence.Identity(null, "MissingMemberException: ShimInput", Path.Combine(root, "missing"), Path.Combine(root, "missing.dll"));
        Check(absent.Contains("sharedEvidence=unavailable: MissingMemberException: ShimInput\n") && absent.Contains("sharedInputRequested=unknown\n") &&
            absent.Contains("controlsSha256=absent\n") && absent.Contains("controlsProfile=absent\n") && absent.Contains("shimSha256=absent\n"), "unbound identity");
        Check(SharedEvidence.Identity(null, "line\nbreak", mod, native).Split('\n').Count(l => l.StartsWith("sharedEvidence=")) == 1, "a value spans lines");

        string[] columns = SharedEvidence.Header.Split('\t');
        Dictionary<string, string> Row(int frame) => columns.Zip(shared.Row(frame, 7).Split('\t'), (c, v) => (c, v)).ToDictionary(p => p.c, p => p.v);
        var waiting = Row(10);
        Check(waiting.Count == columns.Length && waiting["phase"] == "Waiting" && waiting["started"] == "0" && waiting["held"] == "0" && waiting["marker"] == "7", "waiting row");
        var consumer = ArtOfSimRally.Mod.Shim.ShimInput.Consumer;
        ArtOfSimRally.Mod.SharedInputStartup.Set(ArtOfSimRally.Mod.FixturePhase.Attached);
        consumer.Started = true; consumer.Frame = 11; ArtOfSimRally.Mod.Shim.ShimInput.IsolatedFrame = 11;
        consumer.Owned[0] = consumer.Owned[9] = consumer.Owned[21] = consumer.Owned[22] = true;
        consumer.Values[0] = .9f; consumer.Values[9] = 1; consumer.Values[21] = 1; consumer.Values[22] = .2f;
        ArtOfSimRally.Mod.Shim.ShimInput.Last = new ArtOfSimRally.Mod.Shim.ShimInput.Composition { Frame = 11, SteerLive = true, AppliedSteer = .25f, AppliedClutch = .6f,
            Alignment = .02f, Steer = 1.02f, Clutch = .6f, Keyboard = ArtOfSimRally.Mod.Shim.KeyboardSteer.Present, KeyboardSteered = true };
        var attached = Row(11);
        Check(attached["phase"] == "Attached" && attached["started"] == "1" && attached["fresh"] == "1" && attached["isolated"] == "1" && attached["lastFrame"] == "11", "attached row");
        Check(attached["appliedSteer"] == "0.25" && attached["appliedClutch"] == "0.6" && attached["alignment"] == "0.02" && attached["steer"] == "1.02" && attached["clutch"] == "0.6", "composition columns");
        Check(attached["keyboard"] == "Present" && attached["keyboardSteered"] == "1", "keyboard columns");
        // Buttons only: steer (0) is an axis, 22 is held below the threshold; confirm (9) and modMenu (21) are held.
        Check(attached["held"] == ((1UL << 9) | (1UL << 21)).ToString("x", CultureInfo.InvariantCulture), "held owned buttons: " + attached["held"]);
        var stale = Row(12);
        Check(stale["fresh"] == "0" && stale["isolated"] == "0" && stale["held"] == "0" && stale["lastFrame"] == "11", "stale frame row");
        ArtOfSimRally.Mod.SharedInputStartup.StoppedNow = true; consumer.Terminal = true;
        var outcome = shared.Outcome().Split('\n').Where(l => l.Length > 0).Select(l => l.Split(new[] { '=' }, 2)).ToDictionary(p => p[0], p => p[1]);
        Check(outcome["sharedPhase"] == "Attached" && outcome["sharedAttachedFrame"] == "11" && outcome["sharedStopped"] == "true" &&
            outcome["sharedStopReason"] == "fixture stop" && outcome["sharedRefusal"] == "none" && outcome["sharedTapeRows"] == "3", "outcome lines");
        ArtOfSimRally.Mod.SharedInputStartup.Set(ArtOfSimRally.Mod.FixturePhase.Refused, "no shared profile beside the mod.");
        Check(shared.Outcome().Contains("sharedRefusal=no shared profile beside the mod.\n") && shared.Outcome().Contains("sharedPhase=Refused\n"), "refusal outcome");

        // Replay outcomes (sana 36418/36495): partial only after every driving row played and passed.
        const string Waited = "frames waited 120 s for 'Japan|RESULTS|FINISHED'";
        const string DrivePassed = "passed (driving rows played, trajectory playback)";
        string stage = ReplayOutcome.PostStage(Waited, true, 1200, 0, DrivePassed);
        Check(stage == "partial:post-stage-divergence: all 1200 driving rows played (driving rows played, trajectory playback); " + Waited, "partial line: " + stage);
        Check(ReplayOutcome.PostStage(Waited, true, 1200, 1, DrivePassed) == null, "driving rows still unplayed");
        Check(ReplayOutcome.PostStage(Waited, true, 0, 0, DrivePassed) == null, "no driving rows (divergence before the stage)");
        Check(ReplayOutcome.PostStage(Waited, true, 1200, 0, "failed: trajectory application") == null, "failed driving verdict");
        Check(ReplayOutcome.PostStage("frames would skip required rows in 'x'", false, 1200, 0, DrivePassed) == null, "not a wait failure");
        Check(ReplayOutcome.PostStage(null, true, 1200, 0, DrivePassed) == null, "no failure");
        string fields = ReplayOutcome.Fields(stage, 2, "Japan|RESULTS|FINISHED", 120);
        Check(fields == "scope=stage-only\nfullSessionPassed=false\ndrivingSegmentsTotal=2\ndrivingSegmentsCompleted=2\nexpectedMarker=Japan|RESULTS|FINISHED\ntimeoutSeconds=120\n",
            "partial fields: " + fields);
        Check(ReplayOutcome.Fields(DrivePassed, 2, "x", 120) == "" && ReplayOutcome.Fields("failed: " + Waited, 2, "x", 120) == "" &&
            ReplayOutcome.Fields("aborted: owner took over (F12)", 2, "x", 120) == "", "passed, failed and aborted results changed");
        Check(!(stage + "\n" + fields).Split('\n').Any(l => l.StartsWith("passed", StringComparison.Ordinal)), "a partial result reads as a pass to Wheelkit");
        Check(ReplayOutcome.PostStage("frames waited 120 s for 'a\nb'", true, 5, 0, DrivePassed)!.Split('\n').Length == 1 &&
            ReplayOutcome.Fields(stage, 1, "a\nb", 120).Contains("expectedMarker=a b\n"), "a value spans result lines");
        return before;
    }
}
