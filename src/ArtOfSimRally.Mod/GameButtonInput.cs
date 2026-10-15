using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rewired;
using UnityEngine;

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
        // Only inside a latched Core test session (current frame): its injected actions replace the
        // primary player's native results. Ordinary sessions OR shared buttons into native input.
        internal static bool Suppressed => Shim.ShimInput.Consumer.LatchedDelivery(Time.frameCount);
        internal static bool Prepare(Player player)
        {
            if (!Primary(player)) return false;
            // The dynamically loaded watchdog's execution-order attribute did
            // not put it before the game's UI module in the qualified build.
            // Poll at the first eligible consumer, once per rendered frame.
            WheelInput.PrepareGameQuery();
            return true;
        }
        internal static void TickTitle()
        {
            // The title uses a Rewired ButtonJustReleased delegate, not GetButton.
            // Match its release semantics through the same guarded reader. Do not
            // synthesize Rewired events globally or make pedals skip the title.
            if (!WheelInput.Enabled || !GameButtonCompatibility.Allowed ||
                !(WheelInput.GameButtonReleased(WheelInput.Channel.Start) || WheelInput.GameButtonReleased(WheelInput.Channel.Confirm))) return;
            var splash = SplashScreenControl.Instance;
            var panels = UIManager.Instance == null ? null : UIManager.Instance.PanelManager;
            if (splash == null || !splash.isActiveAndEnabled || panels == null || panels.Peek() != panels.SplashScreenPanel) return;
            splash.EndSplashScreen();
            ModLog.Info("Profile " + (WheelInput.GameButtonReleased(WheelInput.Channel.Start) ? "start" : "confirm") + " release advanced the title screen.");
        }
        // The one native-to-shared table, for dispatch and prompt labels: a Rewired action id and pole to its
        // shared action (null when Art-only) and Art channel (null for shifts, which Core steps).
        // build 17584229 (RewiredConsts.Action and the game's own reads):
        // - CarCameras.UpdateSwitchCamera=61; PauseScreen.UpdateMe=19 (do not also send 8 and toggle twice);
        // - SoundController plays the horn on GetButtonDown(30) and stops it on GetButtonUp(30);
        // - ShortcutManager arms 44 recover, 45 repair puncture or 46 quick restart on GetButtonDown and acts
        //   after GetButton is held for one second (its own context checks stay the game's);
        // - CarCameras orbits on GetAxis(22) and GetAxis(63): 63- looks back, 63+ forward, 22- left, 22+ right;
        // - AxisCarController reads 0 steer (split), 2 throttle, 3 brake, 4 handbrake, 5 clutch, 12/13 shifts.
        internal static bool NativeAction(int rewiredId, bool negative, out string sharedId, out WheelInput.Channel? artChannel)
        {
            sharedId = null; artChannel = null;
            if (rewiredId < 0) return false;
            if (rewiredId == _horizontal || rewiredId == 14) return Map(negative ? "navLeft" : "navRight", negative ? WheelInput.Channel.NavLeft : WheelInput.Channel.NavRight, out sharedId, out artChannel);
            if (rewiredId == _vertical) return Map(negative ? "navDown" : "navUp", negative ? WheelInput.Channel.NavDown : WheelInput.Channel.NavUp, out sharedId, out artChannel);
            if (rewiredId == 63) return Map(negative ? "lookBack" : null, negative ? WheelInput.Channel.LookBack : WheelInput.Channel.LookForward, out sharedId, out artChannel);
            if (rewiredId == 22) return Map(null, negative ? WheelInput.Channel.LookLeft : WheelInput.Channel.LookRight, out sharedId, out artChannel);
            if (rewiredId == 0) return Map("steer", WheelInput.Channel.Steer, out sharedId, out artChannel);
            if (negative) return false;
            if (rewiredId == _submit) return Map("confirm", WheelInput.Channel.Confirm, out sharedId, out artChannel);
            if (rewiredId == _cancel || rewiredId == 17) return Map("back", WheelInput.Channel.Back, out sharedId, out artChannel);
            switch (rewiredId)
            {
                case 61: return Map("camera", WheelInput.Channel.CameraSwitch, out sharedId, out artChannel);
                case 19: return Map("start", WheelInput.Channel.Start, out sharedId, out artChannel);
                case 30: return Map("horn", WheelInput.Channel.Horn, out sharedId, out artChannel);
                case 44: return Map("reset", WheelInput.Channel.RecoverCar, out sharedId, out artChannel);
                case 45: return Map(null, WheelInput.Channel.RepairPuncture, out sharedId, out artChannel);
                case 46: return Map(null, WheelInput.Channel.QuickRestart, out sharedId, out artChannel);
                case 2: return Map("throttle", WheelInput.Channel.Throttle, out sharedId, out artChannel);
                case 3: return Map("brake", WheelInput.Channel.Brake, out sharedId, out artChannel);
                case 4: return Map("handbrake", WheelInput.Channel.Handbrake, out sharedId, out artChannel);
                case 5: return Map("clutch", WheelInput.Channel.Clutch, out sharedId, out artChannel);
                case 12: sharedId = "shiftUp"; return true;
                case 13: sharedId = "shiftDown"; return true;
                default: return false;
            }
        }
        private static bool Map(string shared, WheelInput.Channel channel, out string sharedId, out WheelInput.Channel? artChannel)
        { sharedId = shared; artChannel = channel; return true; }
        // Only game-button channels are dispatched here; driving axes and shifts have their own consumers.
        private static bool GameChannel(int action, bool negative, out WheelInput.Channel channel)
        {
            channel = default(WheelInput.Channel);
            if (!NativeAction(action, negative, out _, out var mapped) || !mapped.HasValue || !WheelInput.IsGameButton(mapped.Value)) return false;
            channel = mapped.Value; return true;
        }
        internal static bool Button(int action, bool negative, bool down) =>
            GameChannel(action, negative, out var channel) && WheelInput.GameButton(channel, down);
        // GetButtonUp: the release edge, including a horn release owed after an interrupted press.
        internal static bool Released(int action) => GameChannel(action, false, out var channel) && WheelInput.GameButtonReleased(channel);
        internal static float Axis(int action)
        {
            if (action < 0 || (action != _horizontal && action != _vertical && action != 14 && action != 22 && action != 63)) return 0;
            return (Button(action, false, false) ? 1 : 0) - (Button(action, true, false) ? 1 : 0);
        }
        internal static bool ContinuePressed(Player player)
        {
            // StageIntroCinematic and GameIntroduction use GetAnyButtonDown,
            // not a named Submit action. Expose only the two explicit continue
            // controls; steering, pedals, navigation and camera are not a skip.
            return Prepare(player) && (WheelInput.GameButton(WheelInput.Channel.Confirm, true) ||
                WheelInput.GameButton(WheelInput.Channel.Start, true));
        }
    }
    [HarmonyPatch]
    internal static class GameButtonPatch
    {
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> Targets()
        {
            foreach (var name in Names)
                yield return AccessTools.Method(typeof(Player), name, new[] { typeof(int) });
        }
        internal static readonly string[] Names = { "GetButton", "GetButtonDown", "GetButtonUp", "GetNegativeButton", "GetNegativeButtonDown" };
        [HarmonyPostfix]
        private static void After(Player __instance, int __0, MethodBase __originalMethod, ref bool __result)
        {
            if (!GameButtonInput.Prepare(__instance)) return;
            string name = __originalMethod.Name;
            bool shared = name == "GetButtonUp" ? GameButtonInput.Released(__0)
                : GameButtonInput.Button(__0, name.StartsWith("GetNegative", StringComparison.Ordinal), name.EndsWith("Down", StringComparison.Ordinal));
            __result = GameButtonInput.Suppressed ? shared : __result | shared;
        }
    }
    [HarmonyPatch(typeof(Player), "GetAxis", new[] { typeof(int) })]
    internal static class GameButtonAxisPatch
    {
        [HarmonyPostfix]
        private static void After(Player __instance, int __0, ref float __result)
        {
            if (!GameButtonInput.Prepare(__instance)) return;
            float value = GameButtonInput.Axis(__0);
            if (value != 0 || GameButtonInput.Suppressed) __result = value;
        }
    }
    [HarmonyPatch(typeof(Player), "GetAnyButtonDown", new Type[] { })]
    internal static class GameContinuePatch
    {
        [HarmonyPostfix]
        private static void After(Player __instance, ref bool __result)
        {
            bool pressed = GameButtonInput.ContinuePressed(__instance);
            __result = GameButtonInput.Primary(__instance) && GameButtonInput.Suppressed ? pressed : __result | pressed;
        }
    }
}
