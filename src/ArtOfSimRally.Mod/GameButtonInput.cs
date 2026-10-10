using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rewired;

namespace ArtOfSimRally.Mod
{
    /// <summary>Augment this game's player actions using the early watchdog
    /// snapshot. No virtual pad, backend replacement, OS input or focus changes.
    /// The stock input-module and pause guards still own F6 handback.</summary>
    internal static class GameButtonInput
    {
        private static int _horizontal = -1, _vertical = -1, _submit = -1, _cancel = -1;
        internal static void Observe(int horizontal, int vertical, int submit, int cancel)
        { _horizontal = horizontal; _vertical = vertical; _submit = submit; _cancel = cancel; }
        internal static bool Primary(Player player) => WheelInput.Enabled && GameButtonCompatibility.Allowed && ReferenceEquals(player, PadManager.GetPlayer());
        internal static bool Button(int action, bool negative, bool down)
        {
            if (action < 0) return false;
            if (action == _horizontal || action == 14)
                return WheelInput.GameButton(negative ? WheelInput.Channel.NavLeft : WheelInput.Channel.NavRight, down);
            if (action == _vertical)
                return WheelInput.GameButton(negative ? WheelInput.Channel.NavDown : WheelInput.Channel.NavUp, down);
            if (negative) return false;
            if (action == _submit) return WheelInput.GameButton(WheelInput.Channel.Confirm, down);
            if (action == _cancel || action == 17) return WheelInput.GameButton(WheelInput.Channel.Back, down);
            // build 17584229: CarCameras.UpdateSwitchCamera=61;
            // PauseScreen.UpdateMe=19 (do not also send 8 and toggle twice).
            if (action == 61) return WheelInput.GameButton(WheelInput.Channel.CameraSwitch, down);
            if (action == 19) return WheelInput.GameButton(WheelInput.Channel.Start, down);
            return false;
        }
        internal static float Axis(int action)
        {
            if (action < 0 || (action != _horizontal && action != _vertical && action != 14)) return 0;
            return (Button(action, false, false) ? 1 : 0) - (Button(action, true, false) ? 1 : 0);
        }
    }
    [HarmonyPatch]
    internal static class GameButtonPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (var name in new[] { "GetButton", "GetButtonDown", "GetNegativeButton", "GetNegativeButtonDown" })
                yield return AccessTools.Method(typeof(Player), name, new[] { typeof(int) });
        }
        [HarmonyPostfix]
        private static void After(Player __instance, int __0, MethodBase __originalMethod, ref bool __result)
        {
            if (!GameButtonInput.Primary(__instance)) return;
            __result |= GameButtonInput.Button(__0, __originalMethod.Name.StartsWith("GetNegative", StringComparison.Ordinal),
                __originalMethod.Name.EndsWith("Down", StringComparison.Ordinal));
        }
    }
    [HarmonyPatch(typeof(Player), "GetAxis", new[] { typeof(int) })]
    internal static class GameButtonAxisPatch
    {
        [HarmonyPostfix]
        private static void After(Player __instance, int __0, ref float __result)
        {
            if (!GameButtonInput.Primary(__instance)) return;
            float value = GameButtonInput.Axis(__0);
            if (value != 0) __result = value;
        }
    }
}
