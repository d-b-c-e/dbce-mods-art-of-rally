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

## Packaging and failure-path follow-up

The RC8 clean-source build passed all 19 automated suites. The new force-trial
suite initially exposed a stale manual-checklist name allowlist; that validator
was corrected and its six regressions pass. The optional SessionTools RC9 ZIP
was extracted, its completion seal checked, its analysis runner passed the same
27,453 capture checks, and its packaged installer/uninstaller ran against an
isolated game layout. This does not replace final-artifact live validation.

The recorder now follows the shipping force path's focus/settings gates. A
deliberate zero/release while unfocused is not a normal force-model sample.
New captures retain the game assembly hash, checked on playback; older tapes
without it retain their existing compatibility limitations. The synthetic
fixture checks for competing games before changing any owner preferences.

## RC10 checkpoint

Clean source `dcacff43595101bb293714b86b272afc8e284f08` passed all 19 automated
gates for `0.4.0-rc.10`. Both the normal ZIP and optional SessionTools ZIP were
built from that source. Archive SHA-256 values:

- Normal: `84FD75B561D22771CE37BA4DA69387A33F22538744FDDBA8634849067B363A05`.
- SessionTools: `0246F0C063CBA25E1BDE1B52EA56843A928A943296EA06883947C4B020D31CEC`.

After the owner restarted the PC, the extracted optional archive passed its seal,
managed-assembly checks, three-file fixture install/removal and 27,453 original
capture analysis checks. No game ran. Private receipts:
`results/rc-0.4.0-rc.10-b258d6c4f0b84f89b168437e884ea648/automated.json` and
`results/session-tools-rc10-post-reboot/verification.json`.

At that checkpoint RC10 had not been installed or tested live and was not published. The installed
shipping mod remains RC4; owner settings retain their protected hash. See the
[graphics incident](2026-10-04-graphics-hang.md). The release draft remains
conditional on the final current-build capture/replay check.

## Evening owner acceptance (supersedes the checkpoint above)

RC10 and its exact matching optional probe were installed at 20:39 local, with
eight packaged payloads verified and six protected files unchanged. Backup and
receipt: `results/session-rc10-install-20261004-203934`.

Fresh synthetic roundtrip7 completed 721 force rows, 1,210 encoded packets,
three collision entries and two effect requests. Offline analysis passed 26,665
assertions with zero force delta and zero native-magnitude mismatches. Replay
`204130` completed all 721 driving poses with zero position application error.
The owner correctly objected to using that off-road synthetic trajectory as the
visual test. It is storage/signal evidence, not acceptance of a racing line.

The temporary small-window test also used the wrong fullscreen enum (0 means
exclusive, not false/windowed). It caused failed exclusive switching/fallback.
The override was removed. Playback now preserves owner display preferences and
TripleScreen.xml separately from the recorded scenario. No display topology or
wheel/SimHub tune was changed. The broader GPU incident remains unexplained.

The original owner session5 `replay-20261004-204543` completed automatically:
13,455 driving/finish poses, zero detected position error, finish/results/final
menu, normal close and restoration receipt. Triple rendering reported three
active cameras at 2560x1440. The owner confirmed "Yes, this looks right", then
"everything looks great" and explicitly requested shipping and moving to iRacing.
That is scoped owner acceptance, not a passed full hardware matrix.

The final source adds a five-second recovery for a blocked game-bindings handoff
(26 route assertions) and environment preservation (15 isolated assertions).
The first final-labelled artifact passed all 20 suites but checklist generation
rejected the added suite name. Its archive/evidence is preserved; the validator
allowlist is corrected before the replacement clean-source release build.

## Final release

0.4.0 is now published and installed from clean source 30d421b. All 20 final
gates passed (environment suite now 18 assertions); exact final session-5 replay
210753 passed all 13,455 poses and environment restoration. Published downloads
match. See [the final release evidence](2026-10-05-release-0.4.0.md).
