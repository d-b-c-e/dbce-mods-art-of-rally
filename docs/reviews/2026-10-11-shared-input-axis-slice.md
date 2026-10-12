# Art shared input axis consumer and copied bridge

Source-only slice from Art main `7c1b67e4a7be9d7b3e1a6f075088ac62634bed9d`,
branch `codex/art-shim-input`, worktree `games/.worktrees/art-shim-input`.
Canonical owner edit `experiments/CustomVehicles/SESSION.md` stays untouched.
Toolkit pin remains `local+c8ec2ee7fc389b2b3a20be849bc39f1203628f26.clean`.

## Behavior

The production input postfix calls a dormant shared-frame sink after existing
panel/focus suppression. With no attachment/publication it returns false and
runs the complete legacy path unchanged. There is no startup caller, backend
setting, saved binding conversion, packaged native DLL or installation.

An explicitly prepared bridge borrows existing WheelFfb read slots, qualifies
identity/capabilities, and forwards one original physical snapshot per frame to
Core. Those same bytes fill Art's legacy cache; the old reader is not called a
second time. Failure closes the copied provider, clears cache and retains
ownership until restart. Rediscovery and range learning stop only after handoff.

The sink consumes Core's steer/throttle/brake/clutch/analog-handbrake values.
Core remains the controls parser/resolver and status producer. Other shared
actions are declared Unsupported; menus/shifter/shortcuts stay legacy. With a
current native-isolation stamp, keyboard steering works at wheel centre and
pedals/handbrake merge by maximum. Off-centre steering retains alignment. The
stock finishing driver and focus/panel suppression retain their behavior.

Unavailable actions add no shim input. Clean keyboard input stays available.
Malformed frames, changed ownership, repeated/out-of-order frames, wrong thread
and stop cannot replay shared input. Original frame sequence/time/edges/status/
injection flags remain immutable. Test-latched frames and bridge capture/control
flags refuse pending qualified fences and a source recording contract. Accepted
FFB producer, v2 tune and original recordings are unchanged.

The source-only Rewired isolation adapter reads Core's bound instance IDs from
its existing device-report JSON (framework serializer, no Controls parser).
It disables only exact matching joysticks and waits a Unity frame after every
disable/new controller before asserting a native-clean stamp. Whole players,
maps, keyboard and mouse are untouched. Restoration undoes only its own changed
flags and preserves each original enabled value. Unknown joystick identity,
throwing runtime properties, or a Core-bound GUID missing from Rewired refuses
qualification; different RawInput identities are never guessed by name/index.
This adapter also has no production startup caller. Live identity compatibility
and the one-frame cache assumption still require the first muted rig probe.

## Validation

- Release mod and net48/net8 tests build with zero warnings/errors.
- Full legacy WheelInput, sink and production Rewired boundary: 426 assertions pass. All legacy
  checks run before publication; explicit dormant checks preserve all stock
  input fields. Native read/save counters and XML remain unchanged.
- Separate production shared-read fixture: 12 assertions pass: preparation
  does not poll/open, one read/tick per frame, shared exact cache, repeated frame
  retention, failure without fallback/resumption, unchanged settings, single
  native stop and isolation restoration. Idle-rediscovery failure guard passes.
- Decoder/sink base: 352 assertions, independent marshaled C layout. Fake bridge
  adds 10 checks and isolation adds 21; metadata probe adds 35. The prior metadata
  run passed 416 checks on .NET8 before the two final setter-throws-after-write
  restoration assertions were added; the earlier base probe also passed Mono.
- Fake source -> actual candidate DLL -> Core -> Art sink passes .NET8 and
  Unity 2019 Mono (391 each in the final run, count can vary with worker readiness). Fixture uses
  native QPC, preserving the production source clock rather than Mono Stopwatch.
- Native details: [bridge implementation](../research/2026-10-11-shared-input-bridge.md).
- The full default P21 provider-host harness passes 129 supervised fake-COM
  checks, 84 provider checks, teardown lexer/compiler audits and their mutation
  controls at shim `d46096c`. Receipt:
  `E:/Source/research/.worktrees/art-shim-bridge-evidence/p21-20261011/receipt.json`.
  The extra optional host-defect mutation matrix was not run.

Reproduce from this worktree:

```powershell
dotnet build ArtOfSimRally.sln -c Release --nologo -warnaserror
dotnet run --project tests/WheelInput/WheelInput.csproj -c Release
dotnet run --project tests/WheelInput/WheelInput.csproj -c Release -- --shim-read-only
dotnet build tests/ShimInput/ShimInput.csproj -c Release --nologo -warnaserror
dotnet tests/ShimInput/bin/Release/net8.0/ShimInput.dll --bridge E:/Source/research/.worktrees/art-shim-bridge/build-art-bridge/windows/Release/dbce_shim.dll
python tools/testing/Run-UnityMono.py tests/ShimInput/bin/Release/net48/ShimInput.exe -- --bridge E:/Source/research/.worktrees/art-shim-bridge/build-art-bridge/windows/Release/dbce_shim.dll
```

Test-Rc.ps1 adds default fake consumer/bridge and production-read checks to the
existing regression checkpoint. Actual candidate DLL probing remains explicit.
No RC package or full rig checkpoint was produced.

## Activation, migration and rig boundary

Toolkit `ecc49bd` and shim `d46096c` are isolated source candidates for review.
No candidate replaces a vendored/installed artifact. Attachment still needs
muted proof of actual Rewired identity/cached wheel behavior, explicit profile
Apply/status/startup and complete lifecycle policy. Legacy XML remains the rollback
backend. This is a working source bridge, not a complete controls conversion
or a release-ready runtime.

After source boundaries/reviews pass, reserve the rig, close the game and back
up accepted install/settings/raw preferences and original session seals. Install
only the exact reviewed package; begin muted. Prove keyboard/title/menu
coexistence, each identity, five axes, focus/pause/finish, disconnect/reconnect/
restart and bounded stop. Reject duplicate native handles, FFB producer changes
or altered capture meanings. Replay accepted original force requests muted;
physical coexistence needs an owner-attended check. Keep Art v2 tune fixed.

## Standards audit

STD-033/034/035/036 remain partial until activation and live qualification.
STD-012 original recordings/signals stay untouched. Existing partial/unchecked
rows STD-002/003/007/008/009/011/012/013/014/015/016/017/018/019/027 and missing
rows STD-025/026/028/029/030/032 are not upgraded by this slice. STD-031 remains
in the accepted reference-update section.
