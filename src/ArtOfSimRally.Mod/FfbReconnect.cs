using System;

namespace ArtOfSimRally.Mod
{
    // A native Ready flag describes an allocated handle, not a successful read.
    // One bounded recovery request per sustained outage; temporary reader closes
    // and failed retries must not replenish the budget. No hardware calls here.
    internal sealed class FfbReadRecovery
    {
        private string identity;
        private double failedSince = double.NaN, healthySince = double.NaN;
        private bool requested;
        public void Reset()
        { identity = null; failedSince = healthySince = double.NaN; requested = false; }

        public bool Observe(double now, string guid, bool? responsive)
        {
            if (double.IsNaN(now) || double.IsInfinity(now)) return false;
            if (!string.Equals(identity, guid, StringComparison.OrdinalIgnoreCase))
            { Reset(); identity = guid; }
            if (string.IsNullOrEmpty(guid)) return false;
            if (!responsive.HasValue)
            { failedSince = healthySince = double.NaN; return false; }
            if (responsive.Value)
            {
                failedSince = double.NaN;
                if (double.IsNaN(healthySince) || now < healthySince) healthySince = now;
                if (now - healthySince >= .5) requested = false;
                return false;
            }
            healthySince = double.NaN;
            if (double.IsNaN(failedSince) || now < failedSince) failedSince = now;
            if (requested || now - failedSince < 2) return false;
            requested = true;
            return true;
        }
    }

    // Startup/window races get a small retry budget. Never reconnect in a
    // driving frame or while assigning an axis, even if a timer has expired.
    internal sealed class FfbReconnect
    {
        private int remaining;
        private IntPtr window;
        private double focusedSince, nextAttempt;
        public bool Pending => remaining > 0;
        public void Request() { remaining = 5; window = IntPtr.Zero; nextAttempt = 0; }
        public bool TryBegin(double now, bool enabled, bool driving, bool assigning, IntPtr focusedWindow)
        {
            if (!enabled || driving || assigning || focusedWindow == IntPtr.Zero)
            { window = IntPtr.Zero; return false; }
            if (window != focusedWindow) { window = focusedWindow; focusedSince = now; }
            if (!Pending || now < nextAttempt || now - focusedSince < .5) return false;
            remaining--; nextAttempt = now + 5;
            return true;
        }
        public void Complete(bool connected) { if (connected) remaining = 0; }
        public void Cancel() { remaining = 0; window = IntPtr.Zero; }
    }
}
