# Handoff

2026-11-08, ART-L (legacy removal). Installed on the rig: 0.4.4-rc.46 (`a1c0862`), launch-checked.

Still to do for ART-L's "one Sync-Toolkit manifest":
- Triple copies: `src/Dbce.TripleScreen.Core` and `docs/triple/contracts` are hand copies of dbce-triple-screen-toolkit (code identical except the retired `TripleViewportLayout.cs`, which art still uses, and the new `RigDefinition.cs`/`RigPlanner.cs`). Sync-Toolkit has no triple part yet. `src/Dbce.TripleScreen.Protocol` has no upstream; it is art's own code.
- `lib/playback` (+ `provenance.json`, dev recorder only) is toolkit Playback `e64d9f6`; toolkit HEAD deleted `ArtifactSeal`, which `session-tools.ps1` uses, so moving it onto the manifest means porting the dev recorder first.
- `lib/dbce-ui/dbce_ui.dll` (+ `PROVENANCE.md`) is the `[Dev] UiHost=hook` core; UI-10 owns UiHost and should vendor it through Sync-Toolkit when the hook becomes the only host.
- `lib/toolkit`: dotnet is toolkit `591ad47`, shared `96229f2` (same content), native and shim still the v0.16.0 bytes.
- Local branch `napo/art-std046-audit-20261104` (one docs commit, not on origin) was left in place.
