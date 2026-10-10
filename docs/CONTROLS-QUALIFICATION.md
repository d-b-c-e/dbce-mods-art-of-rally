# Developer raw-control qualification

This is separate from pose playback. Wheelkit production Apply writes the
selected profile, then independent raw samples enter the real reader before
binding interpretation. Observe the actual menu/car response; an accepted command
or changed normalized value alone is not a completed input test.

Build the developer Recorder and Wheelkit ConfigurationQualification projects.
Use a reviewed x64 native candidate with the process-latched test injection API:

```powershell
./tools/testing/Start-ControlsQualification.ps1 -Result results/controls-NEW `
  -NativeCandidate <reviewed WheelFfb.dll> -NativeSha256 <exact SHA256> -ObserveSeconds 180
```

The runner takes the shared lease after 300 owner-idle seconds, snapshots files
and raw PlayerPrefs, disables all wheel/motion outputs, applies the profile and
temporarily installs the probe/native candidate. The saved UMM startup window is
hidden for this run. No resolution or focus override is made. Its cold request
expires in five minutes **for admission**; after admission the probe's own
300-second bound applies. The runner also stops if owner input resumes.

`command-context.json` supplies the nonce, PID, exact start time and executable
folder for `Send-ControlsCommand.ps1`. Select commands from `raw-workload.json`,
which was generated from the original profile without consulting applied game
bindings. Inspect current game state before each menu press. The client does not
retry failed commands. Raw samples expire; force blocking lasts until exit.

The runner stops the probe, requests normal window close, verifies the applied
settings (`verify-art-runtime`), restores the original files/preferences and
releases the lease. A verification failure remains a failed command even after
successful restoration. `restored.txt` certifies restoration, **not input**.
If normal close fails, no files are restored underneath the live process and
the lease remains held.

After that process is closed, recovery is explicit and uses no game/input calls:

```powershell
./tools/testing/Start-ControlsQualification.ps1 -Recover -Result results/controls-NEW
```

Use the original `-GameDir` for a nondefault install. Recovery validates the
allowed file inventory and original backup hashes, refuses a different lease,
and can renew its own expired token. It refuses an already completed restore
to avoid overwriting newer owner changes. Independent restoration steps continue
after a failure; errors retain the lease and receipt for retry. New logs/saves
are archived, and a newly created empty probe directory is removed. Unexpected
files in that directory are retained and reported.

`./tools/testing/Test-ControlsRecovery.ps1` checks this entry in disposable roots
with a file-only environment stub. It covers exact restore, stale lease renewal,
corrupt backups, out-of-scope paths, another lease, unexpected files and partial
environment restoration failure. It never touches game files, registry or devices.
