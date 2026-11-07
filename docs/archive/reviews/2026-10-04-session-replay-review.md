# Recording and playback review — 2026-10-04

Claude's whole-session playback is a useful game-specific prototype, but its current success result is too permissive for a regression gate. Reuse the existing shared recording and replay contracts; keep Art of Rally's hooks, state restoration and input interpretation in an adapter.

## Reviewed scope and evidence

Reviewed `bae790b`, `b5c8e46`, and Claude's concurrent follow-up **`c700c6bb0e7790353d533132b11ee5aa2536a457`** on `claude/unified-mod`. The latter correctly moves car input capture/application into a `CarController.FixedUpdate` prefix and restricts pose correction to `UNDERWAY`; the findings below include that change. Line references below are for that commit. Its `SessionTape.cs` SHA-256 is `2D66E2759E71E0DD5CA007733C898CD3DAF8253925472B4D8F35A8DF6D8EEE6A`.

This review changed no runtime sources, installed payloads, settings or shared toolkit files and launched no game. Claude's files were changing during the review, so offline tests used frozen copies under `results/session-review-20261004-codex/`. The pre-existing game session was left under its existing owner's control. Findings about Unity ordering, performance and physical outputs are source analysis unless explicitly identified as tested.

Verified offline:

- The existing Art of Rally force replay rebuilt in an isolated output directory and passed **215,926 assertions across both preserved owner captures**: 8,974 Norway rows and 2,947 crash-baseline rows, zero device-magnitude mismatches. This is arithmetic/signal reprocessing, not driving the game.
- The shared `Dbce.Wheel.Recording` source passed **49 tests** from an isolated copy. All eight source hashes and the vendored Recording DLL hash match Woden's `lib/recording/provenance.json`. The library remains unpublished and separately pinned.
- The portable replay contract recovered from toolkit commit **`87b47d53121c0b3fc8479e622ae8a79c8ca3fa58`** passed **17 tests**, including the symlink case on this machine. Its Python files are absent from the toolkit's current working tree, but present in that Git object. The active toolkit checkout is at `6f9c662` with substantial local changes; do not reset it to recover these files.
- Seven source-logic checks reproduced the edge cases listed below against `c700c6b`. Five concern alignment/completion; two concern multiple reads of one input within a frame. These checks compile extracted methods without loading Unity, native devices or the game.

Private evidence and reproduction:

```powershell
pwsh -File results/session-review-20261004-codex/Test-SessionSnapshot.ps1 `
  -Source results/session-review-20261004-codex/SessionTape.c700c6b.cs
python -m unittest discover -s results/session-review-20261004-codex/portable-contract -p 'test_*.py' -v
```

The first command deliberately reports the current defects as `FAIL`; its exit status indicates that the diagnostic ran, not that these invariants passed. `logic-findings.json` records the source hash. `evidence.json` records current tape counts/hashes and retained replay results. The shared test receipt is `test-results/shared-recording.trx`.

## Prioritized fixes

### R1 — P1: Success does not establish full playback

`tools/testing/Recorder/SessionTape.cs:402–404, 430–431, 528–550`.

`Tick` passes when only `_frames.Finished` is true, without requiring car-tape completion or a verified terminal state. `Stop` passes merely because the current frame segment is the last segment, even if most of its rows remain. `Aligner.Next` can skip the unread suffix of a segment when the live marker advances. Its early-input workaround can consume a short final segment without the live game ever reaching that segment's marker.

The extracted-source fixtures demonstrate:

1. Tape `[A, B]`, live marker `A` on both calls: `Finished == true`, although `B` was never observed.
2. Tape `[A, A, A, B]`: a live transition after the first row jumps straight to row 3 and silently drops rows 1 and 2.
3. One frame row versus three car rows: frame completion is true with two car rows still pending.
4. Stopping after one of three rows in the only segment satisfies the production `Stop` pass predicate.
5. An empty frame tape makes `Finished` index segment `-1` instead of being rejected during preflight.

**Fix:** distinguish consumed input, transition requested, transition observed, and all required streams complete. Never silently discard rows in strict mode. Treat manual stop/process exit as aborted unless the complete terminal contract has already been met. Empty/missing required streams must fail validation before arming. Record consumed/skipped/remaining counts per stream; `_fixedStep` counts live callbacks, including waits, so it is not a consumed-row count.

### R2 — P1: The no-force guarantee ends on failure, stop or completion

`SessionTape.cs:222–230, 251–257, 434–446`; `src/ArtOfSimRally.Mod/TelemetryPump.cs:45–76`.

Every force-muting prefix checks `Replaying`. `Finish` sets the mode to `Off` while the game can remain active, immediately making all those prefixes permit ordinary force again. This applies to failed alignment, external stop, successful completion and `-KeepGameOpen`. A patch-attachment exception also sets the mode to Off; attachment is not transactional and expected mute methods are not checked for a nonzero match count.

The patch list only changes wheel FFB arguments. Forza UDP telemetry continues through the normal sender, so replay can still feed SimHub-controlled outputs if they are connected. Therefore the documentation's **all force output is muted** claim is broader than the implementation. No physical output was exercised during this review.

**Fix:** establish a one-launch output policy before replay attachment, independent of playback progress/result. Keep physical output suppressed through failure and teardown, until explicit attended takeover or process exit. Verify all required output boundaries before injecting inputs; roll back partial hook installation while retaining suppression. Route replay telemetry to an observation sink or explicitly separate diagnostic export from live actuator consumers. Do not rewrite the owner's saved output preferences.

### R3 — P1: Rendered frames and physics steps have no common replay clock

`SessionTape.cs:127–169, 324–341, 375–405, 513–515`.

Menu input advances once per rendered frame, while car input advances once per physics step. Files contain separate ordinals and markers but no common timestamp, recorded delta, frame-to-step mapping or hook phase. A menu/pause action recorded at 60 FPS can occur at a different point in the drive when replay renders at 120 FPS. The aligners do not solve the ordering of events within a long `UNDERWAY` segment. `WaitLimit = 7200` means about 60 seconds at 120 FPS and 240 seconds at 30 FPS, despite reporting two minutes; the car timeout cannot advance when there are no car callbacks.

Claude's new physics-prefix fix improves the car seam, but old and new tapes still have the same format/version even though pose/input sampling moved from a post-physics seam to a pre-physics seam.

**Fix:** record a shared monotonic clock plus render frame, fixed tick, sequence and hook phase; use an actual monotonic deadline for state waits. Give the adapter an explicit policy for simulation-time input and UI transitions. Preserve input edges exactly once and retain causal pre/post state observations. Version the capture seam and reject incompatible old tapes instead of interpreting them silently. Do not use a ten-frame lookahead as the generic transition contract.

### R4 — P1: Missing initial-state checks can replay a different scenario

`SessionTape.cs:128–129, 450–483`; `tools/testing/Session.ps1:65–79`.

The manifest records game/Unity version and a start date only; replay does not validate even those values. PlayerPrefs are exported but not restored or compared, and Steam save data/mod settings are not captured as replay preconditions. Markers identify scene, panel and event status, not car, stage direction, weather, assists, menu selection or occurrence number. A different car/weather on the same scene passes the marker check. The documentation's assertion that changed saved selections necessarily fail instead of driving the wrong scenario is therefore incorrect.

Reseeding the global Unity random generator at every marker change alters the recorded session as well as playback. A screen-name seed does not establish the same initial RNG state or random-call ordering. It also repeats the same seed when revisiting the same marker. This is a deliberate scenario modification and must not be hidden inside passive recording.

**Fix:** retain and verify exact game/mod/probe/toolkit/config identity, then capture or explicitly establish the adapter's initial scenario. Snapshot/restore only the owned state with a recovery receipt; account for cloud/saved progression before using player data as a replay sandbox. Record the resolved stage/car/weather/direction and validate them before driving. Prefer explicit scenario selections or scoped RNG capture over reseeding the global generator from UI names. Add occurrence IDs so repeated stages and menus cannot alias each other.

### R5 — P1: Damaged recordings can remain apparently usable

`SessionTape.cs:121–170, 361, 409, 419–446`.

There is no format/schema version, immutable artifact manifest, row-count receipt, complete footer, drop counter or file-size bound. The reader eagerly loads every line. Three existing filenames are the launcher's completeness check. Most critically, recording exceptions call `Fail`, but `Fail` acts only in replay mode: a disk-full/serialization/marker-write error during recording is swallowed without marking the tape incomplete. `Stop` has no transactional finalization or retry state. `StartRecording` itself opens streams with overwrite enabled.

**Fix:** reuse bounded recording infrastructure with exclusive file creation, background errors/drop accounting, a completed footer, and hashed artifacts. Capture failure must stop acceptance and retain an explicit incomplete reason. Finalization should be retryable; a crash-truncated tape should be inspectable as partial evidence but ineligible for an exact replay pass. Reject unsupported versions, nonfinite/out-of-range channels, broken sequences and oversized files before attachment.

### R6 — P2: Recording reintroduces synchronous disk work in the game loop

`SessionTape.cs:324–334, 383–389, 463–470`.

Every fixed step allocates strings/arrays and writes through `StreamWriter`; every frame serializes a dictionary; every 60 rendered frames flushes all streams on the main thread. Buffered writes still flush when their buffer fills, so the physics callback can block on storage. This compromises stutter/timing comparisons, an area the old probe deliberately protected with preallocated buffers. No new frame-time benchmark was run here.

**Fix:** enqueue bounded snapshots to a background writer and report drops/queue pressure. Cache hot-path reflection and components. Keep screenshot scheduling separate from capture timing. Measure capture-off versus capture-on overhead before using a session to diagnose stutter. The existing shared recorder is bounded but performs dictionary copies/allocations; it is a starting point, not an allocation-free guarantee.

### R7 — P2: Launcher failures leave requests armed or return successful exit status

`tools/testing/Session.ps1:32–57, 61–94`.

The request is written before `Start-Game` checks whether the game is already running. A rejected launch can therefore leave a pending replay that activates on a later normal launch. Requests lack expiry and an acknowledgement tying them to a process/version. `-Replay` prints `result.txt` regardless of whether its content says failed/stopped, so normal script completion can return success to automation. `Send-Probe` bounds connection time but not `ReadLine`; a hung game can block `-Status`, `-Stop`, or even the timeout diagnostic indefinitely.

**Fix:** preflight first; atomically publish a unique expiring request; require probe acknowledgement of that request ID, process and adapter version; cancel only the matching unconsumed request on launch failure. Validate result status and return distinct success/failure/aborted codes. Bound response reads as well as connection and startup waits. Use unique output directories and publish the result atomically after final evidence is finalized.

### R8 — P2: Session hooks have no release-gate coverage or full teardown

`tests/Recorder/Recorder.csproj`; `tests/ProbeHooks/Program.cs`; `tools/testing/Recorder/RecorderMain.cs:74–77`.

The Recorder tests compile the older capture classes, not `SessionTape`. ProbeHooks calls `RecorderMain.Attach`, while the new hooks are attached separately through `SessionTape.TryArm/Patch`; passing the existing gate does not test their installation. Unload only unpatches `ArtOfSimRally.DevRecorder`, not `ArtOfSimRally.DevRecorder.SessionTape`, and does not stop/dispose the session tape. Old hooks or writers can survive a probe unload.

**Fix:** make the coordinator independently testable, cover adapter hook discovery/attachment on the supported runtime, and provide one idempotent teardown path for partial attach, stop, unload, shutdown and re-arm. Keep the output suppression lifecycle separate as described in R2. Exercise the result/timeout/cleanup behavior with a fake game host before another attended run.

## Additional fidelity issues

- **Multiple input reads:** `Bool`/`Float` at lines 290–299 only store true/nonzero values. A true→false or 0.5→0 sequence within one recorded frame retains the earlier nonzero value. The extracted-source fixtures reproduce both. If same-frame values can vary, choose explicit sampling phases or preserve ordered reads; removing a dictionary entry only fixes last-value snapshots, not call ordering. This review does not establish how often the current game produces such transitions.
- **Artificial pose correction:** the retained Japan run reports `passed`, 173 pose corrections, 430 gear sets and 4.22 m maximum pose error. That is useful evidence of assisted playback, not deterministic input replay or validated force physics. Pose correction and recorded gear enforcement can hide regressions and perturb collision/telemetry observations. Make strict replay and assisted viewing explicit policies; strict replay should report divergence, with corrections disabled. Do not infer successful execution of the newer `c700c6b` behavior from this older run.
- **Screenshot completion:** `CaptureScreenshot` is requested before result publication, but no completion/hash is awaited. The runner may close the process immediately after seeing the result. A result should enumerate which checkpoints were actually captured/verified rather than treating a scheduled screenshot as durable evidence.
- **Raw settings input remains outside the tape:** the mod uses Unity input and direct USB paths as well as the patched game wrappers. Recording the UMM open/closed flag does not capture arbitrary settings edits or prove that all physical input is ignored. State supported scope explicitly and make settings/config transitions part of the adapter when needed.

## Reuse what already exists

| Existing asset | Verified location / identity | Reuse |
|---|---|---|
| Art of Rally developer capture and arithmetic replay | `tools/testing/Recorder/CaptureSession.cs`, `tools/testing/Replay`, `docs/DEVELOPMENT-CAPTURE.md`; two current corpus cases pass | Preserve its existing schemas/corpus and force-model checks. Do not replace these with pose playback. |
| Shared bounded numeric recorder/reader | `E:/Source/toolkits/dbce-wheel-mod-toolkit/dotnet/Dbce.Wheel.Recording`; Woden pin `lib/recording/provenance.json` | File ownership, background writing, limits, counters, footer validation; pin/release deliberately. All eight source hashes match the existing Woden pin. |
| Portable case and force-observation contract | Toolkit commit `87b47d53121c0b3fc8479e622ae8a79c8ca3fa58`, `tools/replay/replay_case.py` | Exact source/initial-state/config identity, bounded observations, structural comparison, separate baseline and tuning trials. Recovered and tested without modifying the active toolkit tree. |
| Woden consumer implementation | `E:/Source/games/dbce-mods-woden-rally-edge/docs/RECORDED-PLAYBACK.md` and `tools/Start-RecordedGame.ps1` | Expiring one-launch requests, saved preference preservation, source/config identity and actual-model reprocessing. Its declared capability is `signal-reprocess`. |
| Art of Rally full-session hooks | `SessionTape.cs` | Keep the Rewired/UMM/splash/car/scene logic as this game's adapter, after the fixes above. |

The shared contract distinguishes **`signal-reprocess`** from **`game-input-replay`** and requires an initial-state artifact for the latter. Its documents describe native format ownership for MAME INP and F-Zero tapes; those adapters were not validated in this review. A small current F-Zero `src/fzero_replay.c` also supports scripted frame input and viewport changes, which should not be mistaken for proof of a complete recorded playthrough.

Do not silently add fields or a third capability to the existing strict v1 schema: unknown keys are rejected. Put compatible fidelity policy in a hashed profile artifact, or propose a versioned contract extension when it changes capability semantics. Likewise, the numeric Recording v1 API requires strictly increasing sample times; a game-input stream with several ordered events at one tick needs an explicit event/phase representation, not artificial timestamp nudges.

## Recommended ownership and implementation sequence

```text
Shared toolkit
  case/artifact identity + validated storage + results/comparison
  pure session coordinator + clocks + transitions + output policy
                |
Game adapter    |  one per game / engine integration
  scenario restoration and verification
  input sampling/application at declared phases
  stage/car/checkpoint identity and game-specific observations
                |
External runner
  request/acknowledgement + process ownership + deadlines + artifacts
```

1. **Make this prototype honest first:** fix R1/R2/R5/R7, add synthetic failure tests, and label existing success receipts as historical assisted runs. Preserve the current owner tapes unchanged.
2. **Recover and pin the shared foundation:** use a separate toolkit worktree at the retained contract commit; reconcile the unpublished recorder with its exact Woden provenance and release it through the toolkit workflow. The active toolkit worktree contains unrelated work and should not be reset or force-switched.
3. **Extract portable policy, keep the hooks local:** move case/storage validation, result semantics, clock/transition coordination and request lifecycle out of the static game class. Make the adapter declare whether it supports signal reprocessing, validated game input replay, assisted viewing, checkpoint capture and initial-state restoration.
4. **Version Art of Rally's tape adapter:** retain a legacy reader for existing tapes with their original sampling-phase/fidelity limits. Generate a new case manifest that references immutable original bytes; never rewrite the owner's recording to make it fit a new schema. New sessions use the versioned clock/phase/state contract.
5. **Integrate regression coverage:** before live testing, cover 30/60/120 FPS schedules, multiple fixed steps per frame, delayed/early transitions, repeated stage/marker occurrences, pause/focus/settings changes, empty/truncated/nonfinite tapes, full/dropped queues, failed writes, partial hook attachment, unload, stale requests, process exits and result codes. Require all required streams/checkpoints and the requested completion reason for success.
6. **Then perform a scoped attended acceptance:** repeat a known stage with exact scenario identity, show strict divergence separately from assisted corrections, verify recorded screenshot files and restore the pre-run environment. Only claim the capability actually observed. Keep hardware/force acceptance separate from replay equivalence.

The reusable unit is the case contract, lifecycle and evidence pipeline plus narrow game adapters. Porting this entire `SessionTape` class into each game would carry its timing assumptions and failure modes into every new integration.
