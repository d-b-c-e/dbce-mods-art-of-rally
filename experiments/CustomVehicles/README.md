# Custom vehicles: second development track

Owner direction, 2026-10-08: finish the cosmetic Turtle Van first, then develop a
serious, reusable vehicle framework. This directory records that second track;
it is not a released product or a claim of working custom physics.

## First deliverable: cosmetic Turtle Van

The [Turtle Van experiment](../TurtleVan/README.md) has original body/interior meshes,
textures, animated wheels, cockpit, and a 0.1.3 candidate for menu previews and
cinematic visibility. It inherits the stock rotary kei's complete physical setup.
Its drawn track width can differ from physical contact patches. A dedicated menu
entry does not itself create a new suspension, collider or engine configuration.

Finish attended menu -> intro -> drive -> finish/replay -> menu testing, removal /
stock restoration and camera handback before calling that candidate release-ready.
Package as a UMM download. Public host and publication are still undecided; nothing
has been uploaded. Native livery installation cannot load the custom geometry.

## Evidence and prior art

- The current [Steam store features](https://store.steampowered.com/app/550320/art_of_rally/)
  do not advertise Steam Workshop. The developer's [custom livery tutorial](https://steamcommunity.com/app/550320/discussions/0/3108015514308229529/)
  describes PNG images installed under an existing car.
- [Cars Extended](https://github.com/MMike17/ArtOfRally_CarsExtended) attempted a new
  vehicle framework. Its README explicitly marks it unfinished, with unresolved
  wheel jitter, missing effects and failure paths that repeatedly recreate the event
  manager. It describes assembling the native driving components onto a custom
  prefab. It is useful research, not an adopted dependency or a verified solution.
- Installed build 17584229's `Setup` writes `weight`, `weightRepartition`,
  `centerOfMass`, `inertiaFactor`, `maxPower`, `maxPowerRPM`, `maxTorque`,
  `maxTorqueRPM`, transmission, suspension, tire and wheel setup data. This confirms
  physical vehicle parameters exist; it does not prove arbitrary overrides work.
- Current local approach preserves the game's initialized donor prefab and overlays
  original meshes. The existing event manager is read from its field, avoiding
  the lazy factory's recursive creation failure mode.

## Framework milestones

1. **Reference vehicle lifecycle.** Prove the Turtle Van throughout menus, stage
   intros, driving, restarts, photo/replay/finish and camera handback. Verify clone
   cleanup and correct restoration of stock vehicles.
2. **Creator package and tools.** Publish a versioned manifest/model contract,
   Blender exporter with named wheel/steering/driver markers, template, validation,
   packaged preview and step-by-step guide. Demonstrate a second original vehicle
   without changing C# code. Support multiple packages, missing DLC, duplicate ids,
   malformed content and clean removal. Treat stock donor selection as an explicit
   creator choice rather than a runtime accident.
3. **Physical vehicle definition.** Add explicit engine curve/power/torque units,
   gearbox/differential, mass/weight distribution/center of mass/inertia, wheelbase,
   physical track, wheel/tire sizes, suspension, brakes and authored collision shapes.
   Trace when `Setup` applies defaults and when wheel/inertia caches initialize.
   First establish a cloned stock rig with numerically identical behavior, then
   change one parameter at a time with attended tests and captured evidence.
4. **Game integration and distribution.** Independent identity/stats, liveries,
   damage, lighting and sound; compatibility with Art of Sim Rally; versioned package
   migration and a stable host/install route. Custom physics requires explicit
   treatment of ranked/daily/weekly eligibility before public release. Keep the
   framework in this game's repository and decide its public module boundary after
   the experiment is proven.

No custom handling overrides, rebuilt physics rigs, working export UI, independent
statistics, Workshop uploader or public SDK are delivered by the cosmetic candidate.

## Present package foundation (experimental, not stable SDK)

`TurtleVan/src/VehiclePackage.cs` loads a data-only `vehicle.json`, model JSON and PNG.
The built-in manifest is `TurtleVan/vehicle.json`. Extra folders can currently be
placed at `Mods/ArtOfSimRally.TurtleVan/Vehicles/<package-id>/`; discovered packages
inherit the declared donor's existing class and unlock/DLC rules. No plugin DLL or
executable script is loaded from a vehicle package. Removing packages takes effect
on restart; only native donor selections are persisted in game saves.

Model v1 uses metres with X right, Y up, Z forward; group-local mesh positions and
normals, UVs and triangle indices; `body`, `steering`, `wheelFL`, `wheelFR`, `wheelRL`,
`wheelRR` origins; and a driver camera point. Front wheel Z must exceed rear wheel Z.
One PNG supplies the mesh UV texture; the material name `glass` selects transparency.
The authored `wheelRadius` normalizes drawn wheels to the donor's real tire radius.
Body scaling derives from authored/native wheelbase and stays uniform. This is an
internal foundation; do not present it as the finished creator workflow.
