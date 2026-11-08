# Shared FFB output gate

`SharedFfbOutput.cs` is MIT source to vendor into a managed mod that already
ships a pinned `Dbce.Wheel.Ffb.dll` / `WheelFfb.dll` pair. It is portable C# 7.3
and has no Unity, loader, reader or force-model dependency. Vendoring it does
not change the pinned DLLs, so a consumer's package pins stay the same.

It holds the game-independent output path that Truckful (`cf8d761`) and Over the
Hill (`d7fd2c2`) had copied:

- `FfbSink`: the open/write/stop/ready seam that tests replace with a fake.
- `FfbDelivery`: request, integer command, attempted and accepted are separate
  facts. Acceptance is not torque, and a stop is a request, not proof of zero force.
- `FfbOutput`: the state machine. It sends one command per new completed physics
  tick while the host's gates hold. A stale source (100 ms), a changed
  epoch/device/window, a lost device or any failure requests zero/stop/shutdown;
  failures wait 5 s before another open. A clock fault or a throwing cleanup
  latches force off for the process.
- `SharedFfbPair.CreateSink`: binds the pinned pair. The host passes the
  directory, both SHA-256 pins and the native version. Creating the sink loads
  neither the managed `Dbce.Wheel.Ffb` assembly nor the native module: every
  `WheelFfbNative` use is inside a delegate body, so a host may create it at load
  with FFB Off (Over the Hill does). Nothing loads until the first eligible
  `Open`. Hash mismatches refuse before loading the native module; the loaded
  module's version is checked before device initialization. It
  uses strict-GUID `Initialise`, a 150 ms hold timeout, exit guards and an
  initial zero, and stops only an attempt it owns.
- `UnityForegroundWindow.Focused`: the existing DRIVE/Truckful/Over the Hill
  output-window policy, a Win32 check for this process's visible focused
  `UnityWndClass` window.

**Stays in the game:** reading physics, the force model and normalization, the
`FfbSource` adapter (`Available`, `Request`, `Epoch`, finite `Tick`), eligibility
(focus, pause, F6, input freshness), the target device identity, the pins and
the plugin lifecycle (create, suspend before F6, dispose).

## Rules that differ from one of the originals

- **Backward tick:** it suspends output, and the next fresh tick starts again
  from there. This is Over the Hill's rule. Truckful kept the old high-water
  tick and stayed suspended until it was passed. Truckful's model already
  rejects non-increasing ticks, so its output never received one.
- **Repeated tick status:** "No new completed physics tick" (Over the Hill).
  Truckful said "sample". No test or tool reads the Truckful wording.
- **Muting identity change:** when an epoch/device/window change requests
  cleanup and that cleanup throws, `Update` returns at once with force latched
  off. Both originals continued to open and write once in that same call.
- **Tick type:** `double`. An integer `TickId` converts exactly.
- **Hashing:** `SHA256.ComputeHash` and `BitConverter` hex, so the file builds
  on .NET Framework/Mono as well as .NET 6.

## Vendoring

Copy the exact file plus this repository's MIT license, and record the source
commit and SHA-256 in the consumer's provenance. Keep the consumer's own pins
and adapter beside it. Consumers built with nullable warnings as errors compile
it unchanged; the file disables the nullable warnings for its legacy API.

Checks: `dotnet run --project tests/SharedFfbOutput` (fake sink only; no module
load, device or game). Consumer fake-output suites stay with the consumer.
