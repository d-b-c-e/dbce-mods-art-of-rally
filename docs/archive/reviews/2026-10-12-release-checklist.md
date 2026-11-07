# Art shared input: release checklist (2026-10-12, source only)

Consolidates the shared-input candidate ([package](../research/2026-10-12-shared-input-candidate-package.md)), the
session tools ([replay](../SESSION-REPLAY.md)) and public issue #3, for sana 40566. Nothing was installed or launched
for this note. The owner's tune and the uncommitted `experiments/CustomVehicles` work on main are untouched.

## Where things stand

- **Public:** 0.4.1 (`7c3de40`) is the latest public release.
- **Main:** carries this candidate as one clean-tree squash on `7c1b67e` (2026-10-12). It contains:
  - the integration;
  - rc.18's runtime (`26f5cca`, plus the tests-only `24439a3`);
  - the copy-run recorder and tools (`02fcf1b`).

  No shim binary is in main or its history. The shim blob is only in `mufa/art-shim-actions` history, which the
  integration had already squashed (`7544d9f`). The read-snapshot native `lib/input-shim/native/WheelFfb.dll`
  (`44E46799`, MIT toolkit `bbbf6e3`, pinned in `lib/input-shim/CANDIDATE.json`) is on main.
- **Installed for the owner:** private rc.18 shared-input candidate over rc.17 (receipt `BF171FA9`, 2026-10-12
  22:46 CT). It changed exactly the mod, `build.json` and the two code-identical TripleScreen DLLs. Hashes re-read
  for this note:

  | Item | Value |
  |---|---|
  | Mod | `E4F0BBC8` (rc.17: `AB4C7856`) |
  | Native `UnityForceFeedback.dll` (mod folder and Plugins) | `44E46799`, both copies |
  | `dbce_shim.dll` | `7DDC9621` |
  | Recorder (SessionTools rc.15, since 23:07; was rc.12 `5B706C9E`) | `DAA62504` |
  | `Settings.xml` | `91090837` |
  | Wheelkit profiles | `31283715` |

  - **Settings:** `SharedInput` absent (off), `TransmissionMode` absent ("Follow game"), wheel input, shifter and
    force feedback on, Strength 50.
  - **No `dbce-controls.ini`:** the shared profile has never been applied to the real install, so Art's own bindings
    drive.
  - **Wheelkit:** the owner runs 0.6.24 (`64fe71e`). Art's last profile application (2026-10-09) wrote triple
    geometry only.
  - **Recording is available.**
    - With `SourceRoot` null, 0.6.24 falls back to `E:\Source\games`, so Art's session tools resolve to this main
      checkout, and an ordinary human-origin Record is offered. The readiness receipt is in the hub, under
      `artifacts/art-recording-readiness-20261012`. An empty `SessionToolsFolders` is not a blocker.
    - Main's session scripts are source-identical to the packaged SessionTools rc.14 and rc.15 scripts. The installed
      rc.15 recorder is built from main's recorder source.
  - **Optional, stable selection:** the verified rc.15 bundle at `E:\Tools\ArtOfSimRally-SessionTools-0.4.0-rc.15`
    pins the scripts to a package rather than the checkout. The owner chooses it in Wheelkit; the live settings are
    untouched.
  - **Caution:** this checkout's ignored `tools/testing/Recorder/bin` holds a stale 2026-10-10 recorder build
    (`0EFD97A3`, `8b363f4`). `Install-Recorder.ps1 -SkipBuild` from here would install it.

## Exact package candidates

| Component | Candidate | Source | Bytes | Evidence |
|---|---|---|---|---|
| Mod, shared input | `ArtOfSimRally-0.4.2-rc.18-shared-input.zip` | `26f5cca` | `C9506342` | Test-Rc rc.919 on `24439a3` (tests-only successor, runtime and packaging inputs identical; rc.918 on `26f5cca` failed to build the WheelInput stubs), installer test (50), muted copy run-03 (input and first cold replay), rehearsal; installed |
| Mod, shared input (previous) | `ArtOfSimRally-0.4.2-rc.17-shared-input.zip` | `096bf5e` | `3A588814` | Test-Rc gates, inert-folder Wheelkit Apply, the latched copy run; installed until rc.18 |
| Native | `UnityForceFeedback.dll` (toolkit `bbbf6e3`, ABI 901) | | `44E46799` | function audit and mixed-reader fixtures; muted runtime only |
| Shim (private, not in the public repo) | `dbce_shim.dll` (art-shim-bridge `574bb88`) | | `7DDC9621` | every ordinary shared run |
| Session tools | `ArtOfSimRally-SessionTools-0.4.0-rc.15.zip` | `5628e9b` | `69F4B691` | scripts identical to rc.14; recorder source equals `02fcf1b` (gated by Test-Rc rc.921); rehearsal; extracted to `E:\Tools`, seal verified |
| Session tools (previous) | `ArtOfSimRally-SessionTools-0.4.0-rc.14.zip` | `aea8a58` | `9A2E36A8` | automated plumbing pass (run 162920, accepted with the UMM classification); restore-gap fix `0d2f81b` |
| Recorder DLL | installed rc.15 `DAA62504` (decompiled code equals the copy-qualified `0390B89D` except its informational version); rc.14 `064F0E15` and rc.12 `5B706C9E` share the earlier recorder source | | | |
| Recorder DLL, copy runs | dev recorder `0390B89D` | `02fcf1b` | | the rc.18 copy runs (diag-03, run-02, run-03); not installed for the owner |
| Wheelkit | 0.6.24 (`64fe71e`): contract v2 writer (0.6.23) and the Art automated origin | | | sana's integration review |

- **Mod runtime:** no change under `src`, `native` or `installer` from `096bf5e` to `aea8a58`. The 12 later
  commits are session tools, tests and docs. rc.18's `26f5cca` is the one runtime change since, the pinned DualShock
  4 identity (below): four files under `src/ArtOfSimRally.Mod/Shim`. A release can rebake the runtime under its final
  label (`package.ps1 -Version 0.4.2 -SharedInputCandidate`), but the RC numbers so far are local labels only.

## Evidence still missing (release blockers)

1. **Apply Controls on the real install.** Only an inert-folder Apply exists (INI `C685B218`, Settings `AB4C69E8`).
   It changes exactly two settings:
   - `SharedInput=true`;
   - `TransmissionMode=Sequential`. **This replaces the owner's "Follow game" default.** It is a driving behaviour
     change the owner should accept first; otherwise the profile must not carry it.
2. **Ordinary owner session with the shared profile.** No physical wheel, pedal, handbrake, sequential-shift or menu
   input has gone through the shared profile; only scripted Rewired values have. The menu actions are Confirm, Back,
   Start, Nav and camera.
3. **Force feedback with the shared profile.** Never run: every shared run was muted or had force feedback off. It
   needs the owner holding the wheel; no unattended force.
4. **Keyboard steering over a resting wheel (rc.17).** Fixture-only. It needs attended key presses.
5. **Clutch and H-pattern.** Latched stimuli only, with no physical pedal or shifter and no unlatched session. The
   owner's shifter is the DS-8X in sequential mode; the clutch is on R12 axis 6.
6. **Issue #3, RS V2 buttons.** There is no raw button-edge evidence. The owner's rig is an R12 with the KS Pro rim,
   not RS V2. It can show the R12-base button path for the owner's rim, not the reporter's hardware.
   - Per button, record two observations with force off: the same reader (Art's F6 Bind status, through the
     FFB/read handle) and an independent reader (Wheelkit's Controls page).
   - No fix claim for the reporter.
7. **Ordinary recording (B).** An owner-attended Wheelkit Record with human origin. It needs only the owner at the
   wheel:
   - Wheelkit 0.6.24 offers it through the main-checkout fallback, and the rc.15 recorder is installed.
   - Selecting the `E:\Tools` rc.15 bundle in Wheelkit is optional.
8. **Review choices and rebake items:**
   - **The dormant probe code: opt-in, private candidates and tests only (sana 47008; source, not yet packaged).**
     Public 0.4.0 and 0.4.1 never contained it.
     - The probe is `ShimStartup`, `ShimDrive`, `DriveTrial`, `LatchedTrial` and `ProbeNoForce`. It runs only when the
       game is launched with `DBCE_ART_SHIM_REQUEST`, and only with a validated request: deadline, exact game, mod,
       native and settings paths, and native hash pins. It is still the only machinery for the latched clutch, gate
       and menu evidence in blocker 5.
     - The MSBuild property `ArtShimProbe` (default `false`) now selects the flavour:
       - A normal build compiles the inert `ShimStartup.Inert.cs` facade and none of the probe's types. A probe
         request set for it is logged once and ignored.
       - `package.ps1` passes `true` for `-SharedInputCandidate` and `false` otherwise, then checks the built
         assembly's `ArtShimProbe` stamp and probe types (`tools/package/ShimProbeFlavour.ps1`).
       - `Prepare-ShimProbe.ps1` refuses a build that lacks the probe.
     - The production shared-input path (session, identity lease and mapper, observation, isolation) is in both
       flavours.
     - Installed rc.18 was built before the switch and carries the probe, as every candidate does.
   - **The recorder's Rewired inventory dump: opt-in (source, not yet packaged).**
     - It runs only with `rewiredInventory=1`, recording only, reset for every request; `identityProbe` follows the
       same rules. The copy diagnostics ask for it explicitly with `Invoke-SharedInputCopy.ps1 -RewiredInventory`.
     - **Until a later SessionTools package**, the installed rc.15 recorder (`DAA62504`) still writes
       `rewired-inventory.tsv` (device names, GUIDs, paths) in every recording.
     - `ControlsProbe` stays request-driven.
   - **The `CONTROLS.json` clutch omission wording: resolved.** Schema 2 (contract v2, from rc.15) has an empty
     `writerOmits`, so the wrong schema 1 reason ("the reviewed profile does not bind it") no longer exists.
     - rc.17 and rc.18 ship schema 2.
     - Clutch is labelled runtime-shown (latched, `DD301314`) and not qualified from a physical pedal or in any
       unlatched session.
     - The contract also records rc.18's run-03, the packaged native driven by a virtual DualShock 4 with force off.
   - **The private shim:** decide whether and how it may ship publicly (licensing).

## Smallest next test: Apply Controls to one ordinary launch

There are three steps. Step A needs no owner; steps B and C need the owner at the wheel.

**A. Apply Controls with the game closed, under the lease, with exact backups.** Unattended.
- **Back up first:** `Settings.xml` (`91090837`), the absent `dbce-controls.ini`, and `mapping-profiles.json`.
- **Run:** Wheelkit 0.6.24 Apply Controls for Art with the active "Moza KS Pro Triple" profile, through the app's
  production path.
- **Expected:** the INI equals `C685B218` and Settings equals `AB4C69E8`, or the result without `TransmissionMode`
  if the owner declines that change. Any other difference stops the run and restores the backups. No launch.

**B. One ordinary launch, owner holding the wheel, force feedback on with the unchanged tune.** About 5 minutes.
- **F6 Controls:** shows the shared profile attached with its 14 actions.
- **Driving:** steer both ways; throttle, brake and handbrake; shift up and down.
- **Menus:** Confirm or Start at the title, Back, the four directions, camera.
- **Keyboard:** brief keyboard steering with the wheel resting.
- **Force feedback:** feels as before, and F8 stops it.
- **Evidence:** UMM log attach and action lines; the native `ffb.log` showing the read slot and the force open on
  the same handle; the owner's per-item answers.

**C. Issue #3 observations, same sitting, force feedback not needed.** For each rim button:
- Art F6 Bind on a button channel: the status line, then Cancel without saving;
- Wheelkit's Controls page: the live button index.

Record the pairs. **Afterwards**, the owner keeps the shared profile or the backups are restored exactly. Step B can
itself be the B recording through Wheelkit's Record, human origin, as set up now.

## rc.18: pinned DualShock 4 identity (2026-10-12)

**Why.** Rewired reads a DualShock 4 through its own HID driver, which has no RawInput extension. So the exact
path/product join refused shared input whenever one was connected, even beside an unrelated shared wheel.

**What changed.**
- **Runtime source:** `26f5cca`, under `src/ArtOfSimRally.Mod/Shim`.
  - `PinnedDs4Identity` reads the path that driver opened, through the private chain observed at runtime. It reads
    fields only, and only for the reviewed `Rewired_Core` `2DD147A1…` / `Rewired_Windows` `4389BCA4…` pair. That
    pair is hashed once per loaded pair.
  - The product comes from the public HID vendor/product getters.
  - `JoystickView` keeps RawInput first and hands the identity lease the actual `DualShock4Extension`.
- **Matching unchanged:** exact path, then product. There is no VID/PID-only match and no unknown-row skip.
- **Refusals:** any other hash, member, type or an empty path refuses by name, and the mapper and lease messages carry
  that reason.
- **No other controller family.**

**Gates and install.**
- **Test-Rc:** `0.4.2-rc.919+24439a3….clean`, 20 of 20 checks. `24439a3` adds only the WheelInput stub types that
  the first gate, `rc.918` on `26f5cca`, failed to build (CS0234); that failed gate is kept.
- **Installer test:** 50 assertions on the rc.18 ZIP.
- **Install:** a rehearsal on a scratch copy of the owner tree, then the owner install. The wrapper and evidence are
  in `results/shared-input-rc18-install`. The rehearsal tree-after equals the owner's.
  - The prior state matched rc.17's recorded runtime. The only differences were UMM launch artefacts, listed in the
    receipt: `Params.xml` and `.cache` shadow copies equal to the installed DLLs.
  - No launch, Apply, SharedInput change, force or device operation.

**Evidence.** The hub `dbce-project-mgmt` folder `artifacts/art-shim-pad-input-20261012`. The copy runs used the dev
recorder `02fcf1b` / `0390B89D`.
- **diag-03 (`bfca187`):** the pinned string equals the owned DS4 interface path exactly.
- **run-02 (`b4b0f09`):** recorded FAIL. The checker had never resolved the car tape's marker index. The corrected
  reanalysis (`8d46d45`, checker `6EFB23E0`) is labelled as such.
- **run-03 (`cfbf150`): PASS.** A virtual DualShock 4 was bound by Wheelkit's production Apply, in a disposable copy,
  muted, force feedback off.
  - The pad was isolated and the shared start was accepted.
  - All 8 holds were delivered in order and at amplitude on the shared and car timelines.
  - The first cold replay passed with no device (`gearMismatchSteps=0`).
  - The copy was restored byte-exact.

**Not claimed.**
- Only a virtual DS4 (ViGEm) was exercised. A physical DS4, USB or Bluetooth, may use the other HID
  device-information implementer (`vUeJq…`) and is then refused by name until probed.
- rc.18 claims no physical wheel, force or owner acceptance. Blockers 1–8 above still need the owner at the wheel.
- A Rewired update disables the adapter; its hash refusal is named in the log.
