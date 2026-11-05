using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Only Unity input/time/UI and optional Art reflection endpoints are substituted.
// Store, dispatch, save/reset and overlay-notification code are production sources.
namespace RallyCustomVehicles
{
    internal static class Input
    {
        internal static readonly HashSet<KeyCode> Held=new HashSet<KeyCode>(), Down=new HashSet<KeyCode>();
        public static bool GetKey(KeyCode key)=>key!=KeyCode.None && Held.Contains(key);
        public static bool GetKeyDown(KeyCode key)=>key!=KeyCode.None && Down.Contains(key);
        internal static void Clear(){Held.Clear();Down.Clear();}
        internal static void Press(KeyCode key){Clear();Held.Add(key);Down.Add(key);}
    }
    internal static class Application { public static bool isFocused=true; }
    internal static class Time { public static float unscaledTime; }
    internal static class GUILayout
    {
        internal static string Click;
        public static object Width(float _) => null;
        public static void BeginHorizontal(){} public static void EndHorizontal(){}
        public static void Label(string text,params object[] options){}
        public static bool Button(string text,params object[] options)
        {if(Click!=text)return false;Click=null;return true;}
    }
    public static partial class Main
    {
        internal static readonly List<string> Messages=new List<string>();
        private static void Log(string text)=>Messages.Add(text);
#if BASELINE
        private static Vector3 eyeAdjustment;
        private static float pitch=6, fov=65;
        private static string viewFor;
        internal static CockpitViewStore CockpitViews;
        private static object ArtConfig()=>ArtOfSimRally.Mod.Main.Settings;
        private static bool ArtKeysOn()=>ArtOfSimRally.Mod.Main.Settings.CameraTuningKeys;
        private static double? ArtRealisticDegrees()=>ArtOfSimRally.Mod.Triple.TripleScreen.RealisticStartDegrees;
        private static void SaveView()=>CockpitViews.Set(viewFor,new[]{eyeAdjustment.x,eyeAdjustment.y,eyeAdjustment.z,pitch,fov});
#endif
        internal static void Begin(CockpitViewStore store,string id,bool realistic=false,double? seat=37.6)
        {
            CockpitViews=store;Application.isFocused=true;Input.Clear();GUILayout.Click=null;
            UnityModManagerNet.UnityModManager.UI.Instance=null;
            ArtOfSimRally.Mod.Main.Settings=new ArtOfSimRally.Mod.Settings();
            ArtOfSimRally.Mod.Triple.TripleScreen.RealisticStartDegrees=seat;
            ArtOfSimRally.Mod.Triple.TripleScreen.CockpitRealisticActive=realistic;
            ArtOfSimRally.Mod.CameraKeys.Listening=-1;HarmonyLib.AccessTools.ArtAvailable=true;
#if BASELINE
            viewFor=id+(realistic ? "_realistic" : "");var v=store.Get(viewFor);
            eyeAdjustment=new Vector3(v[0],v[1],v[2]);pitch=v[3];fov=v[4];
#else
            artMainType=artTripleType=artKeysType=null;retryCockpitAt=0;Time.unscaledTime=0;cockpitSaveStatus="";
            LoadView(id,id+(realistic ? "_realistic" : ""));TuneCockpit();
#endif
        }
        internal static float[] Values()=>new[]{eyeAdjustment.x,eyeAdjustment.y,eyeAdjustment.z,pitch,fov};
        internal static void Tick()=>TuneCockpit();
        internal static void Press(KeyCode key){Input.Press(key);TuneCockpit();Input.Clear();TuneCockpit();}
#if !BASELINE
        internal static void Switch(string id,bool realistic,double? seat)
        {SaveView();ArtOfSimRally.Mod.Triple.TripleScreen.RealisticStartDegrees=seat;LoadView(id,id+(realistic ? "_realistic" : ""));Input.Clear();TuneCockpit();}
        internal static void Retry(float now){Time.unscaledTime=now;FlushCockpitPending();}
        internal static void Draw()=>DrawCockpitTuning();
        internal static void WithoutArt(){artMainType=artTripleType=artKeysType=null;HarmonyLib.AccessTools.ArtAvailable=false;}
#endif
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static bool ArtAvailable=true;
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        public static Type TypeByName(string name)=>ArtAvailable ? typeof(AccessTools).Assembly.GetType(name) : null;
        public static FieldInfo Field(Type type,string name)=>type.GetField(name,Flags);
        public static PropertyInfo Property(Type type,string name)=>type.GetProperty(name,Flags);
        public static MethodInfo Method(Type type,string name)=>type.GetMethod(name,Flags);
    }
}
namespace UnityModManagerNet
{
    public static class UnityModManager
    {public sealed class UI {public static UI Instance;public bool Opened;} }
}
namespace ArtOfSimRally.Mod
{
    public static class Main {public static Settings Settings {get;set;}=new Settings();}
    public sealed class Settings
    {
        public bool CameraTuningKeys=true;
        public KeyCode SettingsKey=KeyCode.F6;
        public KeyCode KeyUp=KeyCode.Keypad9,KeyDown=KeyCode.Keypad3,KeyForward=KeyCode.Keypad8,KeyBack=KeyCode.Keypad2,
            KeyLeft=KeyCode.Keypad4,KeyRight=KeyCode.Keypad6,KeyPitchDown=KeyCode.Keypad7,KeyPitchUp=KeyCode.Keypad1,
            KeyFovUp=KeyCode.KeypadMinus,KeyFovDown=KeyCode.KeypadPlus,KeyReset=KeyCode.Keypad5;
    }
    public static class CameraKeys {public static int Listening {get;set;}=-1;}
}
namespace ArtOfSimRally.Mod.Triple
{
    public static class TripleScreen
    {
        public static bool CockpitRealisticActive {get;set;}
        public static double? RealisticStartDegrees {get;set;}
        public static int Announcements;
        public static string LastNote;
        internal static void ReannounceCamera(string note=null){Announcements++;LastNote=note;}
    }
}
