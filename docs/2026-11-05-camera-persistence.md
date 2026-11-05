# Real camera persistence and missing pose/default behavior

Assignment kole526824. Fresh worktree
`E:/Source/_work/napo-art-camera-persistence-20261105`, branch
`napo/art-camera-persistence-20261105`, off local main388696f. PUBLIC/local only;
no push/install/runtime. Installed rc.38 and owner calibration remain unchanged.

## Qualified source and file-only delivery

rc.39 delivered from clean local-main package source8f67b511, ZIP E8351B57/
Mod3D4221FF/build31773C6E. Actual producer/metadata379/archive27/installer70 passed
CPU526985, release06:56:20.5943170Z. Standing526985 admitted1793861915431 once
on rawidle6400.859s; four writes, nine payload hashes,51 backups/security and47
protected files independently verified. Settings,24 saves/four CV State,
calibration/native/shim unchanged. Exact lease released06:58:50.2950667Z;
standing approval stopped, no custody. No game/UI/device/owner acceptance.
Full hashes, backup/rollback and evidence in [Local deployment](LOCAL-DEPLOYMENT.md).

CPU526948 subsequently PASS30.51s: all four actual-old runtime regression modes
failed their expected assertions after a successful old-source compile. Current
CameraTuning1,056, SettingsUi3,565 and Lifecycle95 checks passed; full mod compile
0 warnings/0 errors. Candidate source596c37ad, Mod
9359DCFCB48C17348966E9AFBF95AC21C1B64ECBEB34CC938AE58794A45C42F8.
All230 input pins/certs unchanged. Independent589-image census found29 recorded
identities absent, scoped0; exact90542db200d046f7a2afff76078336bc released
2026-11-05T06:51:23.8192355Z, named lease/token absence corroborated. No custody.
Evidence `obj/camera-persistence-20261105/cpu-526948` raw logs/result/audit/closure;
result1F6AB6B7E67E38730954A833C0D2F3DCADEE477B8ADC670B27C2594F413E1D59,
auditECCEDC7FE6D0879036B460E131B65394504029967510CEEE0C3226F347069DED.
No game/device/UI/install/public push occurred during that source qualification;
the later authorized package/file-only installation is recorded above.

CameraTuning now links the actual CameraMountMemory, CameraCarIdentity,
ChaseFov and ChasePoseStore. Its Reset/Track/Load/Remember/offset storage is no
longer substituted. GeometryStubs supplies synthetic scene boxes/transforms,
clock/input/host and reflected package identity; production geometry and storage
code consumes them. These doubles do not establish real Unity geometry, physics,
reflection integration, callback ordering, input reachability or rendering.

The mounted store captures before target/set/settings changes and at edit/save
boundaries, preserving the last nudge even without another LateUpdate. A changed
Custom Vehicles package ID on the same donor/target changes the key. Unknown
package identities receive no persistent guessed key. A late measured body can
replace a default start while preserving offsets; a changed seat calculation
recomputes the baseline and keeps the saved FOV delta. Geometry is reused across
set switches instead of resampling it on every F6/active-view handoff.

Stock chase now stores side/height/forward/pitch in ChaseCameraPoses by stable
car identity, stock view index and Game/Realistic set. Existing FOV lists remain
shared across cars for each view/set; legacy FOV data is not migrated into poses.
Pose overlays use car axes for translation and camera-local pitch, are removed
before the game camera updates, and are released on mounted/cinematic/disable/
unload handoff. A foreign Camera Mod keeps rotation/pose ownership; saved pose
records remain intact. Stored tweaks have no arbitrary bounds; nonfinite values
cannot be applied, and rotation conversion reduces full turns without rewriting
the saved pitch. Reset clears the active car/view/set pose and that view/set's
shared FOV, preserving other cars, views and sets. Pose alone does not mark FOV
Custom. Context changes use the existing capture/focus/release gate.

Individual F6 Default buttons now receive the calculated current-car/set field
baseline used by whole-view Reset. F6 tracks the selected mounted camera before
drawing these fields, including when a stock chase view is active. Applying one
Default clears only that field's offset; FOV remains shared across cars. No
calibrated placement constants, native/shim binary, force, bindings or starting
choice is changed.

## Initial prepared checks, subsequently passed CPU526948

The same new tests can compile against the unchanged rc.38 production
Keys/Tuner/ChaseFov/CameraMountMemory via CameraSourceRoot. Four targeted old-RED
modes exercise target-switch last edit, immediate-save last nudge, same-target
custom-package identity change, and missing stock chase pose dispatch. Compiler
errors are not acceptable regression witnesses; each must fail its specific
runtime assertion. The F6 Default cases test the actual new baseline handler;
no old rendered/click-path reproduction is claimed.

Current production-path cases cover two cars x both sets x both mounts, eight
pose fields, FOV sharing, same-target settings reload, reset scope, failed locked
save plus newest-value retry/reload, UTF8-no-BOM/CRLF, valid legacy absolute-pose
migration, late body/seat data, custom-package isolation, chase keyboard/USB
mapping, car axes, noncompounding undo/release, foreign camera ownership and
actual XML persistence. SettingsUi must verify the new runtime-only inventory
field; Lifecycle must compile/run its affected callback owner path. Full mod
compile is required. CPU GO is requested only after source/runner/pins freeze;
no compilation/test occurred during preparation.

## Remaining gates

Successor CV0.6.7/Art rc40 closes the companion store/imported-key gap offline
and is installed file-only; see [CV cockpit persistence](../experiments/CustomVehicles/COCKPIT-PERSISTENCE.md). Stable stock
view indices, measured meshes, actual hotkey/UI dispatch, callback handoffs,
stage/car/restart rendering, final extreme-lens behavior and owner seating/
comfort require later bounded runtime/owner evidence. Existing ready_for_test
perspective/curve status and rc.30 historical driving/force acceptance are not
promoted or downgraded by these source changes. No complete STD-046 adoption.
