using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace RallyCustomVehicles
{
    public static partial class Main
    {
        private static Vector3 eyeAdjustment;
        private static float pitch=6f, fov=65f;
        internal static CockpitViewStore CockpitViews;
        private static string viewId;
        private static bool viewIsRealistic, fovEdited, waitForCockpitRelease;
        private static double? viewSeat;
        private static float[] loadedView;
        private static string cockpitSaveStatus="";
        private static float retryCockpitAt;
        private static Type artMainType, artTripleType, artKeysType;
        private static Type ArtType(ref Type cached,string name) => cached ?? (cached=AccessTools.TypeByName(name));
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float[] CurrentView() => new[]{eyeAdjustment.x,eyeAdjustment.y,eyeAdjustment.z,pitch,fov};
        private static void ApplyView(float[] value)
        { eyeAdjustment=new Vector3(value[0],value[1],value[2]);pitch=value[3];fov=value[4]; }

        private static void LoadView(string id,string key)
        {
            viewId=id;viewIsRealistic=key!=id;viewSeat=viewIsRealistic ? ArtRealisticDegrees() : null;
            ApplyView(CockpitViews?.GetView(id,viewIsRealistic,viewSeat) ?? (float[])CockpitViewStore.Default.Clone());
            loadedView=CurrentView();fovEdited=false;waitForCockpitRelease=true;
            NotifyCockpitChange(null);
        }
        private static bool ArtRealistic()
        {
            try
            {
                var type=ArtType(ref artTripleType,"ArtOfSimRally.Mod.Triple.TripleScreen");
                var property=type==null ? null : AccessTools.Property(type,"CockpitRealisticActive") ?? AccessTools.Property(type,"RealisticActive");
                return property!=null && (bool)property.GetValue(null);
            }
            catch(Exception){return false;}
        }
        private static double? ArtRealisticDegrees()
        {
            try
            {
                var type=ArtType(ref artTripleType,"ArtOfSimRally.Mod.Triple.TripleScreen");
                return type==null ? null : (double?)AccessTools.Property(type,"RealisticStartDegrees")?.GetValue(null);
            }
            catch(Exception){return null;}
        }
        private static void NotifyCockpitChange(string note)
        {
            try
            {
                var type=ArtType(ref artTripleType,"ArtOfSimRally.Mod.Triple.TripleScreen");
                if(type!=null) AccessTools.Method(type,"ReannounceCamera")?.Invoke(null,new object[]{note});
            }
            catch(Exception){ } // An absent/older Art overlay never blocks the cockpit.
        }
        private static void SaveView()
        {
            if(viewId==null || CockpitViews==null)return;
            var value=CurrentView();
            if(!fovEdited && loadedView!=null && value.SequenceEqual(loadedView)){FlushCockpitPending();return;}
            try
            {
                CockpitViews.SetView(viewId,viewIsRealistic,viewSeat,value,fovEdited);
                loadedView=value;fovEdited=false;cockpitSaveStatus="";
            }
            catch(Exception ex)
            {
                // Accepted valid session edits survive car/set switches and a
                // failed disk write. Validation failures never replace the snapshot.
                if(CockpitViews.HasPendingChanges && !(ex is InvalidDataException)) {loadedView=value;fovEdited=false;}
                CockpitSaveFailure(ex);
            }
        }
        private static void CockpitSaveFailure(Exception ex)
        {
            string message="Cockpit changes not saved: "+ex.Message;
            if(cockpitSaveStatus!=message)Log(message);
            cockpitSaveStatus=message;retryCockpitAt=Time.unscaledTime+5f;
        }
        private static void FlushCockpitPending()
        {
            if(CockpitViews==null || !CockpitViews.HasPendingChanges || Time.unscaledTime<retryCockpitAt)return;
            try {CockpitViews.Flush();cockpitSaveStatus="";}
            catch(Exception ex){CockpitSaveFailure(ex);}
        }
        private static object ArtConfig()
        {
            try
            {
                var type=ArtType(ref artMainType,"ArtOfSimRally.Mod.Main");
                return type==null ? null : AccessTools.Property(type,"Settings")?.GetValue(null);
            }
            catch(Exception){return null;}
        }
        private static KeyCode ArtKey(string name,KeyCode fallback)
        {
            var cfg=ArtConfig();var value=cfg==null ? null : AccessTools.Field(cfg.GetType(),name)?.GetValue(cfg);
            var key=value is KeyCode saved ? saved : fallback; // None stays unbound.
            var settings=cfg==null ? null : AccessTools.Field(cfg.GetType(),"SettingsKey")?.GetValue(cfg);
            var settingsKey=settings is KeyCode custom ? custom : KeyCode.F6;
            return key==KeyCode.F7 || key==KeyCode.F8 || key==KeyCode.F10 || key==settingsKey ||
                key<=KeyCode.None || key>=KeyCode.Mouse0 || !Enum.IsDefined(typeof(KeyCode),key) ? KeyCode.None : key;
        }
        private static bool ArtKeysOn()
        {
            var cfg=ArtConfig();var value=cfg==null ? null : AccessTools.Field(cfg.GetType(),"CameraTuningKeys")?.GetValue(cfg);
            return !(value is bool on) || on;
        }
        private static bool ArtCapturing()
        {
            try
            {
                var type=ArtType(ref artKeysType,"ArtOfSimRally.Mod.CameraKeys");
                return type!=null && (int)(AccessTools.Property(type,"Listening")?.GetValue(null) ?? -1)>=0;
            }
            catch(Exception){return true;}
        }
        private static readonly string[] CockpitKeyNames={"KeyUp","KeyDown","KeyForward","KeyBack","KeyLeft","KeyRight","KeyPitchDown","KeyPitchUp","KeyFovUp","KeyFovDown","KeyReset"};
        private static readonly KeyCode[] CockpitKeyDefaults={KeyCode.Keypad9,KeyCode.Keypad3,KeyCode.Keypad8,KeyCode.Keypad2,KeyCode.Keypad4,KeyCode.Keypad6,
            KeyCode.Keypad7,KeyCode.Keypad1,KeyCode.KeypadMinus,KeyCode.KeypadPlus,KeyCode.Keypad5};
        private static bool Modifier() => new[]{KeyCode.LeftControl,KeyCode.RightControl,KeyCode.LeftAlt,KeyCode.RightAlt,KeyCode.AltGr,
            KeyCode.LeftShift,KeyCode.RightShift,KeyCode.LeftCommand,KeyCode.RightCommand,KeyCode.LeftWindows,KeyCode.RightWindows}.Any(Input.GetKey);
        private static bool Nudge(ref float value,float delta)
        {float next=value+delta;if(!Finite(next)||next==value)return false;value=next;return true;}
        private static void ResetDriverView()
        {
            if(viewId==null || CockpitViews==null)return;
            if(viewIsRealistic && !CockpitViews.HasCalculatedSeat(viewSeat))
            {cockpitSaveStatus="Seat measurement is needed before resetting Realistic FOV.";return;}
            ApplyView(CockpitViews.Calculated(viewIsRealistic,viewSeat));fovEdited=true;
            NotifyCockpitChange("Cockpit reset to calculated start · field of view reset for every car");
        }
        private static void TuneCockpit()
        {
            if(!Application.isFocused || UnityModManager.UI.Instance!=null && UnityModManager.UI.Instance.Opened || !ArtKeysOn() || ArtCapturing() || Modifier())
            {waitForCockpitRelease=true;return;}
            if(waitForCockpitRelease)
            {
                for(int i=0;i<CockpitKeyNames.Length;i++) if(Input.GetKey(ArtKey(CockpitKeyNames[i],CockpitKeyDefaults[i])))return;
                waitForCockpitRelease=false;
            }
            bool changed=false;
            void Step(string name,KeyCode fallback,ref float value,float delta)
            {if(Input.GetKeyDown(ArtKey(name,fallback)))changed|=Nudge(ref value,delta);}
            Step("KeyForward",KeyCode.Keypad8,ref eyeAdjustment.z,.02f);Step("KeyBack",KeyCode.Keypad2,ref eyeAdjustment.z,-.02f);
            Step("KeyRight",KeyCode.Keypad6,ref eyeAdjustment.x,.02f);Step("KeyLeft",KeyCode.Keypad4,ref eyeAdjustment.x,-.02f);
            Step("KeyUp",KeyCode.Keypad9,ref eyeAdjustment.y,.02f);Step("KeyDown",KeyCode.Keypad3,ref eyeAdjustment.y,-.02f);
            Step("KeyPitchDown",KeyCode.Keypad7,ref pitch,1f);Step("KeyPitchUp",KeyCode.Keypad1,ref pitch,-1f);
            float oldFov=fov;
            Step("KeyFovUp",KeyCode.KeypadMinus,ref fov,2f);Step("KeyFovDown",KeyCode.KeypadPlus,ref fov,-2f);
            if(oldFov!=fov){fovEdited=true;NotifyCockpitChange(null);}
            if(Input.GetKeyDown(ArtKey("KeyReset",KeyCode.Keypad5))){ResetDriverView();changed=true;}
            if(changed)SaveView();
        }
        private static void DrawCockpitTuning()
        {
            if(viewId==null){GUILayout.Label("Select a custom vehicle to adjust its cockpit.");return;}
            GUILayout.Label("Cockpit "+(viewIsRealistic ? "Realistic" : "Game")+" set · pose per vehicle; field of view shared across vehicles.");
            bool changed=Row("Left / right",ref eyeAdjustment.x,.02f);
            changed|=Row("Up / down",ref eyeAdjustment.y,.02f);changed|=Row("Forward / back",ref eyeAdjustment.z,.02f);
            changed|=Row("Look down (degrees)",ref pitch,1f);float oldFov=fov;
            changed|=Row("Vertical field of view",ref fov,2f);
            if(oldFov!=fov){fovEdited=true;NotifyCockpitChange(null);}
            if(GUILayout.Button("Reset driver view")){ResetDriverView();changed=true;}
            if(changed)SaveView();
            if(CockpitViews!=null && CockpitViews.NeedsFovChoice(viewIsRealistic))GUILayout.Label("Legacy FOVs differ between vehicles. Adjust or reset field of view to choose this set's shared value; existing views stay intact until then.");
            if(cockpitSaveStatus.Length>0)GUILayout.Label(cockpitSaveStatus);
        }
        private static bool Row(string label,ref float value,float step)
        {
            GUILayout.BeginHorizontal();GUILayout.Label(label+": "+value.ToString("0.00"),GUILayout.Width(240));
            bool changed=false;if(GUILayout.Button("−",GUILayout.Width(42)))changed|=Nudge(ref value,-step);
            if(GUILayout.Button("+",GUILayout.Width(42)))changed|=Nudge(ref value,step);
            GUILayout.EndHorizontal();return changed;
        }
    }
}
