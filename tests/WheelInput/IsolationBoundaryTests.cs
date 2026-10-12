using ArtOfSimRally.Mod.Shim;
using Rewired;

internal static class IsolationBoundaryTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
        var wheelId = Guid.NewGuid(); var padId = Guid.NewGuid();
        var wheel = new Joystick { deviceInstanceGuid = wheelId }; var duplicate = new Joystick { deviceInstanceGuid = wheelId };
        var pad = new Joystick { deviceInstanceGuid = padId };
        ReInput.controllers.Joysticks.Clear(); ReInput.controllers.Joysticks.AddRange(new[] { wheel, duplicate, pad }); ReInput.isReady = true;
        // Links the production adapter: public runtime getters/setter, never
        // whole-player or map state. Its compile also checks Art's shipped API.
        var isolation = new NativeInputIsolation(new[] { wheelId }, new RewiredInputIsolation());
        Check(!isolation.Check(1) && !wheel.enabled && !duplicate.enabled && pad.enabled, "production adapter identity selection");
        Check(isolation.Check(2) && wheel.Writes == 1 && duplicate.Writes == 1 && pad.Writes == 0, "production adapter cached-frame barrier");
        pad.deviceInstanceGuid = Guid.Empty;
        Check(!isolation.Check(3) && isolation.Status.Contains("duplicate mapped wheel"), "production adapter guessed unknown device");
        pad.deviceInstanceGuid = padId; ReInput.isReady = false;
        Check(!isolation.Check(4), "production adapter ignored Rewired readiness"); ReInput.isReady = true;
        Check(isolation.Check(5), "production adapter failed requalification");
        Check(isolation.Restore() && wheel.enabled && duplicate.enabled && pad.enabled && pad.Writes == 0, "production adapter changed unrelated input on restore");
        ReInput.controllers.Joysticks.Clear(); ReInput.isReady = false;
        return checks;
    }
}
