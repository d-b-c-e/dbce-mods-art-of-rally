// SPDX-License-Identifier: MIT
#nullable disable
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Dbce.Wheel.Input
{
    /// <summary>Developer-test client for one already resident WheelFfb module.
    /// This class never loads a DLL, opens a device, edits a binding or computes
    /// a replacement sample. The native module owns injection and its force fence.
    /// Serialize its use with the reader owner's lifecycle; do not unload while bound.</summary>
    public sealed class NativeTestInjectionClient
    {
        public const int NoForce = 1;
        public const int MaxCommandBytes = 512;
        private const int ReplyBytes = 1024;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int EnableCall(int flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int SubmitCall([In] byte[] command, [Out] byte[] reason, int reasonSize);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int StatusCall([Out] byte[] text, int size);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int InjectedCall(int slot);

        private readonly EnableCall _enable;
        private readonly SubmitCall _submit;
        private readonly StatusCall _status;
        private readonly InjectedCall _injected;
        private bool _attempted, _faulted;

        // Explicit delegates also permit hardware-free tests of the real client.
        public NativeTestInjectionClient(EnableCall enable, SubmitCall submit, StatusCall status, InjectedCall injected)
        { _enable = enable; _submit = submit; _status = status; _injected = injected; Status = "not armed"; }

        /// <summary>The resolver must return exports from the SAME resident module
        /// as OpenReadDevice/ReadDeviceState. Identity/path verification is the caller's job.</summary>
        public static NativeTestInjectionClient FromResidentExports(Func<string, IntPtr> resolve)
        {
            try
            {
                if (resolve == null) throw new ArgumentNullException("resolve");
                return new NativeTestInjectionClient(
                    Bind<EnableCall>(resolve("EnableTestInjection")),
                    Bind<SubmitCall>(resolve("SubmitTestInjection")),
                    Bind<StatusCall>(resolve("GetTestInjectionStatus")),
                    Bind<InjectedCall>(resolve("LastReadInjected")));
            }
            catch (Exception e)
            {
                var absent = new NativeTestInjectionClient(null, null, null, null);
                absent.Status = "test exports unavailable: " + e.Message;
                return absent;
            }
        }
        private static T Bind<T>(IntPtr p) where T : class
        { return p == IntPtr.Zero ? null : (T)(object)Marshal.GetDelegateForFunctionPointer(p, typeof(T)); }

        public bool Available { get { return _enable != null && _submit != null && _status != null && _injected != null; } }
        public bool Armed { get; private set; }
        // Sticky even if native arming returns ambiguously or later status fails.
        // A failed test must never resume assignment/calibration of synthetic input.
        public bool BlockAssignments { get { return _attempted; } }
        public string Status { get; private set; }

        public bool Arm()
        {
            if (_attempted) return RefreshStatus();
            _attempted = true;
            if (!Available) return Fault("native test exports unavailable; restart without the test request");
            try
            {
                int result = _enable(NoForce);
                if (result != 1)
                {
                    string text; ReadStatus(out text);
                    return Fault("native arm refused (" + result + "): " + text);
                }
                return RefreshStatus();
            }
            catch (Exception e) { return Fault("native arm uncertain: " + e.Message); }
        }

        public bool RefreshStatus()
        {
            if (!_attempted || _faulted || !Available) return false;
            try
            {
                string text;
                if (ReadStatus(out text) != 1) return Fault("native no-force latch unconfirmed: " + text);
                Status = text; Armed = true; return true;
            }
            catch (Exception e) { return Fault("native status unavailable: " + e.Message); }
        }

        /// <summary>Submit only raw, independently specified test data. Acceptance
        /// is not delivery or a game response. Native code validates grammar and TTL.</summary>
        public bool SubmitRaw(string command, out string reason)
        {
            reason = "test client not armed";
            if (!RefreshStatus()) return false;
            byte[] encoded;
            try
            {
                if (string.IsNullOrEmpty(command) || !command.StartsWith("inject raw ", StringComparison.Ordinal))
                    throw new ArgumentException("only raw injection commands are accepted");
                foreach (char c in command) if (c < 32 || c > 126) throw new ArgumentException("command must contain printable ASCII only");
                int length = Utf8.GetByteCount(command);
                if (length > MaxCommandBytes) throw new ArgumentException("command exceeds UTF-8 byte limit");
                encoded = new byte[length + 1]; Utf8.GetBytes(command, 0, command.Length, encoded, 0);
            }
            catch (Exception e) { reason = e.Message; return false; }
            try
            {
                byte[] reply = Buffer();
                int result = _submit(encoded, reply, reply.Length);
                reason = Decode(reply);
                if (result != 0 && result != 1) { Fault("invalid native submission result"); return false; }
                return result == 1;
            }
            catch (Exception e) { reason = "native submission uncertain: " + e.Message; return Fault(reason); }
        }

        /// <summary>Call immediately after the corresponding reader call, under
        /// that reader's lifecycle lock. True means this last read was substituted.</summary>
        public bool LastReadInjected(int slot)
        {
            if (!Armed || _faulted || slot < 0) return false;
            try
            {
                int result = _injected(slot);
                if (result == 0 || result == 1) return result == 1;
                return Fault("invalid native last-read result");
            }
            catch (Exception e) { return Fault("native last-read evidence unavailable: " + e.Message); }
        }
        private int ReadStatus(out string text)
        { byte[] reply = Buffer(); int result = _status(reply, reply.Length); text = Decode(reply); return result; }
        private static byte[] Buffer()
        {
            var buffer = new byte[ReplyBytes];
            for (int i = 0; i < buffer.Length; ++i) buffer[i] = 0xff;
            return buffer;
        }
        private static string Decode(byte[] bytes)
        {
            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0) throw new InvalidOperationException("unterminated native reply");
            return Utf8.GetString(bytes, 0, length);
        }
        private bool Fault(string reason) { _faulted = true; Armed = false; Status = reason; return false; }
    }
}
