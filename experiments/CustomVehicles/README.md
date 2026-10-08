# Custom Vehicles — offline development candidate 0.2.0

A reusable, data-only vehicle experiment for Art of Rally, developed from the
Turtle Van prototype. The owner authorized this second development track on
2026-10-08. This candidate is built and tested offline; it is **not installed,
published, or qualified as working custom physics**.

## What is here

- Multiple vehicle folders with stable IDs, versioned manifests, content
  fingerprints, strict validation and isolated errors for malformed neighbors.
- A generic Blender exporter, dry-run check, editable original Trail Scout example,
  and an adapter for the Turtle Van's revised 0.1.4 window geometry.
- VehicleTool: validate, inspect identity, and package only declared data files.
- Native chooser entries and 3D previews inherited from the cosmetic prototype;
  instance-pinned visuals, animated wheels, cockpit camera, cinematic attachment,
  and native donor fallback on removal. This expanded runtime needs attended testing.
- Physical definitions for mass/distribution/COM/inertia, engine, gearing,
  suspension/brakes, wheelbase/track/tire dimensions and a simple collision box.
- An experimental implementation with per-instance ownership, rollback, native
  initialization hooks and opt-in stock/custom initialization reports.
- Network and local result/ghost/stat/progression guards with a process-lifetime
  taint latch. Guard prefixes are exercised offline against the compiled plugin.

**Physical writes are hard-locked in `Main.Load` (`RuntimeQualified=false`).**
The Free Roam checkbox cannot enable them in this candidate. An attended no-op
comparison, shared-asset audit, save/score checks and one-field handling test must
precede unlocking this path. The example handling values are proposals, not tunes.

## Two independent example packages

| Vehicle | Donor / native class | Triangles | Indexed vertices | Parts |
|---|---|---:|---:|---:|
| Turtle Van | `Car_Kei`, Group 2, Australia DLC | 82,804 | 133,133 | 31 |
| Trail Scout | `Car_Mini`, Group 2 | 26,336 | 49,006 | 29 |

Both have open interiors and authored window openings. Wheels are separate groups.
Trail Scout is an original compact trail wagon that demonstrates a second vehicle
without another C# implementation. The native menu specifications, drivetrain,
sounds, effects and unlock rules still come from the explicitly selected donor.
Custom statistics, arbitrary drive layouts, custom sounds, damage deformation,
lights, hot reload, Workshop upload and integrated replay identity are unfinished.

The framework's sRGB palette encoding is corrected and mesh vertices are indexed;
the installed cosmetic Turtle Van 0.1.4 remains separate. Its accepted appearance
does not qualify this newly exported framework palette or runtime.

## Build and inspect

From this directory, on Windows with .NET SDK, .NET Framework 4.8 targeting support
and the local game/UMM installation, plus Python 3 with Pillow for tool checks:

```powershell
.\tools\build.ps1
# Recreate editable models and previews as well (Blender 5.2):
.\tools\build.ps1 -RegenerateModels
# Another game installation:
.\tools\build.ps1 -GameDir 'E:\SteamLibrary\steamapps\common\artofrally'
```

The script builds the plugin, production-loader tests and CLI, runs the offline
gates, stages a new uniquely named candidate under `dist/`, and writes an archive
and hash receipt under `artifacts/`. It does not launch or install anything and
does not package game/Unity/UMM assemblies.

A separate `CreatorSDK.zip` bundles the standalone Windows validator, its licensed
NuGet JSON dependency, exporter and editable examples. No game installation is
needed to export and validate a model with that kit.

```powershell
.\tools\VehicleTool\bin\Release\net48\VehicleTool.exe validate examples\trail-scout\vehicle.json
.\tools\VehicleTool\bin\Release\net48\VehicleTool.exe inspect examples\turtle-van\vehicle.json
.\tools\VehicleTool\bin\Release\net48\VehicleTool.exe pack examples\trail-scout\vehicle.json artifacts\my-trail-scout.zip
```

See [CREATOR.md](CREATOR.md) for authoring and [QUALIFICATION.md](QUALIFICATION.md)
for exact lifecycle assumptions, peer findings, score protection and remaining
runtime work. The creator contract is experimental; schema 2 is not a public
compatibility promise.

## Later attended installation

Preserve the current game/mod/config baseline and close the game. The candidate
would occupy `Mods/ArtOfSimRally.CustomVehicles/`, with vehicle folders in
`Vehicles/<id>/`. Disable the separate `ArtOfSimRally.TurtleVan` plugin before
loading it: both own chooser/camera presentation. The framework refuses to load
alongside an enabled or active cosmetic plugin, including startup load order. No installer is supplied for this
unqualified milestone.

Custom selection IDs live in the framework's `State/selections.json`; native saves
retain stock donor indices. Missing packages resolve to the native donor. Add,
remove or change package folders while the game is closed. Registered chooser
slots cannot be unloaded safely mid-menu, so unloading requires restarting.

To uninstall later, close the game and remove only this candidate's mod folder.
Its custom IDs and diagnostics are entirely mod-owned. The accepted cosmetic
plugin can then be re-enabled. Nothing here changes native FFB, wheel bindings,
main Art of Sim Rally configuration or installed files during development.
