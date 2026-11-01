using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    // Chase cameras keep the game's speed-dependent field of view plus the player's saved offset per chase view, adjusted
    // with the field-of-view keys (owner 2026-10-31: every camera adjustable, and it sticks). The game smooths its FOV
    // from the camera's current value in CarCameras.Update, so the offset is removed before that and re-applied after
    // LateUpdate; otherwise it would compound every frame.
    internal static class ChaseFov
    {
        private static readonly AccessTools.FieldRef<CarCameras, List<CameraAngle>> Angles =
            AccessTools.FieldRefAccess<CarCameras, List<CameraAngle>>("CameraAnglesList");
        private static readonly CameraRepeat Up = new CameraRepeat(), Down = new CameraRepeat();
        private static Camera _camera;
        private static float _applied;

        [HarmonyPatch(typeof(CarCameras), "Update")]
        internal static class RemoveBeforeGame
        {
            [HarmonyPrefix] private static void Before() => Remove();
        }

        /// <summary>Whether a chase view's field of view in the given set was changed from stock (the overlay's "(Custom)").</summary>
        internal static bool Custom(Settings cfg, int index, bool realistic)
        {
            var offsets = realistic ? cfg.ChaseFovRealisticOffsets : cfg.ChaseFovOffsets;
            return index >= 0 && index < offsets.Count && offsets[index] != 0f;
        }

        internal static void Remove()
        {
            if (_camera != null && _applied != 0f) _camera.fieldOfView -= _applied;
            _applied = 0f;
        }

        /// <summary>From the mounted-camera postfix, for a stock chase view only.</summary>
        internal static void Apply(CarCameras rig, Settings cfg)
        {
            var cam = UIManager.Instance?.PanelManager?.mainCamera;
            var list = Angles(rig);
            if (cam == null || list == null || rig.CurrentCameraAngle == null) return;
            int index = list.IndexOf(rig.CurrentCameraAngle);
            if (index < 0 || index > 31) return;
            // Realistic set: the seat's true field of view plus its own offsets, instead of the game's speed-based zoom.
            var seat = Triple.TripleScreen.RealisticActive ? Triple.TripleScreen.RealisticStartDegrees : null;
            var offsets = seat.HasValue ? cfg.ChaseFovRealisticOffsets : cfg.ChaseFovOffsets;
            while (offsets.Count <= index) offsets.Add(0f);

            if (cfg.CameraTuningKeys && Main.Enabled && !Main.SettingsVisible && Application.isFocused && !CameraKeys.ModifierHeld())
            {
                float now = Time.unscaledTime, step = CameraRepeat.Bounded(cfg.CameraFovStep, .5f, 10f, 2f);
                float delta = (Up.Tick(CameraKeys.Held(8), now) ? step : 0f) - (Down.Tick(CameraKeys.Held(9), now) ? step : 0f);
                bool reset = Input.GetKeyDown(cfg.KeyReset) && offsets[index] != 0f;
                if (delta != 0f || reset)
                {
                    offsets[index] = reset ? 0f : Mathf.Clamp(offsets[index] + delta, -40f, 40f);
                    Main.MarkSettingsDirty();
                    Triple.TripleScreen.ReannounceCamera(reset ? "Field of view reset" : null);
                }
            }
            Remove();
            _camera = cam;
            _applied = seat.HasValue ? Mathf.Clamp((float)seat.Value + offsets[index], 20f, 120f) - cam.fieldOfView : offsets[index];
            cam.fieldOfView += _applied;
        }
    }
}
