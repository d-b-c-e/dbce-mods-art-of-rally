# Handoff

2026-11-08, ART-L (legacy removal). Installed on the rig: 0.4.4-rc.48 (`33770c4`), launch-checked.

`lib/toolkit` carries every toolkit and triple copy through one Sync-Toolkit manifest (`-Check` in package.ps1 and session-tools.ps1): dotnet (now with the dev-only `Dbce.Wheel.Playback.dll`) from toolkit `804b2a6`, triple from `1ecb266`, shared `96229f2`, native and shim still the v0.16.0 bytes. `lib/playback` is gone; art's dev recorder and replay tools end tapes with `complete.txt` (`tools/testing/Common/CaptureComplete.cs`), like the toolkit's TrajectoryRecorder. Tapes recorded before this (`complete.tsv`) no longer replay.
- Left for UI-10: `lib/dbce-ui/dbce_ui.dll` (+ `PROVENANCE.md`), the `[Dev] UiHost=hook` core, to be vendored through Sync-Toolkit when the hook becomes the only host.
- `src/Dbce.TripleScreen.Protocol` has no upstream (the triple toolkit never had it); it is art's own code.
- `experiments/` on disk holds only git-ignored build output (about 790 MB of old CustomVehicles packages); the source was deleted from git. Remove it by hand if nobody wants those zips.
- Local branch `napo/art-std046-audit-20261104` (one docs commit, not on origin) was left in place.
