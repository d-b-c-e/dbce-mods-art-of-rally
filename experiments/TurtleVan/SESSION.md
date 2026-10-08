# 2026-10-07 — first Turtle Van prototype

Baseline: `dbce-mods-art-of-rally` main at
`d1c7d65470bcaf47326c7ce21535b7db4bbadeb7`, initially clean.
Installed game: Steam 550320, build 17584229. Existing main mod: 0.4.2.

Built an original editable Blender model, GLB and runtime JSON/palette, including
a hollow cabin, steering wheel, gauges, seats, rear bench and pizza box. Supplied
image is the exterior reference; unseen rear/interior are interpretations.

Model validation: 44,812 triangles across 31 material/group meshes; no degenerate
triangles or winding/normal disagreements; wheel pivots centered; horizon and
6-degree downward driver sightline both clear. See `previews/validation.json`.
Blender 5.2 rendered exterior/rear/cockpit previews. C# build passed with zero
warnings/errors against the actual installed Unity/game/UMM assemblies.

Probe fits the model to the selected donor's axles; original renderer visibility
is restored on disable. Native wheel transforms drive the new wheel visuals.
The cockpit is appended to the normal camera cycle, after the main mod's views.
No donor physics, collision shape, force, inputs or scoring changes are made.

Installed 0.1.0 to
`D:\Program Files (x86)\Steam\steamapps\common\artofrally\Mods\ArtOfSimRally.TurtleVan`
with the game closed. The van is off at startup and needs the UMM session checkbox.
All existing ArtOfSimRally files were backed up and verified byte-identical after
deployment. Exact payload hashes and local backup are recorded in
`artifacts/install-20261007-230256/receipt.json` (ignored, machine-local).

The game was not launched. Driving, spawning, runtime Standard shader lookup, dirt,
view transitions, restarts, finish/replay and rendered triple screens remain untested.
Owner next step: enable the probe in Ctrl+F10, enter free roam, inspect the exterior,
then cycle views to the cockpit. Collect UMM log if attachment fails. The first
prototype keeps the donor's collision shape and has static instruments/mirrors.

No public release, remote push, production-mod rebuild or force test occurred.

## 0.1.1 startup correction

The owner restarted twice and reported a red UMM status with no settings control.
UMM 0.33.0 log: `System.IO.InvalidDataException: Missing model data` from Main.Load.
The installed JSON independently parsed as schema 1 with all 31 parts; the failing
code used Unity JsonUtility before registering OnGUI. This was a real startup failure,
not a missing restart. The exact native serializer failure mechanism is unconfirmed.

Replaced that parser with the game's Newtonsoft.Json 12.0.1 assembly (reference only;
no dependency DLL is packaged), split pure model parsing into AssetData.cs, added
structural validation and a useful success log, and bumped the probe to 0.1.1.
The build now runs the exact production loader under .NET Framework 4.8 with that
game library: all 31 meshes / 44,812 triangles, eye coordinates and seven malformed
model cases pass. The same test also passes against the actual installed JSON.
This is a managed loader regression check, not a Unity-rendering or native-serializer test.

The owner explicitly closed the game for deployment. Installed 0.1.1 with the existing
probe backed up and the main mod files preserved/hash-verified; see the newest ignored
`artifacts/install-*/receipt.json`. Fresh UMM load/green status and in-game driving
still need the owner's next launch. No game was launched by the agent.
