// MIT. Portable C# 7.3 source, vendored beside a mod's pinned Dbce.Wheel.Ffb.dll / WheelFfb.dll pair. No Unity,
// loader, reader or force model. Extracted from Truckful cf8d761 and Over the Hill d7fd2c2, whose output state
// machines and sinks were identical apart from the tick type and the backward-tick rule (README).
// Legacy hosts have no nullable annotations; these API arguments intentionally accept null.
#pragma warning disable CS8600, CS8601, CS8603, CS8604, CS8618, CS8625
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Dbce.Wheel.Ffb.Output
{
    /// <summary>Test seam around the shared WheelFfbNative binding: open the exact device for a window, write one
    /// command, request zero/stop/shutdown, and report whether the opened device is still ready.</summary>
    public sealed class FfbSink
    {
        public FfbSink(Func<Guid, int, bool> open, Func<int, bool> write, Action stop, Func<bool> ready)
        {
            Open = open ?? throw new ArgumentNullException(nameof(open));
            Write = write ?? throw new ArgumentNullException(nameof(write));
            Stop = stop ?? throw new ArgumentNullException(nameof(stop));
            Ready = ready ?? throw new ArgumentNullException(nameof(ready));
        }
        public Func<Guid, int, bool> Open { get; }
        public Func<int, bool> Write { get; }
        public Action Stop { get; }
        public Func<bool> Ready { get; }
    }

    /// <summary>Model request, integer command, attempted and accepted are separate facts. Acceptance is not torque.</summary>
    public readonly struct FfbDelivery
    {
        public FfbDelivery(string status, float modelRequest = 0, int command = 0, bool attempted = false, bool accepted = false)
        { Status = status; ModelRequest = modelRequest; Command = command; Attempted = attempted; Accepted = accepted; }
        public string Status { get; }
        public float ModelRequest { get; }
        public int Command { get; }
        public bool Attempted { get; }
        public bool Accepted { get; }
        public FfbDelivery WithModelRequest(float request) => new FfbDelivery(Status, request, Command, Attempted, Accepted);
        public override string ToString() =>
            Status + "; request=" + ModelRequest.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
            "; command=" + Command + "; attempted=" + Attempted + "; accepted=" + Accepted;
    }

    /// <summary>One game-model result for the output gate. Epoch names the vehicle/session; Tick identifies one
    /// completed physics step and must be finite and increase within an epoch.</summary>
    public readonly struct FfbSource
    {
        public FfbSource(bool available, float request, string epoch, double tick)
        { Available = available; Request = request; Epoch = epoch; Tick = tick; }
        public bool Available { get; }
        public float Request { get; }
        public string Epoch { get; }
        public double Tick { get; }
    }

    /// <summary>
    /// One command per new completed physics tick, only while the caller's gates hold. A stale source (100 ms), a
    /// changed epoch/device/window, a lost device or any failure requests zero/stop/shutdown through the sink; failures
    /// wait 5 s before another open. A backward tick suspends, and the next fresh tick starts again from there. A clock
    /// fault or a throwing cleanup latches force off for the process. A stop is a request to the driver, never proof of
    /// zero physical force.
    /// </summary>
    public sealed class FfbOutput : IDisposable
    {
        public const double SourceMaxAge = .1, RetrySeconds = 5;
        private readonly FfbSink _sink;
        private readonly bool _enabled;
        private bool _muted, _opened, _touched, _disposed, _ticked;
        private Guid _device;
        private int _window;
        private string _epoch;
        private double _tick, _lastSourceAt, _lastNow = double.NegativeInfinity, _retryAt;
        public FfbDelivery Last { get; private set; } = new FfbDelivery("Off");
        public bool CanAttempt => _enabled && !_muted && !_disposed;
        public FfbOutput(FfbSink sink, bool enabled, bool muted)
        { _sink = sink ?? throw new ArgumentNullException(nameof(sink)); _enabled = enabled; _muted = muted; }

        public FfbDelivery Update(FfbSource source, bool eligible, Guid? target, int window, double now)
        {
            float request = source.Request;
            if (!CanAttempt)
            { Suspend(_disposed ? "Stopped" : _muted ? "Process force mute" : "Off"); return Last = Last.WithModelRequest(request); }
            if (!Finite(now) || now < _lastNow) { LatchMute("Output clock invalid"); return Last; }
            _lastNow = now;
            if (!eligible || !source.Available || !Finite(request) || Math.Abs(request) > 1 || !Finite(source.Tick) ||
                string.IsNullOrEmpty(source.Epoch) || !target.HasValue || target.Value == Guid.Empty || window == 0)
            { Suspend("Output unavailable; current source/input/window/device required"); return Last = Last.WithModelRequest(request); }
            if (_epoch != source.Epoch || _device != target.Value || _window != window)
            {
                Suspend("Source or output identity changed");
                _epoch = source.Epoch; _device = target.Value; _window = window; _ticked = false;
                if (!CanAttempt) return Last = Last.WithModelRequest(request); // A throwing cleanup latched force off.
            }
            if (_ticked && source.Tick == _tick)
            {
                if (now - _lastSourceAt > SourceMaxAge) Suspend("Completed physics source stale");
                else Last = new FfbDelivery("No new completed physics tick", request);
                return Last = Last.WithModelRequest(request);
            }
            if (_ticked && source.Tick < _tick) { Suspend("Physics tick moved backward"); _ticked = false; return Last; }
            _tick = source.Tick; _ticked = true; _lastSourceAt = now;
            if (now < _retryAt) return Last = new FfbDelivery("Waiting after output failure", request);
            int command = (int)Math.Round(request * WheelFfbNative.ForceMax);
            bool attempted = false;
            try
            {
                if (_opened && !_sink.Ready()) { Suspend("Output device lost"); _retryAt = now + RetrySeconds; return Last; }
                if (!_opened)
                {
                    _touched = true;
                    if (!_sink.Open(_device, _window)) { Suspend("Shared output open refused"); _retryAt = now + RetrySeconds; return Last; }
                    _opened = true;
                }
                attempted = true;
                bool accepted = _sink.Write(command);
                var receipt = new FfbDelivery(accepted ? "Native command accepted; physical output unverified" : "Native command refused",
                    request, command, true, accepted);
                if (!accepted) { Suspend("Native command refused"); _retryAt = now + RetrySeconds; }
                return Last = receipt;
            }
            catch (Exception)
            {
                Suspend("Shared output exception"); _retryAt = now + RetrySeconds;
                return Last = new FfbDelivery("Shared output exception; native acceptance unknown", request, attempted ? command : 0, attempted, false);
            }
        }

        public void Suspend(string reason)
        {
            bool stopRequested = _touched;
            if (_touched)
            {
                try { _sink.Stop(); }
                catch (Exception) { _muted = true; reason += "; cleanup call failed; process force mute"; }
            }
            _opened = _touched = false;
            Last = new FfbDelivery(reason + (stopRequested ? "; zero/stop/shutdown requested" : ""));
        }

        public void LatchMute(string reason) { _muted = true; Suspend(reason); }
        public void Dispose() { if (_disposed) return; _disposed = true; Suspend("Stopped"); }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>
    /// The existing shared output binding for a pinned managed/native pair beside the mod: hash both files, load,
    /// require the pinned native version, strict-GUID Initialise, 150 ms hold timeout, exit guards and an initial zero.
    /// Native ownership stays in the shared module. No read/aux device, no ShutdownAll, and no other producer's ready
    /// device or refused module binding is cancelled. Every WheelFfbNative use is inside a delegate body, so creating a
    /// sink loads neither the managed Dbce.Wheel.Ffb assembly nor the native module (a host may create it with FFB Off).
    /// </summary>
    public static class SharedFfbPair
    {
        public const long MaxFileBytes = 64L * 1024 * 1024;

        public static FfbSink CreateSink(string directory, string nativeSha256, string managedSha256, int nativeVersion)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Plugin directory required.", nameof(directory));
            RequirePin(nativeSha256, nameof(nativeSha256)); RequirePin(managedSha256, nameof(managedSha256));
            if (nativeVersion <= 0) throw new ArgumentOutOfRangeException(nameof(nativeVersion));
            bool ownsAttempt = false;
            return new FfbSink((guid, window) =>
            {
                // Called only after the caller's enabled/unmuted/current-source gates. Off never loads or enumerates.
                string native = Path.Combine(directory, "WheelFfb.dll");
                string managed = typeof(WheelFfbNative).Assembly.Location;
                if (WheelFfbNative.Ready || !Sha256(native).Equals(nativeSha256, StringComparison.OrdinalIgnoreCase) ||
                    !Sha256(managed).Equals(managedSha256, StringComparison.OrdinalIgnoreCase)) return false;
                if (!WheelFfbNative.Load(directory) || WheelFfbNative.Version != nativeVersion) return false;
                ownsAttempt = true;
                if (!WheelFfbNative.Initialise("", -1, window, guid)) return false;
                WheelFfbNative.HoldTimeout(150);
                if (!WheelFfbNative.ExitGuards()) return false; // The output gate requests cleanup after a partial open.
                return WheelFfbNative.SetForce(0);
            }, force => WheelFfbNative.SetForce(force), () =>
            {
                if (!ownsAttempt) return;
                try { Stop(); } finally { ownsAttempt = false; }
            }, () => ownsAttempt && WheelFfbNative.Ready);
        }

        /// <summary>Uppercase hex SHA-256 of a pinned dependency, refusing files over <see cref="MaxFileBytes"/>.</summary>
        public static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                if (stream.Length > MaxFileBytes) throw new InvalidDataException("Shared FFB dependency exceeds limit.");
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
        }

        private static void RequirePin(string pin, string name)
        {
            if (pin == null || pin.Length != 64) throw new ArgumentException("Exact 64-digit SHA-256 pin required.", name);
            foreach (char c in pin)
                if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F'))
                    throw new ArgumentException("Exact 64-digit SHA-256 pin required.", name);
        }

        private static void Stop()
        {
            try { WheelFfbNative.SetForce(0); }
            finally
            {
                try { WheelFfbNative.Zero(); }
                finally { try { WheelFfbNative.Stop(); } finally { WheelFfbNative.Shutdown(); } }
            }
        }
    }

    /// <summary>The DRIVE/Truckful/Over the Hill output-window policy: the focused visible top-level window of this
    /// process whose class is UnityWndClass, as a 32-bit handle, or 0. Win32 only; no Unity reference.</summary>
    public static class UnityForegroundWindow
    {
        private static readonly uint ProcessId = (uint)Process.GetCurrentProcess().Id;
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr window, StringBuilder text, int count);

        public static int Focused()
        {
            IntPtr window = GetForegroundWindow();
            if (window == IntPtr.Zero || !IsWindowVisible(window)) return 0;
            GetWindowThreadProcessId(window, out uint process);
            var name = new StringBuilder(128); GetClassNameW(window, name, name.Capacity);
            long value = window.ToInt64();
            return process == ProcessId && name.ToString() == "UnityWndClass" &&
                value >= int.MinValue && value <= uint.MaxValue ? unchecked((int)value) : 0;
        }
    }
}
