# Clean-install lane, Phase B preparation — 2026-10-29

Prepared, not run. Phase A (file level, loader-free copy) passed 15/15 with a non-vacuous comparison
guard: [phase A](2026-10-29-clean-install-phase-a.md).

## What a launch touches

The game keeps state per Windows user, not per install folder, so a copy shares it with the owner:

| State | Location | Handling |
|---|---|---|
| PlayerPrefs (bindings, options) | `HKCU\Software\Funselektor Labs\art of rally` (606-line export) | saved, removed for the run, restored |
| Saves, logs | `%USERPROFILE%\AppData\LocalLow\Funselektor Labs\art of rally` (24 files; `cloud\` is Steam Auto-Cloud) | same |
| Mod logs, shared-input runs | `%LOCALAPPDATA%\ArtOfSimRally` (93 files) | same |
| Triple status | `%LOCALAPPDATA%\DBCE\TripleScreen\games\art-of-rally` | same |
| Steam cloud cache | `Steam\userdata\<id>\550320` | compared only, never restored |

**Steam relaunch (verified in source).** The game calls `SteamAPI.RestartAppIfNecessary(550320)` and quits
when it returns true, so a copied exe started outside Steam would make Steam start the *installed* game.
`steam_appid.txt` (550320) beside the copy's exe is Valve's documented developer path that makes it return
false. The launcher also stops any `artofrally` process whose path is not the copy, including for 20 s after
the copy exits.

**Steam Cloud risk — owner decision required.** A first run starts with no saves. If Steam syncs the
`cloud` folder for that session, it could delete the owner's cloud saves. The launcher refuses to start
unless the operator states `-CloudProtection SteamExited` (Steam fully closed) or `CloudDisabledForArt`
(Steam Cloud switched off in art of rally's Steam properties for the run, then back on).

## Tools

- `tools/testing/ArtUserState.ps1` — `-Save`/`-Compare`/`-Restore`. `-SelfTest` on a throwaway key and
  folders: unchanged state equal, 7 kinds of change detected, restore exact, throwaway state removed —
  **PASS**. Read-only owner snapshot (`-Save` then `-Compare`, no restore): equal, 32.8 MB, kept at
  `E:\Source\temp\art-phaseB\owner-state-20261029-a`.
- `tools/testing/Start-CleanInstallLaunch.ps1` — saves and verifies owner state, isolates it, writes
  `steam_appid.txt`, launches the copy, watches the process path, and in `finally` stops the game,
  restores and compares owner state and checks the installed mod is unchanged; result
  `phaseB-result.json` in the state directory.

## Run order (one guna slot, Rig-Lease held)

1. Owner chooses the Steam Cloud protection.
2. Prepare the copy (`Test-CleanInstallCopy.ps1` recipe), install rc.5 into it.
3. `Start-CleanInstallLaunch.ps1 -Copy <copy> -StateDir <new dir> -CloudProtection <choice>`.
4. **Owner-attended:** first-run F6 setup in the copy, start a stage, close the game. Then relaunch is a
   second run with the same script and a new state directory, *without* isolating (not yet scripted).
5. Remove the copy; keep the state directories until the owner confirms their game.

No force output is expected: a fresh Settings.xml has no FFB device until one is picked in F6.
