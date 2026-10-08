# Turtle Van — experimental art of rally vehicle

An original modeled recreation of the user's yellow/green Turtle Van reference,
including a hollow cabin and driver view. Rear details and interior are an artistic
interpretation, not a verified reconstruction of cartoon production drawings.

## Try the prototype

1. Launch the usual Steam art of rally installation.
2. Open Unity Mod Manager with **Ctrl+F10**, expand **Turtle Van (experimental)**,
   and check **Enable Turtle Van for this session**. Pause first if already driving.
3. Enter free roam or a stage with a normal car. The model fits to that car's axles.
4. Use the game's normal change-camera control; the cockpit is appended after the
   stock and Art of Sim Rally views. Driver position, pitch and FOV can be adjusted
   in the Turtle Van panel while paused.
5. Uncheck the enable box to restore the original visuals. Every game launch
   starts with the van off. No owner camera, control or wheel settings are saved.

This is a **visual replacement**. Handling, collision shape, wheel radius, suspension,
sounds, scoring and FFB come from the selected donor car. The van's upper body and
roof accessories do not have matching collision geometry. The first build does not
have deforming bodywork, working gauge needles, reflective mirrors or firing weapons.
The steering wheel follows the donor's steering; road wheels follow its animated
transforms. A donor's wheelbase/track can visibly stretch the body.

## Current evidence

- Editable Blender model, portable GLB, palette PNG and runtime mesh JSON generated.
- Exterior, rear and cockpit previews rendered and inspected in Blender.
- Independent mesh checks cover finite coordinates, indices, UVs, normal winding,
  triangle budget, wheel pivots and two clear driver sightlines.
- C# candidate builds against installed game build **17584229** with warnings as errors.
- **0.1.0 failed the owner's startup test:** UMM reported `Missing model data` and
  could not register the settings panel. The installed JSON was valid and complete.
- **0.1.1 replaces Unity JsonUtility with the game's Newtonsoft.Json library.**
  The exact production loader passes a managed test against the installed model
  (31 meshes / 44,812 triangles) plus seven corrupt-data cases. In-game loading
  of this fix, spawning, shaders, camera transitions, driving and triples await retest.

## Files and rebuilding

- `assets/turtle-van.blend`: editable source model, named separate objects and materials.
- `assets/turtle-van.glb`: portable preview/import model.
- `tools/model.py`: deterministic procedural construction and runtime export.
- `assets/turtle-van.json` + `palette.png`: original model only; no game assets.
- `src/`: independent development UMM probe, inside the existing game repository.
- `previews/`: rendered views and validation evidence.

Run `tools/build.ps1 -Model` to rebuild everything (Blender 5.2, Python, .NET SDK).
Run `tools/build.ps1` to validate and compile existing assets. `tools/install.ps1`
installs only the probe with the game closed and records a hash receipt; it backs up
the current main mod and leaves its files intact. Generated `dist/` and local
deployment backups are ignored. Remove `Mods/ArtOfSimRally.TurtleVan` with the game
closed to uninstall the probe.

Runtime meshes avoid requiring a Unity 2019 editor or an incompatible Unity 6 asset
bundle. Glass uses the runtime Standard shader's transparency. Original renderers
are hidden reversibly, without disabling native objects or scripts. The event manager
is read through its existing field, never its lazy factory getter. Camera ownership is
released for cinematics, stage transitions, disabling, unloading and normal view cycling.

## Next attended check

Start with free roam, then check the exterior, four moving wheels, clear cockpit,
normal camera cycling, disabling/restoration, pause, restart, stage finish/replay,
and return to menu. Check all three rendered screens separately. If the mod refuses
to attach or shows pink/missing geometry, keep the UMM log for diagnosis.

This probe is development work, not a second wheel/triple product or a public release.
After the asset and runtime approach are accepted, decide how it joins the main mod's
vehicle/camera UI. The earlier no-cockpit decision concerned stock cars without interiors;
the user explicitly requested a newly modeled interior for this vehicle.
