# Handoff

2026-11-08, ART-L (legacy removal). Installed on the rig: 0.4.4-rc.44 (`ce11ece`), launch-checked.

Still to do for ART-L's "one Sync-Toolkit manifest":
- Triple copies: `src/Dbce.TripleScreen.Core`, `src/Dbce.TripleScreen.Protocol` and `docs/triple/contracts` are hand copies of dbce-triple-screen-toolkit (older; `TripleViewportLayout.cs` retired there). Sync-Toolkit has no triple part yet.
- `src/ArtOfSimRally.Mod/Toolkit/FrameRateMonitor.cs` is byte-identical to the toolkit but outside the manifest until the `dotnet` part is re-synced with a `Dbce.Wheel.Telemetry.dll` that contains it (that re-sync also moves the FFB DLL, so it needs its own check).
- `lib/dbce-ui/dbce_ui.dll` (+ `PROVENANCE.md`) and `lib/playback` (+ `provenance.json`, dev recorder only) still carry their own provenance files.
- `lib/toolkit/VERSION` names the last partial sync (shared, toolkit `96229f2`); native, dotnet and shim are still the `v0.16.0-art-held-gates.a5dd678` bytes the manifest pins.
- Local branch `napo/art-std046-audit-20261104` (one docs commit, not on origin) was left in place.
