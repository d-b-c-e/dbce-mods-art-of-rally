# Clean-install lane, Phase A — 2026-10-29

File-level lifecycle of the exact rc.5 package on a disposable copy of the installed game with every
mod-placed file removed (Mods, Mods-retired, Managed/UnityModManager, winhttp.dll,
doorstop_config.ini, Plugins/x86_64/UnityForceFeedback.dll): 453 game files, no loader. No game
launch, no owner file touched (installed Mod.dll still F7F0CDEC…). Script:
`tools/testing/Test-CleanInstallCopy.ps1` (copy under `E:\Source\_work`, removed after the run).

- Candidate `ArtOfSimRally-0.4.4-rc.5-shared-input.zip` `EAB5D08DE1534219EC183A62EF5659B7B60EAC1CC113E7A2FF9C530A77EB481B`; rollback target
  public `ArtOfSimRally-0.4.3.zip` `83CBE857FFB056F4FAB6FC5EE11B324DE78852295EC86B0D437AE2C8AA4C4F16`.
- **PASS, 15 checks:** install exit 0 and places the Unity Mod Manager loader on a loader-free game
  (first real run of that path), payload matches the package, verify.ps1 loads, install adds
  18 files and changes or removes no game file; uninstall exit 0 and restores the copy byte
  for byte (nothing added, removed or changed); reinstall; rollback to public 0.4.3 and forward to
  rc.5 again, payloads matching each package.
- Corrections during the run, recorded so they are not repeated: PowerShell's `Diff` alias made the
  first snapshot comparison vacuous (renamed; the install diff now must add at least 10 files), and
  the installed game's `Plugins/x86_64/UnityForceFeedback.dll` is a mod-placed copy, not a game file.
- Baseline is derived from the owner's install, not from a Steam depot download.
- **Not covered (Phase B):** launching from the clean state, first-run setup in F6, saving and
  relaunching. Game state is per user (HKCU PlayerPrefs, LocalLow, LOCALAPPDATA DBCE/Wheelkit), so a
  launch needs that state isolated or exported and restored, plus a rig slot; F6 setup needs a person.

Files added by install:
- `Mods\ArtOfSimRally\ArtOfSimRally.Mod.dll`
- `Mods\ArtOfSimRally\Dbce.TripleScreen.Core.dll`
- `Mods\ArtOfSimRally\Dbce.TripleScreen.Protocol.dll`
- `Mods\ArtOfSimRally\Dbce.Wheel.Ffb.dll`
- `Mods\ArtOfSimRally\Dbce.Wheel.Telemetry.dll`
- `Mods\ArtOfSimRally\Info.json`
- `Mods\ArtOfSimRally\UnityForceFeedback.dll`
- `Mods\ArtOfSimRally\build.json`
- `Mods\ArtOfSimRally\dbce_shim.dll`
- `artofrally_Data\Managed\UnityModManager\0Harmony.dll`
- `artofrally_Data\Managed\UnityModManager\Config.xml`
- `artofrally_Data\Managed\UnityModManager\Params.xml`
- `artofrally_Data\Managed\UnityModManager\UnityModManager.dll`
- `artofrally_Data\Managed\UnityModManager\dnlib.dll`
- `artofrally_Data\Managed\UnityModManager\installed-by-art-of-sim-rally.json`
- `artofrally_Data\Plugins\x86_64\UnityForceFeedback.dll`
- `doorstop_config.ini`
- `winhttp.dll`
