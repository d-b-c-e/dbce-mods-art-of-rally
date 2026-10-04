using System;
using System.Collections.Generic;
using Dbce.Wheel.Playback;
namespace ArtOfSimRally.Testing
{
    // Compatibility name; stream ordering is shared with the next game's adapter.
    internal sealed class SessionAligner<T> : SegmentCursor<T>
    {
        internal SessionAligner(List<KeyValuePair<int, T>> rows, Dictionary<int, string> names, string label,
            Func<float> clock, int earlyLimit = EarlyLimit, Func<string, bool> required = null)
            : base(rows, names, label, clock, earlyLimit, required) { }
    }
}
