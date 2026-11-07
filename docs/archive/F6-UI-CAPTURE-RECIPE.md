# Art F6 visual and shared-binding workflow capture: recipe (proposal)

2026-10-15, toto, for sana 124216. This is a proposal only: nothing below has been run. It reuses the existing qualified
private-copy path:
- `tools/testing/Prepare-SharedInputCopy.ps1`, which stages a disposable copy with the shared profile and the dev
  Recorder;
- `tools/testing/Invoke-SharedInputCopy.ps1`, which launches the copy with outputs muted from the native latch
  (`DBCE_FFB_MUTE=1`), closes the exact child and restores the shared state.

No owner-install overwrite, no second reader, no device discovery, no OS input and no display-topology change.

## What it shows

1. **Rendered shared layout.** In both Simple and Advanced, at the current resolution:
   - pages Controls, FFB, Cameras, Telemetry and Help;
   - with both text-size modes (`Auto` and `Use UMM scale`), which are the copy's Settings.xml values;
   - the title, tabs, footer and Stop FFB outside the scroll.
2. **Narrow width.** UMM's in-memory window width (`UnityModManager.UI.mWindowSize.x`) is set to 640 for two shots
   (Controls, Cameras), then put back. UMM's Params.xml and scale preference are never written.
3. **Shared-binding messaging.** It drives the host from `SharedBindingCaptureHost.Current` and takes one game-only shot
   per step:

   | Step | Host call | Shot shows |
   |---|---|---|
   | listening | `BeginCapture("steer")` | the row's Cancel (N s), the capture message and "Bind from" |
   | cancel | `CancelCapture()` | "Binding canceled. The prior binding was kept." |
   | draft | `Clear("throttle")` | "unsaved:" on the row, Save bindings / Discard, and the qualified autosave line |
   | discard | `Discard()` | the draft gone, active and next-start unchanged |
   | save | `Clear("throttle")`, then `Save(out _)` | "Saved. Restart the game to use these bindings." (the copy's profile file only) |
   | close intent | `Main.CloseSettings()` with a draft, twice | first the warning card and the panel stays open; second the panel closes, draft not saved |

   Capture only starts when the host reports Editable. In the copy that needs the main menu and a qualified open
   reader. If the host is ReadOnly or Missing, the shots record that state and its AccessReason instead (for example
   "No shared profile").
4. **Game input held while F6 listens.** At the listening step, the event log records `Main.SettingsVisible`, the
   stock-UI barrier state (`StockUiInput` / `MenuInputBarrier`) and the shifter/camera suppression flags. This is state
   evidence, not a physical-input test.

## What it does not show (attended later)

- **A real capture from a moved control** (`ObserveAccepted` produces the draft): someone moves an axis or presses a
  button while a row listens.
- **Rebinding after a disconnect:** "Bind from" with the saved device disconnected and another device open.
- **Restart and use:** the saved profile becomes active after a restart, and the car follows it.
- **Feel and force with the shared profile:** force stays muted here, as in every copy run.

## Source (written for sana 124479; not built or run yet)

- **Recorder `mode=ui`** (new `tools/testing/Recorder/UiCapture.cs`, plus a branch in `SessionTape.TryArm`/`Tick`). It is
  a frame-stepped script over the steps above, through reflection on the Art mod (`AccessTools.TypeByName`, as
  `SharedReadiness` does).
  - It writes `ui-events.log` and calls the existing game-only `ScreenCapture.CaptureScreenshot` into `<tape>/shots`.
  - It starts at the title, waits 3 frames after each change, mutes outputs, and honors the request's `expiresUtc`.
  - It sends no OS input and never calls a device API.
- **`Invoke-SharedInputCopy.ps1 -UiCapture`** writes `mode=ui`, `tape=<result>/ui`, `muteOutputs=1` and `expiresUtc`,
  waits for the recorder's `ui capture complete` line (bounded at about 120 s), then closes and restores as today.

### Required before the source freeze (kiki 124440)

1. **Assert real outcomes and log them.**
   - Each binding step asserts the host state it needs: Editable, the draft present or absent, `SavedPendingRestart`.
   - The Save result and its reason are logged.
   - After a successful Save, a fresh, distinct draft is made before the close-intent step, on an action that is still
     bound. Clearing an already-unbound action makes no draft.
2. **Show more than scroll tops.**
   - Shots include the lower Controls and Cameras content and a focused binding row.
   - The 640 pass uses the larger effective text scale, and its shots show the header and footer together.
3. **Restore everything** the script changed: width, text mode, page, view and scroll. This happens on completion,
   exception and expiry alike.
4. **Completion waits for the final screenshot file** to exist and stop growing, not only for three frames or the log
   marker.
5. **Pin and map.**
   - Pin the Mod, Recorder, native and profile hashes in the result.
   - Map each shot to the host state read in the same frame: access, capturing action, draft and pending restart.
   - The host-call scenes stay messaging and state evidence. They are not moved-control capture, patched input or
     post-restart acceptance.

## Run (after a compiled candidate and a fresh slot)

1. `Prepare-SharedInputCopy.ps1 ... -Recorder`, with the candidate Mod DLL from the composite batch (hash-pinned).
2. `Invoke-SharedInputCopy.ps1 -Result <dir> -UiCapture`. That is one launch of about 3 minutes, under the shared
   lease, and the game takes its window.
3. Kiki reviews `<dir>/ui/shots/*.png` against the shared F6 review checklist:
   - nothing is clipped;
   - columns line up;
   - tabs wrap when narrow;
   - the footer stays visible;
   - the status text is truthful.
4. Restore receipts and the UMM log go with the shots.
   - The copy is disposable and single-use: a staging cannot be launched twice.
   - The runner snapshots the copy's `dbce-controls.ini` and `Settings.xml` like shared files. Restoration keeps their
     post-run bytes under `after/` (`shared-files.json` maps them) and then puts back the staged bytes.
   - It also hashes the copy's mod folder before and after.
   - Owner files are pinned and never written, as in every run.

## Exact coverage (kiki 125216)

| Capture | What it includes |
|---|---|
| Both views | Every page at the top, plus the lower content of Controls and Cameras, in the starting text mode |
| Other text mode | Simple Controls and Simple FFB only |
| 640 width (larger effective scale) | Simple Controls (top and lower), Simple Cameras (top) and Advanced Cameras (lower) |
| Binding steps | Simple Controls |

## Run 1 (run-cd67994, 2026-10-15) and the follow-up

Run 1 drew all 32 shots, with restore ok and the owner's files unchanged.
- **Footer below the viewport.** The footer sat below UMM's outer viewport in every shot (kiki 126079). The body
  height is now measured in `SettingsPresentation` (`BodyHeight`). It takes UMM's viewport height from
  `GUIClip.visibleRect`, the panel's top, and its fixed rows (the panel height minus the drawn body), all from the last
  repaint. The body is then sized so that the footer stays in view. Until a repaint has measured these values, the old
  `PageHeight` estimate applies.
- **Binding steps unmet.** All six binding steps were unmet because the copy was never focused:
  - Art's force connect (`FfbReconnect`) waits for a focused game window, so the shared session stayed "waiting for
    force feedback to finish connecting".
  - The binding editor also needs `Application.isFocused`.

The follow-up is `-UiCapture -UiSet followup -Activate`. It runs only under a fresh coordinator handoff with owner
notice (hub AGENTS.md, owner 2026-10-14).
- **Activation.** The runner makes one bare `SetForegroundWindow` call on the exact child's visible `UnityWndClass`
  window, as soon as that window exists.
  - No keys, Alt taps or SendInput, and no retry.
  - `activation.json` records the call's return value and whether the child was in front one second later.
  - If Windows refuses, the refusal is recorded and not worked around.
- **Shots.** Normal FFB (`simple-p1-top`), then 640 Advanced Cameras at the larger scale (`narrow-advanced-p2-top` and
  `-lower`), then the binding steps.
  - Each state line now also carries `focused`, `body`, `estimate`, `viewport`, `panelTop` and `fixedRows`.
  - The binding steps count only after `focused True`, a force-connect line, and the `Shared input: attached` and
    `first frame accepted` lines in the UMM log.

## Outcomes

- **No change before the title:** while the game reports driving, the script waits; if it never reaches the title, it
  fails at expiry with nothing changed.
- **Refused before any change:** a non-clean binding editor (a capture, a draft, or saved bindings waiting for a
  restart) changes nothing.
- **`end.txt`:** `complete` only when every step ran and every shot file was written. Otherwise it is `incomplete: N
  shot(s) not written` or `failed: <reason>`, and `incomplete.txt` is written as well. An early stop counts as failed:
  the recorder unloading or the game closing restores first and leaves this evidence.
- **Runner labels:**
  - `ui-captured`: complete with no unmet step;
  - `ui-captured-unmet`: complete, but with an unmet step expectation, as in run 1's binding steps;
  - `ui-incomplete`;
  - `ui-failed`.

## FFB-off binding-host variant (sana 131300)

Both earlier runs stopped at the force gate, not at the editor's own gate:
- Art's force connect needs the OS foreground window to be the game's (`FfbNative.FocusedGameWindow`).
- Windows refused the runner's one activation (`activation.json`: returned False, another process still in front).
  Separately, the live foreground-lock timeout read 2147483647 (the registry holds 200000). That reading does not by
  itself prove why the activation was refused.
- So the shared session waited on "force feedback to finish connecting", and the editor stayed ReadOnly.

The variant stages the copy with `Prepare-SharedInputCopy.ps1 -Recorder -ForceFeedbackOff`.
- The copy's `Settings.xml` turns force feedback off. The owner's file is only read, and no tuning value changes.
- The latched native (DD301314) and `DBCE_FFB_MUTE=1` stay as they are.
- With force feedback off, the mod never requests a force connect (`Main.cs` checks `ForceFeedbackEnabled` before each
  request). The shared session then depends only on the readers and on isolation.
- The mod's own gates are unchanged. The binding editor still needs `Application.isFocused`, the menus, the panel open
  and a started consumer.

Evidence:
- Each state line logs Unity's `focused` and the real OS `osForeground`. `osForeground` is True only while the
  foreground window belongs to the game process. Nothing is activated.
- The runner checks force-off from the launch's own UMM log even with the latched native: `Loaded - … ffb=False` and
  no force-device line, in `force-check.json`.
- `observation.json` carries `forceFeedbackOff: true` and the label "FFB-off binding-host evidence (no attended
  foreground or force claim)".
- The run covers bindings with force feedback disabled and renders all changed UI. It makes no claim about an attended
  foreground workflow or about force.

Two runs used this variant on 2026-10-15: `run-d4b7959-ffboff` (the full set) and `run-ae46b06-ffboff-save`
(`-UiSet save`: the validated Save, the next-start row with its footer, and one plain close). For their results and
what remains open, see `SHARED-BINDING-EDITOR.md` "Runtime evidence, 2026-10-15".
- A Save in the copy leaves a `.bindings-backup-*` file.
- The runner archives and removes only the backups created during the run (`CopyModResidue.ps1`), and it checks that
  earlier backups are unchanged.
