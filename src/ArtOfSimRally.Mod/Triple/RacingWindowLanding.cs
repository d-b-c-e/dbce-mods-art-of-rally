using System;
using System.Collections.Generic;
using System.Linq;
using Dbce.Wheel.Display;

namespace ArtOfSimRally.Mod.Triple;

// A -> B -> A is three observed episodes, not lifetime topology uniqueness.
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
    internal bool BlocksOutputResize => _pending;
    internal RacingWindowLanding(IHost host) { _host = host; }

    internal void Update()
    {
        var monitors = new List<MonitorRect>(_host.Monitors);
        var topology = string.Join(";", monitors.OrderBy(m => m.X).ThenBy(m => m.Y)
            .ThenBy(m => m.Width).ThenBy(m => m.Height).ThenBy(m => m.Primary));
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
            // A replacement HWND must independently satisfy the same observed contract.
            // Temporary minimize/ambiguity never consumes the pending placement.
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
