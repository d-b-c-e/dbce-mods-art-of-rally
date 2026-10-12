using System;
using System.Collections.Generic;
using Rewired;
using Rewired.Platforms.Windows.RawInput;

namespace ArtOfSimRally.Mod.Shim
{
    // Source-only adapter for the shipped Rewired API. Construction does not
    // change input; Check/Restore must be called by an explicit session owner.
    internal sealed class RewiredInputIsolation : IJoystickInventory
    {
        private sealed class JoystickView : IRawIsolationJoystick, IRawIdentityDiagnostic
        {
            private readonly Joystick joystick;
            internal JoystickView(Joystick value) { joystick = value; }
            public object Token => joystick;
            public Guid Instance => joystick.deviceInstanceGuid;
            public bool Enabled { get => joystick.enabled; set => joystick.enabled = value; }
            public string Name => joystick.name;
            public string InterfacePath => joystick.GetExtension<RawInputControllerExtension>()?.devicePath;
            public string ProductGuid => joystick.GetExtension<RawInputControllerExtension>()?.productGuid.ToString("D");
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
