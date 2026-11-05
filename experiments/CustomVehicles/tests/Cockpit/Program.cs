using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using RallyCustomVehicles;
using UnityEngine;
using Driver=RallyCustomVehicles.Main;
using Keys=ArtOfSimRally.Mod.Settings;
using Art=ArtOfSimRally.Mod.Main;
using Triple=ArtOfSimRally.Mod.Triple.TripleScreen;

internal static class Program
{
    private static string scratch;
    private static int count,sequence;
    private static int Main(string[] args)
    {
        scratch=Path.Combine(Path.GetTempPath(),"cv-cockpit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);
        try
        {
            if(args.Length==0 || args[0]=="--bounds")Bounds();
            if(args.Length==0 || args[0]=="--shared-fov")SharedFov();
            if(args.Length==0 || args[0]=="--imported-f7")ImportedF7();
#if !BASELINE
            if(args.Length==0){SetsAndReset();Legacy();Failures();Invalid();InputGates();Consumer();}
#endif
            Console.WriteLine("PASS "+count+" cockpit assertions; production store/consumer, simulated host, no Unity scene/game/device.");
            Directory.Delete(scratch,true);return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);Console.Error.WriteLine("Preserved fixture: "+scratch);return 1;}
    }
    private static void Check(bool condition,string message){count++;if(!condition)throw new Exception(message);}
    private static void Same(float[] a,float[] b,string message)
    {Check(a.Length==b.Length && a.Zip(b,(x,y)=>Math.Abs(x-y)<.00001f).All(x=>x),message+" actual=["+string.Join(",",a)+"]");}
    private static string PathFor()=>Path.Combine(scratch,(++sequence)+"/cockpit.json");
    private static CockpitViewStore Store(string path=null)
    {var s=new CockpitViewStore(path??PathFor());s.Load();
#if !BASELINE
        s.ConfigureCars(new[]{"test.a","test.b"});
#endif
        return s;}
    private static void Throws(Action action,string message)
    {bool caught=false;try{action();}catch(Exception){caught=true;}Check(caught,message);}
    private static void Bounds()
    {
        string path=PathFor();var s=Store(path);var v=new[]{-30f,20f,40f,400f,250f};s.Set("test.a",v);
        Same(Store(path).Get("test.a"),v,"finite cockpit values clipped");
#if !BASELINE
        Check(CockpitViewStore.LensFov(250)==179 && CockpitViewStore.LensFov(-10)==1,"only final lens is bounded");
        var copy=s.Get("test.a");copy[0]=99;Same(s.Get("test.a"),v,"Get does not alias state");
        v[0]=33;Check(s.Get("test.a")[0]==-30,"Set does not alias input");
#endif
    }
    private static void SharedFov()
    {
        string path=PathFor();var s=Store(path);s.Set("test.a",new[]{1f,2f,3f,8f,83f});
        Same(Store(path).Get("test.b"),new[]{0f,0f,0f,6f,83f},"cockpit FOV not shared");
    }
    private static void ImportedF7()
    {
        foreach(bool realistic in new[]{false,true})
        foreach(string name in new[]{"KeyUp","KeyDown","KeyForward","KeyBack","KeyLeft","KeyRight","KeyPitchDown","KeyPitchUp","KeyFovUp","KeyFovDown","KeyReset"})
        {
            string path=PathFor();var s=Store(path);s.Set("test.a",new[]{.1f,.2f,.3f,9f,81f});Driver.Begin(s,"test.a",realistic);
            var before=Driver.Values();byte[] bytes=File.ReadAllBytes(path);typeof(Keys).GetField(name).SetValue(Art.Settings,KeyCode.F7);
            Driver.Press(KeyCode.F7);
            Same(Driver.Values(),before,"imported F7 changed cockpit: "+name);
            Check(File.ReadAllBytes(path).SequenceEqual(bytes),"imported F7 wrote cockpit: "+name);
        }
    }
#if !BASELINE
    private static void SetsAndReset()
    {
        string path=PathFor();var s=Store(path);
        s.SetView("test.a",false,null,new[]{1f,2f,3f,8f,83f},true);
        s.SetView("test.b",false,null,new[]{4f,5f,6f,10f,83f});
        s.SetView("test.a",true,37.6,new[]{-1f,-2f,-3f,-8f,42.6f},true);
        s.SetView("test.b",true,37.6,new[]{-4f,-5f,-6f,-10f,42.6f});s=Store(path);
        Same(s.GetView("test.a",false,null),new[]{1f,2f,3f,8f,83f},"Game A restart");
        Same(s.GetView("test.b",true,37.6),new[]{-4f,-5f,-6f,-10f,42.6f},"Realistic B restart");
        Check(Math.Abs(s.GetView("test.a",true,40)[4]-45)<.0001,"seat change retains Realistic FOV offset");
        s.Reset("test.a",true,40);s=Store(path);
        Same(s.GetView("test.a",true,40),new[]{0f,0f,0f,6f,40f},"reset calculated current car/set");
        Same(s.GetView("test.b",true,40),new[]{-4f,-5f,-6f,-10f,40f},"reset shared FOV preserves other car pose");
        Same(s.GetView("test.a",false,null),new[]{1f,2f,3f,8f,83f},"reset preserves other set");
        var j=JObject.Parse(File.ReadAllText(path));Check((int)j["schemaVersion"]==2 && (float)j["realisticFovOffset"]==0,"stored offsets schema");
        Check((float)j["gamePoseOffsets"]["test.a"][3]==2,"pitch stored relative to calculated six degrees");
        Throws(()=>s.SetView("test.a",false,null,new[]{float.NaN,0f,0f,6f,65f}),"nonfinite edit rejected");
        Check(!s.HasPendingChanges,"invalid edits do not create pending data");
    }
    private static string LegacyFile(string text)
    {string p=PathFor();Directory.CreateDirectory(Path.GetDirectoryName(p));File.WriteAllText(p,text,new UTF8Encoding(false));return p;}
    private static void Legacy()
    {
        string path=LegacyFile("{\r\n \"test.a_realistic\": [0,0.02,-0.279999971,2,37.6],\r\n \"test.a\": [0,0,0,6,65]\r\n}\r\n");
        byte[] original=File.ReadAllBytes(path);DateTime time=File.GetLastWriteTimeUtc(path);var s=Store(path);
        Same(s.GetView("test.a",true,37.6),new[]{0f,.02f,-.279999971f,2f,37.6f},"owner-shaped legacy view preserved");
        Same(s.GetView("test.b",true,37.6),new[]{0f,0f,0f,6f,37.6f},"single legacy FOV shared before migration");
        Check(File.ReadAllBytes(path).SequenceEqual(original) && File.GetLastWriteTimeUtc(path)==time,"Load/Get never rewrite legacy");
        Check(!File.Exists(path+".legacy-v1.json"),"no backup/write on read");
        var v=s.GetView("test.a",true,37.6);v[0]=.2f;s.SetView("test.a",true,37.6,v);
        Check(File.ReadAllBytes(path+".legacy-v1.json").SequenceEqual(original),"exact original backup");
        Check(File.ReadAllBytes(path+".previous").SequenceEqual(original),"atomic replace keeps previous bytes");
        s=Store(path);Same(s.GetView("test.a",true,40),new[]{.2f,.02f,-.279999971f,2f,40f},"migration stored zero FOV offset");
        Same(s.GetView("test.a",false,null),new[]{0f,0f,0f,6f,65f},"unmigrated other set unchanged");
        path=LegacyFile("{\"test.a\":[1,2,3,8,65],\"test.b\":[4,5,6,9,75]}");s=Store(path);
        Check(s.NeedsFovChoice(false),"legacy conflict surfaced");v=s.Get("test.a");v[0]=2;s.SetView("test.a",false,null,v);
        s=Store(path);Check(s.Get("test.a")[4]==65 && s.Get("test.b")[4]==75 && s.NeedsFovChoice(false),"pose edit retains differing legacy FOVs");
        Driver.Begin(s,"test.a");Driver.Press(KeyCode.Keypad5);
        s=Store(path);Check(!s.NeedsFovChoice(false) && s.Get("test.b")[4]==65,"equal-value reset chooses shared FOV");
        Same(s.Get("test.b"),new[]{4f,5f,6f,9f,65f},"legacy B pose preserved during A reset");
        path=LegacyFile("{\"test.a\":[0,0,0,6,65],\"test.b\":[4,5,6,9,75]}");s=Store(path);
        Driver.Begin(s,"test.a");Driver.Press(KeyCode.Keypad5);
        Check(!Store(path).NeedsFovChoice(false) && Store(path).Get("test.b")[4]==65,"unchanged reset still commits explicit shared choice");
        path=LegacyFile("{\"test.a_realistic\":[1,2,3,8,47.6]}");s=Store(path);
        v=s.GetView("test.a",true,null);v[0]=2;s.SetView("test.a",true,null,v);
        var json=JObject.Parse(File.ReadAllText(path));Check(json["realisticFovOffset"].Type==JTokenType.Null && json["lastRealisticBaseline"].Type==JTokenType.Null,"unknown seat never seeds guessed FOV baseline");
        Throws(()=>s.Reset("test.a",true,null),"unknown-seat reset refused");
        Same(s.GetView("test.a",true,37.6),new[]{2f,2f,3f,8f,47.6f},"pose save preserves unknown-seat legacy FOV");
        v=s.GetView("test.a",true,37.6);s.SetView("test.a",true,37.6,v);Check(Math.Abs(Store(path).GetView("test.b",true,40)[4]-50)<.0001,"late measured seat establishes actual offset");
    }
    private static void Failures()
    {
        string path=PathFor();var s=Store(path);s.Set("test.a",new[]{0f,0f,0f,6f,65f});
        byte[] disk=File.ReadAllBytes(path);
        using(var held=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None))
        {
            Throws(()=>s.SetView("test.a",false,null,new[]{1f,2f,3f,8f,65f}),"locked save rejected");
            Check(s.HasPendingChanges,"failed IO retained pending session edit");Same(s.Get("test.a"),new[]{1f,2f,3f,8f,65f},"pending session view readable");
            Throws(()=>s.SetView("test.b",false,null,new[]{4f,5f,6f,9f,80f},true),"second pending edit locked");
        }
        Check(File.ReadAllBytes(path).SequenceEqual(disk),"failed IO preserved disk");s.Flush();
        Check(!s.HasPendingChanges,"retry completed pending edit");s=Store(path);
        Same(s.Get("test.a"),new[]{1f,2f,3f,8f,80f},"retry retains A plus newer shared FOV");
        Same(s.Get("test.b"),new[]{4f,5f,6f,9f,80f},"retry retains latest B");
        Check(Directory.GetFiles(Path.GetDirectoryName(path),"*.tmp-*").Length==0,"own temporary files removed");
        string external=File.ReadAllText(path)+" ";File.WriteAllText(path,external);
        Throws(()=>s.Set("test.a",new[]{2f,2f,3f,8f,85f}),"external edit refused");
        Check(File.ReadAllText(path)==external && s.HasPendingChanges,"external bytes preserved, session edit retained");
        path=LegacyFile("{\"test.a\":[0,0,0,6,65]}");s=Store(path);File.WriteAllText(path+".legacy-v1.json","preserve");
        Throws(()=>s.Set("test.a",new[]{1f,0f,0f,6f,65f}),"conflicting legacy backup refused");
        Check(File.ReadAllText(path+".legacy-v1.json")=="preserve" && File.ReadAllText(path).StartsWith("{\"test.a\""),"different backup and original preserved");
    }
    private static void Invalid()
    {
        foreach(string text in new[]{"null","[]","{\"test.a\":[1,2]}","{\"test.a\":[0,0,0,6,NaN]}","{\"test.a\":[0,0,0,6,65],\"test.a\":[0,0,0,6,66]}",
            "{\"schemaVersion\":3}","{\"schemaVersion\":2,\"unknown\":true}","{\"schemaVersion\":2,\"gamePoseOffsets\":null}",
            "{\"schemaVersion\":2,\"realisticFovOffset\":1}","{} {}"})
        {string p=LegacyFile(text);Throws(()=>Store(p),"invalid JSON/schema refused: "+text);Check(File.ReadAllText(p)==text,"bad file unchanged");}
    }
    private static void InputGates()
    {
        foreach(var reserved in new[]{KeyCode.F7,KeyCode.F8,KeyCode.F10,KeyCode.F6,KeyCode.F9,KeyCode.Mouse0,KeyCode.JoystickButton0,(KeyCode)99999,KeyCode.None})
        {
            var s=Store();Driver.Begin(s,"test.a");Art.Settings.SettingsKey=KeyCode.F9;
            Art.Settings.KeyForward=reserved;var before=Driver.Values();Driver.Press(reserved);
            if(reserved==KeyCode.F6)Check(Driver.Values()[2]>.019f,"rebound settings permits former F6");
            else Same(Driver.Values(),before,"reserved/nonkeyboard/unbound key ignored "+reserved);
        }
        var store=Store();Driver.Begin(store,"test.a");Art.Settings.KeyForward=KeyCode.None;Driver.Press(KeyCode.Keypad8);
        Check(Driver.Values()[2]==0,"None never reverts to fallback");
        foreach(string gate in new[]{"focus","panel","off","capture","shift","control","alt","windows"})
        {
            Driver.Begin(Store(),"test.a");RallyCustomVehicles.Input.Press(KeyCode.Keypad8);
            switch(gate)
            {
                case "focus":RallyCustomVehicles.Application.isFocused=false;break;
                case "panel":UnityModManagerNet.UnityModManager.UI.Instance=new UnityModManagerNet.UnityModManager.UI{Opened=true};break;
                case "off":Art.Settings.CameraTuningKeys=false;break;
                case "capture":ArtOfSimRally.Mod.CameraKeys.Listening=0;break;
                case "shift":RallyCustomVehicles.Input.Held.Add(KeyCode.LeftShift);break;
                case "control":RallyCustomVehicles.Input.Held.Add(KeyCode.RightControl);break;
                case "alt":RallyCustomVehicles.Input.Held.Add(KeyCode.AltGr);break;
                case "windows":RallyCustomVehicles.Input.Held.Add(KeyCode.LeftWindows);break;
            }
            Driver.Tick();Check(Driver.Values()[2]==0,"gate suppresses action "+gate);
            RallyCustomVehicles.Application.isFocused=true;UnityModManagerNet.UnityModManager.UI.Instance=null;Art.Settings.CameraTuningKeys=true;
            ArtOfSimRally.Mod.CameraKeys.Listening=-1;RallyCustomVehicles.Input.Press(KeyCode.Keypad8);Driver.Tick();
            Check(Driver.Values()[2]==0,"held action suppressed after gate "+gate);
            RallyCustomVehicles.Input.Clear();Driver.Tick();Driver.Press(KeyCode.Keypad8);Check(Driver.Values()[2]>.019f,"new press works after release "+gate);
        }
        Driver.Begin(Store(),"test.a");Driver.WithoutArt();
        Driver.Press(KeyCode.KeypadMinus);Check(Driver.Values()[4]==67,"fallback minus widens FOV");
        Driver.Press(KeyCode.KeypadPlus);Check(Driver.Values()[4]==65,"fallback plus narrows FOV");
    }
    private static void Consumer()
    {
        string path=PathFor();var s=Store(path);Driver.Begin(s,"test.a");Check(!File.Exists(path),"production LoadView does not write");
        Driver.Press(KeyCode.Keypad8);Driver.Press(KeyCode.KeypadMinus);
        Same(Store(path).Get("test.a"),new[]{0f,0f,.02f,6f,67f},"real keyboard consumer persists pose and FOV");
        Driver.Switch("test.b",false,null);Same(Driver.Values(),new[]{0f,0f,0f,6f,67f},"consumer car switch independent pose/shared FOV");
        Driver.Switch("test.a",true,37.6);Check(Math.Abs(Driver.Values()[4]-37.6f)<.0001,"consumer Realistic calculated start");
        Driver.Press(KeyCode.Keypad4);Driver.Press(KeyCode.KeypadMinus);Driver.Switch("test.a",false,null);
        Same(Driver.Values(),new[]{0f,0f,.02f,6f,67f},"consumer set switch retains Game view");
        Driver.Switch("test.a",true,40);Check(Driver.Values()[4]==42,"consumer seat change retains offset");
        RallyCustomVehicles.GUILayout.Click="Reset driver view";Driver.Draw();
        Same(Driver.Values(),new[]{0f,0f,0f,6f,40f},"UI reset same calculated baseline as keyboard");
        Check(Triple.LastNote!=null && Triple.LastNote.Contains("field of view reset for every car"),"real overlay notification");
        Driver.Switch("test.b",true,40);Check(Driver.Values()[4]==40,"UI reset shared within set");
        Driver.Switch("test.a",false,null);byte[] original=File.ReadAllBytes(path);
        using(var held=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None))
        {Driver.Press(KeyCode.Keypad8);Driver.Switch("test.b",false,null);Driver.Press(KeyCode.Keypad4);Check(s.HasPendingChanges,"consumer retains session edits under failed IO");}
        Check(File.ReadAllBytes(path).SequenceEqual(original),"consumer failed save keeps disk");Driver.Retry(4);Check(s.HasPendingChanges,"retry backoff honored");
        Driver.Retry(6);Check(!s.HasPendingChanges,"consumer pending retry completes");s=Store(path);
        Check(Math.Abs(s.Get("test.a")[2]-.04f)<.0001 && Math.Abs(s.Get("test.b")[0]+.02f)<.0001,"consumer retry preserves edits across car switch");
    }
#endif
}
