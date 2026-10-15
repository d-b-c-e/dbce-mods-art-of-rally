using ArtOfSimRally.Mod;
using Device = Dbce.Wheel.Ffb.WheelFfbNative;
using Channel = ArtOfSimRally.Mod.WheelInput.Channel;

static class GameButtonTests
{
    static int checks;
    static readonly Guid Identity = new("11111111-1111-1111-1111-111111111111");
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static string Binding(string element, int rest = 0) => $"TSS fixture|0|{element}|{rest}|1|guid:{Identity:D}";
    static void Tick() { Time.frameCount++; WheelInput.EnsureUpdatedThisFrame(); GameButtonInput.TickTitle(); }
    static bool Down(Channel channel) => WheelInput.GameButton(channel, true);
    static bool Held(Channel channel) => WheelInput.GameButton(channel, false);
    static bool Continue(bool original = false, Rewired.Player player = null)
    {
        object[] args = { player ?? PadManager.Player, original };
        typeof(GameContinuePatch).GetMethod("After", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null,args);
        return (bool)args[1];
    }
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
        // Actual menu order: Rewired asks before the watchdog. The first read
        // samples; subsequent readers/watchdog see the same edge, even if the
        // physical state changes between them. No extra polling consumes it.
        Device.Buttons[32]=0; Device.Hats[0]=-1; Tick();
        Device.Hats[0]=18000; Time.frameCount++;
        int reads=Device.Reads;
        Check(GameButtonInput.Prepare(PadManager.Player), "early menu consumer refused");
        Check(Device.Reads==reads+1, "early consumer did not read exactly once");
        Check(GameButtonInput.Axis(13)==-1 && GameButtonInput.Button(13,true,true), "early menu lost current hat edge");
        Device.Hats[0]=-1;
        WheelInput.EnsureUpdatedThisFrame();
        Check(GameButtonInput.Prepare(PadManager.Player) && GameButtonInput.Button(13,true,true), "watchdog/second consumer consumed edge");
        Check(Device.Reads==reads+1, "multiple consumers repeated native read");
        Tick(); Check(GameButtonInput.Axis(13)==0 && !GameButtonInput.Button(13,true,true), "next frame retained released hat");
        Device.Hats[0]=18000; Tick();
        Check(GameButtonInput.Prepare(PadManager.Player) && GameButtonInput.Button(13,true,true), "watchdog-first order lost edge");
        // 9329cda (romi 32354). With no shared session, a Rewired query after the watchdog's projection
        // neither reads again nor projects again, even after the physical state changed. The frame keeps
        // the watchdog's edge, as on main.
        Check(!ArtOfSimRally.Mod.Shim.ShimInput.Consumer.Started, "a shared session is running in the legacy fixture");
        Device.Hats[0]=-1; Tick(); Device.Hats[0]=18000; Tick(); reads=Device.Reads; Device.Hats[0]=-1;
        Check(GameButtonInput.Prepare(PadManager.Player) && GameButtonInput.Prepare(PadManager.Player) && Down(Channel.NavDown) && Held(Channel.NavDown) &&
            GameButtonInput.Button(13,true,true) && Device.Reads==reads, "legacy query after the watchdog read or projected again (9329cda)");
        Time.frameCount++; Device.Hats[0]=-1;
        Check(!GameButtonInput.Prepare(new Rewired.Player()) && !Down(Channel.NavDown), "other player sampled wheel");
        Check(GameButtonInput.Prepare(PadManager.Player) && GameButtonInput.Axis(13)==0, "primary player did not sample after other player");
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
        SplashScreenControl.Instance=new SplashScreenControl();
        UIManager.Instance.PanelManager.Current=UIManager.Instance.PanelManager.SplashScreenPanel;
        Device.Buttons[31]=0; Tick(); Device.Buttons[31]=1; Tick();
        Device.Buttons[31]=0; Time.frameCount++;
        Check(GameButtonInput.Prepare(PadManager.Player) && SplashScreenControl.Instance.Ends==0, "input query changed scene");
        WheelInput.EnsureUpdatedThisFrame(); GameButtonInput.TickTitle();
        Check(SplashScreenControl.Instance.Ends==1, "watchdog lost early sampled title release");
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
        SplashScreenControl.Instance=new SplashScreenControl();
        UIManager.Instance.PanelManager.Current=UIManager.Instance.PanelManager.SplashScreenPanel;
        Main.Settings.ThrottleBinding=Binding("button:29"); WheelInput.LoadBindings();
        foreach(var button in new[] {18,29,32})
        {
            Device.Buttons[button]=0; Tick(); Device.Buttons[button]=1; Tick(); Device.Buttons[button]=0; Tick();
            Check(SplashScreenControl.Instance.Ends==0,"unrelated/pedal button dismissed title: "+button);
        }
        Device.Hats[0]=-1; Tick(); Device.Hats[0]=18000; Tick(); Device.Hats[0]=-1; Tick();
        Check(SplashScreenControl.Instance.Ends==0,"hat navigation dismissed title");
        Device.Buttons[31]=0; Tick(); Device.Buttons[31]=1; Tick();
        SplashScreenControl.Instance.EndSplashScreen(); // Native Rewired event wins first.
        Device.Buttons[31]=0; Tick(); Check(SplashScreenControl.Instance.Ends==1,"native event and profile release both ended title");
        UIManager.Instance=null; SplashScreenControl.Instance=null;
        // Run the production postfix: native positives survive; only fresh,
        // guarded Confirm/Start presses augment the primary player's query.
        Array.Clear(Device.Buttons); Device.Hats[0]=-1; Tick();
        Check(Continue(true) && !Continue(), "any-button changed native input or created a press");
        foreach (int button in new[] {31,35})
        {
            Device.Buttons[button]=1; Time.frameCount++;
            reads=Device.Reads;
            Check(Continue() && Continue() && Device.Reads==reads+1, "continue query lost edge or polled twice");
            Check(!Continue(player:new Rewired.Player()), "continue leaked to another player");
            WheelInput.EnsureUpdatedThisFrame(); Check(Continue(), "watchdog consumed continue edge");
            Tick(); Check(!Continue(), "held continue repeated");
            Device.Buttons[button]=0; Tick(); Check(!Continue(), "continue release counted as press");
        }
        foreach (int button in new[] {18,29,32})
        {
            Device.Buttons[button]=1; Tick(); Check(!Continue(), "unrelated/pedal control skipped intro");
            Device.Buttons[button]=0; Tick();
        }
        Device.Hats[0]=18000; Tick(); Check(!Continue(), "navigation skipped intro");
        Device.Hats[0]=-1; Tick();
        foreach (string gate in new[] {"read","focus","panel","build","disabled","assignment","startup-held"})
        {
            Device.Buttons[31]=0; Tick();
            if(gate=="read") Device.ReadOk=false;
            if(gate=="focus") Application.isFocused=false;
            if(gate=="panel") Main.SettingsVisible=true;
            if(gate=="build") GameButtonCompatibility.Allowed=false;
            if(gate=="disabled") Main.Enabled=false;
            if(gate=="assignment") WheelInput.BeginCalibration(Channel.NavUp);
            Device.Buttons[31]=1; Time.frameCount++;
            if(gate=="startup-held") WheelInput.ResetGameButtons();
            Check(!Continue() && Continue(true), "continue gate changed native input or leaked: "+gate);
            Device.ReadOk=true; Application.isFocused=true; Main.SettingsVisible=false;
            GameButtonCompatibility.Allowed=true; Main.Enabled=true; WheelInput.CancelAssign();
            Device.Buttons[31]=0; Tick();
        }
        WheelInput.Close(); Array.Clear(Device.Buttons); Array.Fill(Device.Hats,-1);
        // Queries cannot acquire or reconnect devices. A same-frame watchdog
        // still owns discovery even when the earlier query could not sample.
        Time.realtimeSinceStartup+=60; Time.frameCount++;
        int opens=Device.Opens, closes=Device.Closes, enums=Device.Enumerations;
        reads=Device.Reads;
        Check(GameButtonInput.Prepare(PadManager.Player), "closed-reader primary query refused");
        Check(Device.Opens==opens && Device.Closes==closes && Device.Enumerations==enums && Device.Reads==reads, "query discovered a closed reader");
        WheelInput.EnsureUpdatedThisFrame();
        Check(Device.Opens>opens && Device.Reads==reads+1, "query prevented same-frame watchdog discovery");
        Device.ReadOk=false; Time.realtimeSinceStartup+=6; Time.frameCount++;
        opens=Device.Opens; closes=Device.Closes; enums=Device.Enumerations;
        reads=Device.Reads;
        GameButtonInput.Prepare(PadManager.Player);
        Check(Device.Reads==reads+1 && Device.Opens==opens && Device.Closes==closes && Device.Enumerations==enums, "failed query attempted reconnect");
        Check(!Down(Channel.Confirm) && !Held(Channel.NavDown), "failed query retained action");
        Device.ReadOk=true; WheelInput.EnsureUpdatedThisFrame();
        Check(Device.Opens>opens && Device.Closes>closes && Device.Reads==reads+1, "watchdog recovery acquired/read in wrong phase");
        Tick(); Check(Device.Reads==reads+2, "replacement reader did not sample next frame");
        string legacy="TSS fixture|0|axis:2|0|1000";
        Main.Settings.ThrottleBinding=legacy; WheelInput.LoadBindings();
        Device.Axes[2]=4000; Time.frameCount++;
        GameButtonInput.Prepare(PadManager.Player);
        Check(Main.Settings.ThrottleBinding==legacy, "query learned/pinned a legacy binding");
        reads=Device.Reads; WheelInput.EnsureUpdatedThisFrame();
        var learned=WheelInput.Binding.Parse(Main.Settings.ThrottleBinding);
        Check(Device.Reads==reads && learned.Far==4000 && learned.InstanceGuid==Identity, "watchdog did not learn/pin cached input");
        WheelInput.BeginCalibration(Channel.NavUp); Time.frameCount++;
        reads=Device.Reads; Device.Hats[0]=18000;
        GameButtonInput.Prepare(PadManager.Player);
        Check(Device.Reads==reads && WheelInput.PendingCalibration==null, "query advanced assignment");
        WheelInput.CancelAssign(); WheelInput.Close(); Array.Fill(Device.Hats,-1);
        NativeActions();
        return checks;
    }
    static bool GetButtonUp(int action) => false; // names the production postfix's original method
    static bool PatchedUp(int action, bool native)
    {
        const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        object[] args = { PadManager.Player, action, typeof(GameButtonTests).GetMethod("GetButtonUp", Private), native };
        typeof(GameButtonPatch).GetMethod("After", Private).Invoke(null, args);
        return (bool)args[3];
    }
    // sana 162429, build 17584229: horn plays on GetButtonDown(30) and stops only on GetButtonUp(30); recover 44,
    // repair 45 and restart 46 keep the game's one-second hold (GetButton); look directions are the orbit axes
    // 22 and 63. One table maps them for dispatch and prompt labels.
    static void NativeActions()
    {
        WheelInput.Close(); Device.ReadOk=true; Device.ThrowRead=Device.FailOpen=false;
        Device.Devices = new[] { new Device.DeviceInfo { InstanceGuid=Identity } };
        Array.Clear(Device.Buttons); Array.Fill(Device.Hats,-1);
        Main.Enabled=true; Main.SettingsVisible=false; Application.isFocused=true; GameState.IsDriving=false;
        Main.Settings = new Settings { WheelInputEnabled=true, HornBinding=Binding("button:40"), LookBackBinding=Binding("button:41"),
            RecoverCarBinding=Binding("button:42"), RepairPunctureBinding=Binding("button:43"), QuickRestartBinding=Binding("button:44"),
            LookLeftBinding=Binding("pov:0",27000), LookRightBinding=Binding("pov:0",9000), LookForwardBinding=Binding("pov:0",0) };
        Time.realtimeSinceStartup+=60; WheelInput.LoadBindings(); WheelInput.Open(); GameButtonInput.Observe(12,13,16,17);
        Tick(); Tick();
        foreach (var (id, negative, shared, channel) in new (int, bool, string, Channel?)[] {
            (30,false,"horn",Channel.Horn), (44,false,"reset",Channel.RecoverCar), (45,false,null,Channel.RepairPuncture), (46,false,null,Channel.QuickRestart),
            (63,true,"lookBack",Channel.LookBack), (63,false,null,Channel.LookForward), (22,true,null,Channel.LookLeft), (22,false,null,Channel.LookRight),
            (61,false,"camera",Channel.CameraSwitch), (19,false,"start",Channel.Start), (16,false,"confirm",Channel.Confirm), (17,false,"back",Channel.Back),
            (12,true,"navLeft",Channel.NavLeft), (0,true,"steer",Channel.Steer), (0,false,"steer",Channel.Steer), (2,false,"throttle",Channel.Throttle), (5,false,"clutch",Channel.Clutch) })
            Check(GameButtonInput.NativeAction(id, negative, out var s, out var c) && s == shared && c == channel, "native table " + id + (negative ? "-" : "+"));
        Check(!GameButtonInput.NativeAction(8,false,out _,out _) && !GameButtonInput.NativeAction(64,false,out _,out _) && !GameButtonInput.NativeAction(30,true,out _,out _),
            "unrouted native action mapped (8 pause, 64 view collectible, negative horn)");
        GameButtonInput.Observe(14,15,16,17);
        Check(GameButtonInput.NativeAction(12,false,out var up,out var noChannel) && up=="shiftUp" && noChannel==null &&
            GameButtonInput.NativeAction(13,false,out var downShift,out _) && downShift=="shiftDown", "sequential shift ids");
        GameButtonInput.Observe(12,13,16,17);
        Check(!GameButtonInput.Button(0,false,false) && GameButtonInput.Axis(2)==0, "a driving axis was dispatched as a game button");

        // Horn: press, hold, release; the release reaches the game's GetButtonUp through the production postfix.
        Device.Buttons[40]=1; Tick();
        Check(GameButtonInput.Button(30,false,true) && GameButtonInput.Button(30,false,false) && !PatchedUp(30,false), "horn press");
        Tick(); Check(!GameButtonInput.Button(30,false,true) && GameButtonInput.Button(30,false,false), "held horn repeated its press");
        Device.Buttons[40]=0; Tick(); Check(PatchedUp(30,false) && !GameButtonInput.Button(30,false,false), "horn release missing");
        Tick(); Check(!PatchedUp(30,false) && PatchedUp(30,true) && !PatchedUp(8,false), "horn release repeated or native release lost");
        // A held horn cut off by the settings panel, focus, a failed read or a binding reload still gets one release,
        // and resumes only after a fresh press.
        foreach (string gate in new[] { "panel", "focus", "read", "reload" })
        {
            Device.Buttons[40]=1; Tick(); Check(Held(Channel.Horn), "horn setup " + gate);
            if (gate=="panel") Main.SettingsVisible=true; else if (gate=="focus") Application.isFocused=false; else if (gate=="read") Device.ReadOk=false; else WheelInput.LoadBindings();
            Tick();
            bool early = WheelInput.GameButtonReleased(Channel.Horn);
            Main.SettingsVisible=false; Application.isFocused=true; Device.ReadOk=true;
            if (!early) Tick();
            Check(early || WheelInput.GameButtonReleased(Channel.Horn), "interrupted horn never released: " + gate);
            Check(!Held(Channel.Horn), "interrupted horn resumed without a fresh press: " + gate);
            Device.Buttons[40]=0; Tick(); Tick(); Check(!WheelInput.GameButtonReleased(Channel.Horn), "horn released twice: " + gate);
        }

        // Recover car, repair puncture and quick restart: one press edge, then held for the game's own one-second timer.
        foreach (var (button, action) in new[] { (42,44), (43,45), (44,46) })
        {
            Device.Buttons[button]=1; Tick(); Check(GameButtonInput.Button(action,false,true) && GameButtonInput.Button(action,false,false), "hold action press " + action);
            for (int i=0; i<3; i++) { Tick(); Check(!GameButtonInput.Button(action,false,true) && GameButtonInput.Button(action,false,false), "hold action not continuous " + action); }
            Check(!GameButtonInput.Button(action,true,false), "negative hold action " + action);
            Device.Buttons[button]=0; Tick(); Check(!GameButtonInput.Button(action,false,false), "hold action held after release " + action);
        }

        // Look back is VerticalOrbit negative; forward, left and right are the other orbit poles.
        Device.Buttons[41]=1; Tick();
        Check(GameButtonInput.Axis(63)==-1 && GameButtonInput.Button(63,true,false) && !GameButtonInput.Button(63,false,false) && GameButtonInput.Axis(22)==0, "look back is not VerticalOrbit negative");
        Device.Buttons[41]=0; Tick(); Check(GameButtonInput.Axis(63)==0, "look back held after release");
        foreach (var (angle, action, value) in new[] { (0,63,1), (27000,22,-1), (9000,22,1) })
        { Device.Hats[0]=-1; Tick(); Device.Hats[0]=angle; Tick(); Check(GameButtonInput.Axis(action)==value, "look direction " + angle); }
        Device.Hats[0]=-1; Tick(); Check(GameButtonInput.Axis(22)==0 && GameButtonInput.Axis(63)==0, "look direction held after release");
        WheelInput.Close(); Array.Clear(Device.Buttons); Array.Fill(Device.Hats,-1);
    }
}
