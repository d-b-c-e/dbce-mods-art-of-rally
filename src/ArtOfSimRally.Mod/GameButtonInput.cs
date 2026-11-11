using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Rewired;
using static ArtOfSimRally.Mod.ProfileButtons;

namespace ArtOfSimRally.Mod
{
    /// <summary>Adds the shared profile's buttons to this game's player actions, from this frame's shared snapshot.
    /// No virtual pad, backend replacement, OS input or focus changes. The stock input-module and pause guards
    /// still own F6 handback.</summary>
    internal static class GameButtonInput
    {
        private static int _horizontal = -1, _vertical = -1, _submit = -1, _cancel = -1;
        internal static void Observe(int horizontal, int vertical, int submit, int cancel)
        { _horizontal = horizontal; _vertical = vertical; _submit = submit; _cancel = cancel; }
        internal static bool Primary(Player player) => WheelReaders.Enabled && ReferenceEquals(player, PadManager.GetPlayer());
        internal static bool Prepare(Player player)
        {
            if (!Primary(player)) return false;
            // The dynamically loaded watchdog's execution-order attribute did
            // not put it before the game's UI module in the qualified build.
            // Poll at the first eligible consumer, once per rendered frame.
            WheelReaders.PrepareGameQuery();
            return true;
        }
        internal static void TickTitle()
        {
            // The title uses a Rewired ButtonJustReleased delegate, not GetButton.
            // Match its release semantics through the same guarded reader. Do not
            // synthesize Rewired events globally or make pedals skip the title.
            if (!WheelReaders.Enabled || !(ProfileButtons.Released(Start) || ProfileButtons.Released(Confirm))) return;
            var splash = SplashScreenControl.Instance;
            var panels = UIManager.Instance == null ? null : UIManager.Instance.PanelManager;
            if (splash == null || !splash.isActiveAndEnabled || panels == null || panels.Peek() != panels.SplashScreenPanel) return;
            splash.EndSplashScreen();
            ModLog.Info("Profile " + (ProfileButtons.Released(Start) ? "start" : "confirm") + " release advanced the title screen.");
        }
        // The one native-to-shared table, for dispatch and prompt labels: a Rewired action id and pole to the shared
        // action (a ShimFrame index) the profile delivers for it. Build 17584229 (RewiredConsts.Action and the game's reads):
        // - CarCameras.UpdateSwitchCamera=61; PauseScreen.UpdateMe=19 (do not also send 8 and toggle twice);
        // - SoundController plays the horn on GetButtonDown(30) and stops it on GetButtonUp(30);
        // - ShortcutManager arms 44 recover on GetButtonDown and acts after GetButton is held for one second
        //   (its own context checks stay the game's);
        // - CarCameras orbits on GetAxis(63): 63- looks back;
        // - AxisCarController reads 0 steer (split), 2 throttle, 3 brake, 4 handbrake, 5 clutch, 12/13 shifts.
        internal static bool NativeAction(int rewiredId, bool negative, out int action)
        {
            action = -1;
            if (rewiredId < 0) return false;
            if (rewiredId == _horizontal || rewiredId == 14) action = negative ? NavLeft : NavRight;
            else if (rewiredId == _vertical) action = negative ? NavDown : NavUp;
            else if (rewiredId == 63) action = negative ? LookBack : -1;
            else if (rewiredId == 0) action = 0;
            else if (negative) return false;
            else if (rewiredId == _submit) action = Confirm;
            else if (rewiredId == _cancel || rewiredId == 17) action = Back;
            else switch (rewiredId)
            {
                case 61: action = Camera; break;
                case 19: action = Start; break;
                case 30: action = Horn; break;
                case 44: action = Recover; break;
                case 2: action = 1; break;  // throttle
                case 3: action = 2; break;  // brake
                case 4: action = 4; break;  // handbrake
                case 5: action = 3; break;  // clutch
                case 12: action = Shim.ShimAxisConsumer.ShiftUp; break;
                case 13: action = Shim.ShimAxisConsumer.ShiftDown; break;
            }
            return action >= 0;
        }
        // Only the game's button actions are dispatched here; driving axes and shifts have their own consumers.
        private static bool GameAction(int rewiredId, bool negative, out int action) =>
            NativeAction(rewiredId, negative, out action) && IsGameAction(action);
        internal static bool Button(int rewiredId, bool negative, bool down) =>
            GameAction(rewiredId, negative, out int action) && ProfileButtons.Button(action, down);
        // GetButtonUp: the release edge, including a horn release owed after an interrupted press.
        internal static bool Released(int rewiredId) => GameAction(rewiredId, false, out int action) && ProfileButtons.Released(action);
        internal static float Axis(int rewiredId)
        {
            if (rewiredId < 0 || (rewiredId != _horizontal && rewiredId != _vertical && rewiredId != 14 && rewiredId != 63)) return 0;
            return (Button(rewiredId, false, false) ? 1 : 0) - (Button(rewiredId, true, false) ? 1 : 0);
        }
        internal static bool ContinuePressed(Player player)
        {
            // StageIntroCinematic and GameIntroduction use GetAnyButtonDown,
            // not a named Submit action. Expose only the two explicit continue
            // controls; steering, pedals, navigation and camera are not a skip.
            return Prepare(player) && (ProfileButtons.Button(Confirm, true) || ProfileButtons.Button(Start, true));
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
            // The profile's buttons add to the game's own input; they never hide it.
            __result |= name == "GetButtonUp" ? GameButtonInput.Released(__0)
                : GameButtonInput.Button(__0, name.StartsWith("GetNegative", StringComparison.Ordinal), name.EndsWith("Down", StringComparison.Ordinal));
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
            if (value != 0) __result = value;
        }
    }
    [HarmonyPatch(typeof(Player), "GetAnyButtonDown", new Type[] { })]
    internal static class GameContinuePatch
    {
        [HarmonyPostfix]
        private static void After(Player __instance, ref bool __result)
        {
            __result |= GameButtonInput.ContinuePressed(__instance);
        }
    }
}
