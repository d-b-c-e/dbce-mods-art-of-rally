# Art rc28 closeout for Kobe

Assignment: Kobe230321. Source preparation only; Kobe is the native screen
operator. This note grants no launch, input, disconnect, installation or lease.

## Current reconciliation

Read-only verification on October17, completed by18:42UTC:

- Owner game: `D:/Program Files (x86)/Steam/steamapps/common/artofrally`.
  All nine Art payloads match the rc28 manifest. Installed identity is
  `0.4.2-rc.28+eb2316dc5bc951557c9bf1f9c2abbe0c6b2118ab.clean`;
  mod `296C8EFA49A718AF47F22292B3346BFE5260BEB677969B9B24238556FDF6712A`.
- Both normal native copies remain `44E46799`; shim remains `BC9B030A`.
  Settings `CF315E67`, controls `73E78230`, triples `13F0FD93` and desired
  layout `A9F0C6B7` match the actual rc28 installation record. SharedInput=true,
  H-pattern and ForceFeedbackEnabled=true are the owner's current choices.
- Canonical main remains `643bbd0`; the only dirt is the owner's
  `experiments/CustomVehicles/SESSION.md`, full SHA256
  `42EBDD696A75F695D2655292C180E5E9B7F87F86FCF5A948A8CFF91711F4823F`.
  It was not edited, staged, stashed or reset. Clean source closure `68c4e22`
  differs from compiled product `eb2316d` only in Markdown documentation.
- Optional `a4dc1e2` changes the tab focus outline only. It remains outside the
  installed/frozen candidate; no cosmetic build is needed.
- Retain product35/UI169, package metadata353, delivery/rollback64 and the
  unchanged-output PS5 companion continuation. See
  [the original qualification](2026-10-17-readonly-binding-discard.md) and hub
  `artifacts/art-rc28-owner-upgrade-zori-20261017/RESULT.md`.

No remaining private product-source blocker was found in this scope. Main
promotion remains deferred because the owner checkout is dirty. Public
distribution remains a separate decision for the external private shim
BC9B030A/source421bd4b-c6594fa; the no-shim package is a different artifact.
Driving, force and performance acceptance remain accepted.

## Existing route and one tooling correction

Branch `samo/art-closeout-20261017` in
`E:/Source/games/.worktrees/samo-art-closeout-20261017` starts at source
closure68c4e22. It carries the existing manual route from Beni's `d134c41`
(cherry-picked as `cf73009`) in `tools/testing/Invoke-SharedInputCopy.ps1`.
There is no Recorder, synthetic input or automated binding mutation in this
mode. Product sources and installed/package bytes are unchanged.
Manual leaf SHA256:
`27E8AC39CEECE966431ACDEA6D55EE57341E9A15CF9F6B2509BE283FB837F04C`.

The old manual branch immediately left its loop on `Shared input stopped` or
`not used`. That made inspecting read-only Discard after a stopped reader
impossible. This branch keeps the same bounded manual session open, retains
the unavailable outcome/logs, and uses the existing normal-close/deadline and
restoration behavior. Other run modes retain their stop behavior.

Verification: PowerShell parser and `git diff --check`; four existing extracted
normal-close/deadline outcomes; three actual extracted branch executions for
accepted/stopped/unavailable log input. No runner body, game, device, build or
heavy suite ran. The first branch-check command only emitted a script block;
the corrected check evaluated its body and asserted all three results.
Rendered operation of the adjusted route remains unverified.

The old hub `Run-TargetedUat-07.ps1` is historical and must not be launched
unchanged: it pins rc27, old Settings53CC22B2, an older Stage-OwnedProcess hash,
old operator identities and old grants. Its frozen evidence stays intact.
Use the existing manual leaves below with Kobe's current serialization and
snapshot/restore route, without rebuilding the product or Recorder.

## Concrete session entry

After Kobe's fresh handoff and screen notice, hold the literal shared lease
`%LOCALAPPDATA%/dbce/test-slot.txt` through staging, game close and restoration.
Snapshot **before staging**, as in the existing Art outer route: copy mod,
Recorder directory, native, steam_appid, UMM Params and Log; current LocalLow,
ArtOfSimRally local data, Steam550320 and triples status. Preserve the complete
typed/raw `HKCU/Software/Funselektor Labs/art of rally` snapshot through the
existing Stage-Registry helper. Retain owner payload/config pins separately.
The leaf's snapshots begin after staging and do not replace this outer backup.

Reuse the existing disposable game copy
`E:/Source/games/.worktrees/art-shim-probe-game-20261011`. With a fresh absent
`$run` output and Kobe's held `$lease.token`, the existing commands are:

```powershell
$art = 'E:/Source/games/.worktrees/samo-art-closeout-20261017'
$owner = 'D:/Program Files (x86)/Steam/steamapps/common/artofrally'
$package = 'E:/Source/games/.worktrees/zori-art-readonly-discard-20261017/artifacts/art-ui-20261017/package-01/source/dist/ArtOfSimRally-0.4.2-rc.28-shared-input.zip'
& "$art/tools/testing/Prepare-SharedInputCopy.ps1" `
  -GameCopy 'E:/Source/games/.worktrees/art-shim-probe-game-20261011' `
  -SourceGame $owner -Result $run `
  -Profile "$owner/Mods/ArtOfSimRally/dbce-controls.ini" `
  -PackageZip $package `
  -ExpectedPackageSha256 B7818C8DF90310D3B4C9BE7E67E0459EB22176B31FE670C40F7B0FF2D5981A2F `
  -MutedNative 'E:/Source/toolkits/.worktrees/art-shim-snapshots/native/wheelffb/build/WheelFfb.dll' `
  -ForceFeedbackOff
& "$art/tools/testing/Invoke-SharedInputCopy.ps1" `
  -Result $run -ManualUi -ObserveSeconds 300 -HeldLease $lease.token
```

Run the observing leaf asynchronously so Kobe can operate the exact child.
The DD301314 native is the existing load-time-muted substitution in both copy
locations; the normal44E4 package remains immutable. The leaf also stages
FFB=false. Do not add `-Recorder`, `-Activate`, a controls request or an injection
marker. Read `launch.json` for the actual PID/birth/executable; select that exact
game through Kobe's supported native tools. Stop on a different/replacement
process or an unexpected state. A manual normal close is not a functional pass.

## Targeted observations

1. In the main menu, F6 → Controls. Confirm actual reader attachment and an
   editable shared profile before changing a binding. Capture the new footer
   and complete relevant rows. Preserve earlier accepted row/Help pixels;
   sample only missing native glyphs or changed views. Help's repository
   destinations are small text links and Submit issue remains separate.
2. Click a digital action's Bind, then its visible Cancel promptly in the same
   local operator sequence. Digital timeout is10s; axes11s. Release controls
   first; axis capture needs one second released/centered before movement.
   Confirm the prior binding remains and listening ends. An expired timer is
   not evidence that Cancel failed.
3. For a deterministic real Save failure, **in the disposable copy only**,
   Clear Steering, then Save bindings. `ControlProfileDocument.Edit` must refuse
   with `A usable steering binding is required. Nothing was saved.` Confirm
   the active assignment and on-disk profile are unchanged, the draft remains,
   and the binding failure stays in the footer while switching to another page.
   No filesystem fault injection or product hook is needed.
4. With that draft retained, observe real reader loss if the owner is present
   to disconnect the intended device. On genuinely read-only access, Save is
   disabled and Discard stays enabled when no capture/legacy edit is active.
   Discard clears only the unsaved draft and failure. Without an earlier saved
   change it must not say a restart is needed. If another qualified device
   remains, access can remain Editable: that is not a defect. A lost explicit
   Bind-from choice must become unavailable rather than silently selecting the
   other device; reappearance requires explicit selection. Record which case
   actually occurred. A read-only screenshot alone is not hotplug acceptance.
5. Separately perform one **real moved control** Bind on a suitable non-steering
   action, Save, and verify next-start guidance plus the exact copy-profile
   delta. Close normally. For the required cold-start check, preserve the
   post-Save profile archived by the leaf: find its `shared-files.json` row
   by exact copy profile path, then take `after/<backup filename>`. Use that
   file as `-Profile` in a fresh staging result for a second bounded manual
   launch after first-child closure. Do not reuse `launch.json` or start with
   the original owner profile. Confirm the new Active assignment, no pending
   draft/restart warning, and actual response to the moved physical control.
6. Physical device loss during capture must cancel without changing the prior
   binding; a missing saved identity must not bind to a same-name replacement.
   Any reconnect/recovery claim requires its actual observation. Do not disable
   Windows devices or simulate a disconnected flag as substitute evidence.

The remaining stage actions are already listed in hub
`artifacts/art-rc26-targeted-uat-source-kiki-20261016/PHYSICAL-ACTION-UAT-LIST-niru.md`:
horn press/release, recover held1s, look back, repair with an actual puncture and
spare, quick restart and look left/right/forward. They need actual eligible
state and physical presses. Mark unavailable conditions NOT_EXERCISED; do not
manufacture damage, claim getter-only behavior, or repeat a driving baseline.

Before releasing, retain screenshots, saved-profile delta, shared-lines and
force-check results; require exact child closure, leaf `restored.txt` with no
restore error, then restore/read back the outer pre-staging snapshots and raw
preferences. Verify current owner payloads/configs and dirty SESSION unchanged,
and release only the held token. Preserve failed evidence/rollback if cleanup
is uncertain. No routine independent-review/archive cycle is added.

## Remaining work

Kobe's fresh runtime handoff is the next dependency. This source work holds no
lease or process. No private product blocker or useful additional source fix
was identified; request another authorized source fallback once the route is
handed over. Public designation and canonical dirty-main promotion remain
separate from these runtime observations.
