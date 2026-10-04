# Recording and playback validation, October 4

The owner handed off Claude's session-5 implementation, reported immediate route
drift, then observed a regression with flying/stopping cameras. Recording is an
optional developer tool; the shipping mod's force/telemetry arithmetic is unchanged.

## Findings and fixes

Input resimulation did not reproduce hidden Unity physics/controller state.
Applying trajectory poses without exclusive ownership also failed: the game's
EventManager disabled kinematic mode each frame, stock wheel forces became
nonfinite airborne, and out-of-bounds logic reset the car. The corrected adapter
owns the kinematic player body, uses interpolated MovePosition/MoveRotation,
suppresses those competing physics/reset paths and verifies the previous command
against the next physics tick. Native replay uses the same kinematic approach.

Format-3 menu events alone still selected Japan 5 instead of recorded Japan 6.
Resolved scene-load snapshots now retain stage, weather, car and season state.
The launcher restores the original environment after each test. Files close
before completion hashes are written; incomplete captures cannot pass playback.

## Retained local evidence

| Case | Result |
|---|---|
| owner-session-5/replay-20261004-170002 | Rejected regression: competing physics/kinematic ownership and repeated resets. |
| owner-session-5/replay-20261004-171011 | Failed at 115 seconds with 0.516 m ownership error; remaining wheel physics suppressed afterward. |
| owner-session-5/replay-20261004-171820 | Full route/finish, zero reported error; final completion needed manual Stop. Completion rule subsequently fixed. |
| synthetic-roundtrip-1/replay-20261004-172545 and 173142 | Failed/aborted wrong scene; fixed with resolved scene snapshots. |
| synthetic-roundtrip-2/replay-20261004-173416 | Automatic success: 721 driving poses, zero application error, correct Japan6/Kei/Afternoon. |
| owner-session-5/replay-20261004-173553 | Automatic success through finish, results, final menu and normal closure. All 13,428 driving poses plus 27 finish poses, zero application error. 94 transient non-driving car rows and 284 menu rows skipped and reported. |
| synthetic-roundtrip-3 | Hung before save after adding broader native effect lifecycle observers; probe Stop/normal close failed. Outputs were muted; terminated and marked incomplete. Those extra observers were withdrawn for isolation. |
| synthetic-roundtrip-4 | Incomplete: only first frame, concurrent Woden test running. No success claim. Original owner backup restored and Settings SHA-256 matched at 22:52 UTC. Further launches now refuse a known competing rig game. |

Paths above are under ignored `results/sessions/2026-10-04-*`. Source captures
were not edited. Evidence and owner preference files are not public release assets.
Successful harness runs write environment restoration receipts. The original
shipping mod/settings hashes remained unchanged through the session-5 success.

## Force and telemetry evidence

The second fresh synthetic capture contains 721 real Unity physics force/motion
rows, 3 collision observations, 1,181 encoded telemetry packets and two requested
impact plays, all with physical output muted. Offline arithmetic replay passed
27,453 checks with zero native-magnitude mismatches. An identity tuning trial
changed zero rows above 1e-6 (maximum floating delta 2.98e-8); zero gain produced
zero candidate steering requests. Trials preserve capture and tune bytes.

SignalStatistics has offline coverage for signed range, RMS, nearest-rank
absolute percentiles, saturation, nonfinite rejection and missing channels.
The portable core passed 25 checks, including ordering, required future driving
rows, bounded writer tail closure, exclusive creation and corrupt/path-traversal
completion seals. Core source is separately pinned, not a new native toolkit pin.

These establish trajectory and arithmetic behavior for the stated cases.
They do not establish physical torque, input-physics determinism, every car/stage,
camera mode, restart or multi-stage transition. Existing hardware/UI issues,
including KI-44, remain open. No force tune or SimHub profile was changed.
