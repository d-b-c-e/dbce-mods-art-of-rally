using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace RallyCustomVehicles
{
    // Block mutation of the in-memory result/ghost cache, not just disk writes.
    // Otherwise a later unmodified run could flush a tainted cache to disk.
    [HarmonyPatch]
    internal static class LocalResultGuard
    {
        [HarmonyTargetMethods]
        internal static IEnumerable<MethodBase> Targets()
        {
            var names = new[] {
                new[] { "LeaderboardScreenUpdater", "SetLocalTime" },
                new[] { "GhostManager", "FinalizeRecording" },
                new[] { "GhostManager", "SaveDataToDisk" },
                new[] { "SaveManager", "SaveSeasonData" },
                new[] { "SaveManager", "SaveCareerData" },
                new[] { "SaveManager", "SaveWeeklyData" },
                new[] { "CollectableManager", "SetCollectable" },
                new[] { "CollectableManager", "UnlockNextFreeroamStage" },
                new[] { "StatsAndAchievements", "UnlockAchievement" }
            };
            foreach(var pair in names)
            {
                var m=AccessTools.Method(AccessTools.TypeByName(pair[0]),pair[1]);
                if(m==null || m.ReturnType!=typeof(void)) throw new MissingMethodException("Required local result guard: "+string.Join(".",pair));
                yield return m;
            }
            foreach(var numeric in new[] {typeof(int),typeof(float)})
            {
                var m=AccessTools.Method(typeof(StatsAndAchievements),"IncrementStat",new[] {typeof(string),numeric});
                if(m==null) throw new MissingMethodException("Required IncrementStat guard.");
                yield return m;
            }
        }
        internal static bool VerifyInstalled(string owner) => Targets().All(m=>Harmony.GetPatchInfo(m)?.Prefixes.Any(p=>p.owner==owner)==true);
        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        private static bool Before() => Main.Policy.CanUpload;
    }
}
