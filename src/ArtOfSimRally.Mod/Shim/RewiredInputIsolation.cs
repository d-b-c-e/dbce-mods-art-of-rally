using System;
using System.Collections.Generic;
using Rewired;
using Rewired.ControllerExtensions;
using Rewired.Platforms.Windows.RawInput;

namespace ArtOfSimRally.Mod.Shim
{
    // Source-only adapter for the shipped Rewired API. Construction does not
    // change input; Check/Restore must be called by an explicit session owner.
    internal sealed class RewiredInputIsolation : IJoystickInventory
    {
        private long epoch;
        private bool observing;
        internal long ConnectionEpoch => System.Threading.Interlocked.Read(ref epoch);
        private void Changed(ControllerStatusChangedEventArgs ignored) => Changed();
        private void Changed() => System.Threading.Interlocked.Increment(ref epoch);
        internal void BeginIdentityObservation()
        {
            if (observing) throw new InvalidOperationException("identity observation already begun");
            observing = true;
            ReInput.ControllerConnectedEvent += Changed; ReInput.ControllerPreDisconnectEvent += Changed; ReInput.ControllerDisconnectedEvent += Changed;
            ReInput.PreShutDownEvent += Changed; ReInput.ShutDownEvent += Changed; ReInput.InitializedEvent += Changed;
        }
        internal void EndIdentityObservation()
        {
            if (!observing) return;
            ReInput.ControllerConnectedEvent -= Changed; ReInput.ControllerPreDisconnectEvent -= Changed; ReInput.ControllerDisconnectedEvent -= Changed;
            ReInput.PreShutDownEvent -= Changed; ReInput.ShutDownEvent -= Changed; ReInput.InitializedEvent -= Changed;
            observing = false;
        }
        // The loaded pair, validated once on the first DualShock 4 read.
        private static readonly PinnedDs4Identity Ds4 = new PinnedDs4Identity(typeof(DualShock4Extension).Assembly, typeof(RawInputControllerExtension).Assembly);
        private sealed class JoystickView : IRawIsolationJoystick, IRawIdentityDiagnostic, IIdentityLifetimeDiagnostic
        {
            private readonly Joystick joystick;
            internal JoystickView(Joystick value) { joystick = value; }
            public object Token => joystick;
            public Guid Instance => joystick.deviceInstanceGuid;
            public bool Enabled { get => joystick.enabled; set => joystick.enabled = value; }
            public string Name => joystick.name;
            // RawInput first, unchanged. A joystick Rewired reads through its
            // DualShock 4 driver has no RawInput extension: the pinned reader.
            private RewiredIdentity Identity()
            {
                var raw = joystick.GetExtension<RawInputControllerExtension>();
                if (raw != null) return new RewiredIdentity(raw, raw.devicePath, raw.productGuid.ToString("D"));
                var ds4 = joystick.GetExtension<DualShock4Extension>();
                if (ds4 == null) return default(RewiredIdentity);
                IHIDControllerExtension hid = ds4;
                return Ds4.Read(ds4, () => hid.vendorId, () => hid.productId);
            }
            public string InterfacePath => Identity().Path;
            public string ProductGuid => Identity().Product;
            public object ExtensionToken => Identity().Token;
            public bool Connected => joystick.isConnected;
            public float[] ReadRawAxes()
            {
                int count = joystick.axisCount;
                if (count < 0 || count > 256) throw new InvalidOperationException("Rewired axis count outside probe bound");
                var values = new float[count];
                for (int i = 0; i < count; i++) values[i] = joystick.GetAxisRaw(i);
                return values;
            }
        }
        public bool Ready => ReInput.isReady;
        public IEnumerable<IIsolationJoystick> Joysticks
        {
            get
            {
                foreach (var joystick in ReInput.controllers.Joysticks)
                    yield return joystick == null ? null : new JoystickView(joystick);
            }
        }
    }
}
