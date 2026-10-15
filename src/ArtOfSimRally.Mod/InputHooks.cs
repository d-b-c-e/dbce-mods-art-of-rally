using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rewired;

namespace ArtOfSimRally.Mod
{
    // Whether our postfixes sit on the exact methods latched suppression and car input depend on,
    // checked in the running game before a latched open asserts its fences.
    internal static class InputHooks
    {
        internal static string Missing()
        {
            var missing = new List<string>();
            foreach (var name in GameButtonPatch.Names)
                Need(missing, AccessTools.Method(typeof(Player), name, new[] { typeof(int) }), typeof(GameButtonPatch), name);
            Need(missing, AccessTools.Method(typeof(Player), "GetAxis", new[] { typeof(int) }), typeof(GameButtonAxisPatch), "GetAxis");
            Need(missing, AccessTools.Method(typeof(Player), "GetAnyButtonDown", Type.EmptyTypes), typeof(GameContinuePatch), "GetAnyButtonDown");
            Need(missing, AccessTools.Method(typeof(AxisCarController), "GetInput"), typeof(WheelInputPatch), "GetInput");
            return missing.Count == 0 ? null : String.Join(", ", missing);
        }
        // The dev recorder's capture guards (SessionTape.PatchOutputs): every overload of the upload,
        // achievement, stat and local-save calls, and the telemetry send, carries its prefix.
        internal static string MissingCaptureGuards()
        {
            var missing = new List<string>();
            foreach (var name in new[] { "UploadScore", "UnlockAchievement", "IncrementStat", "ForceStatUpload",
                "SaveLocalSaveGame", "SaveLocalCareer", "SaveLocalLeaderboards", "SaveLocalGhosts", "SaveCustomCollectables" })
            {
                bool any = false;
                foreach (var method in typeof(PlatformSteam).GetMethods())
                {
                    if (method.Name != name) continue;
                    any = true;
                    if (!Guarded(method)) { missing.Add(name); break; }
                }
                if (!any) missing.Add(name + " (absent)");
            }
            if (!Guarded(AccessTools.Method(typeof(TelemetryPump), "SendFrame"))) missing.Add("TelemetryPump.SendFrame");
            return missing.Count == 0 ? null : String.Join(", ", missing);
        }
        private static bool Guarded(MethodBase target)
        {
            var info = target == null ? null : Harmony.GetPatchInfo(target);
            if (info != null) foreach (var p in info.Prefixes) if (p.PatchMethod.DeclaringType != null && p.PatchMethod.DeclaringType.FullName == "ArtOfSimRally.Testing.SessionTape") return true;
            return false;
        }
        private static void Need(List<string> missing, MethodBase target, Type patch, string name)
        {
            var info = target == null ? null : Harmony.GetPatchInfo(target);
            bool attached = false;
            if (info != null) foreach (var p in info.Postfixes) if (p.PatchMethod.DeclaringType == patch) attached = true;
            if (!attached) missing.Add(name);
        }
    }
}
