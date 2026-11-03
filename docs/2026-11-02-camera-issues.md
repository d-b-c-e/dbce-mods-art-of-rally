# Art camera issues, November 2

Kole 498303/498335; source/offline scope, compiler GO 498360. Worktree:
`E:/Source/_work/napo-art-camera-restore-20261102`, starting at 1502abe.
Installed rc.36 and accepted FFB tune remain unchanged. No package/install/live run.

## ISS-0024: owned mounted-view restore

The real BonnetCamera Start postfix appended bonnet/bumper slots without
refreshing a saved selection that had been clamped before those entries existed.
It now records whether it added a slot and uses native RefreshCameraType once,
only when the saved in-range index identifies an owned bonnet/bumper slot,
the mod/rig/target are ready, and that slot is not already selected.

The native method updates selection/pose without changing SETTINGS_CAMERA_PRESET.
Stock, foreign, unavailable and invalid indices are not reinterpreted. Repeated
postfix calls do not override a subsequent player pick. Camera Mod compatibility
and native handling of disabled/unready rigs remain in control. No key, FOV,
camera-set, offset, force or saved-setting change is made.

Shipped-class static evidence: Assembly-CSharp.dll SHA-256
`7807A1D674C8214FE1FBD15B8EDC6E3E5E47700AB6E6C2943CF5BF234551C5E0`.
SetCameraFromSave clamps the stored index to the current list; RefreshCameraType
calls it without incrementing/saving. Native Start initializes the stock list;
the fixture explicitly models an early native selection, not a claim that Start
itself calls RefreshCameraType. The restore closes that ordering gap, but does
not newly establish the owner's actual runtime call order/root cause.
REA MCP was unavailable (stale registration); existing ilspycmd provided the
bounded static read, with no configuration repair or game load. Decompiled class
bytes remain private/ignored and are not committed or shipped.

Original actual hook fails the new clamped-mounted-view fixture. Fixed results:

| Check | Result |
| --- | --- |
| Lifecycle, actual BonnetCamera hook with stubs | 95 assertions pass |
| CameraTuning | 408 assertions pass |
| SettingsUi | 3,549 assertions pass |
| Production net48 Mod Release | zero warnings/errors |

Candidate DLL SHA-256:
`C2C358228C02D1EAE964B542C50DA4127DF5F3E48475D656369DF8BF93FDE727`.
Ignored evidence: this worktree's `results/napo-camera-restore-20261102/` with
baseline/fixed builds/logs, private static evidence, result.json and lease logs.
No game/device/force/display/owner-file mutation. TEMP/TMP restored; lease released.

ISS-0024 is an agent source fix awaiting runtime/owner validation, not resolved
owner acceptance. Later GO must observe a recognisable bonnet and bumper across
stage transitions/restarts and the explicit restore log. Stock views, disabled
views and external Camera Mod must retain normal behavior. Late Custom Vehicles
cockpit restore remains separately owned by its existing runtime path; no new
cockpit fix or rendered result is inferred from this fixture.

## ISS-0023: current implementation and remaining gates

The full issue requests **Wheelkit**, owned by lili: once-set camera bindings,
My Rig Game/Realistic choices, and its separately tracked FFB Apply request.
Kole 498335 confirmed no new Art F6 slice is required when current source already
implements it. No duplicate panel or cross-repo edit was added.

Read Wheelkit main `7c5653c2ccdfc47ff942f38585abfe628ded9e11`:

- MainWindow.xaml contains My Rig's Cameras tab, per-camera starting-set controls,
  binding rows and Keep each game's own keys.
- RigProfileViewModel saves opt-in CameraKeys and per-camera CameraStart. Null
  CameraKeys retains game-owned keys; My Rig FfbOn decides force, rather than
  always enabling it.
- ProfileTargets maps Art keys and CockpitRealistic/OverrideFieldOfView/
  BumperRealistic/ChaseRealistic. Camera sets apply only on triples and require
  the installed artFovSets capability through the existing configurator.
- Existing CameraStartTests cover default/chosen sets, single-screen/off refusal,
  opt-in/complete/nonduplicate bindings and receipt capability. These Wheelkit
  fixtures were inspected, not newly executed or claimed as UI proof here.

Read-only installed Art check: Info.json declares artFovSets/artCurvedTriples true;
DLL remains rc.36 `E5368292772994DBBDEDF729F2A2337E44D629CD8A7943E8D42DE2CBCF7D8CE3`.
No owner profile, binding or settings file was edited to exercise Apply.

**Leave ISS-0023 open:** installed Wheelkit tab/control reachability and a real
selected-profile Preview/Apply/readback remain unverified in this task. Profile
keys currently target Art; cross-game consumers/receipt capability/qualification
must precede advertising universal binding/FOV support. Physical force remains a
separate attended check. Art's existing F6 tuning remains intact.
