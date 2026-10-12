using System;

namespace ArtOfSimRally.Mod.Shim
{
    // Slice B: one bounded latched Core trial idle on Art's main menu. Action-level injections only,
    // navDown then navUp (every other action is masked at open, so nothing can confirm, start a race
    // or change a value); the menu selection is read back after each, then the session ends. Pure:
    // the probe supplies the selection readback and an atomic batch writer.
    internal sealed class LatchedTrial
    {
        internal enum Step { Admission, Down, Up, Ending, Passed, Failed }
        internal const int NavUp = 13, NavDown = 14, StepFrames = 600, SettleFrames = 15;
        // Core run states in the frame (ShimFrame.Run).
        internal const byte Waiting = 2, Completed = 4;
        private readonly ShimAxisConsumer consumer;
        private readonly Func<string> selection;
        private readonly Action<string> writeBatch;
        private readonly string nonce;
        private int batch, frames, settled;
        private bool seen;
        internal Step Current { get; private set; } = Step.Admission;
        internal string Before { get; private set; }
        internal string AfterDown { get; private set; }
        internal string AfterUp { get; private set; }
        internal string Failure { get; private set; }

        internal LatchedTrial(ShimAxisConsumer consumer, Func<string> selection, Action<string> writeBatch, string nonce)
        {
            this.consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            this.selection = selection ?? throw new ArgumentNullException(nameof(selection));
            this.writeBatch = writeBatch ?? throw new ArgumentNullException(nameof(writeBatch));
            this.nonce = nonce ?? throw new ArgumentNullException(nameof(nonce));
        }

        // Once per Unity frame, after this frame's shared snapshot was published.
        internal void Tick(int frame)
        {
            if (Current == Step.Passed || Current == Step.Failed) return;
            if (consumer.Terminal) { Fail("shared session stopped: " + consumer.Status); return; }
            if (++frames > StepFrames) { Fail("no progress in " + Current + " within " + StepFrames + " frames"); return; }
            switch (Current)
            {
                case Step.Admission:
                    if (!consumer.Started || consumer.OriginalFrame.Run != Waiting) return;
                    Before = selection();
                    if (Before == null) { Fail("no menu selection to read back"); return; }
                    Send("inject action navDown 1 ms=300", Step.Down);
                    return;
                case Step.Down:
                    if (!Released(NavDown, frame)) return;
                    AfterDown = selection();
                    if (AfterDown == null || AfterDown == Before) { Fail("navDown did not move the menu selection (" + Before + " -> " + (AfterDown ?? "none") + ")"); return; }
                    Send("inject action navUp 1 ms=300", Step.Up);
                    return;
                case Step.Up:
                    if (!Released(NavUp, frame)) return;
                    AfterUp = selection();
                    if (AfterUp != Before) { Fail("navUp did not return the menu selection (" + Before + " -> " + (AfterUp ?? "none") + ")"); return; }
                    Send("end", Step.Ending);
                    return;
                case Step.Ending:
                    // A few more ticks after Completed let Core's control worker finish its journal, which a
                    // resolved stop requires (its stop waits only briefly).
                    if (consumer.OriginalFrame.Run == Completed && ++settled >= 2 * SettleFrames) Current = Step.Passed;
                    return;
            }
        }

        // Injected, then released, then a few frames for the menu to settle.
        private bool Released(int action, int frame)
        {
            if (consumer.Injected(action, frame)) { seen = true; settled = 0; return false; }
            return seen && consumer.Value(action, frame) == 0f && ++settled >= SettleFrames;
        }
        private void Send(string command, Step next)
        {
            writeBatch("nonce=" + nonce + "\nbatch=" + (++batch) + "\n" + command + "\n");
            Current = next; frames = 0; settled = 0; seen = false;
        }
        private void Fail(string why) { Failure = why; Current = Step.Failed; }

        // What the latched open's fence byte asserts, each fact observed in the running game.
        internal sealed class Fences
        {
            internal bool NoForceArmed { get; set; }
            internal bool SettingsWritesSuppressed { get; set; }
            internal bool TelemetryIdle { get; set; }
            internal string MissingHooks { get; set; }
            internal bool Compatible { get; set; }
            internal bool Primary { get; set; }
            internal bool MainMenu { get; set; }
            internal bool Driving { get; set; }
            internal bool Focused { get; set; }
        }
        // Null to open now. Otherwise the unmet fence, with wait set when only reaching the main menu or
        // focus is still pending (the caller bounds the wait); any other unmet fence refuses.
        internal static string Admission(Fences f, out bool wait)
        {
            string unmet = Unfenced(f);
            wait = false;
            if (unmet == null || f == null || f.Driving || (f.MainMenu && f.Focused)) return unmet;
            var later = new Fences { NoForceArmed = f.NoForceArmed, SettingsWritesSuppressed = f.SettingsWritesSuppressed, TelemetryIdle = f.TelemetryIdle,
                MissingHooks = f.MissingHooks, Compatible = f.Compatible, Primary = f.Primary, MainMenu = true, Focused = true };
            wait = Unfenced(later) == null;
            return unmet;
        }
        // romi 29497: with the explicit diagnostic request flag only, a Trial may end the title splash
        // once (the game's own EndSplashScreen) when that is all that stands between it and its fences.
        internal static bool AdvanceTitle(bool requested, bool alreadyAdvanced, bool onSplash, Fences f)
        {
            if (!requested || alreadyAdvanced || !onSplash || f == null || f.MainMenu) return false;
            bool wait;
            return Admission(f, out wait) != null && wait;
        }
        // Null when every fence holds; otherwise the first that does not.
        internal static string Unfenced(Fences f)
        {
            if (f == null) return "fences not observed";
            if (!f.NoForceArmed) return "native no-force latch not armed";
            if (!f.SettingsWritesSuppressed) return "settings writes are not suppressed";
            if (!f.TelemetryIdle) return "telemetry is sending";
            if (!String.IsNullOrEmpty(f.MissingHooks)) return "input hooks missing: " + f.MissingHooks;
            if (!f.Compatible) return "game build differs from the inspected action map";
            if (!f.Primary) return "the primary player's queries would not be suppressed";
            if (f.Driving || !f.MainMenu) return "not idle on the main menu";
            if (!f.Focused) return "game window not focused";
            return null;
        }
    }
}
