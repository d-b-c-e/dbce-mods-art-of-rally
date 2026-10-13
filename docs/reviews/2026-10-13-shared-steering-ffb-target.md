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

After the coordinator dispatches the next build slot, commit the pin first and
package clean source using the existing script (no standalone helper changes):

```powershell
./tools/package/package.ps1 -Version 0.4.2-rc.20 -SharedInputCandidate `
  -SharedInputShim E:/Source/research/.worktrees/art-shim-profile-info/build-profile-info/windows/Release/dbce_shim.dll
```

SessionTools already uses rc.19; the mod candidate uses rc.20 to avoid ambiguity.
The script checks the exact shim/native hashes, metadata, release assembly,
probe flavor and payload manifest. Verify the ZIP and extracted payload hashes;
run the targeted production startup fixtures with `ART_SHIM_DLL` pointing at the
new pinned DLL. No install or launch is part of packaging.

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
The runtime route remains unresolved; do not launch or silently replace WheelFfb.

Pure target inspection runs before qualification even with force off. A separate
startup-only trace now records the configured steering instance or actual refusal
and the SHA-256 of the frozen profile bytes, before entering Waiting. The line
explicitly states that connection and force are unverified; explicit FFB overrides
retain priority. New fixture checks cover one-time logging before qualification,
the exact hash, no repeat after attachment and all three refusal cases. These
five additional checks await the packaging batch. No runtime run is authorized by
this recipe. Normal physical FFB acceptance remains owner-attended.
