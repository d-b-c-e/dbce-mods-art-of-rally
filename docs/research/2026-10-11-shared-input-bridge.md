# Shared input bridge for Art's existing WheelFfb handle

Implemented source candidates, not installed or activated:

| Layer | Isolated branch / commit | Source review |
|---|---|---|
| Toolkit snapshots | `codex/art-shim-snapshots` / `ecc49bd`, base `25195af` | `docs/READ-SNAPSHOT-BRIDGE.md` |
| Shared shim provider | `codex/art-shim-bridge` / `d46096c`, base `a9e1601` | `windows/docs/COPIED-INPUT-BRIDGE.md` |
| Art adapter | `codex/art-shim-input`, base `7c1b67e` | [axis slice review](../reviews/2026-10-11-shared-input-axis-slice.md) |

## Ownership and provenance

Art's OpenReadDevice borrows the selected wheel's FFB handle. The shared default
DInputProvider opens its own readers and cannot replace that route without
violating single ownership. The new bridge owns only copied memory. No COM
pointer or managed callback crosses its ABI; it creates/acquires no device.

Toolkit exports GetReadDeviceInfoV1 and ReadDeviceSnapshotV1 qualify an already
open slot and read it once. Metadata records actual instance/product GUIDs,
sparse object/range evidence and a globally increasing handle generation.
Samples retain raw buttons, validity, HRESULT/classified Poll and state results,
original QPC start/end and sequence. Qualify in idle preparation; failed reads
invalidate the generation. The owner recovers. Native version 1000 and all old
exports stay unchanged pending a reviewed/versioned distribution candidate.

Shim extension 1 uses byte-compatible 144-byte metadata and 232-byte samples;
base ABI 2 is unchanged. Bridge ticks copy batches, reject old generations,
sequences and future times, and mark omissions Pending. Core's optional
Provider::source_read retains original source generation/sequence and physical
read age: a quick memory copy cannot invent freshness. Ordinary DInput providers
retain their original measured timing. Core remains the only profile parser,
resolver, rearm and status producer. Bridge capture/test-control flags refuse
until source provenance has a versioned recording format and qualified fences.
Existing Art recordings and original producer signals are never rewritten.

## Art wiring and limits

ShimBridgeNative binds exact paths using Unity Mono-compatible APIs and checks
ABI sizes/action order before bridge open. Exact profile bytes go to Core.
ExistingWheelSnapshots binds optional exports of the owner's loaded WheelFfb
path. Old accepted DLLs lack these exports and cannot activate this path.

WheelInput.AttachSharedInput is an internal idle handoff with **no production
caller**. It borrows existing slots; each physical sample feeds Core and Art's
legacy cache without another old-reader call. After handoff, range learning and
automatic rediscovery stop. Failure/close retains bridge ownership, clears cache
and requires restart. There is no backend flag, implicit migration, packaged
native DLL or startup selection. Accepted Art XML/toolkit pin stay unchanged.

Source-only Rewired identity isolation is implemented and compiles against Art's
shipped API. Core's existing device-report JSON supplies bound instance IDs.
It changes exact joystick enabled flags only, restores only its own changes and
waits a frame after changes. Unknown or mismatched RawInput identity refuses with
an explicit reason. Its first muted runtime proof remains required. The handoff
accepts isolation/restore callbacks; none is installed in production. Also pending: previewed
profile Apply/status/capability receipt, explicit startup and lifecycle/recovery
policy. Menus, shifter and shortcuts stay legacy; only five driving axes consume
shared frames. Unsupported shared actions are declared to Core.

## Evidence and rig sequence

Toolkit snapshot fake-COM x64/x86, actual DLL empty lifecycle, legacy POV,
retained identity, injection and 871 external-session checks pass. Shared Core,
old C ABI and copied-provider suites pass. The final P21 default suite passes
129 supervised host and 84 provider checks plus teardown audits/mutation controls;
receipt is in `art-shim-bridge-evidence/p21-20261011/receipt.json`. Managed fake source -> actual shim
DLL -> Core -> Art sink passes on .NET8 and installed Unity 2019 Mono. Mono
Stopwatch uses a different origin than native QPC on this installation; correcting
the fixture to QueryPerformanceCounter matched both native ends. Original stale
samples were correctly classified Stale beforehand.

Cross-review source and pin native candidates first. Complete activation/isolation
and offline fault tests, then use the serialized rig lease, closed game, exact
backups and a muted first run. Qualify keyboard/title/menu passthrough, each
identity, five axes, focus/pause/finish, failure/reconnect/restart and resolved
stop. Compare original accepted force requests on sealed recordings with delivery
muted. Physical coexistence/feel needs owner-attended acceptance. No duplicate
acquisition, physical-force inference or automatic tune/config conversion is
part of this slice.
