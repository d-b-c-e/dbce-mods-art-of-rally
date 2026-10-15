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
    internal static class ShimFrame { internal static readonly string[] Names = { "steer", "throttle", "clutch", "camera" }; }
    internal sealed class Consumer
    {
        internal bool Started = true;
        internal bool Terminal;
        internal readonly HashSet<int> Owned = new HashSet<int>();
        internal bool Owns(int index) => Owned.Contains(index);
        internal bool LatchedDelivery(int frame) => false;
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
        internal long EpochBefore;
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
        internal static Settings Settings = new Settings();
    }
    internal sealed class Settings { internal bool GlyphTextFallback = true, WheelInputEnabled = true; }
    internal static class GameButtonInput
    {
        internal static bool Primary(Rewired.Player player) => ReferenceEquals(player, PadManager.GetPlayer());
        // Mapping behavior is Niru's action-router fixture's responsibility.
        // This stub supplies only the routes needed to test glyph precedence.
        internal static bool NativeAction(int id, bool negative, out string sharedId, out WheelInput.Channel? artChannel)
        {
            artChannel = id == 2 ? (WheelInput.Channel?)WheelInput.Channel.Throttle : null;
            sharedId = id == 0 ? "steer" : id == 2 ? "throttle" : id == 5 ? "clutch" : id == 61 ? "camera" : null;
            return sharedId != null;
        }
    }
    internal static partial class WheelInput
    {
        public enum Channel { Steer, Throttle, Brake, Clutch, Handbrake }
        internal static bool Enabled = true;
        private static readonly string[] AxisNames = { "X", "Y", "Z", "Rx", "Ry", "Rz", "Slider 1", "Slider 2" };
        private static readonly Dictionary<Channel, Binding> _bindings = new Dictionary<Channel, Binding>();
        private sealed class Device { internal bool Ok = true; internal Guid? InstanceGuid; }
        private static Device fixtureDevice;
        internal static string Duplicate;
        internal static string SharedDuplicate(Channel channel) => Duplicate;
        private static Device Resolve(Binding binding) => fixtureDevice;
        internal static void Fixture(Channel channel, Binding binding)
        { _bindings[channel] = binding; fixtureDevice = new Device { InstanceGuid = binding.InstanceGuid }; Duplicate = null; }
    }
}
