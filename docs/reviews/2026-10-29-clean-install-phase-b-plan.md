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
false. The launcher watches for any other `artofrally` process, including for 20 s after the copy exits; one
is a recorded conflict that fails the run, never a process the launcher stops.

**Steam Cloud risk — owner decision required.** A first run starts with no saves. If Steam syncs the
`cloud` folder for that session, it could delete the owner's cloud saves. The launcher refuses to start
unless the operator states `-CloudProtection SteamExited` (Steam fully closed) or `CloudDisabledForArt`
(Steam Cloud switched off in art of rally's Steam properties for the run, then back on).

## Tools (revised after lulu review 471339)

- `tools/testing/ArtUserState.ps1` — `-Save` (backup bytes verified against the live state before any
  change), `-Compare`, `-Restore` (backup verified first; targets must be the known state folders, and no
  path component may be a junction or symlink). `-SelfTest` on a throwaway key and folders under a new root
  (an existing root is refused, never deleted): **11/11 PASS** — unchanged equal, backup verified, no
  overwrite of a backup, mutations detected, exact restore, tampered backup refused with live state
  untouched, outside target refused, junction target and source refused, existing root refused.
  Read-only owner snapshot (`-Save` then `-Compare`, no restore): equal, 32.8 MB, kept at
  `E:\Source\temp\art-phaseB\owner-state-20261029-a`.
- `tools/testing/Start-CleanInstallLaunch.ps1` — preflight changes nothing; save and verify; every state
  change, the launch and the watch run inside `try`, and `finally` stops only the launched PID, waits for
  any other art of rally process to close before restoring (never kills it; reports `restorePending` with
  the restore command if it stays open), restores and compares. Success requires the copy seen at its own
  path, a clean exit, no conflict, no time-out, an exact restore and an unchanged installed mod.
  `-SelfTest` (stand-in `artofrally.exe`, throwaway state): **5/5 PASS** — clean exit 0; failure after the
  key was deleted, failure after launch, time limit, and a second process all exit 1 with state restored
  exactly; the second process is reported, not stopped.

## Run order (one guna slot, Rig-Lease held)

1. Owner chooses the Steam Cloud protection.
2. Prepare the copy (`Test-CleanInstallCopy.ps1` recipe), install rc.5 into it.
3. `Start-CleanInstallLaunch.ps1 -Copy <copy> -StateDir <new dir> -CloudProtection <choice>`.
4. **Owner-attended:** first-run F6 setup in the copy, start a stage, close the game. Then relaunch is a
   second run with the same script and a new state directory, *without* isolating (not yet scripted).
5. Remove the copy; keep the state directories until the owner confirms their game.

No force output is expected: a fresh Settings.xml has no FFB device until one is picked in F6.
