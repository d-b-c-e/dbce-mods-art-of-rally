# Art shared input: reviewed runtime milestones (2026-10-12)

All runs: disposable copy `art-shim-probe-game-20261011`, owner game untouched, rig lease,
owner idle, Steam broker at Medium, `DBCE_FFB_MUTE=1` from the native load-time latch, no
OS input, exact-child close, verified restoration of shared state and owner pins. Evidence
folders are under the ignored `games/.worktrees/art-shim-probe-evidence/`.

Common pins: native `UnityForceFeedback.dll` (toolkit snapshot candidate)
`DD3013147821364AD8795F76B4500FB479F669E6BD89576F83E725A677273414`; owner Wheelkit profile
`dbce-controls.ini` `5206EDFBD1B400E5D6A41D91AE836485BBE27D63992AD6F7483B3BABB0E72B79`; owner
settings `91090837181C1D617F3C0EE52F1DE797A2592C6FEA4C7B2D8E93138318EE36ED`.

| Milestone | Time | Source | Mod DLL | Shim | Result | Evidence |
|---|---|---|---|---|---|---|
| Axes Apply (ordinary copied provider) | 02:11 | `05c7830` (codex/art-shim-input) | `38641A00CB521BB73BAFA36E22EC0C4C8FEA5FDF661134001EE1ABF1D4F91B4E` | `7DDC962132819EDAE76EF84CE6509F1936B8C38177A36D498B8ABBCFDCFFBCAF` | 120 published menu frames, isolation restored | `apply-20261012-021108` |
| Ordinary SharedInput startup smoke | 03:23 | `0adda89` (src `8f9b945`) | `CF608D9ADFF098A771BB0DCF6A2B9EAB5A00B79EC29C413596A260BEC7A653F8` | `7DDC9621` | attached after FFB settled; first frame accepted, 14 owned actions | `shared-smoke-20261012-032314` |
| Menu Trial (latched navDown/navUp) | 04:43 | `f9c7458` | `78A08FA7BE1E8CD38E988089B32A69D80F8E7A85078FC349AF2515671BD1EA3C` | `0D10F913C998C97D7DDF3A8F5F470D010092C3681152059DC88D7E0A7BB0386B` (art-shim-bridge f38b855) | Career, Time Attack, Career; navigation wait 65 frames; Run Completed | `trial-20261012-044311` (Observe `observe-latched-20261012-044249`) |
| Car Drive (latched raw stimuli in a stage) | 06:05 | `96cc440` | `8754D961E1280047F26D3C1BD74A9A78361F6EAD5148813C81B5CB3580839D6E` | `0D10F913` | all 7 rows passed (below) | `drive-20261012-060415` (Observe `observe-drive-20261012-054434`) |

## Car Drive details

- Recorder `ArtOfSimRally.DevRecorder.dll`
  `985C2CA4947A569C6E99DF52B4DEBC095A298647B6BBC8CEFB31B0B9383DB6B5`, route rc13 muted
  synthetic capture (Japan6, CUSTOM, Car_Kei, Afternoon) from its taped start state.
- Staged copy settings: the owner file plus `TransmissionMode=Sequential`
  (`9D32D71027F03107862DAD4C2A097764F06BA0E5A6788A7262930261EBF82200`).
- Rows (each: 20 consecutive clean release samples on steer, throttle, brake, handbrake):
  throttle 40% hold 172; steer -30% hold 61; steer +30% hold 72; shiftUp gear 2 -> 3;
  shiftDown gear 3 -> 2; brake 60% hold 95; handbrake 100% hold 90. Every hold sample:
  applied sign, composition equal to Art's for the applied values, car fields equal to the
  composition. The recorder's car tape (485 UNDERWAY ticks) agrees: steer +-0.3,
  throttle 0.4, brake 0.6, handbrake 1.0, gear 2 -> 3 -> 2.

## Ordinary native-script coexistence

First run, 06:52 (`40acdf9`, `native-script-20261012-065119`): **FAILED** as sealed
("car input at step 736 is no composed result"). Kept as the original failure.

What held:
- The title hold waited 26 s for the shared consumer.
- All 1976 stage frames were composed, isolated, live and focused.
- The wheel stayed at rest: steer 0.140733957, with a one-count jitter on 146 frames.
- Art read the script exactly.
- Steering stayed the wheel's while Art read ±0.8.
- The pedals and handbrake passed through unchanged.

Why it failed:
- 959 of the 1080 stage car ticks matched.
- The other 121 ticks were two of Art's out-of-bounds resets (steps 736-796 and
  1017-1076). The resting wheel and an 11 s throttle took the car off the road.
- `OutOfBoundsManager` then calls `PlayerManager.InternalInertialDampeners` every frame
  for 1 s. That forces `handbrakeInput = 1` outside GetInput, over a composed
  steer/throttle/brake.
- The fix (accepted, romi 32048) shortens the script. The verdict stays as strict, and
  its failure text now names that signature.

Accepted for source and fixtures in romi 31616. Scripted Rewired native-path evidence,
not physical keyboard or wheel/FFB qualification.

- Stage with `Prepare-SharedInputCopy.ps1 -Recorder`. This stages the ordinary
  copied-provider shim `7DDC9621`, native `DD301314`, profile `5206EDFB`, the owner
  settings with SharedInput on, and the dev recorder. There is no probe request and no
  latched session.
- Run with `Invoke-SharedInputCopy.ps1 -Result <staged> -RouteSource <rc13 capture>`,
  after a `-PreflightOnly` pass. The run is muted: `DBCE_FFB_MUTE=1` plus the recorder's
  output, save and upload guards. The route's start state is applied and the owner
  environment restored. The recorder request is runner-owned and removed afterwards.
- The ordinary session qualifies only in the menus. In the 03:23 smoke it began about
  29 s after launch, and the route reaches the stage about 28 s after recording starts.
  The recorder therefore holds the route, untouched at the title, until the mod's
  consumer has started. It gives up after 120 s.
- At the stage the recorder (`carInput=native`, `NativeEvidence.At`) scripts the Rewired
  axes that Art's `AxisCarController` reads. Every value clears Art's largest deadzone
  (0.75). Each action is held alone and briefly, so the car stays near the start:

  | Seconds | Scripted values |
  |---|---|
  | 0-1.5 | throttle 0.8 |
  | 1.5-2.5 | brake 0.8 |
  | 2.5-4 | handbrake 1 |
  | 4-5.5 | steer +0.8 |
  | 5.5-7 | steer -0.8 |
  | 7-12 | released |

- `native.tape` has one row per player `GetInput` call, with:
  - the script values;
  - Art's own result, from a `Priority.First` postfix that runs before the mod;
  - the final result, from the recorder's `Priority.Last` postfix;
  - whether the shared composition ran that frame, Rewired isolation, and live steering;
  - the shared applied values and the alignment term.

  `native-verdict.txt` is sealed with the capture.
- What we expect with the wheel untouched:
  - Without force the wheel rests off centre (shared steer 0.1407 in the 02:11 Apply
    receipt). Steering therefore stays the wheel's (plus Art's alignment term) while Art
    reads the scripted ±0.8. This is the ordinary exclusive ownership.
  - Throttle, brake and handbrake pass Art's scripted values through unchanged, because
    the composition takes the larger value and the pedals are neutral.
  - The car tape carries the composed values.
  - Scripted steering passes through only with the wheel at exact centre, which needs an
    attended run.
- Fixtures: `tests/Recorder` `Native()` covers a centred and an off-centre clean session,
  the alignment term, the automatic-reverse swap, and refusals. The refusals are:
  unfocused, no composition, no isolation, steering not live, a moved wheel, a pressed
  pedal, script leaking into wheel-owned steering, changed pedals, a swallowed script,
  a short capture, missing coverage, a stale car tick.

## Claims and limits

Claimed: ordinary startup attaches and accepts frames (no physical input); latched
menu navigation reaches Art's UI; latched raw driving stimuli reach Art's car through
the shared shim's composition. Not claimed: ordinary (non-latched) driving or
navigation with physical input, keyboard during a latched session (suppressed by
contract), keyboard fallback after a terminal fault (source/fixture only), force
feedback together with the shared profile, H-pattern, clutch, finishing a stage.

Failed or refused runs on the way are recorded in
`2026-10-12-shim-actions-slice-a.md` and `2026-10-12-shim-driving-slice-plan.md`.
