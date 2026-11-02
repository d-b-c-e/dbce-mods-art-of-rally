# Rig vehicle candidates — 2026-11-01

Offline response to vemi requests 490579/490598. No installation, game launch, rig use or change to the accepted Turtle Van master. These are candidates for conversion and seating checks. Generated files and reference images are in `artifacts/rig-vehicles/` (git-ignored); preserve that folder for handoff. The active Blender scene was unchanged.

## Files

Paths relative to `experiments/CustomVehicles/artifacts/rig-vehicles/`:

- `turtle-van-real-scale/`: `vehicle.json`, `model.json`, `palette.png`, `turtle-van-real-scale.blend`, `turtle-van-real-scale.glb`, `measurements.json`, exterior/cockpit/rear-cabin PNGs.
- `fj62-cabin/`: original `upstream.scene.json` and `upstream-fj62.glb`; candidate `scene.json`, `materials.json`, `fj62-cabin.glb`, `fj62-cabin.blend`, `diagnostics.json`, `measurements.json`, exterior/cockpit PNGs, Construct LICENSE/NOTICE.

Van package: `dbce.turtle-van-real-scale` 0.1.0, donor `Bonus_LtVanLow`, no physics override. The same model.json serves Custom Vehicles and the test converter. Game donor-wheelbase fitting may still scale an instance: these are authored dimensions, not in-game measurements.

## Dimensions (metres)

Runtime JSON: X right, Y up, Z forward. Both GLBs: X left, Y up, Z forward. Van Blender file: X right, Y forward, Z up. Its export rotates around Z by 180 degrees to produce the agreed glTF convention. Do not apply another front/back correction.

| Measurement | Turtle Van | FJ62 cabin |
|---|---:|---:|
| Overall length | 4.526279 | 4.785503 |
| Body width | 2.026766 across paint/armor | 1.800 upstream target; new side shell 1.796 |
| Width with mirrors | 2.268390 | 2.110000 |
| Cab roof top | 2.200000 | 1.811 upstream |
| Overall top | 3.100000 turret/muzzle | 1.900270 antenna |
| Lowest point | -0.002500 tire tread | 0 |
| Wheelbase | 2.794000 | 2.730000 |
| Front/rear track | 1.720000 / 1.720000 | 1.470000 / 1.470000 |
| Nominal tire radius | 0.350000 | 0.395000 |
| Tire outer geometry radius | 0.352500 with tread | 0.395000 |
| Eye, runtime X-right | (-0.427895, 1.650000, 0.450825) | (-0.380000, 1.570000, -0.500000) |
| Eye, glTF X-left | (+0.427895, 1.650000, 0.450825) | (+0.380000, 1.570000, -0.500000) |

Van `driver_h_point` is (0.427895, 0.966279, 0.306116) in glTF coordinates (negate X for runtime). It is an inferred design Empty near cushion level, not an SAE manikin measurement. Eye, H-point, track, raised roof and tire choice are inferred design decisions within the supplied brief. The van has 2.5 mm of tire-tread penetration at nominal hub height; this is not a loaded-tire measurement.

## Turtle Van

Closest dimensional platform: **1986 Chevrolet G10/Sportvan, 110-inch wheelbase**, not a claim that the fictional vehicle is a Chevrolet. GM archive PDF page 13 (printed Sportvan/Rally page 6, September 1985) gives G11006: 110-inch wheelbase, 178.2-inch length, 79.5-inch width, 79.4-inch standard height. Metric: 2.794 / 4.52628 / 2.01930 / 2.01676 m. Candidate uses a deliberately raised 2.2 m cab roof plus fictional accessories. Armor width is about 7.5 mm wider than nominal platform width. No factory eye/H-point was found or invented.

`creator/make_rig_turtle.py` reads the accepted TurtleVan master and asserts its SHA-256 stays unchanged. Evaluated meshes retain object names, materials and group membership. Separate longitudinal axle/overhang mapping and vertical cabin mapping correct proportions. Wheel scaling about each pivot preserves circular tires; steering is separately resized to about 0.38 m diameter.

Kept original yellow/green shell, twin roof cannons, red radar, spoiler, visor, side armor, vents, grinning bumper, mesh lettering, hollow cabin, seats, gauges, steering and pedals. Added closed roof hatch with gasket/hinges/handle; door trim, armrests and pulls; belts and anchors; doghouse console, radio, switches and vents; rear workstation with monitor/keyboard, storage drawers, pizza box, ceiling ribs, dome lights and rear handles. Static design details, not working mechanisms. No opening-door, cannon or hatch animation.

Independent visual review found the original shell-base lower hemisphere hanging through the cabin ceiling. Candidate now clips that underside at roof level. The actual shell roof above the cab remains decorative; its dimensions are not OEM measurements.

## Construct FJ62

Upstream Construct 1.0.0 at `2a12a1185690ac2683709d0e97d55bfdb251a23c` contains a **rudimentary interior**: torus steering wheel, two front seat-back boxes, rear seat-back box, box dashboard. All four are `inferred`, citing the exterior FJ61 blueprint. No cushions, controls or eye marker. Closed `body` and `glasshouse` lofts do not establish a usable cockpit simply because interior objects exist.

Upstream facts label WB 2.73, length 4.75 and width 1.8 `verified` against Oulu's J6 compilation; height 1.8 `verified` against fastcarcheck; tire diameter .79 `inferred` from the blueprint. Oulu cites Toyota owner manual OM60461E and regional heights; its European track values are 1.475 front / 1.460 rear versus model 1.470 both. Those source labels are retained, not independently upgraded to factory-verified geometry. Fastcarcheck returned HTTP 403. Attachment bounds differ from nominal facts.

`creator/make_fj62_cabin.py` replaces sealed cabin masses with thin sides, floor, bulkheads, posts and glazing. Adds cushions/bases/headrests, console, two levers, gauges, vents, radio, heater controls, steering column/spokes and passenger handle. A restored FJ62 cabin photo informs layout; all additions and eye are `inferred`. This is a cabin blockout, not a measured or finished replica. Mirrored wheels are combined under `tyre-front`/`tyre-rear`; split connected components if independent wheel objects are needed.

Visual review found overly reflective glass unsuitable for sightline checks. The exported candidate replaces transmission with double-sided 10% alpha glazing for simple test renderers; original scene/material map and upstream GLB remain available. This compatibility finish is not a glass-material measurement.

## Sources and distinctions

Reference-only imagery stays under local `research/`; no downloaded imagery is embedded as model textures.

1. [GM Heritage G-Van kit](https://news.chevrolet.com/content/dam/company/no_search/heritage-archive-docs/vehicle-information-kits/chevrolet/1986-Chevrolet-G-Van.pdf): platform table visually checked at PDF page 13; local `gm-page13.png`.
2. [NECA official 1987-cartoon van](https://store.necaonline.com/products/teenage-mutant-ninja-turtles-1987-cartoon-the-turtle-van): photo 18 inspected, plus official feature descriptions. Cartoon-style shell, twin cannons, red radar, visor, yellow side treatment and cabin guide the candidate. Side-chair deployment not implemented.
3. [Playmates 2013/1988 comparison](https://www.tmnttoys.com/13vehicles/classicspartywagon/index.html): comparison photo inspected; 1988 on right has orange side panel, unequal wheel sizes, upper side windows and different roof equipment. Candidate retains cartoon-style uniform wheels and green roof instead of mixing every toy feature.
4. [Playmates instructions](https://w.tmnttoys.com/21vehicles/partywagon/instruct.pdf): spoiler, flip top, mirror blasters and Foot Tenderizer descriptions establish toy functions, not candidate mechanics or full-size measurements.
5. [Super7 Party Wagon](https://super7.com/products/tmnt-ultimates-party-wagon): later toy interpretation with roof access, safari windshield and seating. Interior-room context, not a real-scale dimensional source.
6. [Mirage Studios TMNT #3](https://miragelicensing.com/comics/mirage/volume01/03/03.html): official 1985 synopsis identifies April's van and mistaken-identity chase. Different van context; no evidence for cartoon turret or command-cabin dimensions. Panel downloaded only as local reference.
7. [Sunbow Marvel Archive model sheets](https://sunbowmarvelarchive.blogspot.com/2024/04/tmnt-1987-model-sheets.html): search located a 171-sheet archive but access was blocked. No claim to have inspected a van turnaround; no episode-by-episode viewing completed.
8. [Barker Designs Mutant Mayhem promotional build](https://brian-barker-fln3.squarespace.com/events1-2): builder description identifies a 1993 Delica and custom interior/rack. Later interpretation, no measurements borrowed.
9. [Jesse Starr Creations fan build](https://www.youtube.com/watch?v=9iSEN6JuE_I): builder description identifies a Kei-van base, illustrating that replicas do not establish one canonical full-size platform. Footage was not measured.
10. [1990 Vanagon fan replica](https://topclassiccarsforsale.com/volkswagen/268896-1990-volkswagen-vanagon-custom-teenage-mutant-ninja-turtles-party-wagon-bus.html): custom seats, console and rear space described by the listing; fan design context, not canon.
11. [Oulu J6 specs](https://cc.oulu.fi/~thu/LC/HJ61/J6.html): checked against Construct facts; Toyota manual is cited there.
12. [Restored 1989 FJ62 reference](https://curatedovrlnd.com/products/1989-toyota-land-cruiser-restored): photo 7734 inspected for round instruments/vents, center radio, passenger handle and floor levers. Restoration means stock accuracy cannot be assumed. No texture copying.

Rights: van derives from this repository's original fan geometry/source under its MIT license; TMNT names/designs remain third-party IP, not rights granted by this work. Construct scene/runtime are Apache-2.0; LICENSE/NOTICE accompany outputs. Blender extension is GPL-3.0-or-later; outputs are not claimed by it. No public release is implied.

## Checks and limits

Van creator export and strict VehicleTool validation pass, within the 100k-triangle limit. Exact final count/hash: `van-validation.json`. Independent JSON checks assert WB, track, eye, circular tires and budget. Exported model/palette reconstructed for inspected exterior/cockpit/rear renders.

FJ62 Construct check: zero errors, 12 tiny-detail warnings (10 inherited, two gauge needles). Candidate GLB: 251 parts / 22,966 triangles; upstream: 214 / 20,846. Blender imports and renders the candidate. Diagnostics, import measurements and render logs retained locally.

Windows browser captures cannot start DevTools with Chrome or installed Chromium. Blender offline rendering works. Blender MCP's addon rejects background mode intentionally; geometry jobs use Blender CLI directly. Full new Construct Turtle Van authoring was not attempted: improving the existing richer Blender model was the selected route.

Owner seating, triple-screen projection/framing, rig feel, game rendering/donor scaling and driving remain untested. Plain studio renders lack road/horizon cues; Wheelkit's calibration scene supplies those next. These files are ready for converter checks, not final acceptance.

Reproduce: Blender 5.2.1 background with `creator/make_rig_turtle.py`; Python `creator/measure_rig_turtle.py`; existing VehicleTool validates vehicle.json. Python `creator/make_fj62_cabin.py` authors and exports FJ62 using the local Construct checkout. Local render scripts/logs are in `artifacts/rig-vehicles/` and are not shipping runtime code.

Final review: independent critic rechecked the corrected van rear cabin and FJ62 cockpit. Roof intrusion and distorted glazing blockers cleared; adequate for calibration-scene prototype checks, with physical clearance/rig fit unverified. Van final validation: 87,332 triangles, 31 parts, package hash cec23e89bd52ae66f885fac3cba46b4e0268c975b9d5d02cf6b66c99d5bd1ca8. GLB eye translation was independently parsed and asserted. Checksums: artifacts/rig-vehicles/SHA256.json. Exterior/forward van images predate only the underside clip; corrected rear image is from the final export.

Handoff source base: 551e8d6bbdc717ae55d5c07192630fae79ba2414. The report and three creator scripts are committed together; generated output stays local under artifacts.
