using ArtOfSimRally.Mod;
using Device = Dbce.Wheel.Ffb.WheelFfbNative;
using Channel = ArtOfSimRally.Mod.WheelInput.Channel;

static class GameButtonTests
{
    static int checks;
    static readonly Guid Identity = new("11111111-1111-1111-1111-111111111111");
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static string Binding(string element, int rest = 0) => $"TSS fixture|0|{element}|{rest}|1|guid:{Identity:D}";
    static void Tick() { Time.frameCount++; WheelInput.Update(); }
    static bool Down(Channel channel) => WheelInput.GameButton(channel, true);
    static bool Held(Channel channel) => WheelInput.GameButton(channel, false);
    public static int Run()
    {
        foreach (var bad in new[] { Binding("pov:4"), Binding("pov:0",36000), Binding("pov:0",-1), "TSS fixture|0|pov:0|0|1", Binding("pov:0")+"|cal:0:0:0" })
            Check(WheelInput.Binding.Parse(bad) == null, "invalid POV accepted: " + bad);
        var serialized = Binding("pov:0",27000);
        Check(WheelInput.Binding.Parse(serialized).ToString() == serialized, "POV roundtrip");
        var north=WheelInput.Binding.Parse(Binding("pov:0",0));
        Check(north.HatPressed(4500) && north.HatPressed(31500) && !north.HatPressed(4501) && !north.HatPressed(-1) && !north.HatPressed(65535), "POV diagonal/neutral boundary");
        WheelInput.Close(); Device.ReadOk=true; Device.ThrowRead=Device.FailOpen=false;
        Device.Devices = new[] { new Device.DeviceInfo { InstanceGuid=Identity } };
        Array.Clear(Device.Buttons); Array.Fill(Device.Hats,-1);
        Main.Enabled=true; Main.SettingsVisible=false; Application.isFocused=true; GameState.IsDriving=false;
        Main.Settings = new Settings { WheelInputEnabled=true, CameraSwitchBinding=Binding("button:32"),
            NavUpBinding=Binding("pov:0",0), NavDownBinding=Binding("pov:0",18000),
            NavLeftBinding=Binding("pov:0",27000), NavRightBinding=Binding("pov:0",9000),
            ConfirmBinding=Binding("button:31"), BackBinding=Binding("button:18"), StartBinding=Binding("button:35") };
        Time.realtimeSinceStartup+=60; WheelInput.LoadBindings(); WheelInput.Open();
        Device.Buttons[32]=1; Device.Hats[0]=0; Tick();
        Check(!Down(Channel.CameraSwitch) && !Held(Channel.NavUp), "startup held control activated");
        Device.Buttons[32]=0; Device.Hats[0]=-1; Tick();
        Device.Buttons[32]=1; Tick();
        Check(Down(Channel.CameraSwitch) && Held(Channel.CameraSwitch), "camera fresh press missing");
        Check(Down(Channel.CameraSwitch), "second consumer lost edge");
        GameButtonInput.Observe(12,13,16,17);
        Check(GameButtonInput.Button(61,false,true), "camera action61 missing");
        Check(!GameButtonInput.Button(62,false,true), "unrelated action got camera");
        Check(!GameButtonInput.Button(61,true,true), "negative camera action emitted");
        Check(GameButtonInput.Primary(PadManager.Player) && !GameButtonInput.Primary(new Rewired.Player()), "other player accepted");
        GameButtonCompatibility.Allowed=false; Check(!GameButtonInput.Primary(PadManager.Player),"unknown build routed action IDs"); GameButtonCompatibility.Allowed=true;
        Tick(); Check(!Down(Channel.CameraSwitch) && Held(Channel.CameraSwitch), "held camera repeated");
        foreach(var (angle,channel,action,negative) in new[] { (0,Channel.NavUp,13,false), (18000,Channel.NavDown,13,true), (27000,Channel.NavLeft,12,true), (9000,Channel.NavRight,12,false) })
        {
            Device.Hats[0]=-1; Tick(); Device.Hats[0]=angle; Tick();
            Check(Down(channel) && GameButtonInput.Button(action,negative,true), "hat direction missing");
            Check(GameButtonInput.Axis(action)==(negative?-1:1), "navigation axis direction wrong");
            Check(!GameButtonInput.Button(action,!negative,true), "opposite navigation activated");
            Tick(); Check(!Down(channel) && Held(channel), "hat hold edge repeated");
        }
        foreach(var (button,action) in new[] { (31,16), (18,17), (35,19) })
        {
            Device.Buttons[button]=1; Tick();
            Check(GameButtonInput.Button(action,false,true), "menu action missing");
            Check(!GameButtonInput.Button(8,false,true), "pause double routed to8");
            Device.Buttons[button]=0; Tick();
        }
        foreach(var gate in new[] { "read", "focus", "panel" })
        {
            Device.Buttons[32]=0; Tick(); Device.Buttons[32]=1; Tick(); Check(Down(Channel.CameraSwitch), "setup press");
            if(gate=="read") Device.ReadOk=false; else if(gate=="focus") Application.isFocused=false; else Main.SettingsVisible=true;
            Tick(); Check(!Held(Channel.CameraSwitch) && !Down(Channel.CameraSwitch), "gate leaked button");
            Device.ReadOk=true; Application.isFocused=true; Main.SettingsVisible=false;
            Tick(); Check(!Held(Channel.CameraSwitch), "gate recovery replayed held button");
            Device.Buttons[32]=0; Tick(); Device.Buttons[32]=1; Tick(); Check(Down(Channel.CameraSwitch), "gate release did not rearm");
        }
        Time.frameCount++; Check(!Down(Channel.CameraSwitch), "stale snapshot reused");
        Device.Hats[0]=9000; WheelInput.BeginCalibration(Channel.NavUp); Tick();
        Check(WheelInput.PendingCalibration==null, "held hat bound immediately");
        Device.Hats[0]=-1; Tick(); Device.Hats[0]=4500; Tick();
        Check(WheelInput.PendingCalibration==null,"diagonal captured for navigation");
        Device.Hats[0]=-1; Tick(); Device.Hats[0]=18000; Tick();
        Check(WheelInput.PendingCalibration?.IsHat==true && !WheelInput.CanSaveCalibration, "hat bind/release requirement");
        Device.Hats[0]=-1; Tick(); Check(WheelInput.CanSaveCalibration, "hat release not observed");
        Check(WheelInput.SaveCalibration() && Main.Settings.NavUpBinding==Binding("pov:0",18000), "F6 uses same stored POV binding");
        var drivetrain=new Drivetrain { automatic=true };
        Main.Settings.TransmissionMode="Sequential"; TransmissionInput.Apply(drivetrain); Check(!drivetrain.automatic,"manual preference ignored");
        Main.Settings.TransmissionMode="Automatic"; TransmissionInput.Apply(drivetrain); Check(drivetrain.automatic,"automatic preference ignored");
        Main.Settings.TransmissionMode="Follow game"; drivetrain.automatic=false; TransmissionInput.Apply(drivetrain); Check(!drivetrain.automatic,"game preference overridden by default");
        Main.Enabled=false; Main.Settings.TransmissionMode="Automatic"; TransmissionInput.Apply(drivetrain); Check(!drivetrain.automatic,"disabled mod applied transmission"); Main.Enabled=true;
        // Real title routing: release after a fresh press, only on its active panel.
        UIManager.Instance=new UIManager();
        foreach(var button in new[] {31,35})
        {
            SplashScreenControl.Instance=new SplashScreenControl();
            UIManager.Instance.PanelManager.Current=UIManager.Instance.PanelManager.SplashScreenPanel;
            Device.Buttons[button]=0; Tick();
            Device.Buttons[button]=1; Tick(); Check(SplashScreenControl.Instance.Ends==0,"title advanced on press instead of release");
            Device.Buttons[button]=0; Tick(); Check(SplashScreenControl.Instance.Ends==1,"title release ignored");
            Tick(); Check(SplashScreenControl.Instance.Ends==1,"title advanced twice");
        }
        foreach(var gate in new[] {"read","focus","panel","build","wrong-screen","inactive","startup-held"})
        {
            SplashScreenControl.Instance=new SplashScreenControl();
            UIManager.Instance.PanelManager.Current=UIManager.Instance.PanelManager.SplashScreenPanel;
            Device.Buttons[31]=0; Tick(); Device.Buttons[31]=1; Tick();
            if(gate=="read") Device.ReadOk=false;
            if(gate=="focus") Application.isFocused=false;
            if(gate=="panel") Main.SettingsVisible=true;
            if(gate=="build") GameButtonCompatibility.Allowed=false;
            if(gate=="wrong-screen") UIManager.Instance.PanelManager.Current=new object();
            if(gate=="inactive") SplashScreenControl.Instance.isActiveAndEnabled=false;
            if(gate=="startup-held") { WheelInput.ResetGameButtons(); Tick(); }
            Device.Buttons[31]=0; Tick(); Check(SplashScreenControl.Instance.Ends==0,"title gate leaked release: "+gate);
            Device.ReadOk=true; Application.isFocused=true; Main.SettingsVisible=false; GameButtonCompatibility.Allowed=true;
            Tick(); Check(SplashScreenControl.Instance.Ends==0,"title recovery manufactured release: "+gate);
        }
        UIManager.Instance=null; SplashScreenControl.Instance=null;
        WheelInput.Close(); Array.Clear(Device.Buttons); Array.Fill(Device.Hats,-1);
        return checks;
    }
}
