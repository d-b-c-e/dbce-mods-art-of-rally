using System.Text.Json;
using ArtOfSimRally.Mod;
using Host = ArtOfSimRally.Mod.Main;
using Device = Dbce.Wheel.Ffb.WheelFfbNative;

internal static class HeldGateCases
{
    internal static int Run()
    {
        var results = new List<object>(); int failures = 0;
        void Check(string name, bool pass) { results.Add(new { name, pass }); if (!pass) failures++; }
        Shifter.Close(); GameState.IsDriving = false;
        Array.Clear(Device.Buttons);
        Device.Devices = new[] { new Device.DeviceInfo { Index = 0, Name = "held gate fixture" } };
        Host.Settings = new Settings { ShifterEnabled = true, ShifterDeviceIndex = 0, ShifterDeviceName = "held gate fixture", ShifterIsHPattern = true, Gear1Button = 0, Gear2Button = 1 };
        Host.Enabled = true; Host.SettingsVisible = false; Application.isFocused = true;
        Check("actual legacy reader opens only the fake device", Shifter.Open(0));
        Device.Buttons[0] = 128; var drive = new Drivetrain { gear = 1 };
        Shifter.Update(drive); Check("held first engages initially", drive.gear == 2 && drive.Shifts == 1);
        drive.gear = 1; Shifter.Update(drive);
        Check("held first reengages after native stage reset", drive.gear == 2 && drive.Shifts == 2);
        Shifter.Reset(); drive.gear = 1; drive.RefuseShift = true; Shifter.Update(drive);
        drive.RefuseShift = false; Shifter.Update(drive);
        Check("held gate retries a refused native change", drive.gear == 2);
        Shifter.Reset(); Device.Buttons[0] = 0; Device.Buttons[1] = 128;
        drive.gear = 2; drive.changingGear = true; int before = drive.Shifts;
        Shifter.Update(drive); Check("in-flight native change is left alone", drive.Shifts == before);
        drive.changingGear = false; Shifter.Update(drive);
        Check("held next gate engages when native change ends", drive.gear == 3);
        Host.SettingsVisible = true; drive.gear = 1; before = drive.Shifts; Shifter.Update(drive);
        Check("panel never applies a gate", drive.Shifts == before);
        Host.SettingsVisible = false; Shifter.Update(drive);
        Check("absolute held gate resumes without lever release", drive.gear == 3);
        Device.Buttons[1] = 0; Shifter.Update(drive);
        drive.gear = 2; Shifter.Update(drive);
        Check("physical neutral remains true after native reset", drive.gear == 1);
        Shifter.Close();
        Console.WriteLine(JsonSerializer.Serialize(new { schema = "art.held-gate-regression@1", pass = failures == 0, checks = results.Count, failures, productionConsumer = true, nativeOrDeviceLoaded = false, assertions = results }));
        return failures == 0 ? 0 : 1;
    }
}
