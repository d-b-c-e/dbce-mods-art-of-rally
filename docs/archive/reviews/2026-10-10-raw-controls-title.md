# Raw controls after Wheelkit Apply: title screen

The two bounded, muted October 10 runs used production Wheelkit Apply, independent
raw commands from the original selected profile, and the native d1ddd2e test
fence. Both closed normally and restored owner files/preferences. No display
override, OS input, physical force or motion output was used.

- `results/raw-controls-startup-20261010-01`: five steering samples reached the
  native read and normalized values (-0.4999847 / +0.5). UMM's saved ShowOnStart
  covered the title. Input acceptance beyond that reader is unqualified.
- `results/raw-controls-menu-20261010-02`: UMM startup window temporarily hidden;
  native Start 35 and Confirm 31 reached the mod while focused. Neither advanced
  the visible title. Settings verification and restoration passed. The native
  status recorded two accepted commands and 161 reads carrying injected input.

The first run's byte-strict Settings.xml check failed. Inspection found all 92
fields preserved; XmlSerializer reorders newly added fields. Wheelkit b080cd3
retains hash-pinned applied bytes and separately verifies complete field values,
attributes and nested array order while accepting top-level order/formatting.
The second run passed that explicit semantic check; other files remained exact.
Restoration always restores original bytes, regardless of verification outcome.

## Cause and source fix

Local ILSpy inspection of Assembly-CSharp SHA-256
`7807A1D674C8214FE1FBD15B8EDC6E3E5E47700AB6E6C2943CF5BF234551C5E0`
(Steam 17584229) shows SplashScreenControl subscribes to Rewired's
ButtonJustReleased delegate. Postfixes on GetButton/GetAxis cannot supply that
event. Private inspection output: `results/title-input-20261010/`.
REA registration was aligned but its tools were unavailable in this active
session; existing ILSpy was used without changing registration or game files.

GameButtonInput now calls the game's own EndSplashScreen only on a fresh,
observed Start/Confirm release while the splash is active and topmost. The exact
game hash, enabled reader, focus, settings-panel, assignment and current-frame
guards still apply. Failed reads/focus loss cannot manufacture a release.
Pedals and unrelated buttons cannot dismiss the title. No global Rewired event
is synthesized. Subsequent menu actions still use the existing game action path.

The reviewed title candidate a9cc6fb passed all 20 RC gates and the two original
force-corpus cases as 0.4.2-rc.8, then was installed with owner settings retained.
The exact package/gate is in the private detached build tree
`../.worktrees/art-title-rc7-20261010/results/rc-0.4.2-rc.8-8559cf44dd7340f188612d420bae91e6/`;
the install receipt is `results/profile-controls-rc8-install/`.

## Live title pass, menu-order defect

`results/raw-controls-title-20261010-03` stopped before sending any input: the
owner-input watcher had expired on October 8, and the fallback classified this
game's launch input as owner return. Normal close, verification and restoration
passed. The existing watcher was restarted for the remaining rig grant, and the
next launch waited for five quiet minutes. No idle policy was bypassed.

Run `results/raw-controls-title-20261010-04` (04:31-04:36 CT) applied the selected
profile through production Wheelkit, then sent independent raw Start button 35.
Its release reached the main menu. The mod logged the Start release; WGC frames
and the trace's selected object both show title -> Career. This qualifies that
title route on the current build. It does not qualify driving or all buttons.

The next raw POV-0 South sample reached `NavDown=1` while focused, but every
actual player-0 action-15 GetAxis/GetNegativeButtonDown read remained zero and
Career stayed selected. The log warned that the game's query preceded this
frame's snapshot. The dynamically loaded watchdog's DefaultExecutionOrder(-1000)
did not establish the required runtime ordering. This is a real menu defect,
not a failed binding write.

The source fix samples at the first eligible Rewired consumer or the watchdog,
whichever comes first, once per rendered frame. Later consumers retain the same
non-consuming edge. The frame is stamped before polling to avoid recursion.
Title transitions remain in the watchdog, outside input queries. Tests cover
both call orders, one native read, held/released frames, unrelated players and
deferred title transitions. Claude's review moved discovery, logging, range
learning and assignment entirely out of the query path (9fd1776; 364 input
assertions). The query reads existing handles only. All 20 clean RC gates and
the two original force-corpus cases passed for rc.11. Its exact package was
installed with 11 protected files unchanged; receipt:
`results/profile-controls-rc11-install/install-receipt.json`.

Run 04 accepted two commands and carried 159 injected reads. Normal close,
explicit semantic Settings.xml verification and exact owner-file/preferences
restoration passed; no process or lease remained. The native no-force latch and
temporary disabled output settings were retained throughout the test. The
archived ffb.log is retained owner history, not evidence of test force output.

## Run 05: menu path passed; intro query missing

`results/raw-controls-menu-20261010-05` applied the original profile through
Wheelkit's production writer. Raw Start 35 dismissed the title; four separate
POV-0 South pulses selected Time Attack, Custom Rally, Online Events (not
entered), then Free Roam. Confirm 31 reached location, car selection and the
Finland free-roam scene. Fresh game-window frames were inspected before each
press. This qualifies those menu inputs on rc.11 and closes the observed query
ordering defect. It does not qualify driving.

The stage remained in its cinematic. A fresh Confirm and Start did not advance
it. Inspection of the same pinned game assembly found `StageIntroCinematic.Update`
uses `Input.anyKeyDown || PadManager.GetPlayer().GetAnyButtonDown()`, not a named
Submit action. `GameIntroduction` uses the same query for its text. The mod had
no adapter for this query. Private decompilation: `results/freeroam-intro-20261010/`.

The follow-up augments parameterless `Player.GetAnyButtonDown` for the primary
player with fresh profile Confirm/Start only. It preserves native true results,
does not generate OS input, and leaves scene transitions to the game. The
existing build/focus/panel/assignment/read/release guards apply. Camera, Back,
navigation and ordinary pedal bindings cannot skip the intro. Production-postfix
fixtures cover both controls, repeated queries, held/released states, other
players and all guards (386 input assertions; clean solution build). The actual
Mono patch-attachment check includes this hook. Live qualification remains due.

Run 05 ended normally after ten commands, with 167,310 trace rows. Semantic
Settings.xml verification and exact owner-file/Unity-preference restoration
passed at 10:09:10Z. The process exited and the lease was released. The no-force
native latch stayed armed; no physical force or motion output was enabled.

## Runs 06/07: stage controls and centre-window picture verified

All 20 clean RC gates, including actual Mono patch attachment and both original
force cases, passed for `0.4.2-rc.12` / `f6165bc`. The exact ZIP SHA-256 is
`F22626664CC4E41D9E6A188F1226CC0385EA75D0D2472C8731C5B47024049511`.
Installation retained 11 protected files; receipt is
`results/profile-controls-rc12-install/install-receipt.json`. Claude reviewed
the fresh-edge GetAnyButtonDown addition without a blocking finding.

`results/raw-controls-drive-20261010-06` used the frozen Wheelkit production
writer and original profile. Confirm now ends the intro, selects Begin stage,
then starts the real player car. Observations at AxisCarController.GetInput:
steering -0.499985/+0.5, throttle 0/0.500008/1, brake 0/0.5, and handbrake
0/0.500008. These are actual game inputs, not expected values substituted by
the observer. The initial game's handbrake=1 occurs only for the first 1.22 s
after Begin stage; it is not the later half-handbrake sample. Camera button 32
reaches action 61 and changes CarCameras index 0 -> 1. Start selects the actual
PauseScreen/Resume object. The 200,000-row limit safely stops the observer at
212 s; normal close and exact restoration complete at 10:24:56Z.

The first screenshots appeared to show a stuck cinematic without a HUD. This
was a capture-selection error: WGC's largest-window policy selected a Unity
Secondary Display at (-2560,0), tied in size with the centre. No rendering change
was made. Toolkit `2e99ca0` adds `--list` and explicitly owned `--hwnd` capture,
checking window ownership before and after capture. Run 07 lists all three
2560x1440 windows at -2560, 0 and 2560 and selects the centre at (0,0).

`results/raw-controls-windows-20261010-07` repeats the production Apply chain
on the same installed rc12. Centre frames show the intro, Begin stage menu,
normal car/HUD, raw throttle accelerating to 19 mph, changed camera view,
Start opening Pause, POV South selecting Recover car, POV North returning to
Resume, and Back resuming. Both side windows were captured separately. This
closes the apparent missing-centre-picture defect; it is not a complete triples
geometry or all-camera acceptance matrix. The run stops explicitly after 16
commands / 178,100 rows. Semantic config verification, normal exit and exact
owner-file/Unity-preference restoration pass at 10:39:52Z; no process/lease remains.

Both tests used the irreversible native no-force fence, disabled physical/network
outputs and blocked progression writes. No physical FFB acceptance is inferred.
Left/right navigation, clutch and auxiliary shifter input remain unqualified by
these runs. The original profile is applied only for each bounded test and then
restored; this does not release Wheelkit's candidate controls writer to users.
