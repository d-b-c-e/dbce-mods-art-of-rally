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
deferred title transitions (354 input assertions; zero-warning solution build).
The ordering fix still needs its own clean RC gates and live menu qualification.

Run 04 accepted two commands and carried 159 injected reads. Normal close,
explicit semantic Settings.xml verification and exact owner-file/preferences
restoration passed; no process or lease remained. The native no-force latch and
temporary disabled output settings were retained throughout the test. The
archived ffb.log is retained owner history, not evidence of test force output.
