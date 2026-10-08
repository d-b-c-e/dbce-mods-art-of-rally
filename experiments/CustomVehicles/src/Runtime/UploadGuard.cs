using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace RallyCustomVehicles
{
    [HarmonyPatch]
    internal static class UploadGuard
    {
        private static readonly string[][] Required = {
            new[] { "PlatformSteam", "UploadScore" },
            new[] { "LeaderboardsConnection", "PostLeaderboardEntry" },
            new[] { "LeaderboardsConnection", "PostLeaderboardReplayBlob" }
        };
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (var pair in Required)
            {
                var method = AccessTools.Method(AccessTools.TypeByName(pair[0]),pair[1]);
                if (method == null || method.ReturnType != typeof(void)) throw new MissingMethodException("Required score guard missing: " + string.Join(".",pair));
                if (!method.GetParameters().Any(p=>p.ParameterType==typeof(PlatformAPIFailed))) throw new MissingMethodException("Required failure callback missing: " + method);
                yield return method;
            }
        }
        internal static bool VerifyInstalled(string owner) => Targets().All(m=>Harmony.GetPatchInfo(m)?.Prefixes.Any(p=>p.owner==owner)==true);
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Before(MethodBase __originalMethod, object[] __args)
        {
            if (Main.Policy.CanUpload) return true;
            Main.Log("Blocked online upload after custom physics: " + __originalMethod.Name);
            // Report failure, never a fake successful upload. Both stage time and
            // replay callbacks are notified; request code is never executed.
            foreach (var callback in __args.OfType<PlatformAPIFailed>())
            {
                try { callback("Custom vehicle physics: online times are disabled until restart."); }
                catch (Exception ex) { Main.LogError(ex); }
            }
            return false;
        }
    }
}
