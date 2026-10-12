using System;
using System.Threading;

namespace ArtOfSimRally.Mod.Shim
{
    internal struct DrivingValues
    {
        internal float Steer, Throttle, Brake, Clutch, Handbrake;
        internal bool StartEngine;
    }

    // The game's normalized shared-action sink. It never reads devices or files,
    // resolves bindings, changes settings, emits forces or rewrites a capture.
    internal sealed class ShimAxisConsumer
    {
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly bool[] owned = new bool[ShimFrame.Count];
        private ShimFrame current;
        private int gameFrame, pendingShift;
        internal bool Started { get; private set; }
        internal bool Terminal { get; private set; }
        internal string Status { get; private set; } = "no shared input session";
        internal ShimFrame OriginalFrame => current;

        // Actions with an Art consumer: driving axes, sequential shifts, H-pattern gates
        // (validated against the current car), menu buttons and the two mod shortcuts.
        private static readonly string[] Reasons = BuildReasons();
        private static string[] BuildReasons()
        {
            var reasons = new string[ShimFrame.Count];
            reasons[12] = "Art has no select action";
            reasons[18] = "Art has no look-back action in the inspected build";
            reasons[19] = "Art reset is not a qualified shared action";
            reasons[20] = "Art has no horn";
            return reasons;
        }
        internal static bool Supported(int action) => action >= 0 && action < ShimFrame.Count && Reasons[action] == null;

        // Bound actions without an Art consumer must be marked Unsupported by Core
        // at open, never presented as Live with no game consumer.
        internal static string UnsupportedActions
        {
            get
            {
                var lines = new System.Text.StringBuilder();
                for (int i = 0; i < ShimFrame.Count; i++) if (!Supported(i)) lines.Append(ShimFrame.Names[i]).Append('=').Append(Reasons[i]).Append('\n');
                return lines.ToString();
            }
        }

        // A latched Core test session (injected actions with provenance) is accepted only when its
        // opener verified the fences and force latch; every frame must then be latched, and an
        // ordinary session never accepts one. Fixed before the first accepted frame.
        private bool acceptLatched;
        internal bool AcceptLatched
        {
            get => acceptLatched;
            set { if (Started || Terminal) throw new InvalidOperationException("latched acceptance is fixed at open"); acceptLatched = value; }
        }
        // Optional delivery mask (probe trials): owned actions outside it stay owned but are never
        // delivered, so neither the shared frame nor Art's own binding can drive them.
        private ulong deliverable = ulong.MaxValue;
        internal void DeliverOnly(params int[] actions)
        {
            if (Started || Terminal) throw new InvalidOperationException("the delivery mask is fixed at open");
            deliverable = 0; foreach (int a in actions) if (a >= 0 && a < ShimFrame.Count) deliverable |= 1UL << a;
        }
        private bool Delivers(int action) => (deliverable & (1UL << action)) != 0;
        private bool Delivered(int action) => owned[action] && Delivers(action);

        // True only while a started session owns the action; ownership is fixed at open.
        internal bool Owns(int action) => Started && action >= 0 && action < ShimFrame.Count && owned[action];
        // The accepted frame belongs to this Unity frame and the session is not terminal.
        internal bool Fresh(int unityFrame) => Started && !Terminal && unityFrame == gameFrame;
        internal bool Live(int action, int unityFrame) => Fresh(unityFrame) && Owns(action) && Delivers(action) && current[action].Status == ShimStatus.Live;
        // Core suppresses native input for its whole latched session: injected actions are the only input.
        internal bool LatchedDelivery(int unityFrame) => Fresh(unityFrame) && current.Latched && current.SuppressNative;
        internal bool Injected(int action, int unityFrame) => Live(action, unityFrame) && current[action].Injected;
        // A latched session reports its delivered actions test-neutral between injections: a known
        // released state, so press edges can arm on it (the first injection is not swallowed).
        internal bool KnownNeutral(int action, int unityFrame) => Fresh(unityFrame) && Owns(action) && Delivers(action) && current[action].Status == ShimStatus.TestNeutral;
        internal float Value(int action, int unityFrame) => Live(action, unityFrame) ? current[action].Value : 0f;
        // One queued sequential step per call; latched presses survive frames with no physics step.
        internal int TakeShift()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("shim shift taken on another thread");
            if (Terminal) { pendingShift = 0; return 0; }
            int step = Math.Sign(pendingShift); pendingShift -= step; return step;
        }
        internal void ClearShifts() => pendingShift = 0;

        internal bool Publish(byte[] bytes, int unityFrame, out string reason)
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) { reason = "shim sink called on another thread"; return false; }
            if (Terminal) { reason = Status; return false; }
            ShimFrame next;
            if (!ShimFrame.TryDecode(bytes, out next, out reason)) { Refuse(reason); return false; }
            if (acceptLatched ? !next.Latched : (next.Latched || next.SuppressNative || next.Run != 0))
            { reason = acceptLatched ? "latched session published an unlatched frame" : "Art shared input has no verified test-session fences"; Refuse(reason); return false; }
            for (int i = 0; i < ShimFrame.Count; i++)
                if (next[i].Owned && !Supported(i)) { reason = "unsupported Art action: " + ShimFrame.Names[i]; Refuse(reason); return false; }
            if (Started)
            {
                if (unityFrame <= gameFrame || next.Sequence <= current.Sequence || next.TickMicroseconds < current.TickMicroseconds)
                { reason = "shim frame did not advance monotonically"; Refuse(reason); return false; }
                for (int i = 0; i < ShimFrame.Count; i++)
                    if (next[i].Owned != owned[i]) { reason = "shim action ownership changed after open"; Refuse(reason); return false; }
            }
            else for (int i = 0; i < ShimFrame.Count; i++) owned[i] = next[i].Owned;
            // Core's press edges (rearmed, Live only); presses before the first accepted frame are not replayed.
            if (Started)
            {
                if (owned[ShiftUp] && Delivers(ShiftUp) && next[ShiftUp].Status == ShimStatus.Live && (next.Down & (1UL << ShiftUp)) != 0) pendingShift = Math.Min(pendingShift + 1, 2);
                if (owned[ShiftDown] && Delivers(ShiftDown) && next[ShiftDown].Status == ShimStatus.Live && (next.Down & (1UL << ShiftDown)) != 0) pendingShift = Math.Max(pendingShift - 1, -2);
            }
            current = next; gameFrame = unityFrame;
            Started = true; Status = "shared input actions (source candidate)"; reason = ""; return true;
        }
        internal const int ShiftUp = 5, ShiftDown = 6;

        private void Refuse(string reason)
        {
            // Before first acceptance the caller may remain native. Afterward a
            // transport fault retains ownership and can never revive an old read.
            Terminal = true; Status = reason; pendingShift = 0;
        }
        internal void Stop()
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("shim stop called on another thread");
            Refuse("shared input stopped; restart required");
        }

        internal bool Compose(int unityFrame, bool independentNativeInput, float steeringAlignment, ref DrivingValues game)
        {
            if (!Started) return false;
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) throw new InvalidOperationException("shim compose called on another thread");
            // A prior frame cannot prove isolation this frame. Clear suspect game
            // wheel values until the adapter supplies current independent sources.
            bool fresh = !Terminal && unityFrame == gameFrame;
            // A latched Core session suppresses native input: its injected actions are the only input.
            if (!independentNativeInput || current.SuppressNative) game = default(DrivingValues);
            float steer = fresh && Delivered(0) ? current[0].Applied : 0f;
            if (steer != 0f) game.Steer = steer + steeringAlignment;
            // At exact centre preserve Art's physical steering-alignment effect
            // when the shim owns steering and no independent steering is active.
            else if (fresh && Delivered(0) && current[0].Status == ShimStatus.Live && game.Steer == 0f)
                game.Steer = steeringAlignment;
            if (fresh)
            {
                if (Delivered(1)) { game.Throttle = Math.Max(game.Throttle, current[1].Applied); game.StartEngine |= game.Throttle > 0f; }
                if (Delivered(2)) game.Brake = Math.Max(game.Brake, current[2].Applied);
                if (Delivered(3)) game.Clutch = Math.Max(game.Clutch, current[3].Applied);
                if (Delivered(4)) game.Handbrake = Math.Max(game.Handbrake, current[4].Applied);
            }
            return true;
        }
    }
}
