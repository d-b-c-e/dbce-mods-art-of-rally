using System.Text;
using ArtOfSimRally.Mod;
using UnityEngine;
using Native = Dbce.Wheel.Ffb.WheelFfbNative;

// Crash = 120 ms push + 25 Hz rattle at half magnitude (owner's CrashFeel pick F,
// 2026-10-05). Asserts commands and timing only, never felt or measured output.
static class RattleTests
{
    static int assertions;
    static void Check(bool value, string why) { assertions++; if (!value) throw new Exception(why); }

    sealed class Output : ILandingOutput, ICrashRattleOutput
    {
        public double Now = 100;
        public int PushStops, RattleCreates, RattlePlays, RattleStops, Releases;
        public bool RattleCreateFails, RattlePlayFails, RattleStopFails;
        public float Push, RattleMagnitude, RattleHz;
        public int RattleFade;
        public readonly List<string> Order = new();
        public int Create(ImpactKind kind, int hz, int ms) { Order.Add("create " + kind); return kind == ImpactKind.Crash ? 7 : 1; }
        public bool Play(ImpactKind kind, int slot, float magnitude, float hz)
        { Order.Add("play " + kind); if (kind == ImpactKind.Crash) Push = magnitude; return true; }
        public bool Stop(ImpactKind kind, int slot) { Order.Add("stop " + kind); if (kind == ImpactKind.Crash) PushStops++; return true; }
        public void Release() { Releases++; Order.Add("release"); }
        public int CreateRattle(int hz, int ms)
        { RattleCreates++; Check(hz == 25 && ms == 250, "rattle shape changed"); Order.Add("create rattle"); return RattleCreateFails ? -1 : 5; }
        public bool PlayRattle(int slot, float magnitude, float hz, int fadeMs)
        {
            Check(slot == 5, "rattle used another slot");
            RattlePlays++; RattleMagnitude = magnitude; RattleHz = hz; RattleFade = fadeMs; Order.Add("play rattle");
            return !RattlePlayFails;
        }
        public bool StopRattle(int slot) { RattleStops++; Order.Add("stop rattle"); return !RattleStopFails; }
    }

    static void Shape()
    {
        var o = new Output(); var trace = new List<ImpactDelivery>();
        var m = new ImpactMixer(o, () => o.Now, trace.Add);
        m.Prepare(true, true, true, false); Check(o.RattleCreates == 0, "rattle allocated while driving");
        m.Prepare(true, true, true, true);
        Check(o.RattleCreates == 1 && o.Order.IndexOf("create rattle") > o.Order.IndexOf("create Crash"), "rattle not prepared after the push slot");
        m.Prepare(true, true, true, true); Check(o.RattleCreates == 1 && m.RattleStatus == "ready", "rattle rebuilt or not ready");

        Check(m.Trigger(ImpactKind.Crash, 1, 50, 10) == ImpactResult.Accepted, "crash rejected");
        Check(o.Push == .5f && o.RattleMagnitude == .25f && o.RattleHz == 25 && o.RattleFade == 150, "crash no longer plays F");
        Check(o.Order.IndexOf("play rattle") > o.Order.IndexOf("play Crash"), "rattle started before the push");
        Check(trace[trace.Count - 1].Rattle && trace[trace.Count - 1].DurationMs == 250, "start report lacks the rattle");
        o.Now = 100.2; m.Tick(10.2); Check(o.PushStops == 0 && o.RattleStops == 0, "cue cut before the rattle ended");
        o.Now = 100.251; m.Tick(10.251); Check(o.PushStops == 1 && o.RattleStops == 1, "cue end did not stop both effects");
        Check(m.Counts(ImpactKind.Crash).EarlyStops == 0, "natural 250 ms end counted as early");

        // A stronger landing during the rattle tail stops both before playing.
        o.Now = 101; m.Trigger(ImpactKind.Crash, .2f, 50, 11);
        o.Now = 101.15; Check(m.Trigger(ImpactKind.Landing, 1, 20, 11.15) == ImpactResult.Accepted, "stronger landing lost to the rattle tail");
        Check(o.RattleStops == 2 && o.Order.LastIndexOf("stop rattle") < o.Order.LastIndexOf("play Landing"), "landing played over a live rattle");
        Check(m.Counts(ImpactKind.Crash).EarlyStops == 1, "replacement during the rattle not counted early");
        o.Now = 102; m.Tick(12); int rattles = o.RattlePlays; m.Trigger(ImpactKind.Landing, 1, 20, 12);
        Check(o.RattlePlays == rattles, "landing played the crash rattle");
        m.Shutdown(); Check(o.Releases == 1, "shutdown left impact slots");
    }

    static void Fallbacks()
    {
        var o = new Output { RattleCreateFails = true }; var m = new ImpactMixer(o, () => o.Now); m.Prepare(true, true, true, true);
        Check(m.RattleStatus == "unavailable; push only", "missing rattle not reported");
        Check(m.Trigger(ImpactKind.Crash, 1, 50, 10) == ImpactResult.Accepted && o.RattlePlays == 0 && o.Push == .5f, "push lost without a rattle");
        o.Now = 100.121; m.Tick(10.121); Check(o.PushStops == 1, "push-only crash held the wheel for 250 ms");

        o = new Output { RattlePlayFails = true }; m = new ImpactMixer(o, () => o.Now); m.Prepare(true, true, true, true);
        Check(m.Trigger(ImpactKind.Crash, 1, 50, 10) == ImpactResult.Accepted && o.RattleStops == 1, "rejected rattle failed the crash or kept playing");
        Check(m.RattleStatus.StartsWith("rejected"), "rejected rattle not reported");
        o.Now = 100.121; m.Tick(10.121); Check(o.PushStops == 1, "rejected rattle extended the cue");
        o.Now = 101; m.Trigger(ImpactKind.Crash, 1, 50, 11); Check(o.RattlePlays == 1, "rejected rattle retried automatically");

        o = new Output { RattleStopFails = true }; m = new ImpactMixer(o, () => o.Now); m.Prepare(true, true, true, true);
        m.Trigger(ImpactKind.Crash, 1, 50, 10); o.Now = 100.3; m.Tick(10.3);
        Check(o.Releases == 1 && !m.Available(ImpactKind.Crash) && !m.Available(ImpactKind.Landing), "failed rattle stop left slots usable");

        o = new Output(); m = new ImpactMixer(o, () => o.Now); m.Prepare(true, true, true, true);
        m.Prepare(false, false, true, true); Check(o.Releases == 1, "disabling impacts kept the rattle");
        m.Prepare(false, true, true, true); Check(o.RattleCreates == 2, "re-enabling crashes did not rebuild the rattle");
    }

    static void Production()
    {
        int creates = Native.RattleCreates, plays = Native.RattlePlays, stops = Native.RattleStops;
        var car = CrashTests.SetUp();
        Check(Native.RattleCreates == creates + 1, "production did not prepare the rattle while idle");
        CrashTests.CollisionHook.Invoke(null, new object[] { new PlayerCollider { body = car.body }, CrashTests.Hit() });
        Check(Native.RattlePlays == plays + 1 && Native.LastRattle == .25f && Native.LastRattleHz == 25 && Native.LastRattleFade == 150,
            "production crash did not play the push + rattle");
        GameState.IsDriving = false; ImpactController.Tick();
        Check(Native.RattleStops == stops + 1, "pause left the rattle running");
        var text = new StringBuilder(); ImpactController.AppendSupport(text);
        Check(text.ToString().Contains("25 Hz rattle at half magnitude") && text.ToString().Contains("(rattle ready)"), "support file lacks the rattle");
        ImpactController.Shutdown();
    }

    public static int Run() { Shape(); Fallbacks(); Production(); return assertions; }
}
