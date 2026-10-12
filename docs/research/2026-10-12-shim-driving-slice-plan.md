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
