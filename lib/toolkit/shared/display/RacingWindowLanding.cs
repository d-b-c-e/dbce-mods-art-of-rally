// MIT. Source-vendored window landing sequence for a game that opens its own window (Unity Mono: art of rally and
// #DRIVE Rally, through shared/display/unity/RacingWindow.cs): observe where the window opened, decide once per display
// topology with WindowLandingPolicy, resize to a window on the racing row's home display, place it when the engine has
// taken that size, and save the engine's start monitor when that is certain. Extracted from art of rally (2026-11-10).
// Pure: every Windows, engine and preference call goes through IHost. Needs LINQ and Func (not the Mono 2.0 profile).
#nullable disable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dbce.Wheel.Display
{
    /// <summary>
    /// The landing sequence, ticked by the host (about twice a second) while no span window is in use. A -> B -> A is
    /// three observed topology episodes. A temporary minimize or an ambiguous window never consumes a pending placement,
    /// and a replacement window must meet the same observed size before it is placed.
    /// </summary>
    internal sealed class RacingWindowLanding
    {
        internal interface IHost
        {
            float Now { get; }
            IReadOnlyList<MonitorRect> Monitors { get; }
            IList<EngineDisplay> EngineDisplays { get; }
            int? SavedMonitor { get; }
            string PersistenceBlock { get; }
            bool SaveMonitor(int? expected, int index);
            bool Observe(out Observation observation);
            void Resize(int width, int height);
            bool Place(long window, int x, int y);
            void Log(string message);
        }

        internal struct Observation
        {
            internal long Window;
            internal int X, Y, Width, Height, OffsetX, OffsetY, EngineWidth, EngineHeight;
            internal bool Windowed;
        }

        private readonly IHost _host;
        private string _topology;
        private bool _admitted;
        private bool _pending;
        private float _deadline;
        private int _x, _y, _width, _height;
        private MonitorRect? _openedOn;
        private bool _observedOpening, _topologyChanged;

        /// <summary>A resize this sequence asked for is still settling: the host must not request another output size.</summary>
        internal bool BlocksOutputResize => _pending;

        internal RacingWindowLanding(IHost host) { _host = host; }

        internal void Update()
        {
            var monitors = new List<MonitorRect>(_host.Monitors);
            var topology = string.Join(";", monitors.OrderBy(m => m.X).ThenBy(m => m.Y)
                .ThenBy(m => m.Width).ThenBy(m => m.Height).ThenBy(m => m.Primary).Select(m => m.ToString()).ToArray());
            if (_topology != topology)
            {
                if (_topology != null) _topologyChanged = true;
                if (_pending) Finish("topology changed; stale placement cancelled");
                _topology = topology;
                _admitted = false;
            }
            if (_pending)
            {
                if (_host.Now >= _deadline) { Finish("resize/placement deadline expired"); return; }
                if (!_host.Observe(out var current) || !current.Windowed ||
                    current.EngineWidth != _width || current.EngineHeight != _height ||
                    current.Width != _width || current.Height != _height) return;
                var placed = _host.Place(current.Window, _x - current.OffsetX, _y - current.OffsetY);
                Finish(placed ? "client placement accepted" : "Windows refused placement; no retry this episode");
                return;
            }
            if (_admitted || !_host.Observe(out var opening)) return;
            if (WindowLandingPolicy.DisplayOf(monitors, opening.X, opening.Y, opening.Width, opening.Height) == null) return;
            if (!_observedOpening)
            {
                _observedOpening = true;
                // A first observation after a topology change cannot prove launch order.
                if (!_topologyChanged) _openedOn = WindowLandingPolicy.DisplayOf(monitors,
                    opening.X, opening.Y, opening.Width, opening.Height);
            }
            int? saved = null;
            var preferenceAvailable = true;
            try { saved = _host.SavedMonitor; }
            catch (Exception failure)
            {
                preferenceAvailable = false;
                _host.Log("Window landing: start monitor read failed (" + failure.GetType().Name +
                    "); preference unavailable; persistence suppressed for this episode.");
            }
            var decision = WindowLandingPolicy.Decide(monitors, RacingRowPolicy.Select(monitors), false,
                opening.X, opening.Y, opening.Width, opening.Height,
                _topologyChanged || !preferenceAvailable ? null : _openedOn, _host.EngineDisplays, saved);
            _admitted = true;
            _host.Log("Window landing: " + decision.Reason + ". " + decision.PersistReason + ".");
            if (preferenceAvailable && decision.Persist.HasValue)
            {
                var blocked = _host.PersistenceBlock;
                if (blocked != null) _host.Log("Window landing: start monitor unchanged: " + blocked + ".");
                else if (_host.SaveMonitor(saved, decision.Persist.Value))
                    _host.Log("Window landing: start monitor " + (saved?.ToString() ?? "unset (0)") +
                        " -> " + decision.Persist.Value + ".");
                else _host.Log("Window landing: start monitor write rejected or failed; see preference result above.");
            }
            if (!decision.Move) return;
            _x = decision.X; _y = decision.Y; _width = decision.Width; _height = decision.Height;
            _pending = true;
            _deadline = _host.Now + 10f;
            _host.Resize(_width, _height);
        }

        private void Finish(string reason)
        {
            _pending = false;
            _host.Log("Window landing: " + reason + ".");
        }
    }

    /// <summary>Whether this launch may save the engine's start monitor. Presence is conservative: even a disabled or
    /// stale developer marker suppresses the write.</summary>
    internal static class LandingLaunchPolicy
    {
        /// <param name="mute">DBCE_FFB_MUTE, set for unattended runs.</param>
        /// <param name="gameMarker">The game's own developer or test-launch reason (a probe or recorder marker), or null.</param>
        internal static string Block(string mute, string gameMarker, bool editor, IEnumerable<string> arguments)
        {
            if (!string.IsNullOrEmpty(mute)) return "muted-launch marker";
            if (!string.IsNullOrEmpty(gameMarker)) return gameMarker;
            if (editor) return "editor launch";
            foreach (var arg in arguments)
                if (string.Equals(arg, "-batchmode", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-nographics", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "-runTests", StringComparison.OrdinalIgnoreCase)) return "batch/test launch";
            return null;
        }
    }

    /// <summary>The start-monitor write: refused when the launch is blocked or the preference changed under us, and the
    /// previous in-memory value restored when the write fails. Delegates are the engine boundary.</summary>
    internal sealed class LandingPersistence
    {
        private readonly Func<string> _inspect;
        private readonly string _initialBlock;
        internal LandingPersistence(Func<string> inspect) { _inspect = inspect; _initialBlock = Inspect(); }
        internal string Block => _initialBlock ?? Inspect();

        private string Inspect()
        {
            try { return _inspect(); }
            catch { return "launch classification unavailable"; }
        }

        internal static bool MarkerPresent(string path, Func<string, FileAttributes> attributes)
        {
            if (string.IsNullOrEmpty(path)) throw new IOException("Marker path unavailable");
            try { attributes(path); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            // Access, invalid-path and other I/O errors must reach classification's catch.
        }

        internal bool Save(int? expected, int index, Func<int?> read, Action<int> set,
            Action remove, Action flush, Action<string> log)
        {
            var attempted = false;
            try
            {
                var blocked = Block;
                if (blocked != null) { log("Window landing: start monitor write rejected: " + blocked + "."); return false; }
                if (read() != expected) { log("Window landing: start monitor write rejected: preference changed."); return false; }
                attempted = true;
                set(index);
                flush();
                return true;
            }
            catch (Exception failure)
            {
                log("Window landing: start monitor write failed (" + failure.GetType().Name + "); persistence unconfirmed.");
                if (attempted)
                {
                    try
                    {
                        if (expected.HasValue) set(expected.Value); else remove();
                        log("Window landing: previous in-memory start monitor restored; disk state unconfirmed.");
                    }
                    catch (Exception restore)
                    {
                        log("Window landing: start monitor restore failed (" + restore.GetType().Name + "); memory/disk state unconfirmed.");
                    }
                }
                return false;
            }
        }
    }
}
