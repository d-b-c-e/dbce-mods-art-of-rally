using System;
using System.Collections.Generic;
using Rewired;

namespace ArtOfSimRally.Mod.Shim
{
    // Source-only adapter for the shipped Rewired API. Construction does not
    // change input; Check/Restore must be called by an explicit session owner.
    internal sealed class RewiredInputIsolation : IJoystickInventory
    {
        private sealed class JoystickView : IIsolationJoystick
        {
            private readonly Joystick joystick;
            internal JoystickView(Joystick value) { joystick = value; }
            public object Token => joystick;
            public Guid Instance => joystick.deviceInstanceGuid;
            public bool Enabled { get => joystick.enabled; set => joystick.enabled = value; }
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
