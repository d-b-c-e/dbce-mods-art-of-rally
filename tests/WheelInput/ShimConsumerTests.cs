using System.Reflection;
using ArtOfSimRally.Mod;
using ArtOfSimRally.Mod.Shim;
using Host = ArtOfSimRally.Mod.Main;
using Clock = ArtOfSimRally.Mod.Time;

// Goes through the actual production WheelInputPatch after all legacy tests.
internal static class ShimConsumerTests
{
    internal static int Run()
    {
        int checks = 0;
        void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
        void Same(float a, float b, string why) => Check(Math.Abs(a - b) < .000001f, why);
        byte[] Frame(ulong sequence, ShimStatus state)
        {
            var bytes = new byte[296];
            BitConverter.GetBytes(296).CopyTo(bytes, 0); BitConverter.GetBytes(2).CopyTo(bytes, 4);
            BitConverter.GetBytes(sequence).CopyTo(bytes, 8); BitConverter.GetBytes((long)sequence).CopyTo(bytes, 16);
            float[] axes = { -.4f, .6f, .3f, .8f, .45f };
            for (int i = 0; i < 31; i++)
            {
                if (i < 5) BitConverter.GetBytes(axes[i]).CopyTo(bytes, 48 + i * 8);
                bytes[52 + i * 8] = (byte)(i < 5 ? state : ShimStatus.Unsupported);
            }
            return bytes;
        }
        object[] Input()
        {
            var args = new object[] { new AxisCarController { SteeringOutOfAlignmentEffect = .1f }, .95f, .1f, .9f, .2f, .1f, true };
            typeof(WheelInputPatch).GetMethod("Override", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);
            return args;
        }
        Host.Enabled = true; Host.SettingsVisible = false; ArtOfSimRally.Mod.Application.isFocused = true;
        GameEntryPoint.EventManager = new EventManager(); Host.Settings.WheelInputEnabled = false;
        string saved = File.ReadAllText(Host.Path); int saves = Host.Saves;
        int reads = Dbce.Wheel.Ffb.WheelFfbNative.Reads;
        Clock.frameCount = 20000; ShimInput.IsolatedFrame = Clock.frameCount;
        var dormant = Input();
        Same((float)dormant[1], .95f, "unpublished shim changed throttle"); Same((float)dormant[2], .1f, "unpublished shim changed brake");
        Same((float)dormant[3], .9f, "unpublished shim changed steering"); Same((float)dormant[4], .2f, "unpublished shim changed handbrake");
        Same((float)dormant[5], .1f, "unpublished shim changed clutch"); Check((bool)dormant[6], "unpublished shim changed engine start");
        string why;
        Check(ShimInput.Consumer.Publish(Frame(1, ShimStatus.Live), Clock.frameCount, out why), why);
        var result = Input();
        Same((float)result[1], .95f, "production seam lost keyboard throttle"); Same((float)result[2], .3f, "production brake");
        Same((float)result[3], -.3f, "production alignment"); Same((float)result[4], .45f, "production analog handbrake");
        Same((float)result[5], .8f, "production clutch"); Check((bool)result[6], "production engine start");
        Host.SettingsVisible = true; result = Input();
        for (int i = 1; i < 6; i++) Same((float)result[i], 0, "panel did not suppress shim");
        Check(!(bool)result[6], "panel started engine"); Host.SettingsVisible = false;
        ArtOfSimRally.Mod.Application.isFocused = false; Same((float)Input()[1], 0, "focus did not suppress shim");
        ArtOfSimRally.Mod.Application.isFocused = true;
        GameEntryPoint.EventManager.status = EventStatusEnums.EventStatus.FINISHING_STAGE_ANIMATION;
        Same((float)Input()[3], .9f, "shim changed finishing driver");
        GameEntryPoint.EventManager = null; Same((float)Input()[3], .9f, "shim created/read lazy manager");
        GameEntryPoint.EventManager = new EventManager();
        Host.Enabled = false; Same((float)Input()[3], .9f, "disabled mod applied shim"); Host.Enabled = true;
        Clock.frameCount++; ShimInput.IsolatedFrame = Clock.frameCount;
        Check(ShimInput.Consumer.Publish(Frame(2, ShimStatus.DeviceMissing), Clock.frameCount, out why), why);
        Same((float)Input()[3], .9f, "missing device lost independent keyboard");
        ShimInput.IsolatedFrame = -1; result = Input();
        for (int i = 1; i < 6; i++) Same((float)result[i], 0, "missing device fell through to wheel");
        Clock.frameCount++; ShimInput.IsolatedFrame = Clock.frameCount;
        Same((float)Input()[3], .9f, "stalled provider lost keyboard");
        ShimInput.Consumer.Stop(); Same((float)Input()[3], .9f, "stopped provider lost keyboard");
        Check(Dbce.Wheel.Ffb.WheelFfbNative.Reads == reads, "sink polled native device");
        Check(Host.Saves == saves && File.ReadAllText(Host.Path) == saved, "sink changed owner settings");
        return checks;
    }
}
