using System.Text.Json;
using ArtOfSimRally.Mod;
using UnityEngine;

internal static class StageLateCases
{
    internal static int Run(Action<string, string, object[]> patch)
    {
        var results = new List<object>(); int failures = 0;
        void Check(string name, bool pass) { results.Add(new { name, pass }); if (!pass) failures++; }
        void Hook(string type, string method, CarCameras rig) => patch(type, method, new object[] { rig });
        CarCameras Early(int preset)
        {
            BonnetCamera.Release(false);
            Main.Enabled = true; Main.OtherCameraModLoaded = false; Main.Settings = new Settings();
            GameState.IsPlayerView = true; SaveGame.Preset = preset;
            UIManager.Instance.PanelManager.mainCamera = new Camera();
            var rig = new CarCameras();
            for (int i = 0; i < 8; i++) rig.CameraAnglesList.Add(new CameraAngle(1, 1, 1, CameraAngle.CameraAngles.CAMERA1));
            rig.RefreshCameraType(); return rig;
        }
        foreach (int preset in new[] { 8, 9 })
        {
            var rig = Early(preset); rig.target = null; Hook("AddToRotation", "Append", rig);
            Check("unready target does not restore " + preset, rig.Refreshes == 1);
            rig.target = new Transform(); Hook("DriveCamera", "Mount", rig);
            Check("late target restores saved mount " + preset, ReferenceEquals(rig.CurrentCameraAngle, rig.CameraAnglesList[preset]) && rig.Refreshes == 2);
            Check("restore leaves preference unchanged " + preset, SaveGame.Preset == preset);
            rig.CurrentCameraAngle = rig.CameraAnglesList[0]; SaveGame.Preset = 0;
            Hook("DriveCamera", "Mount", rig);
            Check("later player choice wins " + preset, ReferenceEquals(rig.CurrentCameraAngle, rig.CameraAnglesList[0]) && rig.Refreshes == 2);
        }
        {
            var rig = Early(8); rig.enabled = false; Hook("AddToRotation", "Append", rig);
            Check("disabled rig waits", rig.Refreshes == 1);
            rig.enabled = true; Hook("DriveCamera", "Mount", rig);
            Check("late enabled rig restores once", rig.Refreshes == 2 && ReferenceEquals(rig.CurrentCameraAngle, rig.CameraAnglesList[8]));
        }
        {
            var rig = Early(9); Main.Enabled = false; Hook("AddToRotation", "Append", rig);
            Main.Enabled = true; Hook("DriveCamera", "Mount", rig);
            Check("late enabled mod restores once", rig.Refreshes == 2 && ReferenceEquals(rig.CurrentCameraAngle, rig.CameraAnglesList[9]));
        }
        {
            var rig = Early(8); GameState.IsPlayerView = false; Hook("AddToRotation", "Append", rig);
            Check("intro retains native selection", rig.Refreshes == 1);
            GameState.IsPlayerView = true; Hook("DriveCamera", "Mount", rig);
            Check("player handoff restores pending mount", rig.Refreshes == 2 && ReferenceEquals(rig.CurrentCameraAngle, rig.CameraAnglesList[8]));
        }
        {
            var rig = Early(8); rig.target = null; Hook("AddToRotation", "Append", rig);
            SaveGame.Preset = 0; rig.CurrentCameraAngle = rig.CameraAnglesList[0]; rig.target = new Transform();
            Hook("DriveCamera", "Mount", rig);
            Check("player change cancels pending restore", rig.Refreshes == 1 && ReferenceEquals(rig.CurrentCameraAngle, rig.CameraAnglesList[0]));
        }
        {
            var rig = Early(10); rig.target = null; Hook("AddToRotation", "Append", rig);
            var cockpit = new CameraAngle(0, 0, 0, CameraAngle.CameraAngles.CAMERA1); rig.CameraAnglesList.Add(cockpit);
            rig.target = new Transform(); rig.CurrentCameraAngle = cockpit; Hook("DriveCamera", "Mount", rig);
            Check("late foreign cockpit and preference remain owned by its adapter", rig.Refreshes == 1 && ReferenceEquals(rig.CurrentCameraAngle, cockpit) && SaveGame.Preset == 10);
        }
        BonnetCamera.Release(false); SaveGame.Preset = null; Main.Enabled = true; GameState.IsPlayerView = true;
        Console.WriteLine(JsonSerializer.Serialize(new { schema = "art.stage-late-regression@1", pass = failures == 0, checks = results.Count, failures, productionHook = true, engineOrDeviceLoaded = false, assertions = results }));
        return failures == 0 ? 0 : 1;
    }
}
