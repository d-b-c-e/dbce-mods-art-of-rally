using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace ArtOfSimRally.Mod
{
    // FOV offsets remain shared by stock chase view/set. Pose offsets belong to
    // the stable car/view/set. Both are overlays on the game's current camera,
    // removed before Update and reapplied after LateUpdate without compounding.
    internal static class ChaseFov
    {
        private static readonly AccessTools.FieldRef<CarCameras, List<CameraAngle>> Angles =
            AccessTools.FieldRefAccess<CarCameras, List<CameraAngle>>("CameraAnglesList");
        private static readonly CameraRepeat Up = new CameraRepeat(), Down = new CameraRepeat();
        private static Camera _camera;
        private static float _applied;
        private static Vector3 _appliedPosition;
        private static Quaternion _appliedRotation = Quaternion.identity;
        private static readonly CameraRepeat[] PoseRepeats = { new CameraRepeat(), new CameraRepeat(), new CameraRepeat(), new CameraRepeat(),
            new CameraRepeat(), new CameraRepeat(), new CameraRepeat(), new CameraRepeat() };
        private static Settings _inputSettings;
        private static string _inputCar;
        private static int _inputView;
        private static bool _inputRealistic;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

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
            if (_camera != null)
            {
                if (_applied != 0f) _camera.fieldOfView -= _applied;
                _camera.transform.position -= _appliedPosition;
                _camera.transform.rotation *= Quaternion.Inverse(_appliedRotation);
            }
            _applied = 0f;
            _appliedPosition = Vector3.zero; _appliedRotation = Quaternion.identity;
        }

        private static void ReleaseRepeats() { Up.Release(); Down.Release(); foreach (var repeat in PoseRepeats) repeat.Release(); }
        internal static void Release()
        {
            Remove(); _camera = null; ReleaseRepeats();
            if (_inputSettings != null) { _inputSettings = null; CameraTuner.SuppressUntilRelease(); }
        }

        private static bool Nudge(ref float value, int increase, int decrease, float step, float now)
        {
            float delta = (PoseRepeats[increase].Tick(CameraKeys.Held(increase), now) ? step : 0f) -
                (PoseRepeats[decrease].Tick(CameraKeys.Held(decrease), now) ? step : 0f);
            float next = value + delta;
            if (delta == 0 || float.IsNaN(next) || float.IsInfinity(next)) return false;
            value = next; return true;
        }

        /// <summary>From the mounted-camera postfix, for a stock chase view only.</summary>
        internal static void Apply(CarCameras rig, Settings cfg, bool allowPose = true)
        {
            var cam = UIManager.Instance?.PanelManager?.mainCamera;
            var list = Angles(rig);
            if (cam == null || list == null || rig.CurrentCameraAngle == null) { Release(); return; }
            int index = list.IndexOf(rig.CurrentCameraAngle);
            if (index < 0 || index > 31) { Release(); return; }
            // Realistic set: the seat's true field of view plus its own offsets, instead of the game's speed-based zoom.
            bool realistic = Triple.TripleScreen.RealisticFor(Triple.CameraOverlay.Kind.Chase);
            var seat = realistic ? Triple.TripleScreen.RealisticStartDegrees : null;
            var offsets = realistic ? cfg.ChaseFovRealisticOffsets : cfg.ChaseFovOffsets;
            while (offsets.Count <= index) offsets.Add(0f);
            string car = CameraCarIdentity.Key(rig.target);
            bool poseAvailable = allowPose && car != null;
            var pose = ChasePoseStore.Read(cfg, car, index, realistic);
            if (_inputSettings != null && (!ReferenceEquals(_inputSettings, cfg) || _inputCar != car || _inputView != index || _inputRealistic != realistic))
            { ReleaseRepeats(); CameraTuner.SuppressUntilRelease(); }
            _inputSettings = cfg; _inputCar = car; _inputView = index; _inputRealistic = realistic;

            if (cfg.CameraTuningKeys && Main.Enabled && CameraTuner.ReadyForKeys())
            {
                float now = Time.unscaledTime, step = CameraRepeat.Bounded(cfg.CameraFovStep, .5f, 10f, 2f);
                float delta = (Up.Tick(CameraKeys.Held(8), now) ? step : 0f) - (Down.Tick(CameraKeys.Held(9), now) ? step : 0f);
                bool reset = CameraKeys.Pressed(10);
                float next = reset ? 0f : offsets[index] + delta;
                bool changed = false;
                if ((delta != 0f || reset) && !float.IsNaN(next) && !float.IsInfinity(next))
                {
                    changed = offsets[index] != next; offsets[index] = next;
                }
                if (poseAvailable)
                {
                    float move = CameraRepeat.Bounded(cfg.CameraMoveStep, .005f, .25f, .02f);
                    float tilt = CameraRepeat.Bounded(cfg.CameraTiltStep, .1f, 10f, 1f);
                    bool poseChanged;
                    if (reset)
                    {
                        poseChanged = pose.Side != 0 || pose.Height != 0 || pose.Forward != 0 || pose.Pitch != 0;
                        pose.Side = pose.Height = pose.Forward = pose.Pitch = 0;
                    }
                    else
                    {
                        poseChanged = Nudge(ref pose.Height, 0, 1, move, now);
                        poseChanged |= Nudge(ref pose.Forward, 2, 3, move, now);
                        poseChanged |= Nudge(ref pose.Side, 5, 4, move, now);
                        poseChanged |= Nudge(ref pose.Pitch, 6, 7, tilt, now);
                    }
                    if (poseChanged) { ChasePoseStore.Write(cfg, pose); changed = true; }
                }
                if (changed)
                {
                    Main.MarkSettingsDirty();
                    Triple.TripleScreen.ReannounceCamera(reset ? "Camera reset to calculated start · field of view reset for every car" : null);
                }
            }
            else ReleaseRepeats();
            Remove();
            _camera = cam;
            float requested = seat.HasValue ? (float)seat.Value + offsets[index] : cam.fieldOfView + offsets[index];
            _applied = CameraTuner.LensFov(requested) - cam.fieldOfView;
            cam.fieldOfView += _applied;
            if (poseAvailable)
            {
                _appliedPosition = rig.target.rotation * new Vector3(pose.Side, pose.Height, pose.Forward);
                var position = cam.transform.position + _appliedPosition;
                if (!Finite(position.x) || !Finite(position.y) || !Finite(position.z)) _appliedPosition = Vector3.zero;
                // Reducing a finite angle modulo one turn protects the engine
                // conversion without limiting the stored player offset.
                _appliedRotation = Quaternion.Euler((float)System.Math.IEEERemainder(pose.Pitch, 360d), 0, 0);
                cam.transform.position += _appliedPosition;
                cam.transform.rotation *= _appliedRotation;
            }
        }
    }
}
