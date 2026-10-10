using HarmonyLib;

namespace ArtOfSimRally.Mod
{
    [HarmonyPatch(typeof(Drivetrain), "SetTransmissionType")]
    internal static class TransmissionInput
    {
        [HarmonyPostfix]
        internal static void Apply(Drivetrain __instance)
        {
            if (!Main.Enabled || Main.Settings == null || !GameButtonCompatibility.Allowed) return;
            var mode = Main.Settings.TransmissionMode;
            if (mode == "Automatic") __instance.automatic = true;
            else if (mode == "Sequential" || mode == "H-pattern") __instance.automatic = false;
        }
    }
}
