using System;
using System.Collections.Generic;
using System.Linq;

namespace ArtOfSimRally.Testing
{
    /// <summary>Plays a tape segment by segment, gated on the live marker.</summary>
    internal sealed class SessionAligner<T>
    {
        private readonly List<List<T>> _segments = new List<List<T>>();
        private readonly List<int> _markers = new List<int>();
        private readonly Dictionary<int, string> _names;
        private readonly string _label;
        private int _segment = -1, _index, _early;
        private float _waitingSince = -1;
        private readonly Func<float> _clock;
        internal const int EarlyLimit = 10;
        internal const int SkipLimit = 3;
        internal const float WaitSeconds = 120f;

        internal SessionAligner(List<KeyValuePair<int, T>> rows, Dictionary<int, string> names, string label, Func<float> clock)
        {
            _names = names; _label = label; _clock = clock;
            foreach (var row in rows)
            {
                if (_markers.Count == 0 || _markers[_markers.Count - 1] != row.Key) { _markers.Add(row.Key); _segments.Add(new List<T>()); }
                _segments[_segments.Count - 1].Add(row.Value);
            }
            Total = rows.Count;
        }
        internal int Segment => _segment;
        internal int SegmentCount => _segments.Count;
        internal int Total { get; }
        internal int Consumed { get; private set; }
        internal int Skipped { get; private set; }
        internal bool PlayedOut => _segment >= 0 && _index >= _segments[_segment].Count;
        // Finished only once the live game has been seen on the final segment.
            internal bool Finished => _segments.Count > 0 && _segment == _segments.Count - 1 && _index >= _segments[_segment].Count && _early == 0;
        internal string Failed { get; private set; }

        private bool Take(out T value) { value = _segments[_segment][_index++]; Consumed++; _waitingSince = -1; return true; }
        private readonly Dictionary<string, int> _skippedBy = new Dictionary<string, int>();
        private void Advance()
        {
            if (_segment >= 0 && _index < _segments[_segment].Count)
            {
                int n = _segments[_segment].Count - _index;
                Skipped += n;
                string name = _names.TryGetValue(_markers[_segment], out var m) ? m : "?";
                _skippedBy[name] = (_skippedBy.TryGetValue(name, out var c) ? c : 0) + n;
            }
            _segment++; _index = 0;
        }
        internal int SkippedWhere(Func<string, bool> marker)
        {
            int total = _skippedBy.Where(p => marker(p.Key)).Sum(p => p.Value);
            // Rows left in the current segment count too.
            if (_segment >= 0 && _index < _segments[_segment].Count && _names.TryGetValue(_markers[_segment], out var m) && marker(m) && !Finished)
                total += _segments[_segment].Count - _index;
            return total;
        }
        internal int CurrentMarker => _segment >= 0 ? _markers[_segment] : -1;
        internal int RemainingInSegment => _segment >= 0 ? _segments[_segment].Count - _index : 0;
        internal bool InLastSegment => _segments.Count > 0 && _segment == _segments.Count - 1;

        internal bool Next(int liveMarker, out T value)
        {
            value = default;
            if (_segments.Count == 0 || Failed != null) return false;
            if (_segment >= 0 && _index < _segments[_segment].Count)
            {
                if (_markers[_segment] == liveMarker) { _early = 0; return Take(out value); }
                // A transition's input is taped in the next segment's first
                // frames (the marker changes that frame): play up to
                // EarlyLimit of them before the live game arrives.
                if (_early > 0 && _early < EarlyLimit && _segment > 0 && _markers[_segment - 1] == liveMarker) { _early++; return Take(out value); }
            }
            // The live game may pass through a short taped state without
            // stopping there: jump up to SkipLimit segments, counting the rows.
            for (int ahead = 1; ahead <= SkipLimit && _segment + ahead < _segments.Count; ahead++)
                if (_markers[_segment + ahead] == liveMarker)
                {
                    for (int i = 0; i < ahead; i++) Advance();
                    _early = 0; return Take(out value);
                }
            if (_segment >= 0 && PlayedOut && _markers[_segment] == liveMarker && _segment + 1 < _segments.Count) { Advance(); _early = 1; return Take(out value); }
            if (Finished) return false;
            if (_waitingSince < 0) _waitingSince = _clock();
            else if (_clock() - _waitingSince > WaitSeconds)
            {
                int next = _early > 0 ? _segment : _segment + 1;
                string expected = next >= 0 && next < _segments.Count && _names.TryGetValue(_markers[next], out var n) ? n : "?";
                Failed = _label + " waited " + WaitSeconds + " s for '" + expected + "'";
            }
            return false;
        }
    }
}
