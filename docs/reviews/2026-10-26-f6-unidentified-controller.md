# DBC-259: unidentified-controller F6 explanation

Local source candidate, based on release HEAD e2b08f3. Not installed or runtime-qualified.

## Reconciliation

Main's dirty shared-input/settings/package/version files have no content differences with git diff --ignore-space-at-eol. CustomVehicles/SESSION.md has 21 unrelated added lines. No dirty files were copied, reset or stashed. Isolated branch egon/art-f6-refusal-dbc259 lives at E:/Source/games/.worktrees/egon-art-f6-dbc259. Retained handoff 12b69aa is documentation only; its KI-52 capture prerequisite stays on DBC-28.

## Behavior

NativeInputIsolation refuses an empty Rewired instance identity before changing joystick flags. Its existing reason flows through SharedInputStartup.Refuse and Lines to SharedInputStatus. Only Phase.Refused with the exact production prefix `native isolation unqualified: Rewired joystick identity unavailable:` receives three lines, in compact and detailed F6:

> Shared profile was not used for this session because a controller could not be identified. This prevents duplicate wheel input. Art bindings are used.
>
> Restart the game with that controller disconnected. If you use Steam Input, disable it for art of rally before restarting; Steam Input may be the cause.
>
> Details: [original qualification reason]

This follows the documented workaround in docs/LOCAL-DEPLOYMENT.md. Other refusals keep their existing advice. CleanupFailed never claims Art bindings are restored and keeps its existing restore warning. No identity admission, isolation, binding, acquisition, SharedInputStartup state transition, shared toolkit or Wheelkit behavior changed. Exact prefix matching deliberately avoids inferring a cause from arbitrary device names or other failures; changing the producer wording later requires updating this presentation mapping.

## Focused checks

Existing tests/ShimInput production-source fixture: Release net8.0 and net48 both passed 12,354 assertions. Presentation and production-isolation checks cover session fallback, conditional workaround/restart, original diagnostic, compact parity, unrelated refusal and cleanup-failure warning. Existing identity/isolation cases remain in this suite. git diff --check passed.

Initial restore failed because this run environment omitted Windows ProgramFiles(x86)/ProgramData. Supplying those standard paths in the child shell resolved restore; no persistent machine configuration changed.

Release mod build completed on 2026-10-26 with zero warnings/errors and warnings treated as errors. No package build, install, launch, rig/input, rendered F6 or physical device checks performed. The fixture verifies pure status presentation and existing offline identity logic, not Unity/Mono runtime or layout. No public push. Settings, Art bindings and working install preserved.

## Review and QA

Darlene reviewed the exact quoted copy in DBC-260; it was applied verbatim. Sandy granted bounded OFFLINE ADMISSION in DBC-261 for source 5579db95c341ac294df6996e561634e968ac0ae5. This does not authorize runtime or establish user readiness. Future runtime evidence must show this reason on actual F6, readable wrapping at normal/enlarged scale, unchanged fallback bindings, and no Steam Input advice for unrelated failures. Current issue restrictions prohibit performing that runtime here.

Read-only standards comparison: STD-011/012/013/014/016/018/019 remain unchecked; existing partial rows remain open. STD-037/038/039/040 have no adoption rows in this release baseline. This narrow explanation follows STD-040 plain-language intent without claiming full parity/adoption or editing shared standards.

## Admitted source and build provenance

Source: 5579db95c341ac294df6996e561634e968ac0ae5; clean worktree at build start. Documentation reconciliation after this build changes no compiled source.

The existing IsolationTests fixture obtains the wrapped reason from production NativeInputIsolation.Check, reproduces the startup period suffix, and checks compact/detailed presentation, preserved Details, unrelated production refusal and CleanupFailed exclusion. Unity SharedInputStartup is not executed. Prior Release net8.0/net48 results of 12,354 assertions each are retained, not rerun in this documentation/build continuation.

Build: dotnet build src/ArtOfSimRally.Mod/ArtOfSimRally.Mod.csproj -c Release -warnaserror -p:SourceRevisionId=5579db95c341ac294df6996e561634e968ac0ae5 -p:BuildSourceState=clean -p:ArtShimProbe=false

Normal inert probe flavour; version 0.4.3, informational source pinned above. Local game/UMM references are build-only and not deliverables. ProgramData and ProgramFiles(x86) were supplied only to the child shell. No source or machine configuration changed.

Output payload SHA-256 (bin/Release; build outputs only, not a validated release package):

- ArtOfSimRally.Mod.dll: CEB6607C4B9E11F98F96531A3540F2332D9CCC8A3C9C27784A89478196222AFF
- Dbce.TripleScreen.Core.dll: C878FCC903D17E0A27375DA601976240A89BD57A6F90BDA08E9BA52B172A33E5
- Dbce.TripleScreen.Protocol.dll: AF60296AA41105D1B94169330986B28BDA916D15E20CFB17F638FC2E1D4961E5
- Dbce.Wheel.Ffb.dll: 6FC2BE9197D846A477C84E81D39DD420B324BD4B79B30CD54BB78567E270A72B
- Dbce.Wheel.Telemetry.dll: 293D09062B7B1FA9FCB7D48B70A6D8F8BFF2F22577EE90487786800F8D76373F

Runtime remains separately authorized: compact/detailed refusal, normal/enlarged wrapping, preserved Details, fallback bindings after cleanup, unrelated refusal and CleanupFailed warning. Requalify the game-owned capture route against current pins; serialize the existing lease, mute physical outputs, record focus/window/devices/resources and owner-state changes, restore exact owner state and verify owned-process closure. No delivered-input, driving or feel claim; rc.30 acceptance is unchanged; KI-52 stays DBC-28.
