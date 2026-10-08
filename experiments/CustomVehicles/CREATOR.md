# Creating a vehicle

Start with `examples/trail-scout/trail-scout.blend` and its `vehicle.json`.
Give your copy a unique lowercase ID, a display name, author and version. Keep the
donor choice explicit: it supplies class, unlock/DLC requirements and all handling
in the current candidate. The example donor `Car_Mini` exists as the meanie in
installed build 17584229. `Car_Kei` requires the Australia DLC.

## Blender authoring

Work in metres: Blender X points right, Y forward, Z up. Apply negative/mirrored
scale before exporting. Keep the car aligned to these axes; the exporter bakes
positive scale, object transforms, text/curves and evaluated modifiers.

Every exported mesh, text or curve object needs a custom string property `group`,
with one of these exact values. Studio props without the property are excluded.

| Group | Empty marker | Meaning |
|---|---|---|
| `body` | `origin_body` | Body/interior, usually at world zero |
| `steering` | `origin_steering` | Steering-wheel center, steering around runtime Z |
| `wheelFL` | `origin_wheelFL` | Front-left wheel center |
| `wheelFR` | `origin_wheelFR` | Front-right wheel center |
| `wheelRL` | `origin_wheelRL` | Rear-left wheel center |
| `wheelRR` | `origin_wheelRR` | Rear-right wheel center |

Add the seventh empty, `driver_eye`, at the seated driver's eye position. All six
groups require geometry even if `cockpit` is false. Left/right axle markers must
share forward position within 2 cm; front wheels must be ahead of rear wheels.
Wheel axles run along X. Author an actual interior and window openings; the exporter
does not hollow a closed body automatically. Closed thin panes render on both sides.

Assign a named material to every face. Up to 64 palette materials are supported.
The exporter reads Principled Base Color, converts linear RGB to sRGB for the PNG,
and emits per-face swatch UVs. A material named exactly `glass` receives transparent
runtime shading. PBR image textures, animated shader nodes and arbitrary Blender
materials are not exported. Runtime glass and Blender transmission are different
shaders, so the studio render is an authoring preview rather than a game screenshot.

```powershell
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
& $blender 'my-car.blend' --background --python creator\export_vehicle.py -- --output 'C:\Vehicles\my-car' --check
& $blender 'my-car.blend' --background --python creator\export_vehicle.py -- --output 'C:\Vehicles\my-car'
.\tools\VehicleTool\bin\Release\net48\VehicleTool.exe validate 'C:\Vehicles\my-car\vehicle.json'
```

`--check` performs evaluated-mesh export checks and reports each group's triangles
without writing any files. The normal export writes the manifest's model/texture
filenames. Add a PNG preview yourself, or omit `preview`. VehicleTool then runs the
same strict package and geometry validator used by the runtime. The runtime creates
its actual chooser preview from the 3D model, not from that PNG.

To reproduce the provided examples, use `tools/build.ps1 -RegenerateModels`.
The Turtle adapter reads the original cosmetic master without changing it and
creates an editable marker-equipped copy under `artifacts/turtle-van-creator.blend`.

## Package contract

One folder contains `vehicle.json`, one model JSON and one PNG palette, plus an
optional preview PNG. Asset references are simple filenames, never absolute paths
or subdirectories. Linked files/folders inside the package boundary are rejected;
a linked Steam-library ancestor outside that boundary is allowed. VehicleTool's
`pack` command includes only declared data files; `.blend`, scripts and DLLs are
excluded. Archives are creator packages, not Steam Workshop uploads.

Manifest schema 2 requires `id`, `name`, `author`, `version`, `donorPrefab`, `model`,
`texture`, and a positive authored `wheelRadius`. `description`, `preview`,
`cockpit` (default true) and `physics` are optional. Remove `physics` for a purely
cosmetic package. Unknown fields, duplicate JSON keys, trailing JSON, non-finite
numbers, missing assets and unsupported schemas fail validation.

Model schema 1 declares `units: "metres"`. The exporter converts to runtime X right,
Y up, Z forward and reverses triangle winding. Each part has group-local positions,
normalized normals, swatch UVs and indexed triangles. Body fitting preserves
proportions with a uniform scale based on wheelbase. Cosmetic wheel offsets widen
the drawn track, while physical contact patches remain those of the donor.

Bounds: 64 KiB manifest, 64 MiB model, 16 MiB per PNG, decoded PNG dimensions at most
4096 square, 128 parts, 100,000 triangles, 300,000 vertices, six named groups, and
64 materials. A catalog accepts at most 16 packages, 500,000 total triangles,
256 MiB estimated decoded textures and 192 MiB source bytes. These are input budgets,
not a measured peak VRAM promise. PNG headers are checked before allocation; Unity
still performs final image decoding and a failed preview is isolated.

## Experimental physical fields

These describe the staged implementation. **None applies while the runtime
qualification lock remains false.** The schema deliberately omits arbitrary scripts,
custom solver code, tire curves, drive-layout switching and differential overrides.
Missing optional fields inherit the donor; a supplied engine or gearbox block must
be complete. Engine power and torque describe the game's generated engine model,
not a measured dynamometer curve. Do not set peak torque RPM equal to peak power RPM.

| Field | Unit / convention |
|---|---|
| `massKg` | Total rigid-body mass, kilograms |
| `frontWeightFraction` | Front fraction, e.g. 0.54 means 54% front |
| `centerOfMassM` | X/Y/Z metres relative to the donor root, not mesh bounds |
| `inertiaScale` | Dimensionless native inertia multiplier |
| `engine.powerKw` | Kilowatts; converted using the donor's `CV2KW` field |
| `engine.torqueNm` | Newton-metres |
| `peakPowerRpm`, `peakTorqueRpm`, `idleRpm`, `limitRpm` | Engine RPM |
| `gearbox.forwardRatios` | 1–8 positive ratios, strictly decreasing |
| `gearbox.reverseRatio` | **Negative** ratio; neutral is inserted by the runtime |
| `gearbox.finalDrive` | Positive final-drive ratio |
| `frontAxle` / `rearAxle.travelM` | Suspension travel, metres |
| `springRateNPerM` | Newtons/metre |
| `bumpDampingNsPerM`, `reboundDampingNsPerM` | Newton-seconds/metre |
| `brakeTorqueNm` | Axle setting, then subject to native brake-balance processing |
| `dimensions.wheelbaseM` | Front-to-rear mount spacing, metres |
| `frontTrackM`, `rearTrackM` | Left-to-right mount spacing, metres |
| `tireRadiusM`, `tireWidthM` | Native tire dimensions, metres |
| `collision.centerM`, `collision.sizeM` | Box center and full extents in root axes |

Position conversions require a positive, uniform donor-root scale. The physical
box replaces one instance's native body MeshCollider mesh with an owned convex
box. It is not a detailed damage hull. Shared donor meshes are never edited.

## Identity and removal

Keep an ID stable across updates; change version when content changes. The loader
fingerprints length-prefixed manifest/model/texture/preview bytes and emits a
separate normalized physics hash. `VehicleIdentity` records schemas, ID/version,
donor and whether physical changes were applied. Matching the name alone is not
enough to reproduce a vehicle. Integration into the main session recorder remains
pending; diagnostic reports and CLI inspection already emit the identity.

Native saves contain only the donor index. The framework remembers IDs in its own
state file and resolves them against the current catalog. Missing packages fall
back to the stock donor. Add/remove/update packages with the game closed; the
loader does not hot-reload changed files. Two packages may use the same donor.
