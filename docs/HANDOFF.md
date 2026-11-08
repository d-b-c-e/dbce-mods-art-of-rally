# Handoff

2026-11-08, ART-L (legacy removal). Installed on the rig: 0.4.4-rc.48 (`33770c4`), launch-checked.

`lib/toolkit` now carries every toolkit and triple copy through one Sync-Toolkit manifest (`-Check` in package.ps1): dotnet from toolkit `591ad47`, triple (core + contracts) from triple toolkit `1ecb266`, shared `96229f2`, native and shim still the v0.16.0 bytes. Still to do:
- `lib/playback` (+ `provenance.json`, dev recorder only) is toolkit Playback `e64d9f6`. Toolkit HEAD deleted `ArtifactSeal` (tapes end with `complete.txt`), and art's dev tools call it in 6 places (`session-tools.ps1`, `Session.ps1`, `Recorder/SessionTape.cs`, `Replay/ForceTrial.cs`, `Replay/RecordedOutputs.cs`). Port those to `complete.txt`, add a Playback source to Sync-Toolkit, re-sync, then delete `lib/playback`.
- `lib/dbce-ui/dbce_ui.dll` (+ `PROVENANCE.md`) is the `[Dev] UiHost=hook` core; UI-10 owns UiHost and should vendor it through Sync-Toolkit when the hook becomes the only host.
- `src/Dbce.TripleScreen.Protocol` has no upstream (the triple toolkit never had it); it is art's own code.
- Local branch `napo/art-std046-audit-20261104` (one docs commit, not on origin) was left in place.
