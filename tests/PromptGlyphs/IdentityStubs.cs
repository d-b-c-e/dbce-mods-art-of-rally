using System;
using System.Collections.Generic;
using Dbce.Wheel.Input;

namespace Rewired
{
    public class Controller { }
    public class Joystick : Controller { public bool isConnected = true; public Guid hardwareTypeGuid; }
    public enum Pole { Negative, Positive }
    public class InputAction { public int id; }
    public class ActionElementMap
    { public string elementIdentifierName; public int elementIdentifierId, axisRange; public Pole axisContribution; }
    public class Player
    {
        public readonly Controllers controllers = new Controllers();
        public class Controllers { public readonly Maps maps = new Maps(); }
        public class Maps
        {
            public readonly List<ActionElementMap> Items = new List<ActionElementMap>();
            public ActionElementMap GetFirstElementMapWithAction(Controller c, int id, bool enabled) => Items.Count == 0 ? null : Items[0];
            public void GetElementMapsWithAction(Controller c, int id, bool enabled, List<ActionElementMap> result) => result.AddRange(Items);
        }
    }
}
namespace ArtOfSimRally.Mod.Shim
{
    internal static class SharedInputStatus { internal enum Phase { Off, Attached } }
    internal static class ShimFrame { internal static readonly string[] Names = { "steer", "throttle", "clutch", "camera" }; internal const int Count = 4; }
    internal sealed class Consumer
    {
        internal bool Started = true;
        internal bool Terminal = false;
        internal readonly HashSet<int> Owned = new HashSet<int>();
        internal bool Owns(int index) => Owned.Contains(index);
    }
    internal static class ShimInput { internal static readonly Consumer Consumer = new Consumer(); }
    internal sealed class JoystickObservation
    { internal object Token; internal string Instance, ProductGuid, InterfacePath; }
    internal sealed class OwnedDeviceIdentity
    { internal Guid Instance, Product; internal ulong Generation = 1; internal string InterfacePath; }
    internal sealed class Inventory { internal long ConnectionEpoch; }
    internal sealed class Isolation
    {
        internal readonly HashSet<object> Tokens = new HashSet<object>();
        internal bool OwnsDisabled(object token) => Tokens.Contains(token);
    }
    internal sealed class SharedInputSession
    {
        internal JoystickObservation[] Before;
        internal OwnedDeviceIdentity[] Owned;
        internal Guid[] RewiredInstances;
        internal Isolation Isolation = new Isolation();
        internal Inventory Inventory = new Inventory();
        internal long EpochBefore = 0;
    }
}
namespace ArtOfSimRally.Mod
{
    internal static partial class SharedInputStartup
    {
        private static Shim.SharedInputStatus.Phase phase = Shim.SharedInputStatus.Phase.Attached;
        private static object session;
        internal static bool Stopped;
        internal static void Fixture(Shim.SharedInputSession value) { session = value; Stopped = false; }
    }
    internal sealed class SharedBindingCaptureHost
    {
        internal static readonly SharedBindingCaptureHost Instance = new SharedBindingCaptureHost();
        internal readonly Dictionary<string, ControlBinding> Active = new Dictionary<string, ControlBinding>();
        internal bool TryActiveBinding(string action, out ControlBinding binding) => Active.TryGetValue(action, out binding);
    }
    internal static class Main
    {
        internal static bool Enabled = true;
    }
    internal static class GameButtonInput
    {
        internal static bool Primary(Rewired.Player player) => ReferenceEquals(player, PadManager.GetPlayer());
        // The production table is tested in tests/SharedInput (buttons). This stub supplies only the routes needed to
        // test glyph precedence, as indices into the stub ShimFrame.Names above.
        internal static bool NativeAction(int id, bool negative, out int action)
        {
            action = id == 0 ? 0 : id == 2 ? 1 : id == 5 ? 2 : id == 61 ? 3 : -1;
            return action >= 0;
        }
    }
}
