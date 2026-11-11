using ArtOfSimRally.Mod;
using ArtOfSimRally.Mod.Shim;
using static ArtOfSimRally.Mod.ProfileButtons;

// The game's own actions from the shared profile's buttons, through the production Rewired postfixes: press edges, holds
// and releases per frame, the title screen's release, intro skips, the horn's owed release and the native action table.
static class GameButtonTests
{
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    // This frame's scripted shared values, and one status for every scripted action (Live, or a read failure).
    static readonly Dictionary<int, float> Values = new();
    static ShimStatus status = ShimStatus.Live;
    static BridgeTests.Source source;
    static void Hold(int action, bool held) => Values[action] = held ? 1f : 0f;
    static void Tick() { Time.frameCount++; WheelReaders.EnsureUpdatedThisFrame(); GameButtonInput.TickTitle(); }
    static bool Down(int action) => ProfileButtons.Button(action, true);
    static bool Held(int action) => ProfileButtons.Button(action, false);
    static bool Continue(bool original = false, Rewired.Player player = null)
    {
        object[] args = { player ?? PadManager.Player, original };
        typeof(GameContinuePatch).GetMethod("After", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, args);
        return (bool)args[1];
    }
    static bool GetButtonUp(int action) => false; // names the production postfix's original method
    static bool PatchedUp(int action, bool native)
    {
        const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        object[] args = { PadManager.Player, action, typeof(GameButtonTests).GetMethod("GetButtonUp", Private), native };
        typeof(GameButtonPatch).GetMethod("After", Private).Invoke(null, args);
        return (bool)args[3];
    }

    public static int Run()
    {
        Fixture.Setup(); Time.frameCount = 50000; Tick();
        foreach (int action in new[] { Confirm, Back, Start, NavUp, NavDown, NavLeft, NavRight, Camera, LookBack, Recover, Horn }) Values[action] = 0f;
        source = new BridgeTests.Source { Pressed = -1 };
        var core = new ScriptedCore { Script = () => Values.Select(kv => (kv.Key, status, kv.Value)).ToArray() };
        // Held at the handoff: never a press until released.
        Hold(Camera, true); Hold(NavUp, true);
        WheelReaders.AttachShared(source, core, () => true, () => true);
        GameButtonInput.Observe(12, 13, 16, 17);
        Tick(); Tick();
        Check(ShimInput.Consumer.Started && !Down(Camera) && !Down(NavUp), "a control held at startup pressed");
        Hold(Camera, false); Hold(NavUp, false); Tick();
        Hold(Camera, true); Tick();
        Check(Down(Camera) && Held(Camera), "camera fresh press missing");
        Check(Down(Camera), "second consumer lost the edge");
        Check(GameButtonInput.Button(61, false, true), "camera action 61 missing");
        Check(!GameButtonInput.Button(62, false, true), "an unrelated action got camera");
        Check(!GameButtonInput.Button(61, true, true), "negative camera action emitted");
        Check(GameButtonInput.Primary(PadManager.Player) && !GameButtonInput.Primary(new Rewired.Player()), "another player accepted");
        Tick(); Check(!Down(Camera) && Held(Camera), "held camera repeated");
        // Navigation: the module's vertical and horizontal actions, positive and negative poles.
        foreach (var (action, rewired, negative) in new[] { (NavUp, 13, false), (NavDown, 13, true), (NavLeft, 12, true), (NavRight, 12, false) })
        {
            Hold(action, false); Tick(); Hold(action, true); Tick();
            Check(Down(action) && GameButtonInput.Button(rewired, negative, true), "navigation missing: " + action);
            Check(GameButtonInput.Axis(rewired) == (negative ? -1 : 1), "navigation axis direction wrong: " + action);
            Check(!GameButtonInput.Button(rewired, !negative, true), "opposite navigation activated: " + action);
            Tick(); Check(!Down(action) && Held(action), "navigation hold repeated its press: " + action);
            Hold(action, false); Tick();
        }
        // Menu actions: submit, cancel and pause (19; never also 8, which would toggle twice).
        foreach (var (action, rewired) in new[] { (Confirm, 16), (Back, 17), (Start, 19) })
        {
            Hold(action, true); Tick();
            Check(GameButtonInput.Button(rewired, false, true), "menu action missing: " + rewired);
            Check(!GameButtonInput.Button(8, false, true), "pause double routed to 8");
            Hold(action, false); Tick();
        }
        // Focus, the settings panel and a read failure end a hold; recovery needs a release before the next press.
        foreach (var gate in new[] { "read", "focus", "panel" })
        {
            Hold(Camera, false); Tick(); Hold(Camera, true); Tick(); Check(Down(Camera), "setup press " + gate);
            if (gate == "read") status = ShimStatus.ReadFailed; else if (gate == "focus") Application.isFocused = false; else Main.SettingsVisible = true;
            Tick(); Check(!Held(Camera) && !Down(Camera), "gate leaked the button: " + gate);
            status = ShimStatus.Live; Application.isFocused = true; Main.SettingsVisible = false;
            Tick(); Check(!Held(Camera), "gate recovery replayed a held button: " + gate);
            Hold(Camera, false); Tick(); Hold(Camera, true); Tick(); Check(Down(Camera), "gate release did not rearm: " + gate);
        }
        Time.frameCount++; Check(!Down(Camera), "a stale snapshot was reused");
        // The game's UI module asks before the watchdog: the first query reads once; later ones and the watchdog see
        // the same edge, without another read.
        Hold(Camera, false); Tick();
        Hold(NavDown, true); Time.frameCount++;
        int reads = source.Reads;
        Check(GameButtonInput.Prepare(PadManager.Player) && source.Reads == reads + 1, "early consumer did not read exactly once");
        Check(GameButtonInput.Axis(13) == -1 && GameButtonInput.Button(13, true, true), "early menu query lost the edge");
        WheelReaders.EnsureUpdatedThisFrame();
        Check(GameButtonInput.Prepare(PadManager.Player) && GameButtonInput.Button(13, true, true) && source.Reads == reads + 1, "watchdog or second consumer read again or consumed the edge");
        Hold(NavDown, false); Tick(); Check(GameButtonInput.Axis(13) == 0 && !GameButtonInput.Button(13, true, true), "next frame kept the released button");
        Time.frameCount++;
        Check(!GameButtonInput.Prepare(new Rewired.Player()) && !Down(NavDown), "another player sampled the wheel");

        Title();
        Continues();
        NativeActions();
        HornRelease();
        WheelReaders.Close();
        return checks;
    }

    // The title screen ends on a fresh Start or Confirm release, on its own panel only (SplashScreenControl uses a
    // ButtonJustReleased delegate); a query never changes the scene.
    static void Title()
    {
        UIManager.Instance = new UIManager();
        foreach (int action in new[] { Confirm, Start })
        {
            SplashScreenControl.Instance = new SplashScreenControl();
            UIManager.Instance.PanelManager.Current = UIManager.Instance.PanelManager.SplashScreenPanel;
            Hold(action, false); Tick();
            Hold(action, true); Tick(); Check(SplashScreenControl.Instance.Ends == 0, "title advanced on press instead of release");
            Hold(action, false); Tick(); Check(SplashScreenControl.Instance.Ends == 1, "title release ignored");
            Tick(); Check(SplashScreenControl.Instance.Ends == 1, "title advanced twice");
        }
        SplashScreenControl.Instance = new SplashScreenControl();
        UIManager.Instance.PanelManager.Current = UIManager.Instance.PanelManager.SplashScreenPanel;
        Hold(Confirm, false); Tick(); Hold(Confirm, true); Tick();
        Hold(Confirm, false); Time.frameCount++;
        Check(GameButtonInput.Prepare(PadManager.Player) && SplashScreenControl.Instance.Ends == 0, "an input query changed the scene");
        WheelReaders.EnsureUpdatedThisFrame(); GameButtonInput.TickTitle();
        Check(SplashScreenControl.Instance.Ends == 1, "watchdog lost the early sampled title release");
        foreach (var gate in new[] { "read", "focus", "panel", "wrong-screen", "inactive", "startup-held" })
        {
            SplashScreenControl.Instance = new SplashScreenControl();
            UIManager.Instance.PanelManager.Current = UIManager.Instance.PanelManager.SplashScreenPanel;
            Hold(Confirm, false); Tick(); Hold(Confirm, true); Tick();
            if (gate == "read") status = ShimStatus.ReadFailed;
            if (gate == "focus") Application.isFocused = false;
            if (gate == "panel") Main.SettingsVisible = true;
            if (gate == "wrong-screen") UIManager.Instance.PanelManager.Current = new object();
            if (gate == "inactive") SplashScreenControl.Instance.isActiveAndEnabled = false;
            if (gate == "startup-held") { ProfileButtons.Reset(); Tick(); }
            Hold(Confirm, false); Tick(); Check(SplashScreenControl.Instance.Ends == 0, "title gate leaked a release: " + gate);
            status = ShimStatus.Live; Application.isFocused = true; Main.SettingsVisible = false;
            Tick(); Check(SplashScreenControl.Instance.Ends == 0, "title recovery manufactured a release: " + gate);
        }
        SplashScreenControl.Instance = new SplashScreenControl();
        UIManager.Instance.PanelManager.Current = UIManager.Instance.PanelManager.SplashScreenPanel;
        foreach (int action in new[] { Back, Camera, NavDown })
        {
            Hold(action, false); Tick(); Hold(action, true); Tick(); Hold(action, false); Tick();
            Check(SplashScreenControl.Instance.Ends == 0, "an unrelated button dismissed the title: " + action);
        }
        Hold(Confirm, false); Tick(); Hold(Confirm, true); Tick();
        SplashScreenControl.Instance.EndSplashScreen(); // The game's own Rewired event wins first.
        Hold(Confirm, false); Tick(); Check(SplashScreenControl.Instance.Ends == 1, "the game's event and the profile release both ended the title");
        UIManager.Instance = null; SplashScreenControl.Instance = null;
    }

    // Intro skips (GetAnyButtonDown): the game's own positives survive; only fresh Confirm/Start presses add to the
    // primary player's query.
    static void Continues()
    {
        foreach (int action in Values.Keys.ToArray()) Values[action] = 0f;
        Tick();
        Check(Continue(true) && !Continue(), "any-button changed native input or created a press");
        foreach (int action in new[] { Confirm, Start })
        {
            Hold(action, true); Time.frameCount++;
            int reads = source.Reads;
            Check(Continue() && Continue() && source.Reads == reads + 1, "continue query lost the edge or polled twice");
            Check(!Continue(player: new Rewired.Player()), "continue leaked to another player");
            WheelReaders.EnsureUpdatedThisFrame(); Check(Continue(), "watchdog consumed the continue edge");
            Tick(); Check(!Continue(), "held continue repeated");
            Hold(action, false); Tick(); Check(!Continue(), "continue release counted as a press");
        }
        foreach (int action in new[] { Back, Camera, NavDown })
        {
            Hold(action, true); Tick(); Check(!Continue(), "an unrelated button skipped the intro: " + action);
            Hold(action, false); Tick();
        }
        foreach (string gate in new[] { "read", "focus", "panel", "disabled", "startup-held" })
        {
            Hold(Confirm, false); Tick();
            if (gate == "read") status = ShimStatus.ReadFailed;
            if (gate == "focus") Application.isFocused = false;
            if (gate == "panel") Main.SettingsVisible = true;
            if (gate == "disabled") Main.Enabled = false;
            Hold(Confirm, true); Time.frameCount++;
            if (gate == "startup-held") ProfileButtons.Reset();
            Check(!Continue() && Continue(true), "continue gate changed native input or leaked: " + gate);
            status = ShimStatus.Live; Application.isFocused = true; Main.SettingsVisible = false; Main.Enabled = true;
            WheelReaders.EnsureUpdatedThisFrame();
            Hold(Confirm, false); Tick();
        }
    }

    // build 17584229: one table maps the game's action ids to the profile's actions, for dispatch and prompt labels.
    static void NativeActions()
    {
        foreach (var (id, negative, shared) in new (int, bool, string)[] {
            (30, false, "horn"), (44, false, "reset"), (63, true, "lookBack"), (61, false, "camera"), (19, false, "start"),
            (16, false, "confirm"), (17, false, "back"), (12, true, "navLeft"), (12, false, "navRight"), (13, true, "navDown"), (13, false, "navUp"),
            (14, false, "navRight"), (0, true, "steer"), (0, false, "steer"), (2, false, "throttle"), (3, false, "brake"), (4, false, "handbrake"), (5, false, "clutch") })
            Check(GameButtonInput.NativeAction(id, negative, out int action) && ShimFrame.Names[action] == shared, "native table " + id + (negative ? "-" : "+"));
        // No profile action: 8 pause (19 is), 64 view collectible, a negative horn, look forward/left/right, repair and restart.
        foreach (var (id, negative) in new[] { (8, false), (64, false), (30, true), (63, false), (22, true), (22, false), (45, false), (46, false), (-1, false) })
            Check(!GameButtonInput.NativeAction(id, negative, out _), "unrouted native action mapped: " + id + (negative ? "-" : "+"));
        GameButtonInput.Observe(14, 15, 16, 17);
        Check(GameButtonInput.NativeAction(12, false, out int up) && up == ShimAxisConsumer.ShiftUp &&
            GameButtonInput.NativeAction(13, false, out int down) && down == ShimAxisConsumer.ShiftDown, "sequential shift ids");
        GameButtonInput.Observe(12, 13, 16, 17);
        Check(!GameButtonInput.Button(0, false, false) && GameButtonInput.Axis(2) == 0, "a driving axis was dispatched as a game button");
        // Recover car: one press edge, then held for the game's own one-second timer.
        Hold(Recover, true); Tick(); Check(GameButtonInput.Button(44, false, true) && GameButtonInput.Button(44, false, false), "recover press");
        for (int i = 0; i < 3; i++) { Tick(); Check(!GameButtonInput.Button(44, false, true) && GameButtonInput.Button(44, false, false), "recover hold not continuous"); }
        Check(!GameButtonInput.Button(44, true, false), "negative recover");
        Hold(Recover, false); Tick(); Check(!GameButtonInput.Button(44, false, false), "recover held after release");
        // Look back is the vertical orbit's negative pole.
        Hold(LookBack, true); Tick();
        Check(GameButtonInput.Axis(63) == -1 && GameButtonInput.Button(63, true, false) && !GameButtonInput.Button(63, false, false), "look back is not the vertical orbit's negative pole");
        Hold(LookBack, false); Tick(); Check(GameButtonInput.Axis(63) == 0, "look back held after release");
    }

    // The horn plays on GetButtonDown(30) and stops only on GetButtonUp(30): a held horn cut off by the panel, focus, a
    // read failure or a reset still gets exactly one release, and resumes only after a fresh press.
    static void HornRelease()
    {
        Hold(Horn, false); Tick();
        Hold(Horn, true); Tick();
        Check(GameButtonInput.Button(30, false, true) && GameButtonInput.Button(30, false, false) && !PatchedUp(30, false), "horn press");
        Tick(); Check(!GameButtonInput.Button(30, false, true) && GameButtonInput.Button(30, false, false), "held horn repeated its press");
        Hold(Horn, false); Tick(); Check(PatchedUp(30, false) && !GameButtonInput.Button(30, false, false), "horn release missing");
        Tick(); Check(!PatchedUp(30, false) && PatchedUp(30, true) && !PatchedUp(8, false), "horn release repeated or the game's own release lost");
        foreach (string gate in new[] { "panel", "focus", "read", "reset" })
        {
            Hold(Horn, true); Tick(); Check(Held(Horn), "horn setup " + gate);
            if (gate == "panel") Main.SettingsVisible = true; else if (gate == "focus") Application.isFocused = false; else if (gate == "read") status = ShimStatus.ReadFailed; else ProfileButtons.Reset();
            Tick();
            bool early = ProfileButtons.Released(Horn);
            Main.SettingsVisible = false; Application.isFocused = true; status = ShimStatus.Live;
            if (!early) Tick();
            Check(early || ProfileButtons.Released(Horn), "interrupted horn never released: " + gate);
            Check(!Held(Horn), "interrupted horn resumed without a fresh press: " + gate);
            Hold(Horn, false); Tick(); Tick(); Check(!ProfileButtons.Released(Horn), "horn released twice: " + gate);
        }
    }
}
