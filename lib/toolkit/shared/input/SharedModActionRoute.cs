// MIT. Extracted from the qualified iRacing cached-frame route; provenance is recorded beside this file.
using System;

namespace Dbce.Wheel.Input
{
    [Flags]
    public enum SharedModAction { None = 0, TogglePanel = 1, StopFfb = 2 }

    // The existing F6/F8 consumers remain the only owners of their effects.
    // This route accepts edges from the already-cached Core frame, never a
    // second physical-device read. Missing/stale status cannot prove release.
    public sealed class SharedModActionRoute
    {
        private const ulong Menu = 1UL << 21, Mute = 1UL << 22;
        private const ulong Relevant = Menu | Mute;
        private ulong _suppressed, _previous;
        private int _context = int.MinValue;
        private bool _allowed;

        public void Reset()
        {
            _suppressed = _previous = 0;
            _context = int.MinValue;
            _allowed = false;
        }

        public SharedModAction Observe(ulong held, ulong live, int context, bool allowed)
        {
            held &= Relevant;
            live &= Relevant;
            if (!allowed || !_allowed || context != _context)
            {
                _suppressed |= held | _previous;
                _previous = 0;
                _context = context;
                _allowed = allowed;
                if (!allowed) return SharedModAction.None;
            }

            _suppressed &= ~(live & ~held); // only a verified Live release clears suppression
            _suppressed |= _previous & ~live; // a stale held action cannot make a new edge
            ulong accepted = held & live & ~_suppressed;
            ulong rising = accepted & ~_previous;
            _previous = accepted;
            SharedModAction result = SharedModAction.None;
            if ((rising & Menu) != 0) result |= SharedModAction.TogglePanel;
            if ((rising & Mute) != 0) result |= SharedModAction.StopFfb;
            return result;
        }
    }
}
