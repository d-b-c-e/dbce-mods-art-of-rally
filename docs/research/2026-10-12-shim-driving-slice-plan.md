# Art shared input, driving slice: plan (romi 30328)

Goal: actual engine steering, throttle, brake, handbrake and sequential shifts
through the shared shim in one bounded, muted stage on the disposable copy, with
the native wheel maps isolated, outputs captured and shared state restored. No
upload, no force, no OS input, no owner install. Plan for review before source.

## Reuse

- **Stage start:** the dev recorder's existing synthetic-integration capture
  (`SessionTape` record mode with `captureDriver=<source>`). Its `CaptureDriver`
  replays an existing menu script to a stage, then would synthesise a short drive.
  The rc13 muted synthetic session reached `Japan6` (CUSTOM, single stage,
  `Car_Kei`, Afternoon) on build 17584229. With `captureDriver` the recorder
  already mutes force/telemetry and blocks `UploadScore`, achievements, stats and
  every local save (`PatchOutputs`), and tapes the car per physics tick
  (`car.tape`: steer/throttle/brake/handbrake/clutch inputs, gear, pose, rpm) and
  every `Drivetrain.Shift` (`shifts`). That tape is the observation capture.
- **Shim session:** the probe's existing Observe/Trial machinery (identity
  lease, exact-object isolation, neutral check, latched Core on 0D10F913, live
  fences, POSIX-rename batches, receipts, runner, restore).

## Changes (source)

1. **Recorder:** a request key `carInput=shared`. The menu script still drives to
   the stage, but at the stage the capture driver no longer writes car inputs
   (`AfterCarInput` leaves them to the game and the mod's shared input). The
   capture ends when the probe writes its receipt or after a bounded
   `captureSeconds` (default 30), whichever comes first; mutes and save/upload
   guards unchanged. Fixture: the recorder contract test (actual built mod +
   recorder) proves the key changes only who supplies car inputs.
2. **Probe `Drive` mode** (beside Observe/Apply/Trial; reviewed latched build
   only, Observe receipt with the same pins). It qualifies only in the stage
   while the car is held (`WAITING_TO_BEGIN`) or stationary at the start: the
   menu script must not see latched suppression, so nothing opens before the
   stage. Fences as the Trial's, plus the recorder's output and save guards
   attached (read from live Harmony state), the stage scene and a CUSTOM mode.
   Delivery mask: steer, throttle, brake, handbrake, shiftUp, shiftDown only.
3. **Stimuli: `inject raw`, not `inject action`.** Core's contract reserves
   action-level injection for navigation; qualification uses raw objects built
   from the intended profile (STD-034 item 3). From the owner profile (5206EDFB):
   steer R12 axis 0 (rest 33265, travel -1), throttle R12 axis 2, brake R12 axis 5
   (rest 1743), handbrake R12 axis 7, shiftUp/shiftDown DS-8X buttons 9/10. A
   fixed, bounded script (about 8 s), each step held then released:
   throttle 40 % (rolling start), steer left 30 %, steer right 30 % (with
   throttle), shiftUp, shiftDown, brake 60 %, handbrake 100 %, then `end`.
4. **Observation per step** (probe receipt), each from the same frame: the raw
   stimulus, the consumer's applied value for the action, the car's actual
   `CarController` input fields and `Drivetrain.gear`/`automatic`, speed. Pass
   rule per axis: the engine input equals the shared applied value (steer plus the
   game's alignment effect) and has the stimulus' sign; released steps return to
   neutral. Gears: one step up and one down when the effective gearbox is manual;
   with an automatic gearbox the receipt states "ignored by policy" (Art's
   TransmissionMode is the default "Follow game"; the owner's game option decides).
   The recorder's `car.tape`/`shifts` are the independent per-tick record.
5. **Runner:** one combined copy runner: stage the probe payload (Drive request)
   and the recorder (`Install-Recorder` into the copy only), write the recorder's
   `session-request.txt` itself (a shared marker; the runner owns it, snapshots and
   removes it), launch through the broker with `DBCE_FFB_MUTE=1`, wait for both
   receipts or the bound, close the exact child, restore everything as today.

## Keyboard (question)

A latched Core session suppresses native input, keyboard included, for its whole
duration by contract; the trial cannot also show keyboard driving. Proposed
meaning of "normal keyboard preserved": nothing persistent changes (Rewired maps,
settings, saves restored), and keyboard driving outside a latched session is the
ordinary path (`a8d0487`, not yet qualified on the rig). Please confirm or
redirect.

## Out of scope

Confirm/back and other buttons in stage, H-pattern gates, clutch, force feedback
coexistence, finishing or uploading a stage, the ordinary (non-latched) session
in a stage.

## Implementation (source and offline tests; not run in game yet)

- Recorder: `carInput=shared`, `captureSeconds`, `stopWhenFile` (`CapturePolicy`, pure and
  tested); in shared mode the capture driver writes no car input and clears its menu
  values at the stage. `captureSource=shared-shim-integration`.
- Probe `Drive` mode (`ShimStartup`, `ShimDrive`): admission only at the stage start,
  fences observed live (adds the recorder's upload/achievement/stat/save and telemetry
  guards read from Harmony, CUSTOM event, car held or stationary); delivery mask steer,
  throttle, brake, handbrake, shiftUp, shiftDown; a probe-only `CarController.Update`
  postfix samples every frame after the game stored its inputs.
- `DriveTrial` (pure): raw stimuli from the profile objects, encoded with Core's own
  denormalize (steer centred over the range, pedals rest to travel end, inversion,
  half away from zero). Each hold sample checks the applied sign, that the composition
  equals Art's for those applied values (steer plus the same call's alignment term) and
  that the car kept it; each release checks neutral; gears are read after the change
  completes; an automatic box leaves the gear rows open. Per-frame samples are written
  to `drive-samples.tsv`.
- Gears: `Prepare-ShimProbe -TransmissionMode Sequential` writes only the staged copy's
  Settings.xml (the game's `Drivetrain.SetTransmissionType` postfix then keeps the box
  manual); Observe and Drive use the same staged settings (the hash is pinned).
- Runner `Invoke-ShimDriveCopy.ps1`: recorder pinned and staged in the copy only, the
  runner owns the recorder's request marker, full snapshot/restore of Art's local folder
  and LocalLow, waits for both the probe receipt and the recorder's end.
- Batch writes: the POSIX rename now retries (bounded, 20 x 10 ms) on access denied or
  sharing violation; seen once in about 4,800 replacements under a concurrent reader.
- Keyboard: a latched session suppresses it by Core's contract; ordinary keyboard
  preservation is source/fixture evidence only (`a8d0487`, `--shared-transport-only`),
  not runtime-qualified. Core's contract doc reserves `inject action` for navigation;
  the pinned parser resolves any bound action, so raw stimuli are a choice, not a gate.

Tests: `tests/ShimInput` 8,296 (net8 and Unity Mono) including the drive steps against a
scripted latched Core (pass, slow shift, automatic box open, and failures for car
divergence in steer or throttle, wrong sign, stuck gear, sticky throttle, composition
off, short hold, never injected, stopped, countdown); `--drive-trial-only` 597 against
the real 0D10F913 Core through the production composition; recorder 82; full suite
20/20; 12 DriveTrial mutants all fail.
