# Managed shim ABI 2

## Optional declared extras (source candidate)

`ShimExtrasNative` binds extension 2 of the same hash-pinned module. Missing
exports or a version/size mismatch leave `ShimNativeLibrary.Extras` null, with
`ExtrasUnavailable`; the legacy exports remain available. This does not adopt a
new native DLL pin for any game. The unchanged native header is authoritative.

An opted-in consumer opens **one** host with `Extras.Open`, passing the exact
complete existing `dbce-controls.ini` and its ASCII game declaration. Verify
`ProfileInfo` against those complete bytes with `ShimProfile.Verify`, then verify
`Extras.Info` with `ShimExtrasInfo.Verify` against the game's exact declaration,
expected shapes and context mode. Native `firstSlot`/`components` supply the
mapping; the wrapper does not parse bindings or derive slots by counting lines.

`ShimExtrasSession` ticks that handle once per engine frame, returning common
and extras frames with the same sequence. The caller passes its observed context
mask (0 when unknown), uses fresh samples only, invalidates on lifecycle changes,
and supplies one stop callback on the owning thread. Do not call ordinary Tick
on this handle or open another host for common actions. A post-open verification
failure must spend/stop that handle before considering any fallback; an unresolved
stop never authorizes a second reader. Before-open optional-extension refusal can
use the ordinary route. Extras do not support latched injection or recording.

The added pure fixtures cover coherence, optional-export/version refusal,
declaration/slot/count checks, context loss, stale sequences, invalid frames,
invalidation, deduplication and exactly-once stop. At source commit `0c57ae4`,
the affected contract/extras selection passed **22/22** actual TRX cases under
the October 15 named offline batch. Durable logs and receipts are in the hub's
`artifacts/truckful-camera-extras-offline-20261015`. This batch used fake frames
and export refusal; no native DLL was loaded, and no device or runtime
qualification is implied.

`Dbce.Wheel.Shim` is the game-neutral managed side of
`native/shim/shim_dll/dbce_shim.h`. It supplies the 31-action table, status and
frame decoding, exact `[Controls]` byte extraction, Core hash verification,
steering binding text after Core acceptance, and an x64 full-path,
consumer-hash-pinned native export loader. It targets .NET 6
for BepInEx IL2CPP consumers and has no game, Unity or loader dependency.
The extraction follows Woden's `WodenRallyEdge.Core/ShimActionMap.cs` and
`ShimProfile.cs`, and `WodenRallyEdge.Plugin/ShimNative.cs` at game commit
`0565f392`; Woden remains unchanged and keeps its pinned production copy until
a separate consumer adoption is reviewed.
Art of Rally's copied-provider bridge uses a separate ABI variant and is not
covered by this loader.

`ShimSnapshot.cs` decodes extension 3 (`native/shim/shim_dll/dbce_shim_snapshot.h`,
snapshot ABI 1): Core's device table and the device reads of one ticked frame,
raw values only, for in-game binding capture without a second reader. The three
exports are optional (an older DLL means no in-game capture): `ShimNativeLibrary`
binds them and reports `SnapshotUnavailable`, and a consumer with its own loader
checks them with `ShimSnapshotAbi.Validate`. The consumer calls `ShimSnapshot.Take`
with the seq of the frame its last successful `Tick` returned, right after that
tick and before the next, and checks the returned `FrameSeq`; it takes snapshots
only while its binding editor is editable, including before a capture begins, so
a fresh device can be chosen. `CaptureValues` applies the device caps for the
shared capture detector.

`ShimSnapshotFeed.cs` holds the shared per-row rules once: a row that is not
opened, or a read that ended or belongs to another epoch, is removed; an
explicitly invalid or stale read is removed at once; a row without a matching
identity report is removed; a valid fresh read becomes an observation; a row with
no read in the frame ages out. An already-read frame seq yields nothing.
`SharedBindingFrameGuard` keeps the wheel's menu and mod actions from dispatching
in any frame a capture listened in, including the frame whose own tick completed
or refused it; the host notes the frame before it takes or refuses a snapshot.
Both files are portable C# (no records, spans or `BinaryPrimitives`) so .NET
Standard 2.1, .NET Framework 4.8 and Mono consumers can vendor them byte-exact.

`ShimCandidateNative.cs` binds the optional first-device candidate, extension 4
(`native/shim/shim_dll/dbce_shim_candidate.h`; native README "First-device
capture candidate"). It is the same portable C#.

- **Binding.** `TryBind` takes the host's own export resolver over the module
  it already loaded and hash-checked. An older DLL gives null with the reason,
  and the host then offers no first-device route.
- **Calls.** `RequestList`, `Select(instance, product)`, `Release` and
  `Report`. `Report` returns the JSON, which the host parses with its existing
  reader.
- **Snapshot.** `SnapshotCallFor(handle)` feeds `ShimSnapshot.Take`. The
  candidate's one-row snapshot uses extension 3's structs. Decode it into its
  own `ShimSnapshotFeed` and capture flow, with its own epoch namespace, never
  the ordinary snapshot's.
- **Owner-thread rules.** List and select only on an explicit user action, on
  the owner thread right after the ordinary tick. Take the candidate snapshot
  every editable frame, which renews its 2 s lifetime. Release on panel close,
  focus loss or a non-ordinary frame. Never deliver candidate values.

The consumer supplies its reviewed DLL hash and verifies its installed receipt,
game build, device isolation and keyboard-only native residual before admitting
shared input. It owns the game-input thread for every handle call, and it must
check `Tick`'s result before decoding the returned frame and stop on that thread.
A resolved or unresolved native stop spends the handle;
the module deliberately remains loaded until process exit because an unresolved
worker may still need its code. Game-specific action delivery and FFB selection
stay in the consumer repo.

`Open`/`Open2` success creates the native host, not an adopted device. The consumer
must keep an opening state and call `Tick` on its input thread before consulting
`DeviceReports`. The worker's opened handles enter Core only at a tick; polling
reports alone cannot make them appear. Keep game-facing output neutral until the
required device identities and isolation are verified, use a bounded opening
deadline, and continue to honor each action's status/rearm afterward. Do not reject
a connected wheel merely because the first post-Open report says unopened.
The [native startup contract](../../native/shim/shim_dll/README.md#opening-is-asynchronous-tick-before-admitting-input)
describes the sequence and its fake-device fixtures. This loader does not drive
the game loop or implement that consumer state machine.

`Open2` mirrors the low-level developer capture/latched ABI. The caller owns
every pointer in `ShimOpenArgs` through the call and must independently enforce
its muted-force and run fences. This library does not arm those developer modes.

The tests compare the action names and constants with the canonical native
headers, check the 128-byte argument layout and 296-byte frame offsets, exercise
profile byte/hash handling and changed-export refusals, and prove a bad DLL pin
is rejected before loading. After a Release build, run
`dotnet/Dbce.Wheel.Shim.Tests/Smoke-Load.ps1 -DllPath <reviewed x64 DLL> -Sha256 <reviewed pin>`
to validate the actual exports and static action-name pointers through this
loader. The smoke calls `Load` only; it does not open a device or qualify a game
adapter.
