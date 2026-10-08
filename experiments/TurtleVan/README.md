# Turtle Van — experimental art of rally vehicle

An original modeled recreation of the user's yellow/green Turtle Van reference,
including a hollow cabin and driver view. Rear details and interior are an artistic
interpretation, not a verified reconstruction of cartoon production drawings.

## Try the prototype

1. Launch the usual Steam art of rally installation with the probe enabled.
2. In Free Roam or Custom Rally, choose **Group 2**, then **Turtle Van** at the end
   of the car list. Its fixed donor is **the rotary kei**, requiring the Australia
   DLC. Choosing this entry activates the model without checking a UMM overlay box.
3. The candidate supplies a rotating menu model and attaches after wheel startup,
   including stage intros. These new paths need the next attended game check.
4. Cycle the game's cameras for the cockpit. Eye position, pitch and FOV remain
   adjustable through **Ctrl+F10 -> Turtle Van (experimental)**.
5. Choose a stock car to return to stock visuals. The optional **Manual overlay on
   any chosen car (this session)** checkbox retains the earlier experiment mode.
   Leave it unchecked when testing menu selection.
6. Selections are remembered only in this running session. Native saves retain the
   stock donor and index, so the next launch starts with a stock selection. Unloading
   a registered menu slot requires restarting; remove the probe only with the game closed.

This is a **visual replacement**. Handling, collision shape, wheel radius, suspension,
sounds, scoring and FFB come from the selected donor car. The van's upper body and
roof accessories do not have matching collision geometry. The first build does not
have deforming bodywork, working gauge needles, reflective mirrors or firing weapons.
The steering wheel follows the donor's steering; road wheels follow its animated
transforms, with a car-relative lateral offset to the authored arches in 0.1.3.
Physical contact points, native wheel radius and suspension remain stock. Since 0.1.2 the body scales uniformly to wheelbase: changing donors
changes overall size, but preserves the van's authored proportions. Native contact patches still follow the donor; the drawn wheels now follow the
authored body width. Tire size still follows the donor radius.

### 0.1.2 shape pass

The editable model is 10% wider, with a hollow rounded lower body, fuller fenders,
rounded bumper, wraparound belt trim, larger four-panel side armor, cooling vents,
headlight details, wheel fasteners and cannon collars. Smooth/weighted normals are
now exported instead of flattening every triangle. The driver's eye and interior
are widened together. No physics or collision dimensions change.

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
  of this fix was subsequently confirmed: the owner's Steam screenshot shows the
  van in-game, and the UMM log records attachment to `Car_Kei(Clone)` and cockpit
  camera registration. The owner called it a good start. This establishes exterior
  rendering, not full camera/cockpit/triples/transition acceptance.
- **0.1.2 installed:** geometry checks pass at 81,916 triangles / 31 meshes, with
  clear driver sightlines and no degenerate triangles or winding errors. Build and
  production-loader tests pass. The owner
  accepted the revised appearance in the Steam screenshot `20261008001505_1.jpg`,
  while noting the wheels were tucked inside the wider body. UMM records uniform
  `(0.9, 0.9, 0.9)` scale on `Car_Kei`.

## 0.1.3 menu and cinematic candidate

- Adds a separate named choice and 3D menu preview to the donor's class. Stock
  choices remain present; choosing a custom entry maps the native season to the donor.
- Builds previews after the native diorama initializes; temporarily removes custom
  metadata before a new diorama Awake so native authored child indices remain valid.
- Hides the donor body as soon as all native wheel transforms exist, including intro
  states. Wheel visuals animate independently of the driving camera during cinematics.
- Drawn wheel centers move outward to the authored arches. No physical wheel or
  collider is repositioned. The widened appearance is still a cosmetic configuration.
- `vehicle.json` separates identity/donor/artwork from the runtime. The loader also
  accepts data-only folders under `Vehicles/<id>/vehicle.json` as an experimental
  foundation. The bundled Turtle Van is the only attended release candidate.
- The rally-complete display selects the custom preview while season data retains
  its native donor identity.
- Menu/save mapping and package validation pass managed tests. All 19 Harmony targets are
  checked against the installed assemblies. Offline tests do **not** establish menu
  rendering, cutscene coverage, wheel alignment or cockpit/triple acceptance.

The owner requested **two separate deliverables**: finish the cosmetic Turtle Van
first; then a serious custom-vehicle framework with creator tooling and physical
vehicle definitions. See [the framework plan](../CustomVehicles/README.md).
No public upload has occurred. Steam does not advertise Workshop support for this
app; the [official custom livery tutorial](https://steamcommunity.com/app/550320/discussions/0/3108015514308229529/)
covers PNG skins. This geometry mod requires UMM distribution as a downloadable mod.

## Files and rebuilding

- `assets/turtle-van.blend`: editable source model, named separate objects and materials.
- `assets/turtle-van.glb`: portable preview/import model.
- `tools/model.py`: deterministic procedural construction and runtime export.
- `assets/turtle-van.json` + `palette.png`: original model only; no game assets.
- `vehicle.json`: versioned identity, donor, texture/model files, wheel radius and cockpit flag.
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

Start with Group 2 -> Turtle Van, verify its rotating preview, then enter a stage
and confirm it is already the van during the intro. Check the outward wheel fit
while stopped, steering and driving; choose a stock car and confirm restoration.
Then check four moving wheels, clear cockpit,
normal camera cycling, disabling/restoration, pause, restart, stage finish/replay,
and return to menu. Check all three rendered screens separately. If the mod refuses
to attach or shows pink/missing geometry, keep the UMM log for diagnosis.

This probe is development work, not a second wheel/triple product or a public release.
After the asset and runtime approach are accepted, decide how it joins the main mod's
vehicle/camera UI. The earlier no-cockpit decision concerned stock cars without interiors;
the user explicitly requested a newly modeled interior for this vehicle.
