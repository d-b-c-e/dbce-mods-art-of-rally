using System;
using System.Threading;

namespace ArtOfSimRally.Mod.Shim
{
    internal struct DrivingValues
    {
        internal float Steer, Throttle, Brake, Clutch, Handbrake;
        internal bool StartEngine;
    }

    // The game's normalized axis sink. It never reads devices or files, resolves
    // bindings, changes settings, emits forces or rewrites a capture.
    internal sealed class ShimAxisConsumer
    {
        private readonly int ownerThread = Thread.CurrentThread.ManagedThreadId;
        private readonly bool[] owned = new bool[5];
        private ShimFrame current;
        private int gameFrame;
        internal bool Started { get; private set; }
        internal bool Terminal { get; private set; }
        internal string Status { get; private set; } = "no shared input session";
        internal ShimFrame OriginalFrame => current;

        // Bound actions outside this first axis slice must be marked Unsupported
        // by Core at open, never presented as Live with no game consumer.
        internal static string UnsupportedActions
        {
            get
            {
                var lines = new System.Text.StringBuilder();
                for (int i = 5; i < ShimFrame.Count; i++) lines.Append(ShimFrame.Names[i]).Append("=Art shared-input axis slice has no consumer for this action\n");
                return lines.ToString();
            }
        }

        internal bool Publish(byte[] bytes, int unityFrame, out string reason)
        {
            if (Thread.CurrentThread.ManagedThreadId != ownerThread) { reason = "shim sink called on another thread"; return false; }
            if (Terminal) { reason = Status; return false; }
            ShimFrame next;
            if (!ShimFrame.TryDecode(bytes, out next, out reason)) { Refuse(reason); return false; }
            if (next.Latched || next.SuppressNative || next.Run != 0)
            { reason = "Art axis slice has no verified test-session fences"; Refuse(reason); return false; }
            for (int i = 5; i < ShimFrame.Count; i++)
                if (next[i].Owned) { reason = "unsupported Art action: " + ShimFrame.Names[i]; Refuse(reason); return false; }
            if (Started)
            {
                if (unityFrame <= gameFrame || next.Sequence <= current.Sequence || next.TickMicroseconds < current.TickMicroseconds)
                { reason = "shim frame did not advance monotonically"; Refuse(reason); return false; }
                for (int i = 0; i < 5; i++)
                    if (next[i].Owned != owned[i]) { reason = "shim axis ownership changed after open"; Refuse(reason); return false; }
            }
            else for (int i = 0; i < 5; i++) owned[i] = next[i].Owned;
            current = next; gameFrame = unityFrame;
            Started = true; Status = "shared input axes (source candidate)"; reason = ""; return true;
        }

        private void Refuse(string reason)
        {
            // Before first acceptance the caller may remain native. Afterward a
            // transport fault retains ownership and can never revive an old read.
            Terminal = true; Status = reason;
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
            if (!independentNativeInput) game = default(DrivingValues);
            float steer = fresh && owned[0] ? current[0].Applied : 0f;
            if (steer != 0f) game.Steer = steer + steeringAlignment;
            // At exact centre preserve Art's physical steering-alignment effect
            // when the shim owns steering and no independent steering is active.
            else if (fresh && owned[0] && current[0].Status == ShimStatus.Live && game.Steer == 0f)
                game.Steer = steeringAlignment;
            if (fresh)
            {
                if (owned[1]) { game.Throttle = Math.Max(game.Throttle, current[1].Applied); game.StartEngine |= game.Throttle > 0f; }
                if (owned[2]) game.Brake = Math.Max(game.Brake, current[2].Applied);
                if (owned[3]) game.Clutch = Math.Max(game.Clutch, current[3].Applied);
                if (owned[4]) game.Handbrake = Math.Max(game.Handbrake, current[4].Applied);
            }
            return true;
        }
    }
}
