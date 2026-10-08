# Qualification and review record — 2026-10-08

The owner requested sustained development and advice from Astra and Claude while
away. Development used the communicate skill and existing hcom peers: `tara`
(this session), `hula` (owner's Astra peer), `nene` (confirmed Claude Code peer).
No duplicate sessions, game/rig launches, installs, input injection or physical
force tests were used for this experiment.

## Evidence boundary

Source/build/mesh/managed tests pass independently of game runtime. The candidate
keeps `RuntimeQualified=false` in Main.Load and checks it again in the GUI and
activation policy. The physical code is a staged implementation, not a qualified
replacement for the native donor. The installed cosmetic Turtle Van 0.1.4 remains
the owner's separate baseline. The framework's changed palette, extra vehicle,
selection persistence and additional hooks have not been seen in game.

Managed tests exercise the production Core files, actual compiled upload/local
prefixes, failure callback isolation, stable ID persistence, policy combinations,
input rejection, duplicate/corrupt neighbor isolation and rollback failure injection.
Harmony target and argument contracts resolve against the installed game's metadata.
These checks do not execute native Unity methods, simulation or network requests.

Final offline checkpoint: 141 managed assertions, 38 resolved Harmony targets and
argument contracts, actual upload/local-prefix callback checks, five creator-tool
integration tests, and Blender dry-run/no-write checks passed. Both exported
packages validate. The exported Turtle Van was reconstructed from JSON/PNG and
rendered from outside and the cockpit; this checks the shipped data/UVs rather
than relying only on the original Blender scene. Both views were inspected.
Twenty-one installed payload/protected-file hashes still match the 0.1.4 install
receipt; `artifacts/installed-baseline-check.json` records the comparison.

## Native lifecycle observations

Read-only inspection of installed Steam build 17584229, Unity 2019.4.38, UMM 0.33.0.
Decompiled research is ignored under `artifacts/research` and is not distributed.

| Input | Native writer / consumers | Experimental placement | Unresolved runtime check |
|---|---|---|---|
| Body mass, COM, distribution, inertia | Setup.LoadBodyData; CarDynamics.Init resets/derives inertia and COM | After Setup.LoadSetup and before CarDynamics.Init | A/B inertia and root-relative COM; reject shared markers |
| Engine values / gearing | Setup engine/transmission; Drivetrain.Init -> CalcValues | Before Init, after Setup; use donor CV2KW (0.7358 in inspected build) | Torque/power caches, clutch, gear limits, damage/restart |
| Axle travel/springs/damping/brakes | Setup suspension/brakes; CarDynamics.SetWheelsParams and SetBrakes | Owned Axle copies, before dynamics Init | Native brake balance and reset paths |
| Mount spacing, tire size | Prefab; Setup.LoadWheelData is empty in this build; Wheel.Start caches tire geometry/inertia | PlayerManager.CreateCar postfix before first Start | Root scale, mount heights, tire stiffness, contact patches, restart |
| Collision | PlayerManager identifies the body MeshCollider; dynamics derives inertia | Owned box mesh before dynamics Init | Actual selected collider, contacts, damage, jump/landing |
| Visuals / camera | Native Wheel.Start establishes modelTransform; CarCameras changes views | Attach after wheel models exist; camera postfix only in player view | Intro/finish/replay/photo/restart and triple-camera handback |

Read the existing manager's private field; do not call the lazy
`GameEntryPoint.EventManager` factory from early initialization. No native code,
FFB, telemetry or core camera hooks were modified.

Physics failure restores journaled mutations in reverse order, releases owned
resources and deactivates the failed car. It never resumes a partially initialized
solver. Even after rollback, result taint remains latched. This source path itself
still requires failure injection inside a real Unity lifecycle before qualification.

## STD-018: scores, ghosts and progression

The activation policy requires qualification, the per-launch toggle, a selected
package with physical fields, Free Roam, and verified local/network patches.
Changing any one to false prevents activation. Before the first physical mutation,
a process-lifetime taint latch blocks unloading and result mutation. Changing back
to stock does not clear it; exit/restart is required.

Network prefixes cover PlatformSteam.UploadScore, backend PostLeaderboardEntry,
and PostLeaderboardReplayBlob. Suppression invokes the supplied failure callbacks,
never success. Local guards cover SetLocalTime before it mutates the offline cache,
GhostManager.FinalizeRecording/SaveDataToDisk, season/career/weekly save entry points,
stat increments, achievements and Free Roam collectable mutation/unlock entry points.
This intentionally prevents further progression for the tainted process.

Guard presence and managed prefix behavior are checked, **not full runtime path
coverage**. Audit alternate platform implementations, legacy Steam leaderboard
routes, queued async writes, stage transitions and already-pending work before
unlocking physics. No claim is made that the parent mod globally adopts STD-018;
its unrelated handling toggle remains a separate audit.

## Attended qualification sequence

1. Back up the actual installed baseline and verify exact files while the game is
   closed. Preserve the accepted cosmetic mod and all main-mod settings. Use the
   user's existing raw preference-preservation method; a registry export alone
   must not be assumed to contain all Unity preferences.
2. Load only the framework presentation owner. Exercise both packages in Group 2;
   stock/custom selections, remove/reorder packages, missing DLC, menu revisit,
   intro, drive, restart, finish, replay/photo and return to stock. Check repeated
   scene resource counts, native saves and camera local transforms/FOV/near plane.
3. Enable the diagnostic checkbox before the next spawn. Capture the same donor
   as stock and custom with identical settings. Reports are written only under
   this mod's State/donor-stock.json and State/donor-custom.json. Run:
   `python tools/compare_instances.py <stock.json> <custom.json>`.
   A match proves only the captured fields. Also compare shared assets, axle/curve
   references, per-wheel caches, rendered contact alignment and native reset paths.
4. Test network and local guard suppression with controlled offline/injected
   callbacks. Compare save/ghost/stat files before/after, including queued writes,
   return-to-stock and later-launch upload opportunities. Never submit a modified
   time to a public leaderboard as a test. Confirm truthful not-uploaded UI.
5. Only after the no-op and guard evidence, make a reviewed candidate enabling one
   physical field (mass) in Free Roam. Capture actual initialized/derived values,
   restart, failure cleanup and donor restoration. Expand one field at a time.
   Wheel/FFB tests require the owner attending the rig.
6. Integrate immutable vehicle identity into recorder manifests before claiming
   reproducible custom-physics replay. Qualify pause/restart/vehicle replacement,
   output mute and derivative resets with the recording owner. Kinematic playback
   is presentation evidence, not a solver test.

## Peer findings incorporated

Hula's architecture review (hcom 251) recommended immutable donors, native-cache
timing, transaction/scene ownership, mod-owned fingerprints, local as well as network
result protection, capture identity and aggregate resource limits. The implementation
adds those foundations; no-op/shared-resource/solver/replay qualification stays open.

Nene's creator review (376) found overly broad linked-ancestor rejection, sRGB atlas
encoding, raw missing-marker errors, a missing dry-run command, vertex duplication
and unclear engine-field requirements. Changes address each. An actual PNG swatch
confirmed the color problem: linear paint (0.045,0.36,0.32) initially encoded as
(11,92,82), then as corrected sRGB (60,162,153). The two models dropped from about
33.4 MB to 13.4 MB of JSON without losing triangles. In-game color acceptance remains
pending. Preview paths are also checked and export destinations made absolute.

Hula's second runtime review (402) found no blocker to retaining the offline lock,
but required an unambiguous body collider and broader cache evidence before any
physical activation. The code now rejects ambiguous collider sets, requiring one
non-trigger mesh on the root rigidbody outside wheel branches. Diagnostics include
collider paths/bounds/convex flags, wheel rim radius/inertia, derived drivetrain
values and game-assembly fingerprint. Selection of the correct collision shape
still requires native/runtime inspection; uniqueness alone does not qualify it.

Nene was asked for a second runtime presentation review. An acknowledged request
is not a completed review or runtime certification.

Reciprocal coordination: hula requested a bounded read-only review of the wheel
toolkit's canonical consumer-path resolver (master 9c3c2ba working diff). The
review found no blocker and suggested two focused fixture cases (games-root input
and absent canonical component root). No toolkit files or runtime were changed.
