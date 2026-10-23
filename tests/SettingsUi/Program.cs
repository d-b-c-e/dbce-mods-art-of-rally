using System.Reflection;
using System.Text.Json;
using System.Xml.Serialization;
using ArtOfSimRally.Mod;
using UnityEngine;
using Host=ArtOfSimRally.Mod.Main;
using Ui=UnityModManagerNet.UnityModManager.UI;
using Time=ArtOfSimRally.Mod.Time;

static class Program
{
    static int assertions;
    static void Check(bool ok,string reason){assertions++;if(!ok)throw new Exception(reason);}
    static string Xml(Settings c){using var w=new StringWriter();new XmlSerializer(typeof(Settings)).Serialize(w,c);return w.ToString();}
    static Settings Read(string xml)=> (Settings)new XmlSerializer(typeof(Settings)).Deserialize(new StringReader(xml));
    static readonly string Wheel="a5444f71-6773-43d7-b43f-3b83492d6f55";
    static void Inventory()
    {
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "SETTINGS-INVENTORY.json")));
        var options = new JsonSerializerOptions();
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var records = inventory.RootElement.GetProperty("fields").EnumerateArray().ToArray();
        var classes = new[] { "editable", "runtime-only", "F6 presentation state", "developer/diagnostic", "excluded/unused" };
        var settings = new (object Value, string File)[] {
            (new Settings(), "Mods/ArtOfSimRally/Settings.xml"),
            (new ArtOfSimRally.Mod.Triple.Settings(), "Mods/ArtOfSimRally/TripleScreen.xml")
        };
        int fields = 0;
        foreach (var (value, file) in settings)
        {
            var type = value.GetType();
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.IsDefined(typeof(XmlIgnoreAttribute))) continue;
                fields++;
                var matches = records.Where(r => r.GetProperty("settingsType").GetString() == type.FullName &&
                    r.GetProperty("key").GetString() == field.Name).ToArray();
                Check(matches.Length == 1, $"Inventory needs exactly one {type.FullName}.{field.Name} entry");
                var row = matches[0];
                Check(row.GetProperty("file").GetString() == file, $"Inventory file differs for {field.Name}");
                var recorded = JsonSerializer.Deserialize(row.GetProperty("default").GetRawText(), field.FieldType, options);
                Check(JsonSerializer.Serialize(recorded, field.FieldType, options) ==
                    JsonSerializer.Serialize(field.GetValue(value), field.FieldType, options),
                    $"Inventory default differs for {type.FullName}.{field.Name}");
            }
        }
        Check(records.Length == fields, "Inventory has unknown or duplicate persisted fields");
        foreach (var row in records.Concat(inventory.RootElement.GetProperty("actions").EnumerateArray()))
        {
            foreach (var key in new[] { "file", "key", "page", "label", "type", "displayUnit", "scale", "min", "max", "step", "default" })
                Check(row.TryGetProperty(key, out _), "Inventory metadata missing: " + key);
            Check(classes.Contains(row.GetProperty("classification").GetString()), "Unknown inventory classification");
            Check(!string.IsNullOrWhiteSpace(row.GetProperty("reason").GetString()), "Inventory classification needs a reason");
        }
    }
    // STD-016: telemetry is on for new settings; a saved Off survives load and save (UMM's XmlSerializer persists it).
    static void TelemetryDefault()
    {
        Check(new Settings().TelemetryEnabled, "new settings do not send telemetry by default");
        Check(Read("<Settings><Strength>50</Strength></Settings>").TelemetryEnabled, "settings saved without the field did not get telemetry on");
        var off = Read("<Settings><TelemetryEnabled>false</TelemetryEnabled></Settings>");
        Check(!off.TelemetryEnabled && !Read(Xml(off)).TelemetryEnabled, "a saved telemetry Off was not kept through load and save");
    }
    static void Views()
    {
        Check(new Settings().FyReference == Settings.DefaultFyReference && !SettingsViewPolicy.CustomFfb(new Settings()), "new standard reference not used by defaults/presentation");
        var oldTune = Read("<Settings><Strength>30</Strength><FyReference>11500</FyReference><CrashStrength>19.52381</CrashStrength></Settings>");
        Check(oldTune.FyReference == 11500f && oldTune.Strength == 30 && oldTune.CrashStrength == 19.52381f && SettingsViewPolicy.CustomFfb(oldTune), "saved legacy tune overwritten or hidden");
        var oldAgain = Read(Xml(oldTune));
        Check(oldAgain.FyReference == 11500f && oldAgain.Strength == 30 && oldAgain.CrashStrength == oldTune.CrashStrength, "legacy tune changed through save/load");
        oldAgain.ResetFfbTuning();
        Check(oldAgain.FyReference == Settings.DefaultFyReference && oldAgain.Strength == 50, "explicit reset did not adopt the new reference");
        var c=Read("<Settings><ForceFeedbackEnabled>false</ForceFeedbackEnabled><Smoothing>0.61</Smoothing><CrashStrength>19.52381</CrashStrength><LandingStrength>20</LandingStrength><BonnetHeight>1.4</BonnetHeight><PreferredDeviceGuid>"+Wheel+"</PreferredDeviceGuid></Settings>");
        Check(!SettingsViewPolicy.Advanced(c)&&SettingsViewPolicy.Page(c)==0,"legacy Setup did not open Simple Controls");
        Check(SettingsViewPolicy.CustomFfb(c)&&SettingsViewPolicy.CustomCamera(c),"custom tune hidden without summary");
        Check(!SettingsViewPolicy.CustomControls(c),"default controls labelled custom");
        c.DirectSteering=false;Check(SettingsViewPolicy.CustomControls(c),"custom controls hidden");c.DirectSteering=true;
        Check(SettingsViewPolicy.Select(c,true,2,false),"Advanced selection failed");
        var again=Read(Xml(c));Check(SettingsViewPolicy.Advanced(again)&&again.SettingsPage==3&&SettingsViewPolicy.Page(again)==2,"view/page not persistent");
        Check(!SettingsViewPolicy.Select(c,false,0,true)&&c.SettingsPage==3&&SettingsViewPolicy.Advanced(c),"edit guard discarded current page");
        SettingsViewPolicy.Select(c,false,0,false);
        Check(c.SettingsPage==1&&c.Smoothing==.61f&&c.CrashStrength==19.52381f&&c.LandingStrength==20&&c.BonnetHeight==1.4f&&!c.ForceFeedbackEnabled,
            "view normalization changed runtime settings");
        c.SettingsPage=1;Check(SettingsViewPolicy.Page(c)==0,"saved Controls page moved");
        c.SettingsPage=2;Check(SettingsViewPolicy.Page(c)==1,"saved FFB page moved");
        c.SettingsPage=5;Check(SettingsViewPolicy.Page(c)==4,"saved Help page moved");
        c.SettingsView="unknown";c.SettingsPage=999;
        Check(!SettingsViewPolicy.Advanced(c)&&SettingsViewPolicy.Page(c)==0&&!c.ForceFeedbackEnabled,"unknown presentation reset tune");
        var network=new ConnectionEdit();string oldHost=c.TelemetryHost;int oldPort=c.TelemetryPort;
        network.Begin(c);network.Host="127.0.0.2";network.Port="invalid";
        Check(!network.Apply(c)&&network.Editing&&c.TelemetryHost==oldHost&&c.TelemetryPort==oldPort,"partial connection committed");
        network.Cancel();Check(c.TelemetryHost==oldHost&&c.TelemetryPort==oldPort,"cancel changed connection");
        network.Begin(c);network.Host="127.0.0.2";network.Port="9001";
        Check(network.Apply(c)&&!network.Editing&&c.TelemetryHost=="127.0.0.2"&&c.TelemetryPort==9001,"connection not atomic");
    }
    static void SelectionAndMigration()
    {
        var c=new Settings{ForceFeedbackEnabled=false};
        Check(FfbSelection.FollowsSteering(c)&&!FfbSelection.TryTarget(c,out _,out _,out _,out _),"new unbound config chose first wheel");
        c.SteerBinding=$"Wheel|9|axis:0|32767|65535|guid:{Wheel}";
        Check(FfbSelection.TryTarget(c,out var name,out _,out var guid,out _)&&name=="Wheel"&&guid==Wheel&&!c.ForceFeedbackEnabled,"follow lost identity or enabled FFB");
        c.PreferredDeviceGuid=Guid.NewGuid().ToString();string explicitGuid=c.PreferredDeviceGuid;
        Check(!FfbSelection.FollowsSteering(c)&&FfbSelection.TryTarget(c,out _,out _,out guid,out _)&&guid==explicitGuid,"legacy explicit target changed");
        c.SteerBinding=$"Other|0|axis:0|20000|60000|guid:{Guid.NewGuid()}";
        Check(FfbSelection.TryTarget(c,out _,out _,out guid,out _)&&guid==explicitGuid,"steering rebind stole override");
        c.FfbDeviceMode="steering";Check(FfbSelection.TryTarget(c,out name,out _,out guid,out _)&&name=="Other"&&guid!=explicitGuid,"follow did not follow rebind");
        c.FfbDeviceMode="explicit";c.PreferredDeviceGuid="";c.PreferredDevice="Wheel";c.PreferredDeviceIndex=0;
        Check(!FfbSelection.TryTarget(c,out _,out _,out _,out _),"unverified legacy index accepted");
        string settingsBefore = Xml(c);
        var sharedGuid = Guid.NewGuid().ToString("D");
        FfbSelection.PrepareShared(true, "Shared steering", sharedGuid);
        Check(!FfbSelection.TryTarget(c,out _,out _,out _,out _),"invalid explicit override fell through to shared steering");
        Check(Xml(c)==settingsBefore,"shared target preparation wrote settings or enabled FFB");
        c.FfbDeviceMode="steering";
        Check(FfbSelection.TryTarget(c,out name,out var sharedIndex,out guid,out _)&&name=="Shared steering"&&sharedIndex==-1&&guid==sharedGuid,"shared steering did not override stale legacy binding");
        c.FfbDeviceMode="explicit";c.PreferredDeviceGuid=explicitGuid;
        Check(FfbSelection.TryTarget(c,out _,out _,out guid,out _)&&guid==explicitGuid,"shared startup stole explicit override");
        c.FfbDeviceMode="steering";FfbSelection.PrepareShared(true, reason:"Install the matching Art shared-input package and restart.");
        Check(!FfbSelection.TryTarget(c,out _,out _,out guid,out var sharedWhy)&&guid==""&&sharedWhy.Contains("matching Art"),"missing extension fell back to stale legacy target");
        FfbSelection.PrepareShared(false);
        Check(FfbSelection.TryTarget(c,out _,out _,out guid,out _)&&guid!=sharedGuid,"ordinary startup did not restore legacy follow semantics");
        c.FfbDeviceMode="explicit";c.PreferredDeviceGuid="";
        c.HandbrakeBinding=$"TSS|4|button:2|0|1|guid:{Wheel}";string legacy=c.HandbrakeBinding;
        Check(SettingsMigration.NeedsHandbrakeSplit(c),"legacy button not identified");SettingsMigration.SplitHandbrake(c);
        Check(c.HandbrakeButtonBinding==legacy&&c.HandbrakeBinding==""&&!c.ForceFeedbackEnabled,"legacy split lost binding or tune");
        c.HandbrakeBinding=$"TSS|4|axis:2|65535|0|guid:{Wheel}";string axis=c.HandbrakeBinding;SettingsMigration.SplitHandbrake(c);
        Check(c.HandbrakeBinding==axis&&c.HandbrakeButtonBinding==legacy,"axis/button coexistence migrated incorrectly");
        Check(Read(Xml(c)).HandbrakeButtonBinding==legacy,"button XML persistence failed");
        c.DiagnosticLogging=true;var leftOn=Read(Xml(c));
        Check(leftOn.DiagnosticLogging&&SettingsMigration.EndSessionOnlySettings(leftOn)&&!leftOn.DiagnosticLogging,"saved support logging survived launch");
        Check(!SettingsMigration.EndSessionOnlySettings(leftOn)&&leftOn.HandbrakeBinding==axis&&leftOn.Smoothing==c.Smoothing&&leftOn.HandbrakeButtonBinding==legacy,"logging reset reported again or touched other settings");
        c.DiagnosticLogging=false;
        c.BonnetHeight=1.9f;c.BumperHeight=1.2f;c.LandingStrength=20;c.KeyUp=KeyCode.U;
        c.ResetCameraMount(false);
        Check(c.BonnetHeight==.95f&&c.BumperHeight==1.2f&&c.LandingStrength==20&&c.KeyUp==KeyCode.U,"camera reset escaped selected pose");
        c.ResetFfbTuning();
        Check(!c.ForceFeedbackEnabled&&c.PreferredDevice=="Wheel"&&c.HandbrakeBinding==axis&&c.BumperHeight==1.2f&&c.KeyUp==KeyCode.U,
            "FFB reset changed preference/device/controls/camera");
        Check(c.Strength==50&&c.Smoothing==.2f&&c.LandingStrength==5&&c.CrashStrength==50&&!c.CrashEffectsEnabled,"FFB reset missed tuning");
    }
    static void Runtime()
    {
        Host.ResetFixture();Ui.Instance=new();FfbNative.Ready=true;FfbNative.Stops=FfbNative.Shutdowns=0;
        ArtOfSimRally.Mod.Application.isFocused=true;GameState.IsDriving=true;Input.Down.Clear();
        Input.Down.Add(KeyCode.F8);Host.TickSettingsUi();Input.Down.Clear();
        Check(!Host.Settings.ForceFeedbackEnabled&&FfbNative.Stops==1&&Host.Saves==1&&Host.Cancels==1,"F8 did not stop before saved Off");
        for(int i=0;i<10;i++)Host.TickSettingsUi();Check(!Host.Settings.ForceFeedbackEnabled&&Host.Requests==0,"panic auto-resumed");
        SettingsViewPolicy.Select(Host.Settings,true,1,false);Host.TickSettingsUi();Check(FfbNative.Stops==1&&!Host.Settings.ForceFeedbackEnabled,"view switch affected FFB");
        FfbNative.Ready=false;Host.SelectForceDevice();Check(Host.Requests==0&&!Host.Settings.ForceFeedbackEnabled,"picker cleared Off");
        Host.SetFeedbackEnabled(true);Check(Host.Requests==1&&Host.Settings.ForceFeedbackEnabled,"explicit On cannot resume");
        WheelInput.Pressed.Add(WheelInput.Channel.StopFfbButton);Host.TickSettingsUi();Check(!Host.Settings.ForceFeedbackEnabled,"USB stop failed to save Off");
        WheelInput.Pressed.Add(WheelInput.Channel.SettingsButton);Host.TickSettingsUi();Check(Ui.Instance.Opened,"USB Settings did not open same UI");Host.CloseSettings();
        Host.ToggleSettings();Check(Ui.Instance.Opened&&Ui.Instance.Selected==0&&Ui.Instance.Filter=="fixture","F6 route did not select same mod panel");
        SettingsPanel.Editing=true;Host.CloseSettings();Check(Ui.Instance.Opened&&!SettingsPanel.Editing,"close did not cancel first");
        Host.CloseSettings();Check(!Ui.Instance.Opened,"second close failed");
        Host.WriteSucceeds=false;Host.MarkSettingsDirty();GameState.IsDriving=false;Time.realtimeSinceStartup+=1;
        int saves=Host.Saves;Host.TickSettingsUi();Check(Host.Saves==saves+1&&Host.SettingsSaveStatus=="Write failed","failed save hidden");
        Host.TickSettingsUi();Check(Host.Saves==saves+1,"failed write retried each frame");
        Host.WriteSucceeds=true;Time.realtimeSinceStartup+=6;Host.TickSettingsUi();Check(Host.SettingsSaveStatus=="Saved","failed write not retried");
        // Footer save state (Lola 129455): settings autosave, shared bindings need Save bindings, so the footer never shows a
        // bare "Saved" while a binding draft is unsaved, keeps the saved-for-next-start state distinct, and keeps errors.
        Check(SettingsViewPolicy.FooterStatus("Saved",false,false)=="Saved"&&SettingsViewPolicy.FooterStatus("Changes pending",false,false)=="Changes pending",
            "footer without binding state changed");
        Check(SettingsViewPolicy.FooterStatus("Saved",true,false)=="Binding edits unsaved · Settings saved","unsaved binding draft hidden behind Saved");
        Check(SettingsViewPolicy.FooterStatus("Saved",false,true)=="Bindings saved for next start · Settings saved","saved-for-next-start state hidden");
        Check(SettingsViewPolicy.FooterStatus("Saved",true,true)=="Binding edits unsaved · Settings saved","a new draft over a saved binding not shown as unsaved");
        Check(SettingsViewPolicy.FooterStatus("Write failed",true,false)=="Binding edits unsaved · Write failed"&&
              SettingsViewPolicy.FooterStatus("Changes pending",false,true)=="Bindings saved for next start · Changes pending","settings status lost beside a binding state");
        Check(SettingsViewPolicy.FooterStatus("Saved",true,true,"Profile is read-only")=="Bindings not saved — Profile is read-only · Settings saved",
            "binding failure hidden behind settings Saved or pending next-start state");
        Check(SettingsViewPolicy.FooterStatus("Write failed",false,false,"Profile is read-only")=="Bindings not saved — Profile is read-only · Write failed",
            "ordinary settings failure hidden by binding failure");
        Check(SettingsViewPolicy.FooterStatus("Saved",true,false,"Bindings not saved: Profile is read-only")=="Bindings not saved — Profile is read-only · Settings saved",
            "the existing draft writer failure duplicates the shared binding failure label");
        // Standalone setup route (Lola 129479 item 3, sana 129606/129640): the common wording, with no claim that this
        // package bundles the setup.
        var setup=string.Join(" ",SettingsViewPolicy.SetupGuidance);
        Check(SettingsViewPolicy.SetupHeading=="Controls setup"&&SettingsViewPolicy.SetupGuidance.Length==2,"setup guidance block changed");
        Check(setup.Contains("close the game and run Set Up Controls.bat from the matching standalone Controls Setup, then restart")&&
              setup.Contains("Wheelkit is optional")&&setup.Contains("devices already opened by this profile"),"setup guidance wording");
        Check(!setup.Contains("this mod package")&&!setup.Contains("download"),"setup guidance claims a bundled or available download");
        Host.SuppressHostClose=true;
        var guard=typeof(SettingsCloseGuard).GetMethod("BeforeClose",BindingFlags.Static|BindingFlags.NonPublic);
        Check(!(bool)guard.Invoke(null,new object[]{false})&&!Host.SuppressHostClose,"UMM key-up ignored cancel guard");
        Check((bool)guard.Invoke(null,new object[]{false}),"UMM close permanently blocked");
        // Unsaved shared-binding draft (kiki 123963): one close intent is the panel button (Main.CloseSettings) followed by
        // UMM's ToggleWindow Harmony guard. The first close is held with the warning; the second closes, and the guard,
        // asked for the same intent, does not re-arm the warning. The draft is never saved by closing.
        Ui.Instance.ToggleWindow(false);Host.TickSettingsUi();
        SettingsPanel.HasDraft=true;Host.ToggleSettings();Host.TickSettingsUi();
        Host.CloseSettings();Check(Ui.Instance.Opened&&DraftCloseGuard.Warned,"first close with an unsaved draft is not held");
       Host.CloseSettings();Check(!Ui.Instance.Opened,"second close with an unsaved draft did not close");
        Check((bool)guard.Invoke(null,new object[]{false}),"the ToggleWindow guard re-armed the acknowledged draft warning");
        Host.TickSettingsUi();Check(!DraftCloseGuard.Warned,"hiding did not reset the draft warning");
        Host.ToggleSettings();Host.TickSettingsUi();
        Check(!(bool)guard.Invoke(null,new object[]{false})&&DraftCloseGuard.Warned,"a UMM-only close in a later session did not warn first");
        Check((bool)guard.Invoke(null,new object[]{false}),"a second UMM-only close was blocked");
        Ui.Instance.ToggleWindow(false);Host.TickSettingsUi();SettingsPanel.HasDraft=false;
        Host.ToggleSettings();Host.CloseSettings();Check(!Ui.Instance.Opened&&!DraftCloseGuard.Warned,"without a draft the first close did not close");
    }
    static int Main()
    {
        try{Inventory();TelemetryDefault();Views();SelectionAndMigration();Runtime();MenuIsolation();Display();Console.WriteLine(JsonSerializer.Serialize(new{status="passed",assertions,hardwareOutput=false,visualInspection=false}));return 0;}
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
    static void MenuIsolation()
    {
        Host.ResetFixture();Ui.Instance=new();StockUiInput.Reset();Input.Held.Clear();Input.Down.Clear();
        var player=Rewired.ReInput.players.AllPlayers[0];
        StockUiInput.Observe(new Rewired.Integration.UnityUI.RewiredStandaloneInputModule());
        Time.frameCount=100;
        Check(!StockUiInput.Blocked,"unopened menu was blocked");
        Input.Down.Add(Host.Settings.SettingsKey);
        Check(StockUiInput.Blocked,"opening key leaked before watchdog");
        Input.Down.Clear();Ui.Instance.Opened=true;
        player.Down.Add(17);player.Buttons.Add(17);player.Axes[101]=-1;
        Check(StockUiInput.Blocked&&!StockUiInput.ButtonDown(player,17)&&StockUiInput.Axis(player,101)==0,"live native menu inputs leaked");
        var module=new Rewired.Integration.UnityUI.RewiredStandaloneInputModule();
        var pointer=new Rewired.UI.PlayerPointerEventData{eligibleForClick=true,dragging=true,pointerPress=new(),rawPointerPress=new(),pointerDrag=new(),clickCount=2};
        module.AddPointer(pointer);
        var dispatch=typeof(StockUiDispatchGuard).GetMethod("BeforeProcess",BindingFlags.Static|BindingFlags.NonPublic);
        Check(!(bool)dispatch.Invoke(null,new object[]{module}),"native pointer/module dispatch not stopped");
        Check(!pointer.eligibleForClick&&!pointer.dragging&&pointer.pointerPress==null&&pointer.rawPointerPress==null&&pointer.pointerDrag==null&&pointer.clickCount==0,"pre-panel pointer press/drag survived ownership");
        Ui.Instance.Opened=false;
        for(int i=0;i<10;i++){Time.frameCount++;Check(StockUiInput.Blocked,"close handed back held cancel/navigation");}
        player.Buttons.Clear();player.Down.Clear();player.Axes.Clear();
        Input.Held.Add(KeyCode.Escape);Time.frameCount++;
        Check(StockUiInput.Blocked,"keyboard cancel released to native quit");
        Input.Held.Clear();Input.Held.Add(KeyCode.Mouse0);Time.frameCount++;
        Check(StockUiInput.Blocked,"held pointer released to native UI");
        Input.Held.Clear();WheelInput.SettingsHeld=1;Time.frameCount++;
        Check(StockUiInput.Blocked,"held Settings button bypassed close barrier");
        WheelInput.SettingsHeld=0;ArtOfSimRally.Mod.Application.isFocused=false;Time.frameCount++;
        Check(StockUiInput.Blocked,"unfocused input was treated as released");
        ArtOfSimRally.Mod.Application.isFocused=true;Rewired.ReInput.isReady=false;Time.frameCount++;
        Check(StockUiInput.Blocked,"Rewired teardown treated as release");
        Rewired.ReInput.isReady=true;Time.frameCount++;
        Check(StockUiInput.Blocked&&StockUiInput.Blocked,"same-frame multiple dispatch calls drained barrier");
        Time.frameCount++;Check(StockUiInput.Blocked,"release frame leaked a key-up");
        Time.frameCount++;Check(!StockUiInput.Blocked,"neutral controls never returned native input");
        player.Down.Add(17);Check(StockUiInput.ButtonDown(player,17),"fresh cancel failed after neutral handback");player.Down.Clear();
        StockUiInput.Capture();Host.Enabled=false;Check(!StockUiInput.Blocked,"disabled mod trapped native controls");Host.Enabled=true;
        StockUiInput.Capture();player.Negative.Add(102);Time.frameCount++;
        Check(StockUiInput.Blocked,"negative controller navigation bypassed barrier");player.Negative.Clear();
        Ui.Instance.Opened=true;Time.frameCount++;Check(StockUiInput.Blocked,"reopening lost ownership");
        Ui.Instance.Opened=false;StockUiInput.Reset();
        Input.Held.Add(KeyCode.Mouse0);Check(StockUiInput.HandoffHeld(),"mouse press did not hold programmatic handoff");Input.Held.Clear();
        WheelInput.SettingsHeld=1;Check(StockUiInput.HandoffHeld(),"USB Settings press did not hold programmatic handoff");WheelInput.SettingsHeld=0;
        Input.Held.Add(KeyCode.JoystickButton0);Check(!StockUiInput.HandoffHeld(),"parked joystick button trapped programmatic handoff");Input.Held.Clear();
        // Build17584229 ReplayManager.Update polls24/25 only inside active
        // playback. Hold each through close; native Axis must remain zero.
        foreach(int action in new[]{24,25})
        {
            StockUiInput.Capture();player.Axes[action]=.05f;
            for(int i=0;i<6;i++){Time.frameCount++;Check(StockUiInput.Axis(player,action)==0&&StockUiInput.Blocked,"held replay scrub resumed at close: "+action);}
            player.Axes.Remove(action);
            for(int i=0;i<3;i++){Time.frameCount++;StockUiInput.Axis(player,action);}
            Check(!StockUiInput.Blocked,"released replay axis never returned input");
        }
        StockUiInput.Capture();player.Axes[24]=1;player.Axes[25]=-1;
        Time.frameCount+=2; // no replay polling in this context
        for(int i=0;i<3;i++){Time.frameCount++;_ = StockUiInput.Blocked;}
        Check(!StockUiInput.Blocked,"inactive replay controls trapped stock menus");
        player.Axes.Clear();StockUiInput.Reset();
    }
    static void Display()
    {
        Check(SettingsDisplayPolicy.Scale(2160,1,false)==2,"4K automatic content remained 1x");
        Check(SettingsDisplayPolicy.Scale(720,1,false)==1,"720p default text shrank");
        Check(SettingsDisplayPolicy.Scale(2160,1.5f,false)==1.5f,"explicit UMM scale was replaced");
        Check(SettingsDisplayPolicy.Scale(2160,1,true)==1,"Use UMM scale ignored explicit 1x");
        foreach(int h in new[]{720,1080,1440,2160})
        {
            float scale=SettingsDisplayPolicy.Scale(h,1,false);int w=h*16/9;
            Check(SettingsDisplayPolicy.Width(w,scale,0)<=w-100,"automatic content escaped screen");
            Check(SettingsDisplayPolicy.PageHeight(h,scale,0)<=h*.45f,"page scroll escaped screen");
        }
        Check(SettingsDisplayPolicy.Width(3840,2,1000)==930,"explicit host width ignored");
        Check(SettingsDisplayPolicy.Width(3840,2,0)==890,"zero UMM preference treated as unlimited 4K viewport");
        Check(SettingsDisplayPolicy.Width(3840,2,960)==890,"actual default UMM host escaped");
        Check(SettingsDisplayPolicy.PageHeight(2160,2,0)>=300 && SettingsDisplayPolicy.PageHeight(2160,2,0)<=340,
            "compact header must leave room for an actionable binding group inside the default host");
        // Measured fit (run-cd67994: footer below UMM's viewport, kiki 126079). Values as that capture drew them at 1440p.
        Check(SettingsDisplayPolicy.BodyHeight(0,87,150,1,420)==420,"unmeasured body did not keep the estimate");
        Check(SettingsDisplayPolicy.BodyHeight(583,87,float.NaN,1,420)==420,"NaN measurement replaced the estimate");
        float fit=SettingsDisplayPolicy.BodyHeight(583,87,150,1,420);
        Check(87+150+fit<=583 && fit>=300,"measured 1440p panel still overflows UMM's viewport or wastes it: "+fit);
        float narrow=SettingsDisplayPolicy.BodyHeight(583,87,260,1.33f,420);
        Check(87+260+narrow<=583 && narrow<fit,"wrapped narrow header did not shrink the body to keep the footer in view: "+narrow);
        Check(SettingsDisplayPolicy.BodyHeight(300,87,250,1,420)==120,"tiny viewport body below the readable minimum");
        float below=SettingsDisplayPolicy.BodyHeight(583,400,150,1,420);
        Check(150+below<=583 && below>=300,"panel below other mods shrank instead of fitting when scrolled to: "+below);
        Check(SettingsDisplayPolicy.PageColumns(890,1)==5,"ordinary host did not keep five tabs on one row");
        Check(SettingsDisplayPolicy.PageColumns(700,2)==4,"narrow host did not wrap compact tabs");
        Check(SettingsDisplayPolicy.PageColumns(200,5)==1,"narrow high-scale page grid invalid");
        foreach(float s in new[]{.5f,1f,1.5f,2f,3f,4f,5f})
        foreach(float host in new[]{960f,1280f,1920f})
        {
            float body=SettingsDisplayPolicy.Width(3840,s,host)-30*s;
            bool stack=SettingsDisplayPolicy.StackRows(body,s);
            Check(stack || 310*s+20+40 <= body,"compact binding actions escape body at "+s+"x/"+host);
        }
        Check(SettingsDisplayPolicy.StackRows(770,4),"960px/4x bound sequential row not stacked");
        Check(SettingsDisplayPolicy.StackRows(740,5),"960px/5x camera/dropdown row not stacked");
        Check(!SettingsDisplayPolicy.StackRows(830,2),"default4K binding rows unnecessarily stacked");
        Check(new Settings().SettingsFollowHostScale,"new settings did not follow UMM scale by default");
        var c=new Settings{SettingsFollowHostScale=true};Check(Read(Xml(c)).SettingsFollowHostScale,"display preference not persisted");
    }
}
