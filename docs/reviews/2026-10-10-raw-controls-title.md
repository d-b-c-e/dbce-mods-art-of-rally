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

Production input tests: 338 assertions. Actual CLR probe hooks: 18; Unity Mono
hooks: 24. Build has zero warnings. A live run of the changed mod is still owed;
no title fix is claimed installed or runtime-qualified by these source tests.
