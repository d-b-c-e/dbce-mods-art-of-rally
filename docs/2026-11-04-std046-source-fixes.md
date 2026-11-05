# Art camera STD-046 bounded source increment

Assignment: kole526255, off main ca1b3fed55c732eda8d59e03476c927b12b7165b.
This public repository remains LOCAL ONLY. No install, game launch, device use,
push or standards promotion is authorized by this source increment.

## Changes awaiting CPU qualification

- Fixed F7 joins the existing reserved camera-key predicate in capture, held
  dispatch and reset edges. Imported bindings remain saved; their independent
  wheel channels remain usable. The fixed key is always reserved, so a held F7
  cannot resume as a tuning action after changing framing.
- Mounted tuning no longer clips saved FOV to20..120. Chase no longer clips
  offsets to-40..40 or Realistic FOV to20..120. Nonfinite adjustment results are
  refused. Camera pose/FOV rows retain the ordinary initial slider span, expand
  around the saved value, and allow +/- beyond it without changing values on draw.
  Final mounted/chase lens writes alone are fenced to1..179degrees, away from
  degenerate perspective endpoints, with a finite fallback. Stored tweaks remain
  unchanged, including offsets beyond that applied-lens range. Actual engine
  handling still needs runtime evidence. This application-only fence incorporates
  coordinator526552's clarification after the initial source freeze3f7da9cd.
- Chase consumes the same sampled wheel-reset edge once, scoped to the active
  chase view/set. Capture, panel, modifier and focus suppression now use the same
  held-until-release gate as mounted tuning.
- Cockpit Custom compares either the Realistic seat baseline or the actual
  Custom Vehicles CockpitViewStore.Default FOV, read by reflection on demand.
  Unknown/nonfinite baselines do not claim Custom. The real overlay integration
  must still be observed with the actual Custom Vehicles component.

Qualification prepared, not run: CameraTuning links actual ChaseFov plus the
existing actual CameraKeys/CameraTuner/settings XML paths; only input, clock,
game camera/angle and Harmony field access are doubles. Four targeted modes
exercise F7 capture, imported F7 dispatch, FOV bounds and chase wheel reset.
CameraSourceRoot permits compiling these same tests against the unchanged three
production source files from ca1b3fe for actual old-RED evidence. TripleScreen
tests exercise the real Custom predicate. A full mod compile is required to
verify actual Unity/Harmony wiring. No passing result is claimed yet.

## Real per-car/set persistence test plan

Replace the current mounted Reset double by linking production
CameraMountMemory. Keep geometry measurement and custom-package identity behind
small explicit host seams, retaining the production Track/Load/Remember/Reset
and SettingsPersistence code. Do not reproduce the store in test doubles.
Use two car identities, both Game/Realistic sets and bonnet/bumper views:

1. Track car A, edit all eight pose offsets and both FOV offsets, switch sets,
   then car B. Assert pose isolation by car/camera/set and FOV sharing across
   cars only for the same camera/set.
2. Flush actual XML, construct fresh settings/store state, then return to each
   car/set. Assert calculated baselines plus the exact saved offsets; include
   off-grid values and legacy absolute-value migration.
3. Reset one camera in one set. Assert its current-car pose and global FOV
   return to calculation; other car poses, camera/set offsets and start choices
   remain exact. Lock the XML destination, edit, fail save, then retry and reload
   to prove the newest edit survives without a false success.
4. Change target/stage readiness and custom-package identity late. Assert the
   old identity is remembered before replacing it and the new car does not
   inherit the prior pose. This may expose existing Track target-switch behavior;
   preserve a failing witness before changing it.
5. Retain the new production ChaseFov XML/reset tests; extend view/set switching,
   stock game FOV changes, disable/reenable and fresh process reload. Game/Harmony
   doubles cannot prove callback ordering, engine FOV behavior or input reachability.

Keep existing passing focused evidence. Only the relevant persistence/lifecycle
checks need rerunning after that increment. A later muted runtime milestone must
observe set labels, Custom/reset, car/stage/restart persistence and cockpit handoff.

## Remaining scope and evidence boundaries

Stock chase still has no per-car pose offsets. Custom Vehicles retains its own
absolute per-vehicle cockpit store and position/FOV bounds; its separate runtime
tuner also reads imported Art keys directly, so its F7 collision needs its own
small companion fix/qualification. This increment changes Art's tuning and label
paths only. Individual F6 Default values remain the existing field defaults;
the whole-view Reset uses the calculated placement. Neither these source changes
nor a future fixture/build pass establishes complete STD-046 adoption, rendered
seams/body/HUD, physical force, seating or owner acceptance. Installed rc.37 and
the owner's historical rc.30 driving/force/performance acceptance are untouched.
