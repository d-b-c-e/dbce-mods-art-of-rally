using System.Collections.Generic;
using System.Xml.Serialization;
using UnityEngine;
using UnityModManagerNet;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// Mod settings, shown in Unity Mod Manager's in-game panel (Ctrl+F10).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plain fields rather than a loader's config-entry wrapper, so the patches
    /// read <c>Main.Settings.Gain</c> with no indirection and no dependency on how
    /// the values were loaded.
    /// </para>
    /// <para>
    /// The in-game panel matters more here than it usually would. Force feedback
    /// strength and the camera mount both have to be judged while driving, and the
    /// alternative is quitting to edit a text file for every adjustment.
    /// </para>
    /// </remarks>
    public class Settings : UnityModManager.ModSettings
    {
        // Presentation only. Unknown/missing view values display Simple.
        public string SettingsView = "Simple";
        public int SettingsPage = 0;
        public bool SettingsFollowHostScale = true;
        public KeyCode SettingsKey = KeyCode.F6;
        // "explicit" targets PreferredDeviceGuid; otherwise FFB follows the shared profile's steering unless a GUID was picked.
        public string FfbDeviceMode = "";
        // ---- Steering -------------------------------------------------------
        // Wheel, pedals, buttons and gears come from the shared profile (SharedInputStartup); these fix the game's own handling of a wheel.

        public bool DirectSteering = true;

        public bool ZeroAxisDeadzone = true;

        public bool BindAnyDevice = true;

        // ---- Transmission ---------------------------------------------------
        // Follow game preserves every existing install; explicit profile choice
        // uses the same drivetrain switch as the game's transmission preference.
        public string TransmissionMode = "Follow game";

        /// <summary>Step past neutral when shifting sequentially.</summary>
        /// <remarks>
        /// The game steps one index at a time through [reverse, neutral, 1st, ...],
        /// exactly as its own ShiftUp/ShiftDown do, so reverse to first takes two
        /// presses with a useless stop in between. Real sequential boxes do have
        /// neutral there, but nobody wants to press through it, and the game
        /// auto-clutches anyway.
        /// </remarks>
        public bool SkipNeutral = true;

        // ---- Force feedback -------------------------------------------------

        public bool ForceFeedbackEnabled = true;

        public int Strength = 50;

        // STD-031 steering reference: Strength 50 is the standard force.
        // Calibrate before clamp/filter; never multiply an already clipped output.
        public const string SteeringReferenceId = "art-of-rally-50-v2";
        public const float DefaultFyReference = 11500f / .6f;

        /// <summary>
        /// Strength as the multiplier the force model actually uses.
        /// </summary>
        /// <remarks>
        /// 50 maps to 1.0. The force reference sets the midpoint calibration;
        /// a saved FyReference is used as saved.
        /// </remarks>
        public float GainFromStrength => Strength / 50f;

        /// <summary>
        /// Front-axle lateral force (N, both wheels, after the trail) treated as
        /// full force, before Strength is applied.
        /// </summary>
        /// <remarks>
        /// Not shown in the panel. It sets where the output starts clipping, which
        /// is a different thing from how strong the wheel feels, and having two
        /// dials for one sensation - one of them inverted, where lower means
        /// stronger - confused everyone who met it. Strength is the only dial now;
        /// this stays as the reference it scales against. A hard corner at 100 km/h
        /// measured 6,000-7,000 N (2026-09-02); the default is 11,500 / 0.6 N (STD-031).
        /// </remarks>
        public float FyReference = DefaultFyReference;

        public float Smoothing = 0.2f;

        public bool Invert = false;

        // New/missing settings use the modest default; saved opt-outs still win.
        public bool LandingEffectsEnabled = true;
        public float LandingStrength = 5f;

        // Experimental until an attended collision/feel comparison is complete.
        public bool CrashEffectsEnabled = false;
        public float CrashStrength = 50f;

        // Optional gear-engagement cue. Saved settings from earlier releases
        // remain silent until the player explicitly enables it.
        public bool ShiftEffectsEnabled = false;
        public float ShiftStrength = 5f;

        // Support detail logging is for one reproduction, so it is never saved and starts off each launch
        // (left on, it kept writing FFB traces for weeks after a support request, 2026-10-05).
        [XmlIgnore] public bool DiagnosticLogging = false;
        /// <summary>Frame-rate counter at the top right of the centre screen (STD-024).</summary>
        public bool ShowFrameRate = false;

        // Set by the device picker in the settings panel, not drawn directly.
        // The name is what persists; the index is only a tiebreaker for rigs
        // where two devices report the same name (Fanatec does this).
        public string PreferredDevice = "";

        public int PreferredDeviceIndex = -1;
        // Set only by an explicit picker selection; FFB targets this identity, never the name or index.
        public string PreferredDeviceGuid = "";

        // ---- Camera ---------------------------------------------------------

        public bool BonnetCameraEnabled = true;

        public float BonnetHeight = 0.95f;

        public float BonnetForward = 1.0f;

        public float BonnetSide = 0f;

        public float BonnetPitch = 3f;

        public float BonnetFOV = 75f;

        public float BonnetLean = 0.1f;

        /// <summary>Adds a bumper view after the bonnet view in the rotation.</summary>
        public bool BumperCameraEnabled = true;

        // Lower and further forward than the bonnet: just above the front
        // bumper, looking down the road. Shares BonnetLean.
        public float BumperHeight = 0.45f;
        public float BumperForward = 1.9f;
        public float BumperSide = 0f;
        public float BumperPitch = 2f;
        public float BumperFOV = 80f;

        // The bonnet/bumper values above are the current car's: its calculated placement plus the player's offsets. Each
        // adjusted car keeps its position offsets here; the field of view offsets are per camera, for every car
        // (CameraMountMemory).
        public List<CameraMount> CameraMounts = new List<CameraMount>();
        public float BonnetFovOffset = 0f, BumperFovOffset = 0f, BonnetFovRealisticOffset = 0f, BumperFovRealisticOffset = 0f;

        // Degrees added to the game's own field of view, per stock chase view (list position); numpad +/- while in it.
        public List<float> ChaseFovOffsets = new List<float>();
        // The Realistic set's per-view offsets, added to the seat's true field of view (F7 in a chase view).
        public List<float> ChaseFovRealisticOffsets = new List<float>();
        // Pose is per car, stock chase view and set. FOV deliberately remains in
        // the shared per-view lists above, so changing cars cannot fork its value.
        public List<ChaseCameraPose> ChaseCameraPoses = new List<ChaseCameraPose>();

        public bool CameraTuningKeys = true;

        // Per-press shortcut steps (toolkit STD-006: 0.02 m, 1 deg tilt, 2 deg FOV).
        // Holding a key repeats the step at a bounded rate (CameraTuner).
        public float CameraMoveStep = 0.02f;
        public float CameraTiltStep = 1f;
        public float CameraFovStep  = 2f;

        // Family numpad layout (toolkit STD-005): 8/2 forward/back, 9/3 up/down,
        // 4/6 left/right, 7/1 tilt forward (look down) / back (look up), +/- FOV,
        // 5 reset. A larger pitch looks down (BonnetCamera: Euler X), so
        // KeyPitchDown raises pitch and sits on 7.
        public KeyCode KeyUp        = KeyCode.Keypad9;
        public KeyCode KeyDown      = KeyCode.Keypad3;
        public KeyCode KeyForward   = KeyCode.Keypad8;
        public KeyCode KeyBack      = KeyCode.Keypad2;
        public KeyCode KeyLeft      = KeyCode.Keypad4;
        public KeyCode KeyRight     = KeyCode.Keypad6;
        public KeyCode KeyPitchDown = KeyCode.Keypad7;
        public KeyCode KeyPitchUp   = KeyCode.Keypad1;
        // + zooms in (narrower), - zooms out (wider): owner 2026-11-01, "+/- are swapped from what I'd expect".
        public KeyCode KeyFovUp     = KeyCode.KeypadMinus;
        public KeyCode KeyFovDown   = KeyCode.KeypadPlus;
        public KeyCode KeyReset     = KeyCode.Keypad5;

        // ---- Telemetry ------------------------------------------------------

        // On by default (STD-016). A saved <TelemetryEnabled>false</TelemetryEnabled> still turns it off.
        public bool TelemetryEnabled = true;

        public string TelemetryHost = "127.0.0.1";

        public int TelemetryPort = 8000;

        public void ResetCameraSteps()
        {
            var defaults = new Settings();
            CameraMoveStep = defaults.CameraMoveStep; CameraTiltStep = defaults.CameraTiltStep; CameraFovStep = defaults.CameraFovStep;
        }

        public void ResetCameraMount(bool bumper)
        {
            var defaults = new Settings();
            if (bumper)
            {
                BumperHeight = defaults.BumperHeight; BumperForward = defaults.BumperForward;
                BumperSide = defaults.BumperSide; BumperPitch = defaults.BumperPitch; BumperFOV = defaults.BumperFOV;
            }
            else
            {
                BonnetHeight = defaults.BonnetHeight; BonnetForward = defaults.BonnetForward;
                BonnetSide = defaults.BonnetSide; BonnetPitch = defaults.BonnetPitch; BonnetFOV = defaults.BonnetFOV;
            }
        }

        public void ResetFfbTuning()
        {
            var defaults = new Settings();
            Strength = defaults.Strength; Smoothing = defaults.Smoothing; Invert = defaults.Invert;
            FyReference = defaults.FyReference;
            LandingEffectsEnabled = defaults.LandingEffectsEnabled; LandingStrength = defaults.LandingStrength;
            CrashEffectsEnabled = defaults.CrashEffectsEnabled; CrashStrength = defaults.CrashStrength;
            ShiftEffectsEnabled = defaults.ShiftEffectsEnabled; ShiftStrength = defaults.ShiftStrength;
            // Preserve the saved Off/On preference, device and all non-FFB settings.
        }

        public override void Save(UnityModManager.ModEntry modEntry)
            => SettingsPersistence.Write(this, GetPath(modEntry));
    }

    /// <summary>One car's bonnet/bumper position offsets from its calculated placement: side, height, forward, pitch for
    /// the bonnet, then the bumper.</summary>
    public sealed class CameraMount
    {
        public string Car;
        public float[] Offsets;
    }

    public sealed class ChaseCameraPose
    {
        public string Car;
        public int View;
        public bool Realistic;
        public float Side, Height, Forward, Pitch;
    }
}
