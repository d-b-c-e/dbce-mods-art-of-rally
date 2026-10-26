# DBC-259: unidentified-controller F6 explanation

Local source candidate, based on release HEAD e2b08f3. Not installed or runtime-qualified.

## Reconciliation

Main's dirty shared-input/settings/package/version files have no content differences with git diff --ignore-space-at-eol. CustomVehicles/SESSION.md has 21 unrelated added lines. No dirty files were copied, reset or stashed. Isolated branch egon/art-f6-refusal-dbc259 lives at E:/Source/games/.worktrees/egon-art-f6-dbc259. Retained handoff 12b69aa is documentation only; its KI-52 capture prerequisite stays on DBC-28.

## Behavior

NativeInputIsolation refuses an empty Rewired instance identity before changing joystick flags. Its existing reason flows through SharedInputStartup.Refuse and Lines to SharedInputStatus. Only Phase.Refused with the exact existing identity-reason prefix receives three lines, in compact and detailed F6:

> Shared profile not used for this session: a controller could not be identified, so the mod cannot rule out duplicate wheel input. Art bindings are used.
>
> Start the game without that controller connected, or, if you use Steam Input, disable it for art of rally, then restart the game. Steam Input is one possible cause.
>
> Details: [original qualification reason]

This follows the documented workaround in docs/LOCAL-DEPLOYMENT.md. Other refusals keep their existing advice. CleanupFailed never claims Art bindings are restored and keeps its existing restore warning. No identity admission, isolation, binding, acquisition, SharedInputStartup state transition, shared toolkit or Wheelkit behavior changed. Exact prefix matching deliberately avoids inferring a cause from arbitrary device names or other failures; changing the producer wording later requires updating this presentation mapping.

## Focused checks

Existing tests/ShimInput production-source fixture: Release net8.0 and net48 both passed 12,348 assertions. Added six checks cover session fallback, conditional workaround/restart, original diagnostic, compact parity, unrelated refusal and cleanup-failure warning. Existing identity/isolation cases remain in this suite. git diff --check passed.

Initial restore failed because this run environment omitted Windows ProgramFiles(x86)/ProgramData. Supplying those standard paths in the child shell resolved restore; no persistent machine configuration changed.

No full mod/package build, install, launch, rig/input, rendered F6 or physical device checks performed. The fixture verifies pure status presentation and existing offline identity logic, not Unity/Mono runtime or layout. No public push. Settings, Art bindings and working install preserved.

## Review and QA

Darlene must review the quoted player text before user-ready claims. Sandy must admit any future QA; the requested QA review is planning/admission only and does not authorize rig/input/launch/install. Future runtime evidence must show this reason on actual F6, readable wrapping at normal/enlarged scale, unchanged fallback bindings, and no Steam Input advice for unrelated failures. Current issue restrictions prohibit performing that runtime here.

Read-only standards comparison: STD-011/012/013/014/016/018/019 remain unchecked; existing partial rows remain open. STD-037/038/039/040 have no adoption rows in this release baseline. This narrow explanation follows STD-040 plain-language intent without claiming full parity/adoption or editing shared standards.
