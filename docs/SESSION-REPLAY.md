# Session recording, playback and force analysis

Install the optional **SessionTools** release archive separately from the normal
mod. Close the game before installing or removing this developer probe.

```powershell
./tools/testing/Install-Recorder.ps1 -SkipBuild  # omit -SkipBuild in a source checkout
./tools/testing/Session.ps1 -Record -Name japan-test
./tools/testing/Session.ps1 -Stop               # save before quitting
./tools/testing/Session.ps1 -Replay -Name japan-test
```

Record from launch, choose a stage, drive, then stop. Normal recording retains
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

Replay writes a separate timestamped directory. Success requires all driving
rows, the final recorded frame state and valid trajectory application. Faster
loads/cinematics may skip transient menu or non-driving rows; counts are reported.
If restoration is pending, close the game and use `Session.ps1 -RestoreEnvironment`
with the explicit `replay-*/owner-environment` backup. Keep that backup until its
restoration receipt exists. Preference import is restricted to the game key.

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
