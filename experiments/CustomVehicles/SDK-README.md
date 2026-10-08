# Custom Vehicles creator kit — Windows development preview

This offline kit includes the generic Blender exporter, editable Trail Scout and
Turtle Van models, a standalone validator/packer, and the authoring contract.
No game, Unity or UMM assemblies are included. `tools/VehicleTool.exe` runs on
.NET Framework 4.8; its Newtonsoft.Json dependency comes from NuGet 13.0.3 and
includes its MIT license. Blender 5.2 was used for the examples.

1. Copy an example folder to a new working folder. Open its `.blend` in Blender.
2. Edit `vehicle.json`: use a new ID/name/author/version and an explicit stock donor.
   Remove `physics` to describe a purely cosmetic vehicle. Physical overrides in
   the companion runtime candidate remain locked pending attended qualification.
3. Keep the six `group` labels and seven empty markers described in CREATOR.md.
4. From this kit's folder, export and validate:

```powershell
$blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe'
& $blender 'my-car\my-car.blend' --background --python creator\export_vehicle.py -- --output 'my-car' --check
& $blender 'my-car\my-car.blend' --background --python creator\export_vehicle.py -- --output 'my-car'
.\tools\VehicleTool.exe validate my-car\vehicle.json
.\tools\VehicleTool.exe inspect my-car\vehicle.json
.\tools\VehicleTool.exe pack my-car\vehicle.json my-car.zip
```

`--check` writes nothing. The normal export bakes geometry and generates an sRGB
palette PNG with UVs. Supply your own preview PNG, keep the example while drafting,
or omit the preview field. The archive includes only declared data files; editable
sources and executable tools stay out of vehicle packages.

CREATOR.md's longer CLI paths refer to the development checkout. In this kit use
`tools/VehicleTool.exe` as above. `creator/make_example.py` can rebuild Trail Scout
and its studio renders. The Turtle master here already contains the generic markers.

This is an unpublished development kit, not a stable public SDK. Adding a vehicle
folder to the separately built framework still requires the framework runtime and
a valid donor/DLC in the game. Do not run the cosmetic Turtle Van plugin and this
framework simultaneously. See QUALIFICATION.md for the pending in-game checks.
