# Art shared input: release checklist (2026-10-12, source only)

Consolidates the shared-input candidate ([package](../research/2026-10-12-shared-input-candidate-package.md)), the
session tools ([replay](../SESSION-REPLAY.md)) and public issue #3, for sana 40566. Nothing was installed or launched
for this note. The owner's tune and the uncommitted `experiments/CustomVehicles` work on main are untouched.

## Where things stand

- **Public:** 0.4.1 (`7c3de40`) is the latest public release. Main is `7c1b67e` and has not been advanced. The
  integration history carries the shim blob, so main will get a squash of the clean tree on a new branch from
  `7c1b67e`.
- **Installed for the owner:** private rc.17 shared-input candidate (receipt `B47ECDAD`, 2026-10-12). Hashes
  re-read for this note:

  | Item | Value |
  |---|---|
  | Mod | `AB4C7856` |
  | Native `UnityForceFeedback.dll` (mod folder and Plugins) | `44E46799`, both copies |
  | `dbce_shim.dll` | `7DDC9621` |
  | Recorder (SessionTools rc.12) | `5B706C9E` |
  | `Settings.xml` | `91090837` |
  | Wheelkit profiles | `31283715` |

  - **Settings:** `SharedInput` absent (off), `TransmissionMode` absent ("Follow game"), wheel input, shifter and
    force feedback on, Strength 50.
  - **No `dbce-controls.ini`:** the shared profile has never been applied to the real install, so Art's own bindings
    drive.
  - **Wheelkit:** the owner runs 0.6.24 (`64fe71e`). Art's last profile application (2026-10-09) wrote triple
    geometry only. `SourceRoot` is null and `SessionToolsFolders` is empty, so Wheelkit Record/Playback for Art is
    not set up on the owner's machine.

## Exact package candidates

| Component | Candidate | Source | Bytes | Evidence |
|---|---|---|---|---|
| Mod, shared input | `ArtOfSimRally-0.4.2-rc.17-shared-input.zip` | `096bf5e` | `3A588814` | Test-Rc gates, inert-folder Wheelkit Apply, the latched copy run; installed |
| Native | `UnityForceFeedback.dll` (toolkit `bbbf6e3`, ABI 901) | | `44E46799` | function audit and mixed-reader fixtures; muted runtime only |
| Shim (private, not in the public repo) | `dbce_shim.dll` (art-shim-bridge `574bb88`) | | `7DDC9621` | every ordinary shared run |
| Session tools | `ArtOfSimRally-SessionTools-0.4.0-rc.14.zip` | `aea8a58` | `9A2E36A8` | automated plumbing pass (run 162920, accepted with the UMM classification); restore-gap fix `0d2f81b` |
| Recorder DLL | in rc.14 `064F0E15`; installed rc.12 `5B706C9E` | same recorder source | | |
| Wheelkit | 0.6.24 (`64fe71e`): contract v2 writer (0.6.23) and the Art automated origin | | | sana's integration review |

- **Mod runtime frozen:** no change under `src`, `native` or `installer` from `096bf5e` to `aea8a58`. The 12 later
  commits are session tools, tests and docs. A release can rebake rc.17's runtime under its final label
  (`package.ps1 -Version 0.4.2 -SharedInputCandidate`), but the RC numbers so far are local labels only.

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
7. **Ordinary recording (B).** An owner-attended Wheelkit Record with human origin needs a SessionTools folder set in
   Wheelkit (rc.14 extracted). It is not configured now.
8. **Review choices and rebake items:**
   - keep or strip the dormant probe code;
   - correct the `CONTROLS.json` clutch omission wording;
   - decide whether and how the private shim may ship publicly (licensing).

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
become the B recording once SessionTools is set in Wheelkit.
