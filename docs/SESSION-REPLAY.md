# Session recording, playback and force analysis

**Wheelkit integration (October 10):** `Session.ps1` also accepts `-SessionsRoot`
for a recording library outside this checkout and replay-only `-OutputDirectory`
for separate results. Both resolve relative paths from the PowerShell location.
Old invocations keep their existing locations. An existing replay result folder
is refused before starting or applying a recorded environment. Wheelkit uses
these parameters, and holds the shared rig lease around the original Art runner.
- **Ordinary Human recording** keeps the owner's outputs: the wheel's force and
  SimHub stay live. Installed 0.6.24 generates `-Record` without `-MuteOutputs`.
- **Automated recording** asks for `-MuteOutputs` explicitly.
- **Playback** always mutes those outputs, in trajectory mode.

This changes no mod/runtime payload or owner configuration. The first Wheelkit
launch is still to be qualified.

`tools/testing/Test-SessionPaths.ps1` executes the actual script against process,
registry and environment doubles: custom paths with spaces, correct tape identity,
separate evidence, restoration, a one-line success result, existing-result refusal
and unchanged legacy paths. It never launches a game or accesses owner state.

Install the optional **SessionTools** release archive separately from the normal
mod. Close the game before installing or removing this developer probe.

```powershell
./tools/testing/Install-Recorder.ps1 -SkipBuild  # omit -SkipBuild in a source checkout
./tools/testing/Session.ps1 -Record -Name japan-test
./tools/testing/Session.ps1 -Stop               # save before quitting
./tools/testing/Session.ps1 -Replay -Name japan-test
```

Record from launch, choose a stage, drive, then press **F11** to stop and save
without closing the game. The optional recorder shows a persistent **REC** timer
and the F11 reminder at the top center of the center display, including menus
and driving. **Recording saved** appears only after the tape is closed and
sealed; an incomplete recording is labeled separately. Wheelkit's Stop recording
button and a normal game exit also save. F11 does not start a new recording.
Normal recording retains
the owner's physical output settings. Add `-MuteOutputs` to mute the wheel and
SimHub while recording requested force and telemetry. Replay always mutes those
outputs for the entire process, including after failure or F12 takeover.
`-Stop` cancels playback; raw F12 returns input control. The launcher closes the
game after playback and restores its saved environment. `-KeepGameOpen` leaves
restoration pending until the game closes.

## Three distinct uses

| Mode | What it proves |
|---|---|
| Default trajectory playback | Recreates the recorded car route, gear/RPM presentation and menu progression using a kinematic body, like the native replay. |
| Offline signal replay | Evaluates FFB against original physics samples and reports force, motion, collision and encoded telemetry data. No game or device needed. |
| `-Playback input-diagnostic` | Re-simulates input and fails on divergence. Unity physics is not promised deterministic. Only the first stage's starting state is restored in this diagnostic. |

Trajectory playback suppresses stock wheel/dynamics updates, reset teleports and
kinematic handoff while it owns the player body. A next-physics-tick position
check detects competing writes. Use the original physics signals for tuning;
forces generated from the kinematic playback are not new physics measurements.

```powershell
# Source checkout, .NET 8 SDK:
dotnet run --project tools/testing/Replay -c Release -- --replay results/sessions/japan-test
# Optional archive, .NET 8 runtime:
dotnet analysis/Replay.dll --replay results/sessions/japan-test

# Offline trial: omitted values retain the captured tune. Game settings are untouched.
'{"gain":0.5,"referenceN":11500,"smoothing":0.2}' | Set-Content trial.json
dotnet analysis/Replay.dll --trial results/sessions/japan-test trial.json results/trial-1
```

Trials require a new output directory, validate the baseline first, preserve
reset epochs and source/config hashes, and save baseline and candidate requests.
Reports include RMS, absolute percentiles and saturation. Native acceptance is
not measured wheel torque. Compare matching scenarios and speeds across games.

## Format 3 and preservation

Sessions live under `results/sessions/<name>`:

- `input.tape`, `car.tape`, `shifts.tape`, `markers.tsv`: rendered-frame menu
  events and physics-tick input/pose samples. Menu events identify buttons.
- `load-NNNN.json`: resolved stage/weather/car/season selections at each load,
  avoiding frame-dependent random selection. The same game build is required.
- `environment-at-start`: explicit game preferences, custom-rally progress and
  mod settings. Replay backs up the current owner environment first and restores
  that backup after the game closes.
- `forces.csv`, `signals.csv`, `collisions.csv`, `manifest.xml`: original force
  inputs, normalized/native requests, motion/contact and collision observations.
- `telemetry.tsv`: 324-byte Forza packets before network suppression. Velocity
  and acceleration use SI units. Dashboard speed retains the game's 0.6 HUD
  scale and is labelled separately in analysis.
- `effects.tsv`: requested play/update effect arguments before muting. These are
  commands, not physical waveforms or a complete stop/duration timeline.
- `end.txt`, `complete.tsv`: counts and hashes written after background writers
  close. Truncated, modified or incomplete tapes are refused. Queue overflow
  invalidates the tape rather than silently dropping rows.
- **Optional diagnostics, only when a request asks** (`rewiredInventory=1`,
  `identityProbe=1`; recording only, reset for every request):
  - `rewired-inventory.tsv`: Rewired's joystick names, instance GUIDs and
    interface paths.
  - `identity-match.tsv` and `ds4-identity-probe.tsv`: the shared identity join's
    inputs.

  The disposable-copy diagnostics ask for them explicitly
  (`Invoke-SharedInputCopy.ps1 -RewiredInventory -IdentityProbe`). An ordinary
  recording writes none of them.
  - **Installed recorder:** the SessionTools 0.4.0-rc.15 recorder installed on
    2026-10-12 (`DAA62504`) predates this switch. It still writes
    `rewired-inventory.tsv` in every recording until a later package carries it.
    Keep those files private like the rest of the capture.

Replay writes a separate timestamped directory. Success requires all driving
rows, the final recorded frame state and valid trajectory application. Faster
loads/cinematics may skip transient menu or non-driving rows; counts are reported.
If restoration is pending, close the game and use `Session.ps1 -RestoreEnvironment`
with the explicit `replay-*/owner-environment` backup. Keep that backup until its
restoration receipt exists. Preference import is restricted to the game key.

New environment snapshots retain every preference's original type and bytes in
`playerprefs-raw.json`, verify the names/count and hash before restoration, then
read back every restored value. Windows `reg export` was observed omitting 21
of 58 values without an error, so the `.reg` reference copy alone is not treated
as a complete new-format backup. Older captures retain their legacy limitations.

Playback retains the current rig's resolution, fullscreen mode, monitor, frame
cap, VSync and triple-screen settings while restoring the recorded stage/car
preferences. New environment backups include `TripleScreen.xml`; legacy backups
leave that file alone. Playback does not need a smaller display resolution.
Use an owner-driven reference route for visual acceptance. The synthetic capture
fixture exercises saving and original-signal capture; its test inputs can leave
the road and are not a reference racing line.

## Input path, replay isolation and partial outcomes (October 12)

**Recording** names its input path (sana 36130/36236). The recorder reads the mod's
existing members by reflection; a mod without them records `sharedEvidence=unavailable`
and the recording continues.
- `session.txt` adds:
  - `inputModIdentity` (from `build.json`) and `inputModSha256`;
  - `inputModVersion`, the loaded mod assembly's informational version. The package build
    pins it to the same identity, so it stays right where a hand-staged copy carries a
    stale `build.json`;
  - the mod's `SharedInput`, `WheelInputEnabled`, `ShifterEnabled` and
    `ForceFeedbackEnabled` settings;
  - `sharedInputRequested` and `sharedEvidence`;
  - the `dbce-controls.ini` hash plus its `Profile`, `Revision` and `Transmission`;
  - the shim and loaded native hashes.
- While shared input was requested, `shared.tape` holds one row per rendered frame:
  - the phase, consumer freshness and this frame's isolation proof;
  - the shared composition (applied and composed axes, alignment, keyboard source and
    whether keyboard steering was kept);
  - the owned shared buttons held.

  It is sealed with the other members. Replay never reads it. The phase, freshness,
  isolation and held columns describe the row's own frame. The composition columns are
  the mod's last composition, made in the frame named by `lastFrame`: normally the
  previous rendered frame, because the recorder ticks before the car reads input.
  `session.txt` states this as `sharedTapeComposition=lastFrame`. The header is unchanged.
- `end.txt` adds the final shared phase, attach frame, stop or refusal reason, and row
  count.
- Mod-internal shortcuts (settings, stop force, camera) appear only as held shared
  buttons. They are not replayed.

**Replay reads no input or force device.** After the recorded environment is applied,
`Session.ps1` sets these off for the replay process only:
- `SharedInput`;
- `WheelInputEnabled` and `ShifterEnabled`;
- the settings, stop-force and camera shortcut bindings;
- `ForceFeedbackEnabled`.

Turning off `SharedInput` alone would just reopen the mod's direct wheel reader. The tape
supplies every input, and replay captures no forces. The recorded values are kept in
`replay-input.json`; the tape's own copy is never edited, and the owner's settings are
restored as before. A malformed or ambiguous settings file is refused before launch.

**Partial outcome.** Sometimes every recorded driving row plays and passes, but a later
menu marker is never reached, for example a best-time or unlock screen the current save
no longer shows. The menu cursor stops after its 120 s wait, and `result.txt` then reads:
- line 1: `partial:post-stage-divergence: …`;
- then the status line;
- then `scope=stage-only`, `fullSessionPassed=false`, `drivingSegmentsTotal`,
  `drivingSegmentsCompleted`, `expectedMarker` and `timeoutSeconds`.

`expectedMarker` is the marker the cursor actually waited out, as its own failure names
it. After an early advance that is the current segment, not the next one.

`Session.ps1` exits 0 for a partial result only after a confirmed restore. A divergence
before or during the stage, or with driving rows unplayed, stays failed. Passed, failed
and aborted results are unchanged.

If the game is still running when replay ends (it would not close, or `-KeepGameOpen`
was given), restoration is pending. `Session.ps1` then writes `restore-pending.txt`
with the `-RestoreEnvironment` command and exits 4, whatever the replay result. A
restore failure throws. Replay does not restore the game's `cloud` save folder: Steam
Cloud syncs it on exit.

## Evidence and limits

October 4: owner session 5 completed automatically through all 13,428 driving
poses, finish, results and final menu. An independent 12-second synthetic physics
capture also completed format-3 replay: 721 driving poses, zero reported position
application error. Its 721 force samples passed 27,453 offline checks with no
native-magnitude mismatches. Identity and zero-gain trials passed. See the
[validation record](reviews/2026-10-04-playback-validation.md).

Legacy formats 1/2 lack semantic buttons, completion seals, exact RPM and resolved
scene snapshots; their menus depend on preferences and controlled random seeds.
Use fresh captures for reusable cases. Settings edits, arbitrary pointer motion
and every third-party mod are not covered. This adapter seeds randomness at state
changes, so these are instrumented sessions. Wider stage/car, restart and
multi-stage coverage is not established by the tested cases. Synthetic drives
are labelled separately from owner drives. Force CSV capture requires the normal
FFB calculation path enabled and ready; physical muting preserves that path.

The shared `Dbce.Wheel.Playback` core owns ordering, bounded writing, seals and
statistics. This adapter owns Unity hooks, scene snapshots, units and restoration.
Its separate version/provenance in `lib/playback` does not replace the native pin.
