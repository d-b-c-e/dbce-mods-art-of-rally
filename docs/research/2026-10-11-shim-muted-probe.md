# Art copied-input compatibility probe (source candidate)

This is a temporary **menu-only, no-force diagnostic**, not a backend release
or a production profile Apply workflow. Normal launches do not read its request,
construct the shim, change Rewired flags, or parse a profile. The saved owner
backend and accepted v2 FFB settings/reference recordings stay unchanged.

The first runtime question is whether each Core-bound DirectInput identity has
the **same exact owned HID interface path and product GUID** in Rewired. DRIVE
has observed different RawInput and DirectInput instance GUIDs for that device.
The source now retains both GUID namespaces and joins them through the toolkit's
generation-checked `DIPROP_GUIDANDPATH` query on the existing handle. It never
uses a name, product alone, array index, shortened collection path or direct
GUID coincidence to authorize isolation. This join has fake-boundary tests;
actual Art driver/API behavior remains unqualified until the muted probe.

## Source behavior

An explicit child-process `DBCE_ART_SHIM_REQUEST` selects Observe or Apply. The
request pins the exact mod/native/shim/profile/Settings bytes and the disposable
game root, expires within 30 minutes, and writes to a fresh result directory.
Invalid startup requests refuse the mod load; identity/reader/isolation refusal
after arming retains Native input. No saved backend field is introduced.

The runner sets child-only `DBCE_FFB_MUTE=1`. The reviewed native candidate
latches its existing irreversible **no-force state during DLL_PROCESS_ATTACH**,
before any force export can run, even if managed request validation or mod load
later refuses. Only the exact value `1` activates this fence; its loader path
does no logging, locking, device, effect or hook work. Both copy-only native
locations must have these candidate bytes. Before custom FFB initialization,
the exact resident module also confirms `EnableTestInjection(1)` and status.
No injection commands are sent. Settings writes/axis learning/assignment are disabled in this
diagnostic process; the menu watchdog skips force recovery, telemetry sender
creation, input-backend switching and persistence work. Native logging goes to
the result directory. Native force exports remain blocked until process exit.
This cannot qualify coexistence with the normal exclusive FFB handle: that
handle is deliberately never created during this probe.

Observe uses shared Core's original parser, qualifies existing reader metadata
without another physical read, snapshots Rewired, and temporarily disables only
the exact matched joystick objects. After the next Unity frame it records raw
axes and restores only flags it changed. Results are independent:

- Core/reader GUID and product correspondence, exact owned HID path/product
  correspondence with Rewired, and both GUID namespaces separately;
- disabled flags and exact restored flags, including untouched controllers;
- whether all mapped raw axes read zero after disabling;
- whether an **already nonzero** raw axis (a pedal can rest at -1) became zero.
  If every axis was already zero, cache clearing is explicitly inconclusive.

Unknown GUIDs/paths, throwing identity APIs, changed inventories, mismatched HID
paths/products and absent owned readers refuse. Raw-axis API failure does not erase the
separate identity/flag facts. It prevents Apply. No owner motion or OS input is
requested. The probe does not change players, maps, keyboard or mouse.

Art's shipped `Rewired_Windows.dll` (SHA-256
`4389BCA4D00D68B600972FCD2A0D5A21208517DF7893A6A4602E740D6691417C`)
returns an empty path and product GUID when `joystick.enabled` is false. Static
getter inspection confirms that the controller/extension object, persistent
GUID and `isConnected` remain readable. Consequently the probe admits the full
exact live path/product join **before** disabling, then retains that explicit
identity witness only for its own changed, still-disabled object. Every use
requires the same complete object inventory, extension reference, persistent
GUID, connected state and connection-event epoch; any connect, pre-disconnect,
disconnect, initialization or shutdown event permanently invalidates the lease.
Even a disconnect/reconnect between frames refuses. The owned reader's complete
144-byte metadata, generation, path and slot set are also rechecked without a
physical read. Enabled objects always require fresh public path/product values.
After restoration, another fresh exact join is required (`IdentityRestored`).
The receipt's disabled path/product fields remain the actual empty getter values;
the captured witness is explicitly described in `IdentityQualification` and is
never represented as a live public path read.

Apply is an optional second **diagnostic process** with a prior matching Observe
receipt. It requires matching Core/RawInput identities and HID paths, exact
disabled/restored flags and neutral raw axes,
then repeats the current checks before attaching. It also requires the Observe
receipt's fresh restored identity join. A neutral-at-rest receipt is
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
   Inventory and restore all LocalLow game files recursively, including `cloud`,
   `Unity` and `Screenshots`, removing only new probe-created files after retaining
   their evidence. Also snapshot and restore the existence/bytes of
   `%LOCALAPPDATA%/DBCE/TripleScreen/games/art-of-rally/status.json` and
   `render-diagnostics.json` in that same folder: the normal triple-screen
   publisher still runs in the copy. Remove only a probe-created shared file
   that was absent before; otherwise restore its exact original bytes.
   Preserve the existing Steam session and shared prefs/logs before launch.
   Never answer a Steam `KickingOtherSession` prompt or restart the owner's client.
   Do not enter a stage, change controls, invoke Steam's normal Play, or run
   Wheelkit Apply. Use no optional recorder/injection markers or extra native
   FFB modules in the disposable copy.
2. Create a fresh non-junction `$copy` using `robocopy $owner $copy /E /XJ`
   (success exit codes 0–7). No `/MIR`, deletion or owner-file replacement. Copy
   `$result\payload\ArtOfSimRally\*` to the copy's `Mods\ArtOfSimRally` only.
   Also place the same candidate native bytes in the copy's
   `artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll`. Confirm the copied
   Settings hash equals the source hash. Ensure UMM is active in the copied game;
   omit optional other mod folders using robocopy `/XD` on their exact source paths.
   The latch verifies exactly one resident `UnityForceFeedback.dll`/`WheelFfb.dll`;
   another loaded copy refuses because it would have an independent force gate.
   Do not fix such a refusal by weakening the check or touching owner DLLs.
3. Before direct launch, write `550320` to **the copy's** `steam_appid.txt` using
   ASCII, with the copied executable directory as its working directory. Without
   this file Art has been observed restarting through Steam into the owner install
   and losing the child environment; see `docs/FORCE-FEEDBACK.md`. The development
   app-ID file suppresses `SteamAPI_RestartAppIfNecessary` according to Valve's
   [Steamworks startup contract](https://partner.steamgames.com/doc/sdk/api#initialization_and_shutdown).
   Do not put that file in the owner installation. Use the runner only in the
   coordinator's already-authorized serialized slot:

   ```powershell
   & "$art\tools\testing\Invoke-ShimProbeCopy.ps1" -Result $result
   ```

   It checks candidate pins and optional-mod absence, snapshots shared bytes and
   raw PlayerPrefs with the existing `SessionEnvironment.ps1`, holds the shared
   rig lease, and launches with child-only request/`SteamAppId`/`SteamGameId`
   variables plus the native load-time mute. It repins immediately before launch
   inside the lease and retries image-path observation for at most five seconds.
   The exact child path and start time are checked before normal close.
   After a receipt or 90 seconds it asks that child to close its main window,
   without keyboard/mouse input or a forced kill. If any Art process remains,
   backup and lease are retained for recovery. Successful restoration produces
   `restored.txt`; source payload hashes are checked without rewriting the owner
   installation. The copied UMM log and `after` shared-file copies remain evidence.
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

The required source candidates are toolkit `82c40c4` (owned path `64ccf67` plus
load-time no-force latch) and copied shim `574bb88`
(includes the shared owned-path report change as `1e88feb`, equivalent to
`f49cfc5`). The new optional native query and atomic copied-provider paths export
do not change base ABI 2 or the 144/232-byte extension 1 structs; no old export
is removed. Art freezes metadata/generation/path through the isolation handoff
and rechecks the exact Rewired path/product/GUID join during the bounded Apply
probe. It still has no production backend activation or physical qualification.

Load-time fence validation: x64/x86 fake-COM suites each pass 44 new checks,
including all force families and external-session entry without touching their
fake device, plus the previous 111 injection/lifecycle/race checks. The actual
DLL reports armed **before** `EnableTestInjection` on both .NET and game Unity
Mono (419 assertions each); clearing the child environment does not clear the
latch. All 62 exports remain present, version 1000 and snapshot ABI unchanged.
These are offline/source checks; no physical force was requested.
