# Art copied-input candidate: source passed, runtime startup blocked

The disposable Observe attempt on October 11 at 23:58 local did not reach the
mod probe. The exact copied executable started with the expected `art of rally`
window title, then its Unity log reported `SteamAPI_Init()` failure and a
Steamworks-not-initialized exception through `PlatformSteam.IsDLCInstalled`,
`CarManager.Init` and `EntryPoint.Start`. There was no new UMM log, native probe
log or `probe-receipt.json`. The underlying Steam/session cause is unknown.

The exact child was asked to close its main window and exited normally. By
23:59:24 local no Art process remained and the rig lease was released. The runner
restored 58 raw PlayerPrefs values and verified 29 shared files and 44 owner
files with zero mismatches. The shared inventory included the whole LocalLow
game tree (`cloud`, `Unity`, `Screenshots`) and both Art triple-screen status
files. No keyboard/mouse or injection command was sent, no Steam prompt was
answered, and no saved backend/profile/tune or owner mod payload was changed.

Evidence (local, retained):

- `E:/Source/games/.worktrees/art-shim-probe-evidence/observe-20261011-235433/sealed-result.json`
- same directory: `startup-player.log`, `launch.json`, `early-close.json`,
  `staged.json`, `offline-validation.json`, `shared-files.json`,
  `shared-environment/state.json`, `restored.txt`, and retained `after/` bytes.
- disposable game: `E:/Source/games/.worktrees/art-shim-probe-game-20261011`.

## Exact candidate

| Component | Source commit | Built SHA-256 |
|---|---|---|
| Art | `9f90df9384c8b7f545ddf5f99822fd4421c37790` | `38641A00CB521BB73BAFA36E22EC0C4C8FEA5FDF661134001EE1ABF1D4F91B4E` |
| Toolkit snapshot/path/load-time mute | `82c40c43d76db3076695aa25f2850b5aa113a351` | `DD3013147821364AD8795F76B4500FB479F669E6BD89576F83E725A677273414` |
| Shared copied provider | `574bb8827098779257bd08a89038c787403d15b0` | `7DDC962132819EDAE76EF84CE6509F1936B8C38177A36D498B8ABBCFDCFFBCAF` |

The candidate native file appeared only in the copy's mod and x64 plugin
locations. Child-only `DBCE_FFB_MUTE=1` selects the native load-time fence even
if managed initialization refuses. Its behavior was proved offline, not by a
live Art probe receipt in this attempt. Original native version 1000, base shim
ABI 2, the 144/232-byte copied structs and all old exports remain unchanged.

Accepted owner native SHA-256 remains
`F8361A08BBE08EAAB936B37800D308CF1FC7EDAB34E2839DB16A447FE34178C5`;
accepted Settings remains
`91090837181C1D617F3C0EE52F1DE797A2592C6FEA4C7B2D8E93138318EE36ED`.
Art's vendored toolkit pin remains `local+c8ec2ee7fc389b2b3a20be849bc39f1203628f26.clean`.
The accepted FFB producer, impact controller, Settings source and toolkit payloads
are unchanged from Art baseline `7c1b67e`.

The input snapshot was the exact owner-applied DRIVE
`BepInEx/config/dbce-controls.ini`, SHA-256
`5206EDFBD1B400E5D6A41D91AE836485BBE27D63992AD6F7483B3BABB0E72B79`.
It supplies shared generic actions; it does not qualify all Art mappings.

## What passed and what remains unknown

The clean Art rebuild passed with zero warnings/errors. Actual copied-provider
tests passed 472 assertions on .NET and 473 on shipped Unity Mono; actual native
load-time mute tests passed 458 on each. Native fake-COM tests passed 155 checks
on each of x64 and x86, including all force families/external-session refusal
before fake device calls. Shim P21 source verification passed for `574bb88` at
`E:/Source/research/.worktrees/art-shim-bridge-evidence/p21-path-20261011/receipt.json`.
Independent source review passed the bounded diagnostic and identity lease.

Static inspection proved Art's public RawInput path/product getters become empty
while disabled. The candidate preserves an admitted identity witness only while
object, extension, persistent GUID, connected state, event epoch and native owned
metadata/generation/path remain unchanged. Raw receipt fields stay raw; reconnects
invalidate the witness and restored identity requires a fresh live exact join.
These boundary tests do not replace the missing Art runtime facts.

No Art runtime identity join, disable/restore, raw cache clearing, copied-provider
frame, keyboard operation, car action, physical FFB coexistence, performance or
rendered acceptance was established. No Apply probe ran. Normal launches remain
on the owner's existing backend; there is no production activation UI or saved
shared-backend setting.

## Next integration work

1. Resolve the observed Steam startup failure in the coordinator's authorized
   window. Do not infer a remote-session cause or answer Steam prompts. Prepare
   a new expiring request from the reviewed artifacts; the old nonce is consumed.
   Run Observe using `tools/testing/Invoke-ShimProbeCopy.ps1`, preserve state, and
   retain separate identity, flag and raw-axis facts. Existing nonzero resting
   pedals can prove a cache transition without requested physical motion.
2. Review a successful Observe receipt before a second cold, muted Apply process.
   Verify `IdentityRestored`, flags and actual raw-neutral observations. Inspect
   every bound action status during its 120 menu frames; frame count alone is
   not Live input or full mapping parity.
3. Once runtime evidence supports it, expose ordinary explicit backend/profile
   Apply in Art's consumer (`Main.cs`, `WheelInput.Shim.cs`, `ShimStartup.cs` and
   its settings UI) using the existing shared profile/parser. Keep temporary
   probe receipt handling out of the permanent product workflow. Preserve an
   explicit Native choice and never migrate the owner's saved backend silently.
4. Adopt reviewed optional native exports through a deliberate toolkit pin
   update. Continue using the existing WheelFfb-owned read slots; do not give the
   shared Core another DirectInput device owner. Revalidate attach/detach,
   disconnect/reconnect, original timestamp/sequence/provenance, input-cache
   neutral ownership, and the exact Rewired lifetime checks. Measure the bounded
   identity verification cost before deciding its ordinary-runtime scheduling.
5. Qualify Art actions and independent keyboard behavior, then obtain the
   separately authorized attended test of normal exclusive FFB/read-slot
   coexistence with the accepted FFB producer/reference unchanged. The current
   muted candidate deliberately cannot establish that physical behavior.
