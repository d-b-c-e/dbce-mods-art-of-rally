using HarmonyLib;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Writes the shared profile's driving values over the game's own, in a postfix on <c>AxisCarController.GetInput</c>:
    /// after the game's deadzone processing and with its steering-alignment effect preserved, so everything downstream
    /// (direct steering, steer assist, telemetry) sees what it would from a wheel Rewired understood. With no shared
    /// session the game's own values stand. While settings are open or the game is unfocused the car gets no input.
    /// </summary>
    [HarmonyPatch(typeof(AxisCarController), "GetInput")]
    internal static class CarInputPatch
    {
        [HarmonyPostfix]
        private static void Override(AxisCarController __instance,
                                     ref float throttleInput, ref float brakeInput, ref float steerInput,
                                     ref float handbrakeInput, ref float clutchInput, ref bool startEngineInput)
        {
            if (Main.Enabled && (Main.SettingsVisible || !Application.isFocused))
            {
                throttleInput = brakeInput = steerInput = handbrakeInput = clutchInput = 0;
                startEngineInput = false; return;
            }
            Shim.ShimInput.TryOverride(__instance, ref throttleInput, ref brakeInput, ref steerInput,
                ref handbrakeInput, ref clutchInput, ref startEngineInput);
        }
    }
}
