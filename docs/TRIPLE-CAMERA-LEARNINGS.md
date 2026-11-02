# Triple-screen cameras and field of view: learnings (2026-11-01)

Owner-driven iteration on art of rally (0.4.4-rc.24 to rc.28) plus a comparison against Assetto Corsa on the owner's
rig. Written as the handoff for making this the Wheelkit / portfolio camera standard. Everything here was either
owner-decided, seen on the rig, or measured; offline-only items are marked.

Rig: three 32" 2560x1440 panels (708 x 398 mm image), 1500R, sides turned 70°, eye 660 mm, 8 mm image gap, separate
Windows displays (not Surround: left at x -2560, centre 0, right +2560).

## 1. Projection: what the research and the rig agree on

- Per-screen off-axis (asymmetric) frustum from the eye is standard practice (iRacing, AC, AMS2 triple modes; Unreal
  nDisplay; Unity off-axis). Our approach matches it. Curve correction and true separate-display output are rare in
  shipped games (iRacing reportedly partial, ACC a 0-1 slider for curved ultrawides, AC only via a CSP filter); we are
  at or ahead of the field there.
- "Realistic" = the calculated, seat-true field of view (owner rig: about 34° vertical). The community split is real:
  seat-true feels slow / "binoculars", wider feels faster but loses scale.
- **Any field of view wider than seat-true stretches the side screens.** The image is correct for a virtual eye closer
  than the real one, so from the seat the periphery looks pulled (a wide-angle lens). Angle correction does not remove
  this; it only keeps the seams continuous. (An earlier answer to the owner said Game FOV does not stretch; that was
  wrong and was corrected.) Single-screen defaults such as 75-80° vertical are extreme on triples (virtual eye ~2.5x
  closer).
- With these panels the seat-true view spans almost exactly ±90°. A camera at the nose therefore sees nothing beside
  or behind it at seat-true FOV; widen it and the car's own wings appear on the side screens.

## 2. Per-camera defaults on triples (owner decision)

| Camera | Starts in | Reason |
|---|---|---|
| Cockpit / interior | Realistic | True to the seat (AC agrees). |
| Bonnet | Game (wide) | "Not realistic, but it was never meant to be; it was meant to be fun." Must show the bonnet. AC's seat-true bonnet camera shows no bonnet; the owner considers that a bug. |
| Bumper | Realistic | Road only: a wider view adds stretch and no information. |
| Chase / third person | Game (the game's own, speed-dependent) | Fun. Still projected per screen (AC does the same). |

Single-screen players keep the game's behaviour; the Game/Realistic choice only exists with triples.

## 3. Interaction model (implemented in Art, owner-approved)

- **F7 switches Game/Realistic on the current camera; each camera remembers its own choice** (bonnet set to Game,
  bumper to Realistic, cycling back to bonnet gives Game). Art keys: `OverrideFieldOfView` (bonnet), `BumperRealistic`
  (default true), `ChaseRealistic`, `CockpitRealistic` (default true) in TripleScreen.xml.
- **Tweaks are offsets on top of a calculated start.** Position offsets are per car, per camera, per set; field-of-view
  offsets are per camera and set, shared by every car (FOV is a taste, position depends on the car). Numpad keys move
  and zoom (STD-005/006 layout); no position limits on the player's own tweaks.
- **Reset (numpad 5)** returns the camera to the calculation: this car's position and that camera's FOV for every
  car; the overlay says so.
- **"(Custom)"** is appended to the camera label when the set's FOV differs from stock (FOV only; position tweaks are
  normal per-car fitting). No separate Custom mode: Realistic and Game are fixed anchors with optional tweaks.
- **Camera overlay on every change**: name near the bottom ("Bonnet Camera - Game FOV (Custom)", "Chase 2 -
  Realistic") plus key reminders; refreshed when the FOV changes.
- **Wheelkit Apply writes only the starting choice per camera, never the player's offsets.**

## 4. Mounted-camera placement from the car model (Art; offline-tested, rig-checked on one car)

- Bonnet: centre line, 10 cm ahead of the windscreen base, 8 cm above the bonnet surface. Bumper: centre line, 5 cm
  ahead of the front, halfway up the front face (between bottom and bonnet height). Same start for both sets.
- Shape from a centre-line top profile (readable meshes, else MeshCollider raycasts), in the car's own oriented
  mesh boxes (world-aligned bounds made cars 5.5-7 m wide). Plausibility gate: 1.2-3.2 m wide, 0.8-3.6 m tall.
- **Find the windscreen by steepness**, walking forward from the roof while the surface falls steeply (>0.35 rise per
  metre); where it flattens is the windscreen base and its height is the bonnet. A front-quarter median took a sloping
  nose for the bonnet and put the camera inside the car. A rear wing above the roof is ignored.
- Keep-outside-the-body applies to the calculated start only; the owner wanted no limits on tweaks.
- The reference-car fit ("start from the last car you tuned") was removed: unpredictable and it spread bad tunes.

## 5. Mounted-camera rendering details

- **Near clip 5 cm** on bonnet/bumper (restored on release): the game's chase-camera near plane cut the light pods
  half a metre ahead.
- **Rotation smoothing 0.06 s** (position rigid; snap on >20° jumps): a rigid mount showed the body's tremor at rest.
  AC goes further: its cockpit camera adds deliberate small shake plus G-force head movement with lag
  (`camera_onboard.ini` [SHAKE], [GFORCES] LAG=0.8).
- **Bumper view hides the car** (renderers set to shadows-only, effects untouched, restored on leaving) as a backup
  for anyone who widens it.

## 6. Assetto Corsa comparison (owner rig, 2026-11-01)

- Setup that works without Surround: Content Manager placed as `AssettoCorsa.exe` (original kept as
  `AssettoCorsa_original.exe`; CM's AppId starter failed "Steam API has failed to initialize"), CSP 0.2.11,
  `video.ini` 7680x1440 windowed with `[CAMERA] MODE=TRIPLE`, CSP `dxgi_tweaks.ini` TRUE_BORDERLESS=1,
  CENTER_WINDOWED=0, SAVE_WINDOW=1 and `[VIDEO] _EXT_POS_X=-2560 _EXT_POS_Y=0` in Documents `cfg/video.ini`.
  Geometry is entered in AC's in-game Triple Screen app in millimetres (angle 70 = fold from flat, distance 660,
  width 708, margin 4 per edge); it writes `cfg/triple_screen.ini` (normalised LEFT_POINT/RIGHT_POINT per screen plus
  SP_* spinner values; seen only with defaults so far, apparently written on exit). Cockpit FOV is
  `cfg/camera_onboard.ini` [MODE] FOV (vertical).
- Observed: every camera (cockpit, two chase, bonnet, bumper, dash) uses the triple projection; the framing looks the
  same across cameras, i.e. one seat-true FOV everywhere (inferred from screenshots, not confirmed in config). Bonnet
  camera shows no bonnet; bumper camera shows no part of the car.
- CSP `triple_custom.ini` [CAMERA_MODES_BEHAVIOR] lets each camera type use the triple projection, disable it, or
  render on the middle screen only: a convention option worth offering.
- As a Wheelkit game, AC needs no code mod: wheel, FFB, telemetry (SimHub native) and triples exist. Integration would
  be writing the rig profile into `controls.ini`, FFB settings, `video.ini`, `triple_screen.ini`, `camera_onboard.ini`
  and the CSP files above. Portfolio decision pending the owner's opinion of it; Shutoko Revival Project 0.9.3 is
  installed as the "fun" content.

## 6b. Decisions after the AC comparison (owner, 2026-11-01 evening)

- **Eye distance is measured to the plane through the panel edges**, as AC and the FOV calculators take it. Our core
  subtracted the curve's sagitta (42 mm at 1500R), placing the panels too close; near objects jumped at the seams in
  every mode. Fixed in the shared core (toolkit `74c5eda`, Art rc.30). The curve now only warps the image.
- **One borderless window spanning the screens is the only presentation mode** (AC/CSP style: three views drawn into
  one 7680x1440 window at x -2560, no Surround needed). No separate-window fallback: the owner judged it extra plumbing
  that is hard to maintain and another mode to test. Surround and non-Surround then share one code path. AC's margin 4
  gives about 3.3 mm between images (we use the measured 8 mm).
- **Ultrawide (32:9) users stay a goal**: proper cylindrical projection for curved ultrawides, as SimCurve does. This
  fits the same pipeline: a curved ultrawide is a few virtual flat slices on a cylinder, each an off-axis view, warped
  by the curve, all in one window.
- Future idea: an in-game overlay like AC's Triple Screen app (live diagram, sliders) for tweaking the rig per game.
- **"More zoomed than AC" at the same seat-true FOV is the cockpit, not the maths** (owner, Turtle Van vs AC's Abarth):
  the van's eye sits high and close behind a large flat dash. Uniform world scale cannot cause it (a uniformly larger
  world looks identical from the same eye). First check the actual FOV: the van's "Realistic" cockpit had been saved
  at 47.6° and later 65° without any label saying so (fixed: "(Custom)" on the cockpit label, reset to the seat FOV).
  Moving the eye back/down keeps the seams exact; widening the FOV bends near objects at the seams. The owner settled
  on a mix of both for the van: a per-vehicle taste, not a default.
- **Owner vision for Wheelkit My Rig**: a config window like AC's where the user sets the number of monitors, resolution,
  physical size, eye distance and curvature per display (including edge cases such as one 49" 32:9 at 1000R: from an
  eye at 800 mm the curve's centre is almost at the eye, so it spans about 73°; an earlier "100°" estimate was wrong). A top-down diagram shows each display as a line or arc, the
  head and the view cone, live. The example render corrects for that rig with the same toolkit projection code games
  use. The N-display rig exists: toolkit `8aa9c60` (`RigDefinition`, `RigPlanner`: flat or curved displays, arc slices,
  spanning-window pixel rects; the triples preset reproduces today's matrices exactly).

## 7. Wheelkit ideas from the owner

- **My Rig live diagram**: a top-down drawing of the screens and the head that updates while width, distance, angle and
  bezel are typed (AC's Triple Screen app: centre green, sides red, yellow view cone). The owner liked it.
- **Test cockpit**: a simple scene (grid floor, straight road with lane lines, posts at known distances) rendered on the
  real screens with the rig projection and curve warp, to check seams, straightness and FOV before any game.
- **Cameras tab** (ISS-0023): key bindings for camera adjustment plus the per-camera starting choice from section 2.

## 8. Open

- EA WRC (Codemasters) screenshots of bonnet, bumper and chase on triples, to check how wide a big studio starts the
  bonnet camera.
- Read `triple_screen.ini` after AC exits with the real values (format for Wheelkit).
- AC curve correction via the CSP scriptable curved-screen filter; AC per-car bonnet/bumper positions (car data is in
  `data.acd`; Content Manager can unpack it).
- Art: the full `tools/testing/Test-Rc.ps1` gate does not build (Lifecycle test stubs lag rc.12+), and two WheelInput
  tests default to deleted worktree shim paths (run with `ART_SHIM_DLL` / `ART_LATCHED_SHIM_DLL` set to the shipped
  `dbce_shim.dll`).
- Custom Vehicles cockpit still keeps its own per-vehicle view store (not the offset model) and no 5 cm near clip
  change (it already uses 2.5 cm).

Sources: [simracingcockpit FOV calculator](https://simracingcockpit.gg/fov-calculator/),
[trophi.ai iRacing FOV](https://www.trophi.ai/post/iracing-fov-getting-your-field-of-view-right),
[Reiza triple-screen tutorial](https://forum.reizastudios.com/goto/post?id=35362),
[Reiza ultrawide correction thread](https://forum.reizastudios.com/threads/ultrawide-perspective-correction.10820/page-3),
[overtake.gg true triple list](https://www.overtake.gg/threads/games-list-with-true-triple-screen-and-or-vr-support.158838/),
[isrtv triple support explainer](https://www.isrtv.com/pc-racing/triple-screen-support-sim-racing-explained/).
