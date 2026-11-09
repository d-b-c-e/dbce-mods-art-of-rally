# art of rally mod — agent guide

Portfolio rules: `E:\Source\dbce-project-mgmt\HOW-WE-WORK.md` (read first; it replaces older process, lease, review and testing instructions). Current focus: `E:\Source\dbce-project-mgmt\NOW.md`. Feature status lives in the portfolio grid (`python E:\Source\dbce-project-mgmt\tools\portfolio_grid.py plan <grid-id>`), not in this file. Grid id: `art`.

## Game and mod
- art of rally, Steam app 550320, `D:\Program Files (x86)\Steam\steamapps\common\artofrally`. Unity 2019.4 Mono (not IL2CPP), Rewired input, 64-bit.
- Loader: Unity Mod Manager (UMM); the release ZIP bundles it. Single mod assembly `src/ArtOfSimRally.Mod`, .NET Framework 4.8.
- Wheel and pedals (direct input for devices Rewired cannot read), force feedback from the tyre model, Forza-format UDP telemetry, bonnet camera, F6 settings panel, triple screens. Public repo.

## Build
- **One command:** `pwsh build.ps1` at the repo root builds and runs every offline test, with no arguments. Run it before pushing.
```powershell
# once: copy UnityModManager.dll and 0Harmony.dll from <game>\artofrally_Data\Managed\UnityModManager into lib\umm (git-ignored)
dotnet build ArtOfSimRally.sln -c Release -warnaserror "-p:GameDir=D:\Program Files (x86)\Steam\steamapps\common\artofrally"
```
Output: `src/ArtOfSimRally.Mod/bin/Release/ArtOfSimRally.Mod.dll`. Keep the classic `.sln`. Details: `docs/BUILDING.md`.

## Test
- `dotnet test` runs nothing here. The suites are executable harnesses run by `tools/testing/Test-Rc.ps1 -Version X.Y.Z-rc.N` (unused RC number); it also builds the ZIP and runs installer checks. See `docs/PRE-RELEASE-TESTING.md`.
- In game: launch through Steam, press F6. Logs: `<game>\artofrally_Data\Managed\UnityModManager\Log.txt`, `%LOCALAPPDATA%\ArtOfSimRally\ffb.log`, `%USERPROFILE%\AppData\LocalLow\Funselektor Labs\art of rally\Player.log`.
- Back up and restore around a game run: `HKCU\Software\Funselektor Labs\art of rally` (bindings, display prefs), the LocalLow save folder above, and the mod settings under the UMM folder.
- Keys: foreground `SendInput` scan codes (game window in front; arrows need `KEYEVENTF_EXTENDEDKEY`) drive the game's menus and throttle into a stage. PostMessage reaches only Unity's legacy `Input` (F6, with focus). Recipe and scripts: `docs/sweep-2026-11.md`.

## Install on the rig
With the game closed: `tools/installer/Install.bat` from the built ZIP (also `tools/testing/Test-Installer.ps1` for installer checks). Procedure and rollback: `docs/LOCAL-DEPLOYMENT.md`. `tools/installer/Uninstall.bat` removes only the mod's files; settings stay.

## Shared code
`lib/toolkit/` is vendored from dbce-wheel-mod-toolkit (native `WheelFfb.dll` shipped as `UnityForceFeedback.dll`, `Dbce.Wheel.Ffb.dll`, `Dbce.Wheel.Telemetry.dll`, the shim, and the input, ui, display and support sources under `lib/toolkit/shared`), plus dbce-triple-screen-toolkit's core and contracts under `lib/toolkit/triple` (part `triple`, compiled by `src/Dbce.TripleScreen.Core`), every file pinned in `lib/toolkit/MANIFEST.txt`. `tools/Sync-Toolkit.ps1` is the toolkit's own script: refresh with `-LocalRepo <clean toolkit checkout> -Parts shared` (or `-Version vX.Y.Z`); `package.ps1` runs `-Check` and refuses edited, missing or unlisted files. Change shared code in the toolkit and re-sync; never edit the vendored copy.

## Game-specific rules
- Online leaderboards exist: no physics or assist changes; FFB, camera and telemetry are fair-play neutral.
- Never commit game assemblies or `.CT` files (the repo is public). The vendored toolkit binaries must stay committed (check `git check-ignore -v lib/toolkit/native/WheelFfb.dll` prints nothing).
- Never switch Rewired's input source at runtime (`ResetAll()` kills keyboard and menus). Do not use xoutput/XInput: it hides the wheel from DirectInput.
- The native FFB DLL is x64 only; one DirectInput instance per session (a released temporary instance crashed a Fanatec user).
- Deploy only with the game closed; the DLLs are locked while it runs.
- The game's Quit kills the process (`ExitGame.Exit`); save before quitting in captures and never call Exit in-process.
- `GameEntryPoint.EventManager` is a lazy factory that queues downloads: never call it from polling; read the existing `eventManager` field.
- `Mz` is unusable as steering force (sign flips at about 8 degrees slip); the force uses `(FyL + FyR) x trail / FyReference`.
- Camera is a two-object rig ("Stage Camera" with child "Camera Main"); restore the child's local identity when releasing it.
- Releases are built locally (no GitHub Actions); see `docs/RELEASING.md`. The owner publishes.

## Key docs
- `docs/FINDINGS.md` (verified game facts) and `docs/KNOWN-ISSUES.md` (defect register)
- `docs/BUILDING.md`, `docs/PRE-RELEASE-TESTING.md`, `docs/RELEASING.md`
- `docs/CONTROLS.md`, `docs/FORCE-FEEDBACK.md`, `docs/CAMERA.md`
- `TOOLKIT-ADOPTION.md` (reference for which toolkit standards are adopted)


<!-- BEGIN project-file-location -->
## Project files and temporary work

Keep project-related research, evidence, scripts, packages, and other working files inside the relevant project folder whenever practical. Keep disposable or machine-specific outputs git-ignored when they do not belong in version control; never ignore files that should be committed merely to hide them. When a project folder is unsuitable, use `E:\Source\temp\<project>` instead of creating scattered drive-root folders. Record the location of material needed by later work and update references when relocating it.

Clean up temporary files and disposable copies promptly once they are no longer useful. Preserve active work, user data, settings, saves, required evidence, backups, rollback artifacts, and worktrees until their retention or replacement is established. Follow existing project, rig, and data-loss safeguards; do not remove files merely because they are old or untracked.
<!-- END project-file-location -->
