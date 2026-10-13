# Shared steering force target: private candidate

A fresh user of the shared controls setup has `dbce-controls.ini` but no legacy
`Settings.SteerBinding`. Following steering previously found no FFB target.
The active shared session locks the FFB device picker because changing devices
closes its readers. Resolving the target after attachment would stop shared input.

The candidate inspects the startup profile before force recovery. It asks the
shared Core's optional `dbce_shim_profile_action_info` export for the configured
steering identity; the native call uses Core's parser and pure binding admission
without creating a host, worker, provider, read handle or force effect. The same
byte array is retained for `SharedInputSession.Begin`. The report's full-file
SHA-256 must match those bytes. Art checks the response schema, action, axis kind
and nonzero instance/product GUIDs; it never reparses a binding.

`FfbSelection` captures whether shared input was selected for this launch and its
resolved target. Explicit selections retain precedence, including legacy explicit
GUIDs. In shared-follow mode, malformed, conflicting or unbound profiles and a
missing optional export provide a correction/restart reason. They never select
legacy steering, a display name, an index or the first available wheel. The native
FFB owner still performs strict instance-GUID and force-capability selection.
Configured identity is not connected hardware or physical force evidence.

Changing the saved shared-input flag or file applies next launch. Qualification
and attachment do not request a force reconnect or retarget the selected device.
No legacy binding is written and no effect, gain, reference or default is changed.
The existing locked picker remains; this slice does not enable a device change
while shared readers are attached. The owner's installed rc18 is untouched.

The inspector requires a fully consistent profile before returning an automatic
force target. Core ordinary input retains its existing ability to use valid
actions beside invalid ones; the force target query returns the invalid action's
name instead of selecting from that partial configuration. Unknown/unsupported
schema and cross-action product conflicts are checked by the same admission code.

## Review and validation status

Source base: Art `95203fd114af33a3459d35e50015039b2c6f1aba`; native copied provider
`f38b85566251cad742f68a0057cd166944bd5a9f`. The extension is additive and optional:
base ABI 2, 296-byte frames, 128-byte open arguments, bridge ABI 1 and 144/232-byte
metadata/samples are unchanged. Existing exports remain.

Source review passed for native `5b96ba8f3d649f353e2aad1aac8342f4334e0ad0` and
Art `123379ab0dda3cc1dd05bd92baecc188dca76afb`. The bounded offline batch held the
shared rig lease from 07:53:22 to 07:56:00 CDT on October 13, then released its
exact token. It opened no physical devices, game or force output. Logs and hashes:
`results/shared-ffb-profile-20261013` in the isolated Art worktree.

The reviewed native DLL is 666,112 bytes, SHA-256
`723E8A38CDA8DDA12546264580A5B3FF5AFFDF469E25E8777149CB792A789276`.
The coordinator independently checked this hash and approved the private-candidate
pin. `CANDIDATE.json` and `ExpectedShimSha256` now match it. WheelFfb, toolkit pins,
settings, gains and accepted force recordings are unchanged. The shim stays outside
the public repository and is supplied to the existing package script explicitly.

Existing packages without the optional export give the matching-package message
in shared-follow mode; explicit force selections and shared input remain available.

Executed checks:

- Native pure plan and C ABI: full grammar, duplicate sections/keys, unsupported
  schema, invalid non-steering action, cross-action product conflict, exact hash,
  unbound action, bounds, truncation and the added export alongside old exports.
- Art `SettingsUi`: legacy follow, explicit selection precedence, failed explicit
  selection, shared follow, no settings/tune write and no stale-target fallback.
- Art `WheelInput --shared-ffb-only`: production Prepare/selection/qualification
  order with fake inspection and qualification boundaries, fresh shared-only
  settings, pre-recovery target, force-settle wait, byte-array identity at Begin,
  startup-latched shared mode, refusal and no late target resolution.
- Art `ShimInput --profile-info <candidate DLL>` on .NET and Unity Mono: actual
  pure export/response validation without a Core host, device or force output.

Results: native Core 169, ABI 164, copied-provider 77 and fake-COM 106 checks;
SettingsUi 146, shared FFB startup 16, ordinary startup 14 and lifecycle 33 checks;
ShimInput 12,327 each on .NET 8 and the installed Unity Mono. The production mod
built with zero warnings/errors. The first native configure lacked ZLIB paths;
the retry used the existing `outrun2-decomp/build-deps/zlib` dependency without
building the game or downloading dependencies. WheelInput's strict build hit four
pre-existing CS0649 warnings in unchanged `DriveTrial.Sample` fields; the fixture
build allowed CS0649 only. Both original failure logs are retained.

## Packaging and bounded runtime check

The private rc.20 package was built from clean
`1d845d8bb26090f13893c2249092f371b4f0e861` using the existing script (no standalone
helper changes):

```powershell
./tools/package/package.ps1 -Version 0.4.2-rc.20 -SharedInputCandidate `
  -SharedInputShim E:/Source/research/.worktrees/art-shim-profile-info/build-profile-info/windows/Release/dbce_shim.dll
```

SessionTools already uses rc.19; the mod candidate uses rc.20 to avoid ambiguity.
The script checked the exact shim/native hashes, metadata, release assembly,
probe flavor and payload manifest. All 15 payload files were verified again from
the extracted ZIP. Package SHA-256:
`8BA67D4EDC3E99F549F3FC2818D8185CC3CC6FE0AB03A7A24EB475FC5B8743EA`
(616,249 bytes); extracted manifest:
`8402DCBC12F6B36855E324555E3D2C436C140647FD6E1F482348D8A02EFA2D54`.
Packaged mod SHA-256:
`40C837E26C9BC68B2211AD06AC79C6DB1E9355B3AD082DDA670F89CE00A1E0A4`.

The package batch held its lease 08:11:53–08:13:16 CDT and released it. Release
assembly checks 281, updated target/startup checks 21/14 and actual shim checks
12,327 each under .NET 8 and Unity Mono passed. The package build had zero warnings
or errors; the fixture retained the four known CS0649 warnings. Receipt and logs:
`results/shared-ffb-package-20261013/package-receipt.json`. No install or launch
was part of packaging.

The approved runtime scope was one existing `Prepare-SharedInputCopy` /
`Invoke-SharedInputCopy` ordinary smoke, with no recorder or injected input,
60-second observation deadline and 10 seconds after the first accepted frame.
Use a separate SettingsSource with `SharedInput=true`, empty `SteerBinding`,
`FfbDeviceMode=steering`, empty preferred name/GUID and preferred index -1. Keep
the exact shared profile bytes pinned and preserve the owner's files. Stage the
exact package with `-ExpectedShimSha256` set to the hash above.

The unchanged package native `44E46799` has no load-time force mute or native
test-injection fence exports (`bbbf6e3`, 51 exports). The existing ordinary runner
requires `-ForceFeedbackOff` and verifies no force startup attempt, but this is
not the independent native barrier required for the proposed run. ControlsProbe's
cold native fence needs `EnableTestInjection` / `GetTestInjectionStatus`, which
this native does not export. Managed output hooks are not an equivalent barrier.
An explicit test-only substitution was reviewed separately below. Do not launch
the production package unattended on the strength of config-off alone.

Pure target inspection runs before qualification even with force off. A separate
startup-only trace now records the configured steering instance or actual refusal
and the SHA-256 of the frozen profile bytes, before entering Waiting. The line
explicitly states that connection and force are unverified; explicit FFB overrides
retain priority. New fixture checks cover one-time logging before qualification,
the exact hash, no repeat after attachment and all three refusal cases. These
five additional checks passed in the packaging batch. The coordinator separately
authorized the one run recorded below. Normal physical FFB acceptance remains
owner-attended.

### Test-only native compatibility and runner

The previously qualified muted native from toolkit `82c40c4` is available at
`toolkits/.worktrees/art-shim-snapshots/native/wheelffb/build/WheelFfb.dll`, SHA-256
`DD3013147821364AD8795F76B4500FB479F669E6BD89576F83E725A677273414`.
Its `DBCE_FFB_MUTE=1` load-time latch independently refuses force entry points.
It was used only for the disposable diagnostic; the production package and
source pins retain `44E46799`.

Source comparison against `bbbf6e3`: the V1 snapshot header and snapshot-state
header are byte-identical; the 144/232-byte packed layouts, C calling convention,
generation/sequence/QPC meaning and owned-path signature are unchanged. The
snapshot implementation adds an external-session guard (inactive without explicit
external entry) and rejects an active injection table. This run enters neither
mode and submits no injections. All 37 required exports in Art's pinned managed
v0.15 binding are present. `GetInputCapabilities` is absent, but Art does not bind
it or use its synthetic POV button indices; Art uses 128 real buttons and the
available `ReadDeviceStateWithPov`. Force behavior is not claimed equivalent.

The prepared one-shot wrapper is
`results/fresh-shared-only-rc20-plan/Invoke-FreshSharedOnly.ps1`, SHA-256
`03D27D6DABD1E144B8AFD7E2AE1740378020998048A254EE911E83DDFB82026D`.
The coordinator reviewed these exact bytes before the run.
It uses existing package-mode staging and `Invoke-SharedInputCopy`, preserving
their validation, child-only mute, shared-state restoration and process checks.
The original package staging receipt remains intact. A separate recorded override
pins DD301 at both disposable native paths; every other package payload remains
exact. An outer snapshot/restore covers all original copy Mods files, the plugin
native, UMM parameters and copy Steam app id, since the inner runner restores
shared state but does not undo payload staging. Preparation, inner run and final
copy restore each acquire the actual shared lease; contention refuses.

The frozen installed Art profile is SHA-256
`73E782304BB56DF5D722A0FADDA7E9C5FFD8347736C11003A6DFEFCDF94FADFA`.
The separate settings fixture is SHA-256
`8C78D71F65B90346215D3B0F9569FF6C2DEB136FF823F7F07C169F43915E5454`;
it preserves force tuning and changes only shared selection, force disabled,
empty legacy steering/preferred fields, preferred index -1 and follow-steering
mode. The expected configured steering instance is
`d71b8350-61b7-11f1-8001-444553540000`. Runtime scope is the configured-target/hash
line before qualification, first accepted shared frame and ten seconds without a
stop. It does not qualify physical force, connected FFB acquisition, driving or
input/force coexistence with the production native.

### October 13 runtime result

The single authorized attempt passed its scoped startup checks. The inner runner
held lease `a5e83981cb194c888ca4ab488d1431f4` from 08:23:32 CDT; preparation and
copy restoration used their own serialized leases. Exact copied-game PID 50160
started at `2026-10-13T13:23:37.2102432Z`. Evidence is in the isolated worktree's
`results/fresh-shared-only-rc20-run-01`:

- `run/shared-lines.txt` records the expected configured steering instance and
  frozen profile hash **before** qualification. The launch logged `ffb=False`.
  It then attached `Moza KS Pro Triple`, isolated the R12 and DS-8X, and accepted
  its first frame with 21 owned actions. The subsequent ten-second settling
  interval completed without a shared-input stop or refusal.
- `run/staged.json`, `production-staged.json` and `test-only-native.json` retain
  the production package's `44E46799` pin separately from the actual disposable
  `DD301314` substitution. Both native paths had the latter, with the irreversible
  child-only `DBCE_FFB_MUTE=1` load-time latch. No input was injected.
- `run/native-ffb-log-delta.txt` contains enumeration and four read-slot opens,
  then four read-slot releases and `FreeDirectInput`. It contains no force
  initialization, acquisition or effect calls. The mod reported no FFB startup
  attempt. This is no-force diagnostic evidence, not measured physical output.
- The shared shutdown receipt in `run/after/new` reports `resolved`, two released
  devices, zero held devices, `inputReleased=true`, recorder joined/complete and
  control joined/complete. `CloseMainWindow` closed the owned child normally;
  no forced termination was used.
- `run/restored.txt` records shared-state restoration at
  `2026-10-13T13:24:10.5705527Z`. `copy-restored.json` records all 26 original copy
  files restored at `13:24:11.0351898Z`, including both `44E46799` native copies
  and the original recorder. The process was absent and the lease released at
  08:24:11 CDT. Owner activity policy was honored and recorded truthfully.

Owner Settings remained SHA-256
`0A380211744928BF91BF3B8E30F791142286D97DBEF4CF65F05ECA95D2E4F68A`;
the shared profile remained `73E78230` (full hash above). No accepted native,
force tune, recording, installed backend or owner payload was changed.
`verdict.json` records target-before-qualification, exact identity/hash and first
frame plus settling success, with `injection=false`, `nativeSubstitution=true`
and `physicalFfbQualified=false`.

This establishes the fresh shared-only configured-target startup path using the
reviewed diagnostic native. It does not establish physical steering/pedal/shifter
behavior, keyboard driving, visual acceptance, force acquisition or simultaneous
shared input and FFB on the production `44E46799` native. No race was driven.

## Local deployment gates and plan

No rc.20 installation has been performed. The owner install still identifies
`0.4.2-rc.18+26f5ccab97566b1e0d5174c5cfe7185eeeb14cf4.clean`. Its receipt is
`games/dbce-mods-art-of-rally/results/shared-input-rc18-install/install-receipt.json`.
Its older settings snapshot predates the owner's current shared profile; preserve
the current Settings/profile bytes, not that historical snapshot.

`AGENTS.md` and `docs/LOCAL-DEPLOYMENT.md` require the complete local automated
gate before installing. The focused checks and runtime above do not replace it.
`Test-Rc.ps1` has no shared-candidate argument: it builds a default package and
always checks the default toolkit native. The existing rc.17/rc.18 procedure is
therefore the appropriate bounded completion route:

1. Under the shared lease, run the existing 20-gate suite once on this clean
   branch with the original two-case recorded corpus and a throwaway label.
   `0.4.2-rc.920` is already occupied; `0.4.2-rc.921` was unused when planned.
   Recheck occupancy before starting. Keep the rc.20 ZIP unchanged.
2. Run `Test-Installer.ps1` on a fresh extraction of the exact rc.20 shared ZIP.
   It tests the actual batch/Windows PowerShell entry points, install/upgrade/
   removal, optional-shim ownership and settings preservation in fake game
   folders. It never launches the game. Do not reuse Test-Rc's deliberately
   corrupted extraction for installation.
3. Retain the already-passed exact rc.20 release/manifest/actual-export checks
   and one-shot startup evidence. No native rebuild, extra Mono sweep, new helper
   Apply or second runtime attempt is needed for this fix's local gate. Physical
   acceptance and any public release remain separate.

Reproducible commands, from the isolated Art worktree, using the reviewed shim
explicitly in the runner process (restore any inherited value afterward):

```powershell
$priorShim = [Environment]::GetEnvironmentVariable('ART_SHIM_DLL', 'Process')
try {
  $env:ART_SHIM_DLL = 'E:/Source/research/.worktrees/art-shim-profile-info/build-profile-info/windows/Release/dbce_shim.dll'
  # Verify SHA-256 723E8A38... (full value above) before invoking the gate.
  ./tools/testing/Test-Rc.ps1 -Version 0.4.2-rc.922 `
    -Corpus E:/Source/games/dbce-mods-art-of-rally/results/regression-corpus/index.json
} finally {
  [Environment]::SetEnvironmentVariable('ART_SHIM_DLL', $priorShim, 'Process')
}
./tools/testing/Test-Installer.ps1 `
  -PackageDirectory <fresh-rc20-extraction>
```

The corpus index remains SHA-256
`C964C158A37F28DB5F677BA4063DAF473737580AF121B28D0DB951122BCAC3D2`.
The latest earlier full gate was rc.919 at `24439a3`, not this target-selection
source. It was precedent, not a substitute for the gate below. The suite covers
telemetry/signals, input/settings/regression, triples, session tape/environment,
lifecycle/force models, recorder/actual Unity Mono, replay/trials/rejection,
release evidence, force vector, native exports, packaging, installers and source
stability. The production build remains warnings-as-errors; the suite's ordinary
WheelInput run may report its four already-recorded CS0649 fixture warnings.
Preserve any failure rather than widening the tests automatically.

### Completed local gate, October 13

The first attempt, rc.921, stopped at the shared-startup fixture and released its
lease at 08:39:16 CDT. The wrapper had omitted `ART_SHIM_DLL`; that fixture's
historical fallback is `art-shim-probe-evidence/apply-20261012-021108/dbce_shim.dll`
(`7DDC9621`), whereas production correctly requires `723E8A38`. The focused checks
had supplied the variable. `Test-Rc.ps1` neither sets this variable nor forwards a
shim argument: future repin gates must set it explicitly. No product source was
changed to bypass the hash check. Failure receipt and log are retained under
`results/rc-0.4.2-rc.921-307265fc06ee40379f37d869f6df0f8d`.

The reviewed bounded retry held lease `3252675a267146b69ba096ed24731b14` from
08:42:56 through 08:45:10 CDT. It verified the DLL hash, set the variable only in
the runner child process, and restored that environment value in `finally`.

- rc.922 passed **20 of 20 gates** on clean `76d4c10`, whose runtime and packaging
  inputs are identical to packaged `1d845d8`. The original two-case corpus passed
  215,928 assertions with zero model/device mismatches; the recorded landing
  check passed 9,582 assertions. Receipt:
  `results/rc-0.4.2-rc.922-4fc49cd99362482091248c049417af53/automated.json`, SHA-256
  `51D72BBF6E9FF9A1A20B6FC8ECF49A036A05F38A56AA6957ADAAAE809275F3F9`.
- `Test-Installer` passed **50 assertions** against the fresh exact rc.20
  extraction. Receipt: `results/installer-438c8d49f19849beadce7bb5a0c7c0a4/report.json`,
  SHA-256 `C52FCE6CB5D4F132F3E054E3ED31DE8FEA0176EE10AFF5B9764BF1F2626A832E`.
- `results/rc20-final-gates-retry/receipt.json` and `exit.json` record the result,
  unchanged `8BA67D4E` rc.20 archive and released lease. The throwaway default
  rc.922 archive is not the installation artifact. No device, game, force or
  owner installation occurred in this batch. No additional native or runtime
  sweep was performed.

The concrete receipt wrapper is `results/rc20-install-plan/Install.ps1`, SHA-256
`5AD5243AB2721D473FB58E697B0C67127C9628A0302972833ABBB7DF940EB6EE`.
Its PowerShell syntax check passed. It pins the exact two gate receipts above,
rechecks packaged-source equivalence, and requires a successful scratch rehearsal
using those same receipts, package and wrapper bytes before owner mode can run.
If owner mutation starts but verification fails, it retains the lease and backup
for recovery; pre-mutation refusals and scratch failures can release normally.
Scratch rehearsal and installation are not yet recorded as complete.

### Source integration and owner-state preservation

Canonical `main` is `95203fd`. This branch descends directly from it and adds only
the reviewed target-selection source/tests, the shim hash/provenance metadata and
these notes. Unlike the earlier experimental bridge branch, its new history adds
no shim binary or private native source. The native `5b96ba8` branch and supplied
shim stay private; neither is merged into this public Art repository. No push,
tag, public release or license-status change is part of this local plan.

After review, integrate with `git merge --ff-only codex/art-shared-ffb-target` in
the canonical worktree, only after checking its HEAD/status again. Before and
after, hash the owner's dirty `experiments/CustomVehicles/SESSION.md` and require
the same bytes (planning hash
`42EBDD696A75F695D2655292C180E5E9B7F87F86FCF5A948A8CFF91711F4823F`).
No branch commit touches that path; do not stash, reset, clean or commit it.
Refuse and report if the main branch or dirty path set changed unexpectedly.
The immutable package continues to identify `1d845d8`; later documentation-only
commits do not become its source identity. A gate on a descendant must demonstrate
unchanged runtime/package inputs (`src`, `lib`, `tools/package`, `tools/installer`,
`Version.props`) as the existing rc.18 receipt procedure does.

Installation will reuse the package's existing `install.ps1 -GameDir` after a
scratch rehearsal and review of its receipt wrapper. With the actual shared lease
held and Art closed, recheck the exact ZIP/hash and current rc.18 payloads against
its receipt, then snapshot the current tree before copying. The snapshot covers
all Mods files (including SessionTools and CustomVehicles), game-root files,
Plugins/x86_64, UMM Params, Assembly-CSharp, owner Wheelkit mapping profiles and
the canonical dirty source file. Back up the complete Art mod folder and second
native copy, and verify the backup hashes. Preserve current Settings.xml,
dbce-controls.ini, TripleScreen.xml and desired-layout.json exactly; no profile
Apply or selection/tune edit is part of installation. Refuse the legacy separate
triple-screen folder if it has appeared, since the installer would migrate it.

The verified current-to-package payload delta is five files in Mods/ArtOfSimRally:
`ArtOfSimRally.Mod.dll`, `build.json`, `dbce_shim.dll` and both TripleScreen DLLs.
The latter are rebuilt package dependencies; no triple-screen source change is
part of this slice. The manifest must verify every installed payload and the
second native copy. Both force native copies must remain exactly `44E46799`,
and managed FFB/telemetry and every other protected file must retain their hashes.
Do not install the diagnostic DD301 native or its fixture Settings. Record the
exact changed set, prior/current identities, package/gate receipts, backup paths
and unchanged settings/profile hashes in a new rc.20 install receipt.

Rollback, if needed with the game closed, restores the backed-up mod payloads
and second native byte-for-byte; it does not restore historical owner settings
over newer owner choices. Keep the rc.18 receipt and backup intact. Release the
lease immediately after verification. No game launch is included in deployment.
