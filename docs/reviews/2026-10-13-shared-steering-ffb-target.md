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

## Packaging and next bounded runtime check

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

Proposed runtime scope: one existing `Prepare-SharedInputCopy` /
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
five additional checks passed in the packaging batch. No runtime run is authorized by
this recipe. Normal physical FFB acceptance remains owner-attended.

### Test-only native compatibility and runner proposal

The previously qualified muted native from toolkit `82c40c4` is available at
`toolkits/.worktrees/art-shim-snapshots/native/wheelffb/build/WheelFfb.dll`, SHA-256
`DD3013147821364AD8795F76B4500FB479F669E6BD89576F83E725A677273414`.
Its `DBCE_FFB_MUTE=1` load-time latch independently refuses force entry points.
It is proposed only for the disposable diagnostic; the production package and
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
`results/fresh-shared-only-rc20-plan/Invoke-FreshSharedOnly.ps1`; it has not run.
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
