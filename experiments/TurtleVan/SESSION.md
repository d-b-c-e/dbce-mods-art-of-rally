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

## 0.1.2 wider body and detail pass

The owner confirmed 0.1.1 was a good start and requested more detail, a wider and
less boxy body. Located the F12 screenshot at
`D:\Program Files (x86)\Steam\userdata\22623167\760\remote\550320\screenshots\20261007234053_1.jpg`.
It shows the custom exterior rendered in photo mode. UMM logs confirm attachment
to `Car_Kei(Clone)` with body scale approximately `(0.7, 0.8, 0.9)` and cockpit
registration. That nonuniform fit contributed to the narrow silhouette. The screenshot
does not establish cockpit/triple-screen or all lifecycle acceptance.

Authored the model 10% wider, including the interior and driver eye. Replaced the
lower panel boxes with one hollow rounded body, and added rounded fenders/bumpers,
wraparound trim, four larger armor panels, cooling vents, lens details, wheel fasteners
and cannon collars. Runtime export now retains smooth/weighted corner normals, with
face-normal fallback at sharp junctions. Reduced invisible bevel tessellation to stay
under the existing 100,000-triangle budget.

During the pass the owner asked whether every donor would warp the van. Runtime
body fit now uses a single wheelbase-derived scale on X/Y/Z. The authored proportions
stay constant across donors; overall size and wheel-arch fit still depend on donor.
Native wheel centers/radii, colliders and handling remain intact. The wider tires are
visual geometry only; physical contact points do not move.

The owner also requested a future lineup choice with preview and visibility during
cutscenes. Inspected installed `CarChooserManager` and `PlayerManager`: menu preview
objects are pre-existing class/index children, while the stage uses a selected prefab
path. The current driving-state attachment gate explains the stock-car intro. Recorded
two separate follow-ups in README: early visual attachment for intros, and full menu
entry/preview plus donor/save mapping. Neither is implemented in 0.1.2.

Final 0.1.2 validation: 81,916 triangles / 31 material-group meshes, zero degenerate
triangles, zero checked normal/winding errors, centered wheel pivots and clear driver
horizon/road sightlines. Exterior/rear/cockpit renders regenerated and exterior/cockpit
inspected. C# build and exact production-loader test pass, including seven malformed
asset cases; triangle-count expectation now comes from the independent geometry report.
Installed with the game closed, previous probe backed up and main-mod hashes preserved;
see the latest ignored install receipt. Fresh 0.1.2 in-game visual acceptance is pending.
