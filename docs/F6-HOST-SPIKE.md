# F6 host spike (UI-6): the in-game hook in art of rally

Measured 2026-11-07 on the rig against the spike table in `dbce-project-mgmt/docs/architecture/2026-11/00-overview.md`
section 3. The host under test is host A: the toolkit's F6 core `dbce_ui.dll` (toolkit `7552afe`, UI-2) drawn by its
DXGI present hook inside the game's own frames. It is turned on by the developer switch `[Dev] UiHost=hook` (UI-4,
[UI-HOST-HOOK.md](UI-HOST-HOOK.md)). Installed build: 0.4.4-rc.43 (source `8f53874`).

Rig: three GS32QCA 2560x1440 screens at 180 Hz as separate monitors (MonitorProfileSwitcher profile "Sim Racing"), plus a
1920x1080 screen above. RTX 5080, driver 610.88. The game runs with VSync on.

## Verdict

**The hook is the Unity host.** No criterion failed in a way that calls for the fallback (UI-5).

- **Passed, measured:** focus, frame cost, size, quit and the off switch, the borderless span, keyboard navigation, and
  the Steam overlay with F6 closed. The panel costs nothing measurable: with F6 closed or open, frame time, GPU busy time
  and display latency are within 0.5% of the build without the UI.
- **Two defects in the core, both fixable in the toolkit:**
  - While F6 is open, Shift+Tab does not reach the Steam overlay. The panel owns the keyboard while it is open, and only
    Alt+Tab, Alt+F4 and the Windows keys pass through.
  - On a single-screen fullscreen window, the panel draws washed out (light grey, low contrast). It draws correctly
    windowed and on the span. The core creates its render target in the swap chain's own format, which suggests Unity
    uses an sRGB or float back buffer in that mode.
- **Not reached by an agent here:** exclusive fullscreen, Surround, wheel navigation, the field-of-view change during a
  stage, and force feedback during a stage. These are owner checks (end of page).
  - Exclusive fullscreen is the main reason to choose the hook. Unity asked for it twice, and DXGI refused both times
    because another window held the foreground (`SetFullscreenState failed (887a0022)`, then Unity fell back to a
    fullscreen window). The hook was not involved in that refusal.

## Results

| # | Criterion | Result | Evidence |
|---|---|---|---|
| 1 | Same pages | **Pass**, with the colour defect above | The in-game Force feedback page on the span matches the toolkit's offline UI-1 render `native/panel/build/desc-art-ffb-7680x1440.png` row for row: same panel position and size, the On/Off switch, the Strength slider with Default, and More options. Display, Cameras and Help were reached with the keyboard (C and C2 page captures). The hook descriptor has only the Force feedback and Cameras pages, which is UI-4's scope, so Controls and Telemetry are absent by design. |
| 2 | Display modes | **Span: pass. Exclusive and Surround: owner check** | Separate monitors (borderless 7680x1440, `Composed: Flip`): the panel draws on the centre screen of the game's swap chain (B, B2), and the F6-closed capture passes Test-GamePicture on all three screens. Single screen fullscreen window (`Hardware: Independent Flip`): the panel draws, but washed out (C, C2). Exclusive was refused twice by DXGI; see the verdict. Surround was not run, because switching the display profile while the PC was in use would have disrupted the owner. The camera toast and the FOV label are not routed through the core in art yet (UI-4 scope), so only the F6 panel was checked. |
| 3 | No focus change | **Pass** | The foreground window was sampled every 200 ms from 11:18:31 to 11:30:46 (12 min 15 s). F6 opened 19 times, with navigation keys in between, plus 12 Shift+Tab presses. The foreground stayed the game's window `0x41D42` the whole time, with zero changes (`foreground.csv`). The FFB log has no line between opening the wheel and shifter at 11:18:29 and releasing them at quit at 11:30:46: no reacquire and no lost-focus line. In C and C2 the panel opened, navigated and closed while another window held the foreground: it never takes focus. Unity's `Application.isFocused` is not logged; the foreground window is the proxy, because Unity's focus follows window activation. |
| 4 | Keyboard and wheel | **Keyboard: pass. Wheel: owner check** | The arrows reached every page (Up, Up to the tabs, then Right through Display, Cameras and Help) and every row. Down, Right changed values in UI-4. While the panel is open, the core turns keyboard, mouse and raw-input messages into `WM_NULL` for the game (`native/panel/dbce_panel_input.cpp`; the toolkit's ui-host test posts an arrow and checks the game never sees it). The mod also counts the panel as settings visible, so its Rewired input block applies. The wheel's D-pad and buttons can't be injected. |
| 5 | Live FOV | **Byte diff: pass. Next frame: owner check** | UI-4: changing the field of view rewrote only `<BonnetFOV>`, and `tests/UiHost` checks the byte diff of every row. The next-frame view change needs a stage in the bonnet view, which an agent can't start (the game ignores injected input). |
| 6 | FFB uninterrupted | **Owner check** | Force feedback stays muted in unattended runs (Safety 2), and the effect runs only while driving. The host-side risk, a focus change making DirectInput reacquire, did not happen (criterion 3). Note for the owner: the mod deliberately stops force while any F6 is open, the UMM panel included. The spike's "no stop" wording conflicts with that existing design, so whether F6 should pause force is a product choice, not a host result. |
| 7 | Steam overlay | **F6 closed: pass. F6 open: fixable fail** | F6 closed: Shift+Tab opened and closed the Steam overlay three times (captures show the overlay over the game). The game kept responding, the core's frame log kept reporting, and F6 opened the panel normally afterwards. F6 open: three Shift+Tab presses never opened the overlay, because the panel keeps the keyboard. There was no crash, hang or lost panel. The core shares Present and ResizeBuffers with Steam's `GameOverlayRenderer64.dll` inline hooks: "dxgi.dll's first 5 bytes jump there ... ours runs a copy of its first instructions". RTSS and ReShade are not installed. Wand 12.50.0 is installed but was not running, so it was not exercised. |
| 8 | Frame cost | **Pass** | Table below. F6 closed versus no UI: average frame time -0.04%, 99th percentile +0.1%, GPU busy -0.3%. F6 open versus no UI: +0.4% p99 frame time, +0.05% GPU busy, and display latency 0.09 ms lower, so less than one refresh (5.56 ms) added. |
| 9 | Size | **Pass** | `dbce_ui.dll` x64 is 701,440 bytes (vendored `lib/dbce-ui`); x86 is 607,232 bytes (toolkit `native/ui-host/build/x86`). Both are under 2 MB. There is no overlay exe for host A. |
| 10 | Failure | **Pass** | With the switch off (run A), the process has no `dbce_ui.dll` module and the UMM log has zero `overlay:`, `dbce_ui` or `UiHost` lines, so no hooks are installed. WM_CLOSE ended the game in 449 ms (no UI), 547 ms (hook, panel closed), and 414 ms and 405 ms (hook, panel open). "A killed overlay" applies only to host B, which has a second process. |

## Frame cost (criterion 8)

PresentMon 2.6.0 ran on the game's process for 60 seconds per phase on the span, and 20 seconds on the single screen,
all on the title scene. The game ignores injected input, so an agent cannot start a stage. With VSync capped at 180 Hz,
the panel's cost shows in GPU busy time and display latency, not in frame rate. UMM's own Mod Manager window was open in
every run, because this rig's UMM opens it at start (`ShowOnStart`), so the runs compare like with like.

| Run | Present mode | Frames | Frame time avg / p99 (ms) | GPU busy avg / p99 (ms) | Until displayed avg (ms) |
|---|---|---|---|---|---|
| A: no UI, span | Composed: Flip | 10,786 | 5.561 / 6.165 | 3.769 / 4.057 | 15.566 |
| B: hook, F6 closed, span | Composed: Flip | 10,791 | 5.559 / 6.172 | 3.757 / 4.054 | 15.547 |
| B: hook, F6 open, span | Composed: Flip | 10,788 | 5.559 / 6.189 | 3.771 / 4.071 | 15.473 |
| C2: hook, F6 closed, single screen | Hardware: Independent Flip | 3,592 | 5.556 / 9.733 | 1.105 / 1.288 | 17.280 |
| C2: hook, F6 open, single screen | Hardware: Independent Flip | 3,592 | 5.555 / 9.745 | 1.111 / 1.290 | 17.234 |

The core's own 10-second frame log agreed: 180 fps average with the panel open and closed, and 1% lows of 114-133 fps
on the span.

## Runs

The raw evidence is in `E:/Source/temp/art-ui6/` (`runs/<run>/`: `run.txt` timeline, captures, `foreground.csv`,
PresentMon CSVs, and `logs/` with UMM `Log.txt`, `dbce_ui.log`, `ffb.log` and `Player.log`). `run.ps1` there
reproduces a run, `pm_summary.py` summarizes the CSVs, and `presentmon/` holds the signed Intel PresentMon CLI.

- **A, no UI, span.** The player default: the dev switch was off. The game took the foreground, and the capture passed
  on all three screens.
- **B and B2, hook, span.** One game process. B measured PresentMon with F6 closed and open, then stopped on a bug in my
  key-sending script, leaving the panel open. B2 attached to the same process for the 7.5-minute F6 loop, the Steam
  overlay presses and the quit. Because the panel was left open, B2's F6 states are inverted from its labels; the
  captures show which state each step was in.
- **C and C2, hook, single screen.** The game asked for exclusive fullscreen at 2560x1440 with the mod's triple view
  off. Both times DXGI refused, because another window held the foreground, and Unity fell back to a fullscreen window.
  The Steam overlay test needs the game in front, so it was skipped there.
- Force feedback was off in every run (Safety 2). Saves, the registry key and the mod settings were backed up first and
  restored afterwards. Settings.xml, TripleScreen.xml, dbce-controls.ini and desired-layout.json came back
  byte-identical. ArtUserState reported only the expected `remotecache.vdf` difference. The dev switch is off again on
  the rig.

## Fixes for the core (toolkit, before the art migration)

- **Shift+Tab while F6 is open.** In `native/panel/dbce_panel_input.cpp` `handle()`, let Shift+Tab through (or close the
  panel on it), as Alt+Tab already is. Then check with the game in front that the Steam overlay opens over an open panel.
- **Washed-out panel on a fullscreen-window swap chain.** Log the back-buffer format in `dbce_overlay_d3d11.cpp`. If it
  is `_SRGB` or a float format, draw through a matching non-sRGB view, or convert ImGui's colours in the shader.

## Owner checks left

Turn the switch on with the game closed (`pwsh tools/testing/Set-UiHostHook.ps1 -On`), and turn it off again afterwards.

1. **Exclusive fullscreen.** In the game's display options, pick fullscreen on one screen, with the mod's triple view
   off. Press F6: the panel should appear over the game. Press F6 again: it should close.
2. **Surround.** Switch to the "Sim Racing Surround" profile and launch: F6 should appear on the centre screen.
3. **Wheel and live FOV.** In a stage in the bonnet view, press the wheel's Settings button. Move with the D-pad to
   Cameras, then Field of view, and press Right: the view should widen at once. The car should not steer or brake
   while the panel is open.
4. **Force feedback.** In a stage, open and close F6. The force pauses while the panel is open (existing design) and
   should come back at once when it closes.
