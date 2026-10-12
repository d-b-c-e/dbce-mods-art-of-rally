# Art copied-input compatibility probe (source candidate)

This is a temporary **menu-only, no-force diagnostic**, not a backend release
or a production profile Apply workflow. Normal launches do not read its request,
construct the shim, change Rewired flags, or parse a profile. The saved owner
backend and accepted v2 FFB settings/reference recordings stay unchanged.

The first runtime question is whether the **same exact instance identity** from
Core and an already-open WheelFfb read slot exists in Rewired. DRIVE has observed
different RawInput and DirectInput instance GUIDs for the same HID path. Art
refuses that case. The receipt includes Rewired's public RawInput extension HID
path/product GUID as diagnostic evidence; it never uses a name, product alone,
array index, or an unproven path mapping to authorize isolation.

## Source behavior

An explicit child-process `DBCE_ART_SHIM_REQUEST` selects Observe or Apply. The
request pins the exact mod/native/shim/profile/Settings bytes and the disposable
game root, expires within 30 minutes, and writes to a fresh result directory.
Invalid startup requests refuse the mod load; identity/reader/isolation refusal
after arming retains Native input. No saved backend field is introduced.

Before custom FFB initialization, the exact resident native module arms its
existing irreversible `EnableTestInjection(1)` **no-force latch**. No injection
commands are sent. Settings writes/axis learning/assignment are disabled in this
diagnostic process; the menu watchdog skips force recovery, telemetry sender
creation, input-backend switching and persistence work. Native logging goes to
the result directory. Native force exports remain blocked until process exit.
This cannot qualify coexistence with the normal exclusive FFB handle: that
handle is deliberately never created during this probe.

Observe uses shared Core's original parser, qualifies existing reader metadata
without another physical read, snapshots Rewired, and temporarily disables only
the exact matched joystick objects. After the next Unity frame it records raw
axes and restores only flags it changed. Results are independent:

- exact Core/reader/Rewired GUID correspondence;
- disabled flags and exact restored flags, including untouched controllers;
- whether all mapped raw axes read zero after disabling;
- whether an **already nonzero** raw axis (a pedal can rest at -1) became zero.
  If every axis was already zero, cache clearing is explicitly inconclusive.

Unknown GUIDs, throwing identity APIs, changed inventories, mismatched RawInput
GUIDs and absent owned readers refuse. Raw-axis API failure does not erase the
separate identity/flag facts. It prevents Apply. No owner motion or OS input is
requested. The probe does not change players, maps, keyboard or mouse.

Apply is an optional second **diagnostic process** with a prior matching Observe
receipt. It requires exact GUID/disabled/restored flags and neutral raw axes,
then repeats the current checks before attaching. A neutral-at-rest receipt is
accepted while its nonzero cache transition remains explicitly inconclusive.
It publishes 120 menu frames through the real copied provider and Art sink, then
stops/restores. After the first accepted frame, failure or completion makes the
source consumer retain neutral ownership until exit; restart to return to ordinary
input. Failure before the first accepted frame leaves the consumer unstarted and
retains Native input.
This proves neither car input, keyboard operation, menus, physical FFB nor full
multi-device action parity. The receipt reports last axis statuses and values;
`PublishedFrames=120` alone does not mean every bound axis became Live.

## Copy-only preparation

Build the isolated Art source and use the reviewed native candidates:

```powershell
$art = 'E:\Source\games\.worktrees\art-shim-input'
$native = 'E:\Source\toolkits\.worktrees\art-shim-snapshots\native\wheelffb\build\WheelFfb.dll'
$shim = 'E:\Source\research\.worktrees\art-shim-bridge\build-art-bridge\windows\Release\dbce_shim.dll'
$owner = 'D:\Program Files (x86)\Steam\steamapps\common\artofrally'
# Choose fresh result/copy paths and an existing shared Core profile.
$copy = 'E:\Source\games\.worktrees\art-shim-probe-game'
$result = 'E:\Source\games\.worktrees\art-shim-probe-observe-01'
$profile = '<exact existing shared Core Controls .ini>'
& "$art\tools\testing\Prepare-ShimProbe.ps1" -Result $result -SourceGame $owner -GameCopy $copy `
  -Settings "$owner\Mods\ArtOfSimRally\Settings.xml" -Profile $profile `
  -NativeCandidate $native -ShimCandidate $shim -Mode Observe
```

The helper copies only into `$result`, records hashes/source state, and generates
the request and a recipe. It does **not** copy into either game, install, start a
process, set persistent environment variables, or open hardware. Do not substitute
the pinned accepted DLL for the additive snapshot candidate: missing optional
exports must refuse.

## Serialized muted rig slot (not run by preparation)

1. Obtain the coordinator's rig slot. Confirm all racing games/probes closed.
   Snapshot/hash the original mod payload and Settings, normal native log,
   UMM configuration, and existing game preferences using the established Art
   test restoration procedure. A copied Unity game still shares PlayerPrefs,
   LocalLow logs and Steam integration; **copying files alone is not isolation**.
   Also snapshot and restore the existence/bytes of
   `%LOCALAPPDATA%/DBCE/TripleScreen/games/art-of-rally/status.json` and
   `render-diagnostics.json` in that same folder: the normal triple-screen
   publisher still runs in the copy. Remove only a probe-created shared file
   that was absent before; otherwise restore its exact original bytes.
   Keep Steam offline/local and preserve the existing prefs/logs before launch.
   Do not enter a stage, change controls, invoke Steam's normal Play, or run
   Wheelkit Apply. Use no optional recorder/injection markers or extra native
   FFB modules in the disposable copy.
2. Create a fresh non-junction `$copy` using `robocopy $owner $copy /E /XJ`
   (success exit codes 0–7). No `/MIR`, deletion or owner-file replacement. Copy
   `$result\payload\ArtOfSimRally\*` to the copy's `Mods\ArtOfSimRally` only.
   Confirm the copied Settings hash equals the source hash. Ensure UMM is active
   in the copied game and optional other mods are disabled in that copy only.
   The latch verifies exactly one resident `UnityForceFeedback.dll`/`WheelFfb.dll`;
   another loaded copy refuses because it would have an independent force gate.
   Do not fix such a refusal by weakening the check or touching owner DLLs.
3. Use a child-only environment, not a user/system variable. This launch example
   is for the coordinator's already-authorized serialized slot, not preparation:

   ```powershell
   $start = New-Object Diagnostics.ProcessStartInfo
   $start.FileName = Join-Path $copy 'artofrally.exe'
   $start.WorkingDirectory = $copy
   $start.UseShellExecute = $false
   $start.EnvironmentVariables['DBCE_ART_SHIM_REQUEST'] = Join-Path $result 'request.json'
   $process = [Diagnostics.Process]::Start($start)
   ```

4. Remain in menus. Read `$result\probe-receipt.json` and the copied UMM log.
   Refusal is a useful result: retain Native and report exact GUID/path evidence.
   Observe should restore promptly. Never infer a HID path bridge from labels.
   If there is no receipt by the request deadline, report startup/log evidence
   and exit normally; do not keep retrying or inject input. The game can kill
   itself on normal Quit, so verify the receipt **before** exiting.
5. If Observe independently qualifies, prepare a **new** result with `-Mode
   Apply -ObserveReceipt <prior receipt>` and identical artifact/profile/settings
   hashes for another cold process. The first receipt alone does not authorize
   promotion or replacing the normal install. Retain all outputs and compare
   owner payload/Settings hashes; restore shared prefs/logs with the established
   harness and confirm the owner backend was never changed.

An additive owned-slot `DIPROP_GUIDANDPATH` query with generation checking is the
smallest possible next native seam if the GUID namespaces differ. That mapping
is not implemented or qualified by this probe. Base ABI 2 and snapshot extension
1 remain unchanged; no old export is removed.
