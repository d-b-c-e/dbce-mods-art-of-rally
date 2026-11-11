using UnityEngine;
namespace UnityModManagerNet
{
    public static class UnityModManager
    {
        public class ModSettings { public virtual void Save(ModEntry e){} public static string GetPath(ModEntry e)=>"unused"; }
        public class ModEntry { public ModInfo Info=new(); }
        public class ModInfo { public string Version="fixture",DisplayName="fixture"; }
        public static List<ModEntry> modEntries=new();
        public class UI
        {
            public static UI Instance=new(); public int tabId; public bool Opened; public int Changes;
            private int ShowModSettings { get; set; }
            private string mModFilter="";
            public string Filter=>mModFilter;
            public int Selected=>ShowModSettings;
            public void ToggleWindow(bool open){Opened=open; Changes++;}
        }
    }
}
namespace ArtOfSimRally.Mod
{
    // The shared profile's Mod menu and Mute force feedback buttons: one press edge each time a test adds it.
    internal static class ProfileButtons
    {
        public const int ModMenu=21, MuteFfb=22;
        public static readonly HashSet<int> Down=new();
        public static bool Pressed(int action)=>Down.Remove(action);
    }
    internal static class Input
    {
        public static readonly HashSet<KeyCode> Down=new();
        public static readonly HashSet<KeyCode> Held=new();
        public static bool GetKey(KeyCode key)=>Held.Contains(key);
        public static bool GetKeyDown(KeyCode key)=>Down.Contains(key);
    }
    internal static class Time { public static float realtimeSinceStartup=100; public static int frameCount; }
    internal static class Application { public static bool isFocused=true; }
    internal static class GameState { public static bool IsDriving; }
    internal static class CameraKeys { public static bool Modified=false; public static bool ModifierHeld()=>Modified; public static void Tick(){} }
    internal static class CameraTuner { public static int Suppressed; public static void SuppressUntilRelease()=>Suppressed++; }
    internal static class GameBindings { public static void Tick(){} }
    internal static class SettingsPanel
    {
        public static bool Editing, HasDraft;
        public static bool CancelPendingEdit(){bool result=Editing; Editing=false; return result;}
        public static bool HoldCloseForDraft()=>DraftCloseGuard.HoldClose(HasDraft);
    }
    internal static class FfbNative
    {
        public static bool Ready=true;
        public static int Stops,Shutdowns;
        public static void StopOutputs()=>Stops++;
        public static void Shutdown(){Stops++;Shutdowns++;Ready=false;}
    }
    public static partial class Main
    {
        internal static Settings Settings=new(); internal static bool Enabled=true;
        internal static bool SettingsVisible=>UnityModManagerNet.UnityModManager.UI.Instance.Opened;
        private static UnityModManagerNet.UnityModManager.ModEntry _modEntry=new();
        public static int Saves,Requests,Cancels; public static bool WriteSucceeds=true;
        public static bool SaveSettings(){Saves++;if(WriteSucceeds){_uiDirty=false;SettingsSaveStatus="Saved";}else SettingsSaveStatus="Write failed";return WriteSucceeds;}
        public static void ReopenForceFeedback(){if(Settings.ForceFeedbackEnabled)Requests++;}
        public static void CancelForceRecovery()=>Cancels++;
        internal static void ResetFixture()
        {
            _uiDirty=false;_uiSaveAt=0;_settingsWereVisible=false;SuppressHostClose=false;
            Settings=new();Enabled=true;Saves=Requests=Cancels=0;WriteSucceeds=true;
            UnityModManagerNet.UnityModManager.modEntries.Clear();UnityModManagerNet.UnityModManager.modEntries.Add(_modEntry);
        }
    }
}
