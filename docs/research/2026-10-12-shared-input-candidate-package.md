# Art shared input: private owner-test candidate package (2026-10-12)

romi 32256 asked for the next offline Art delivery slice. romi 32354 accepted the native
port (option A) and the private `-SharedInputCandidate` packaging. The independent native
review passed (32797), and so did the packaging and ownership review (32927).

Main is not fast-forwarded through this branch, because its history carries the shim
blob (32891). It gets a squash of the clean final tree onto a new branch from `7c1b67e`,
whose ancestry has no `7DDC9621` blob. Nothing has been merged, installed or launched for
this slice.

## What the candidate is

`tools/package/package.ps1 -Version X.Y.Z-rc.N -SharedInputCandidate` builds
`ArtOfSimRally-X.Y.Z-rc.N-shared-input.zip`. It is the normal package plus the following.

| File | SHA-256 | Source |
|---|---|---|
| `ArtOfSimRally/dbce_shim.dll` | `7DDC962132819EDAE76EF84CE6509F1936B8C38177A36D498B8ABBCFDCFFBCAF` | art-shim-bridge `574bb88` copied provider (P21 receipt). Byte for byte the shim of every ordinary shared run; the mod refuses any other. |
| `ArtOfSimRally/UnityForceFeedback.dll` | `44E46799945438BC07DF738472602DB1E3ECC04C3F77CAD5B12FCACF49C69272` | toolkit `mufa/native-091-read-snapshots` `bbbf6e3` (details below) |

`lib/input-shim/CANDIDATE.json` pins both. `build.json` records them as
`sharedInputCandidate`, and the installer's verifier accepts the shim only with that
record. The default package is unchanged: the `lib/toolkit` native `F8361A08` and no shim.

Where the two binaries live:
- **Native:** vendored in `lib/input-shim/native`. It is the same MIT toolkit component
  this public repository already vendors and ships.
- **Shim:** not kept in this public repository (romi 32775). The private build passes
  `-SharedInputShim <private copy>`, which is checked against the pin. See
  `lib/input-shim/PROVENANCE.md`.

### Shipping the shim (romi 32767)

- `dbce_shim.dll` is an optional payload entry, pinned by the build record.
- It is this mod's to replace or remove only while the installed `build.json` pins exactly
  its bytes. That is decided before anything is written.
  - An uninstall, or an install of a release built from this branch without the shim,
    removes an owned copy and keeps any other copy.
  - A candidate install over a different copy stops before writing anything. A
    byte-identical copy is adopted.
- **Reverting:** run this candidate's `Uninstall.bat` first (settings are kept), then
  install the previous package. Older installers, for example the published 0.4.1, do not
  know about the shim and leave it in place.
- `Settings.xml` and unknown files, including an owner-placed `dbce-controls.ini`, are
  kept.

### The opt-in

- **Default:** `Settings.SharedInput` is false; the mod's F6 Controls toggle "Use shared
  Wheelkit profile (candidate)" turns it on.
- **When it takes effect:** at startup only, after a restart.
- **What the Controls page shows:** the status lines say whether the profile attached,
  or why it was not used. When it was not used, Art's bindings stay in charge.

## Native port (romi 32354, option A)

Shared input reads Art's own wheel handle through `GetReadDeviceInfoV1`,
`ReadDeviceSnapshotV1` and `GetReadDevicePathV1`. The accepted native has none of them.
`DD301314`, the native used by the shared runs, sits on a diverged toolkit line, and
`ecc49bd` does not apply cleanly to the baseline.

The port, `bbbf6e3`:
- **Base:** exact `c8ec2ee` (`native-0.9.1-fleet-candidate`).
- **Carried over:** only `ecc49bd` and `64ccf67`. The header, state and test are taken
  unchanged.
- **Left out:** the baseline has no external FFB session and no test-injection table, so
  their guards are absent.

Evidence that force, send, init and hold are unchanged:
- **Source:** `WheelFfb.cpp` has 11 added lines and none removed:
  - the snapshot includes;
  - a handle generation, incremented where `OpenCandidate` creates the force device;
  - snapshot invalidation on read-slot open, on a failed legacy read, and on close.
  No force producer, effect, init, send or hold line changes. `.def` and `wheelffb.h`
  gain only the three exports; the version stays 901.
- **Tests:** the x64 fake-COM suite was built from untouched `c8ec2ee` and from the
  port with the same toolchain.
  - Every shared test has the same exit code and the same PASS lines: burst, constant
    burst, constant recovery, logging, POV read with FFB acquire/recovery/open, and
    retained identity.
  - The only differences are the ABI lifecycle line (48 to 51 exports, version 901) and
    the new `read_snapshot_test` (PASS).
  - The vendored DLL `44E46799` itself passes the ABI test.

Master consolidation is not part of this slice (romi 32391). Nene's unreleased
`GetReadSlotCaps` on toolkit master overlaps with these exports. The direction is to
prefer this generation-bearing contract, with OutRun adapting once the candidate is
reviewed.

Review evidence (romi 32682), toolkit `126a992` (tests and docs only; native source and DLL
unchanged):
- **Function audit.** `audit_against_baseline.py` compares every function with
  `c8ec2ee`: 86 of 91 are identical, and five gain one line each (the list above). All 32
  force, init, send, hold and effect entry points are identical.
- **Struct sizes.** 144 and 232 bytes, pinned with offsets; the same as Art's
  `ShimBridge`.
- **Mixed-reader fixtures.** Slot 0 borrows the FFB handle and slot 1 is a separate
  reader:
  - generations are per slot;
  - an FFB handle change ends only the borrowed slot;
  - each failed read ends only its own slot;
  - close and reopen, and a real `OpenCandidate` reopen, never reuse a qualification.
- **Mutants.**
  - Killed: the generation bump, either legacy-failure invalidation, and the open and
    close invalidations removed together.
  - Equivalent: either one alone, because each covers close-and-reopen.

## Evidence

- **Full suite:** `Test-Rc.ps1 -Version 0.4.2-rc.903 -Corpus <owner corpus>` on clean
  `c19ad00` passed all 20 gates.
  - The recorded corpus passed: 2 cases, landing 9582 assertions.
  - Artifact `646CA450A6A47FB71305A57154FB31B661F72E87A6D1EA0AEAA0FBAF6A3D849A`.
  - The default package's payload list is unchanged.
- **Getting the suite to build and parse again** took two fixes, from before this slice:
  - the regression project needed a `ShimStartup` stub for `a03fb51` (`3633453`);
  - the shared-input Mono gate now skips Mono's BOM-only line, like the other Mono gates
    (`c19ad00`).
- **Candidate package:** `-Version 0.4.2-rc.904 -SharedInputCandidate` built
  `ArtOfSimRally-0.4.2-rc.904-shared-input.zip`
  (`BB8AE5733CFB68684D101B43D9B30EA3B35A5F91CBB2BF11CA3E340A71EC9FB0`). It carries the
  shim `7DDC9621`, the native `44E46799` and the pinned `build.json` record.
- **Installer runs:**
  - Candidate, then the baseline rc.903 installed over it: 58 assertions. The shim is
    removed, the baseline native is restored, and settings are kept.
  - Baseline package alone: 42 assertions.
- **Labels:** these RC numbers are local evidence labels. The owner package's label is
  romi's choice.

## Merge readiness

- **Target:** one squash commit of this branch's final tree on a new branch from
  `7c1b67e`. `main` is pushed only on romi's word.
- **With SharedInput off and no `DBCE_ART_SHIM_REQUEST`:**
  - the probe and shared code are inert;
  - the shifter and GetInput shared paths require a started consumer;
  - force reconnect reopens exactly as main does.
- **The one off-path change, `9329cda`:** `PrepareGameQuery` projects at most once per
  frame. With no shared session it cannot be reached:
  - only `PrepareGameQuery` and the watchdog's `UpdateCore` sample the snapshot;
  - both project at once, and `TickGameButtons` always stamps the frame.
- **Regression for it:** `GameButtonTests` now asserts that with no shared session, a
  query after the watchdog's projection does not read or project again, even after the
  physical state changed, and the edge is kept. A mutant without the guard fails the
  legacy suite.

## Native 44E46799 at runtime (08:29, romi GO 33164)

The muted ordinary-path run on the new native **passed** as sealed.
- **Run:** `native44e-script-20261012-082728`, source `1538f91`.
- **Pins:**
  - runner `26968B8C`, preparer `0E9623A1`;
  - mod `E62F9581`, native `44E46799`, shim `7DDC9621`;
  - profile `5206EDFB`, copy Settings `A39B2F06`, recorder `1CE497CE`.

Force was off throughout. The new native has no `DBCE_FFB_MUTE` latch (that is toolkit
`82c40c4`), so the copy was staged with `ForceFeedbackEnabled=false`, plus the recorder's
output mutes.
- `force-check.json` shows the mod loaded with `ffb=False` and never attempted force
  initialisation.
- The launch's native log (`WheelFfb 901`) only enumerates devices, opens the DS-8X, R12
  and Stalk as separate readers, and closes them at exit. There is no `InitDirectInput`,
  `OpenCandidate` or effect line.

What ran:
- **Shared session:** attached and accepted its first frame (14 owned actions). Its
  qualification pins each owned slot through `GetReadDeviceInfoV1` and
  `GetReadDevicePathV1`. Frames come from `ReadDeviceSnapshotV1`, so all three new exports
  ran live.
- **Verdict:**
  - 1359 stage frames composed with isolation and live steering;
  - the resting wheel (0.1407) held steering on 342 scripted-steer frames;
  - pedals and handbrake passed through;
  - all 721 car ticks matched.
- **Restoration:** exact, with the lease released and no markers left.

Still unqualified:
- the borrowed-FFB-handle snapshot path (fixtures only);
- physical wheel, pedal and keyboard coexistence;
- force feedback with the profile;
- keyboard steering;
- clutch and H-pattern gates.

## Remaining gaps

1. **No Wheelkit writer for Art's profile.** Wheelkit writes `dbce-controls.ini` only for
   DRIVE (`DriveShimControls`, `BepInEx\config`). An Art adapter for
   `Mods/ArtOfSimRally/dbce-controls.ini`, with STD-034 closed-loop qualification, is a
   later small task. Until then the owner copies the Wheelkit-applied file by hand. The
   README and the mod's refusal text now say so; the old wording claimed Wheelkit could
   apply it.
2. **Physical qualification needs the owner.** Wheel, pedals and keyboard through an
   ordinary session were shown only with scripted Rewired values. Force feedback together
   with the shared profile has never run.
3. **Keyboard steering needs a centred wheel.** Without force the wheel rests off centre,
   so steering stays the wheel's (07:01 run).
4. **Clutch and H-pattern are untested.** Profile clutch is unbound on the owner profile.
5. **The probe code ships dormant.** It is gated by an environment variable, a nonce and
   pins. Keep it or strip it from release builds: a review choice.
