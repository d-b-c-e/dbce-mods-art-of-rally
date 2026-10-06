using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Dbce.Wheel.Ffb;

namespace CrashFeel;

// Plays kicks through the pinned toolkit exactly as the mod would: finite
// constant/sine slots created up front, plus a steering stream at ~60 Hz that
// carries the optional cornering load. One kick at a time.
sealed class Engine : IDisposable
{
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] static extern uint timeEndPeriod(uint ms);
    const double StreamPeriodMs = 1000.0 / 60;

    readonly Dictionary<int, int> _constant = new();
    readonly Dictionary<(int, int), int> _periodic = new();
    readonly object _gate = new();
    readonly Stopwatch _clock = Stopwatch.StartNew();
    readonly Thread _thread;
    volatile bool _running = true;
    float _load;
    Step[]? _plan; double _planStart; int _planLength, _next; string _planLabel = "";
    readonly List<string> _results = new();

    public ConcurrentQueue<string> Finished { get; } = new();
    public bool Busy { get { lock (_gate) return _plan != null; } }

    Engine() { _thread = new Thread(Loop) { IsBackground = true, Name = "CrashFeel stream", Priority = ThreadPriority.AboveNormal }; }

    public static Engine? Connect(WheelFfbNative.DeviceInfo device, IntPtr hwnd, out string message)
    {
        if (!WheelFfbNative.Initialise(device.Name, device.Index, unchecked((int)hwnd.ToInt64()), device.InstanceGuid))
        { message = "Connect failed: " + WheelFfbNative.LastError; return null; }
        var engine = new Engine();
        var missing = new List<string>();
        foreach (int ms in Candidates.ConstantSlots)
        {
            int slot = WheelFfbNative.CreateConstantBurst(ms);
            if (slot >= 0) engine._constant[ms] = slot; else missing.Add(ms + " ms push");
        }
        foreach (var (hz, ms) in Candidates.PeriodicSlots)
        {
            int slot = WheelFfbNative.CreatePeriodicBurst(hz, ms);
            if (slot >= 0) engine._periodic[(hz, ms)] = slot; else missing.Add(hz + " Hz sine");
        }
        engine._thread.Start();
        message = "Connected to " + device.Name + ", native " + WheelFfbNative.Version +
            (missing.Count == 0 ? "; all effect slots created" : "; could not create: " + string.Join(", ", missing));
        return engine;
    }

    public bool Has(Step step) => step.Kind switch
    {
        StepKind.Constant => _constant.ContainsKey(step.SlotMs),
        StepKind.Periodic => _periodic.ContainsKey((step.Hz, step.SlotMs)),
        _ => true,
    };

    public void SetLoad(float load) { lock (_gate) _load = load; }

    public bool Play(string label, Step[] plan)
    {
        lock (_gate)
        {
            if (_plan != null) return false;
            _plan = plan; _planLength = Candidates.LengthMs(plan); _next = 0; _planLabel = label;
            _planStart = _clock.Elapsed.TotalMilliseconds; _results.Clear();
            return true;
        }
    }

    // Stops every kick and the load. Safe from any thread.
    public void Stop()
    {
        lock (_gate)
        {
            _plan = null; _load = 0;
            foreach (int slot in _constant.Values) WheelFfbNative.StopConstantBurst(slot);
            foreach (int slot in _periodic.Values) WheelFfbNative.StopPeriodicBurst(slot);
            WheelFfbNative.SetForce(0);
        }
    }

    void Loop()
    {
        timeBeginPeriod(1);
        try
        {
            double nextStream = 0;
            while (_running)
            {
                double now = _clock.Elapsed.TotalMilliseconds;
                lock (_gate)
                {
                    float streamKick = 0;
                    if (_plan != null)
                    {
                        double t = now - _planStart;
                        while (_next < _plan.Length && _plan[_next].AtMs <= t) Execute(_plan[_next++], t);
                        foreach (var step in _plan)
                            if (step.Kind == StepKind.Stream && t >= step.AtMs && t < step.EndMs) streamKick += step.Magnitude;
                        if (t >= _planLength + StreamPeriodMs)
                        {
                            Finished.Enqueue(_planLabel + " | " + string.Join("; ", _results));
                            _plan = null;
                        }
                    }
                    if (now >= nextStream)
                    {
                        float force = Math.Clamp(_load + streamKick, -1f, 1f);
                        WheelFfbNative.SetForce((int)Math.Round(force * WheelFfbNative.ForceMax));
                        nextStream = Math.Max(nextStream + StreamPeriodMs, now);
                    }
                }
                Thread.Sleep(1);
            }
        }
        finally { timeEndPeriod(1); }
    }

    void Execute(Step step, double t)
    {
        bool ok = step.Kind switch
        {
            StepKind.Constant => WheelFfbNative.PlayConstantBurst(_constant[step.SlotMs], step.Magnitude),
            StepKind.Periodic => WheelFfbNative.PlayShapedPeriodicBurst(_periodic[(step.Hz, step.SlotMs)],
                step.Magnitude, step.Hz, step.PhaseHundredths, step.FadeMs),
            _ => true,
        };
        string what = step.Kind == StepKind.Stream ? "steering+" + step.Magnitude.ToString("0.00") + " for " + step.ForMs + " ms"
            : step.Kind + " " + (step.Kind == StepKind.Periodic ? step.Hz + " Hz " : "") + step.SlotMs + " ms " + step.Magnitude.ToString("0.00");
        _results.Add(what + " at " + t.ToString("0") + " ms " + (ok ? "accepted" : "REJECTED 0x" + WheelFfbNative.LastHResult.ToString("X8")));
    }

    public void Dispose()
    {
        _running = false;
        _thread.Join(500);
        Stop();
        WheelFfbNative.ReleaseConstantBursts();
        WheelFfbNative.ReleasePeriodics();
        WheelFfbNative.ShutdownAll();
    }
}
