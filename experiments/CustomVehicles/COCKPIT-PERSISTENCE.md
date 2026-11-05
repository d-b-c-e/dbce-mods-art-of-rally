# Cockpit camera persistence increment (2026-11-05)

Source candidate only; CPU qualification is pending. No package, installed-state
write, game launch, input injection, physical output, or public push.

Cockpit eye translations are offsets from each package's authored camera point.
Pitch is stored relative to the calculated six-degree start. These pose offsets
belong to a vehicle and Game/Realistic set. Vertical FOV offsets belong to the set
and apply across custom vehicles. Game starts at 65 degrees; Realistic uses Art's
measured seat FOV, rounded to one decimal as before. A later seat measurement
changes the baseline while retaining its saved FOV offset. Reset clears only the
current vehicle/set pose and that set's shared FOV offset. The other set and other
vehicle poses stay intact.

Finite saved values have no arbitrary numeric bounds. The final Unity lens alone
uses 1..179 degrees; pitch is reduced modulo 360 before Quaternion.Euler, and a
nonfinite world-space result is not assigned to the camera. This is a numerical
guard, not evidence that every finite adjustment produces useful visuals.

Loading legacy absolute arrays never writes. On the first explicit save the
original bytes are retained in cockpit-views.json.legacy-v1.json and the atomic
replace also retains .previous. Known package poses migrate separately by set.
A single legacy FOV for a set becomes its shared offset when the baseline is
known. Conflicting legacy FOVs stay available until the user explicitly adjusts
or resets FOV; the panel explains that choice. Unknown Realistic seat geometry
does not seed a guessed FOV offset, and explicit FOV save/reset requires a known
current or previously measured baseline. Session edits survive IO failure and
car/set changes; retry compares actual disk bytes, refuses external edits, and
retains the pending session edits on failure. Reloading after an external edit
is an explicit recovery action, not an automatic overwrite.

Imported F7/F8/F10 and the active Settings key never dispatch cockpit actions.
Explicit None stays unbound. Capture mode, focus, the UMM panel, tuning-off and
modifiers suppress dispatch; held actions must be released before tuning resumes.
Without Art the shared layout uses numpad minus to widen and plus to narrow FOV.
FOV edits/reset notify the existing Art camera overlay through optional reflection.

The focused fixture links production store and input/save/reset code with a
simulated host. The plugin build and existing Harmony/guard checks cover the real
assembly separately. None establishes native Unity execution, rendered seams,
physical keyboard/device behavior, driving, or owner acceptance. Installed Turtle
and Art settings/saves/FFB remain untouched; RuntimeQualified stays false.
