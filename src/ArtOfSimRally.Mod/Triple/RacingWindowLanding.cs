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
    internal bool BlocksOutputResize => _pending;
    internal RacingWindowLanding(IHost host) { _host = host; }

    internal void Update()
    {
        var monitors = new List<MonitorRect>(_host.Monitors);
        var topology = string.Join(";", monitors.OrderBy(m => m.X).ThenBy(m => m.Y)
            .ThenBy(m => m.Width).ThenBy(m => m.Height).ThenBy(m => m.Primary));
        if (_topology != topology)
        {
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
        var decision = WindowLandingPolicy.Decide(monitors, RacingRowPolicy.Select(monitors), false,
            opening.X, opening.Y, opening.Width, opening.Height, null, null, null);
        _admitted = true;
        _host.Log("Window landing: " + decision.Reason + "; start monitor unchanged (persistence deferred).");
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
