namespace Dbce.Wheel.Shim;

/// <summary>One ordinary extras-enabled host, one coherent tick per engine frame, on its owner's thread.
/// No device reads, injected/latched run, capture or fallback host is created here.</summary>
public sealed class ShimExtrasSession
{
    public delegate int NativeTick(uint contexts, byte[] common, byte[] extras);
    public const double MaxSequenceAgeSeconds = .25;
    private readonly NativeTick _tick;
    private readonly Action _stop;
    private readonly byte[] _common = new byte[ShimAbi2.FrameSize], _extras = new byte[ShimExtrasAbi.FrameSize];
    private int _engineFrame = int.MinValue;
    private ulong _sequence;
    private double _changedAt = double.NaN, _now = double.NegativeInfinity;
    public ShimExtrasInfo Info { get; }
    public ShimFrame? Common { get; private set; }
    public ShimExtrasFrame? Extras { get; private set; }
    public bool Stopped { get; private set; }
    public string? Failure { get; private set; }

    public ShimExtrasSession(ShimExtrasInfo info, NativeTick tick, Action stop)
    { Info = info ?? throw new ArgumentNullException(nameof(info)); _tick = tick ?? throw new ArgumentNullException(nameof(tick)); _stop = stop ?? throw new ArgumentNullException(nameof(stop)); }

    public void Tick(int engineFrame, double now, uint contexts)
    {
        if (Stopped || engineFrame == _engineFrame) return;
        _engineFrame = engineFrame; Common = null; Extras = null;
        try
        {
            if (!double.IsFinite(now) || now < _now) throw new InvalidDataException("Extras clock is invalid or moved backwards.");
            _now = now; Info.ValidateContexts(contexts);
            Array.Clear(_common); Array.Clear(_extras);
            BitConverter.GetBytes(ShimAbi2.FrameSize).CopyTo(_common, 0);
            BitConverter.GetBytes(ShimExtrasAbi.FrameSize).CopyTo(_extras, 0);
            int result = _tick(contexts, _common, _extras);
            if (result != 0) throw new InvalidDataException("dbce_shim_tick_extras returned " + result);
            var common = ShimAbi2.Decode(_common);
            var extras = ShimExtrasAbi.Decode(_extras, common, Info, contexts);
            if (!double.IsNaN(_changedAt) && common.Sequence < _sequence) throw new InvalidDataException("Extras sequence regressed.");
            if (common.Sequence != _sequence || double.IsNaN(_changedAt)) { _sequence = common.Sequence; _changedAt = now; }
            Common = common; Extras = extras;
        }
        catch (Exception e) { Failure = e.GetType().Name + ": " + e.Message; Stop(); }
    }

    public bool Fresh(int frame, double now) => !Stopped && Common != null && Extras != null && frame == _engineFrame &&
        double.IsFinite(now) && now >= _now && now - _changedAt <= MaxSequenceAgeSeconds;

    /// <summary>All components must be live together. Context changes within a frame refuse cached values.</summary>
    public bool TrySample(string id, int frame, double now, uint contexts, out float x, out float y)
    {
        x = y = 0;
        if (!Fresh(frame, now) || Extras!.Contexts != contexts || !Info.Destinations.TryGetValue(id, out var d) ||
            (Info.ContextMode == "observed" && d.ContextMask != 0 && (d.ContextMask & contexts) == 0)) return false;
        for (int i = 0; i < d.Components; i++) if (!Extras.Slots[d.FirstSlot + i].Live) return false;
        x = Extras.Slots[d.FirstSlot].Value;
        if (d.Components == 2) y = Extras.Slots[d.FirstSlot + 1].Value;
        return true;
    }

    public void Invalidate() { Common = null; Extras = null; }
    public void Stop()
    {
        if (Stopped) return;
        Stopped = true; Invalidate(); _stop();
    }
}
