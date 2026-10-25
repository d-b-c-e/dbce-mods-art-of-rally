art of sim rally - @RELEASE@
============================

Wheel force feedback, direct USB controls, bonnet/bumper views and SimHub telemetry
for the 64-bit Windows game. Tested on Steam; other stores are not confirmed.
Version @RELEASE@ includes Simple/Advanced settings. No SDK, separate toolkit
download or SimHub helper is needed. Some hardware scenarios remain untested.

INSTALL / UPDATE
----------------
1. Close art of rally.
2. Use Extract All on this mod ZIP. Keep its files together, then double-click
   Install.bat. It finds the game, adds the mod loader (Unity Mod Manager, included)
   if the game does not have it yet, and installs the mod. Existing settings are kept.
3. Launch through Steam and press F6 to set up your wheel.

For a custom folder, find artofrally.exe using Steam > Manage > Browse local files.
Open PowerShell in this extracted download and use the real path:
  .\Install.bat -GameDir "D:\Games\artofrally"
If you already use Unity Mod Manager, you can also drag the mod ZIP onto its Mods tab.
Prefer Install.bat for updates if you used it before: both native DLL copies stay
synchronized. Vortex installation has not been validated.

Remove with the game closed: double-click Uninstall.bat. Settings and other mods
stay. The included mod loader is removed too unless other mods use it. A custom path also works: .\Uninstall.bat -GameDir "D:\Games\artofrally"
Before updating, keep the previous ZIP and back up Settings.xml, TripleScreen.xml,
desired-layout.json and dbce-controls.ini from Mods\ArtOfSimRally if present.
The installer preserves settings but does not create a backup or automatically
undo a failed copy. If copying fails, keep the game closed until the full install
succeeds or you restore your previous package. Shared-profile downgrade steps
are in the shared-input section when that flavor is included.

FIRST DRIVE
-----------
Pause before setting up. Opening settings stops mod controls/FFB but does not
promise to pause the game simulation. Keep a mouse/keyboard available for F6.
Simple is the first view; Advanced adds tuning to the same saved values.

* Controls: keep working game bindings. For a missing axis, center/release it,
  click Bind, move only that control through its full range, then release.
  Steering needs both full locks and center. Check the preview, inversion and
  deadzone, then Save calibration. Cancel/timeout/save failure keeps the old
  binding. Explicitly calibrated ranges do not learn themselves during a drive.
  Calibrate adjusts the saved axis; Bind chooses a new one. Check the status at
  the top of Controls if no input appears. Use assigned controls must remain On
  when driving with a separately bound USB handbrake.

* Separate USB handbrake/TSS: select handbrake mode on the device, then bind
  Handbrake (axis) as above. It should read 0-100% with partial travel. If the
  device reports only a button, use Handbrake (button). Axis, button and game
  input coexist by taking the largest value. Each row can use a different USB
  device. TSS physical travel on this new UI still needs an attended check.

* FFB: choose On and Use steering wheel, or explicitly select the physical wheel.
  Follow mode uses the mod's saved Steering binding. If you use only the game's
  steering binding, choose the FFB wheel explicitly. Missing/unverified selections
  stay unavailable; Refresh/retry while paused. Saved Off remains Off.
  Strength defaults to 50. Force builds from 3 to 12 km/h; test while moving.
  Advanced includes direction and smoothing (20% = the old 0.20 setting).
  Higher smoothing softens rapid force changes but adds delay.

* Stop FFB: F8 or the header button stops feedback and saves Off. Choose On in
  FFB explicitly to resume. Controls > Mod buttons also binds optional USB
  Settings and Stop FFB buttons; these work with assigned driving controls Off.

* Driving/menu buttons: Open game bindings uses the game's existing controls
  screen and action maps. Release the click/key used to open it before navigating.
  If unavailable in this scene, use Options > Controls from the main menu. A
  device unreadable by the game's Raw Input route still needs keyboard/pad for
  those actions; direct axes/mod buttons remain available.
  Shifter is an expandable Controls group: select the device and sequential or
  H-pattern mode, then Bind its buttons. Clear unwanted duplicate game bindings.

* Landing vibration: Advanced > FFB, on by default at 5 (range 0-40).
  Crash kick: experimental, off by default at 50 (range 0-100). It uses a finite
  120ms constant-force push/release plus a 25Hz rattle at half the crash strength
  for 250ms, fading over the last 150ms. If the wheel rejects the rattle, the push
  still works. Landing stays a 25Hz sine. One impact owns
  the output at a time. Strengths are percentages of nominal wheel force and
  independent of steering Strength. Saved values are retained: select 50 manually
  to compare the crash default. The accepted force behavior is retained; effect
  support and feel still vary by wheel.
  These controls do not scale telemetry or the ButtKicker; tune that in SimHub.
  Shift vibration (experimental) is off by default at 5 (range 0-20). It adds
  a short wheel rumble when the player's gear engages, without changing the
  gearbox. Landing and crash cues take priority. This is new in 0.2.7;
  its feel on real wheels has not yet been accepted.

Leave the legacy steering limiter override Off; use the game's assist settings.

CAMERAS
-------
Bonnet/Bumper On includes those views in the game's Change camera cycle.
Adjustment bindings in Simple support keyboard keys and USB buttons:
8/2 forward/back, 9/3 up/down, 4/6 left/right, 7/1 tilt down/up, +/- FOV, 5 reset.
Defaults use the numpad. Bind/Clear work without a numpad. Close settings and
release captured/held controls before adjusting a view. F8/F10 and the Settings
key are reserved; Reset defaults refuses a conflict rather than overwriting it.
Advanced adds pose/FOV/lean and Reset this view, which affects only that mount.
Nexus CameraMod retains camera control while loaded; our mounts are suspended.

TELEMETRY / SIMHUB / BUTTKICKER
-----------------------------
In SimHub select Forza Horizon 5 and match the mod's destination. The explicit
local preset is 127.0.0.1:8000. Enable Telemetry while paused and verify speed/RPM.
Advanced > Edit connection commits host and port together on Apply connection;
Cancel changes neither. UDP Sending status does not confirm receiver delivery.
Use built-in Impacts/Road impacts for a ButtKicker. No extra helper is required.
The owner's rig improved at 30Hz; tune your own rig and reduce gain if it clips.
The development recorder remains separate and is not included in this ZIP.

HELP
----
Settings text size follows UMM's scale by default, including 1x. Choose Auto to
let only this mod grow with screen resolution when UMM remains at 1x; a custom
UMM scale still takes priority. UMM's own window size/chrome preferences are
unchanged; resize its window in UMM Settings if needed.
Help > Create support file on Desktop collects settings, device identifiers,
paths and logs locally; nothing is uploaded. For a recurring problem, enable
Advanced > Help > Log detail for support, reproduce briefly, pause, export, and
turn detail off. Ordinary errors/settings can be collected with detail off.
Attach the Desktop support TXT to an issue with wheel/driver, car/stage and
what happened. Check Saved/error status: failed binding writes keep old values.

You can also use F6 > Help > Submit GitHub Issue while paused. Enter a title
and description, optionally select logs, then choose Review report. Review the
preview for private information (redaction is best effort), tick I reviewed this
report, then choose Open reviewed GitHub draft. Check and submit on GitHub.
If asked, manually attach the reviewed support-report.txt saved in a report
folder on your Desktop; attaching uploads it immediately.

Source, docs and license: https://github.com/d-b-c-e/dbce-mods-art-of-rally
Issues: https://github.com/d-b-c-e/dbce-mods-art-of-rally/issues
Tested hardware history: MOZA R12; positive R5/T300 reports. The full hardware
transition matrix, Fanatec/TSS travel and stronger crash feel need more reports.
