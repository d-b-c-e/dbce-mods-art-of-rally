# Course route capture (opt-in, private, read-only)

2026-10-19, levi (kobe 286855). Source candidate. The owner asked for telemetry maps; this captures one stage's
own route and the game's own progress, for the toolkit course seam (toolkit `docs/COURSE-TELEMETRY.md`, Zola). It
follows the Woden (`8fb62ff`) and iRacing (Mubi `08357c0`) captures. It has not run in the game.

## Off by default

Nothing happens unless an operator creates `Mods/ArtOfSimRally/art-course-route-next-launch.json` before starting the
game:

```json
{ "Version": 1, "Id": "<new guid>", "ExpiresUtc": "<ISO 8601, at most 20 minutes ahead>",
  "GameAssemblySha256": "<sha256 of artofrally_Data/Managed/Assembly-CSharp.dll>", "MaxSeconds": 600 }
```

At load, the mod renames (claims) the file before reading it, so a stale, invalid or crashed request never repeats.
A request is refused, and nothing is captured, if any of these hold:
- it is expired or more than 20 minutes ahead;
- it is pinned to another game build;
- it is malformed or oversized;
- it has unknown or duplicate keys.

Only an accepted request patches a `CarDynamics.FixedUpdate` postfix. Without one there is no per-frame work. The
capture never writes game state, never skips game code, sends nothing (no UDP, no SimHub) and changes no setting.
Steering, force feedback, telemetry and performance are unchanged.

## What it reads (installed `Assembly-CSharp` 7807A1D6…, decompiled read-only)

| Value | Source |
|---|---|
| Stage identity | `GameModeManager.GameMode`; `GameModeManager.GetRallyDataCurrentGameMode().GetCurrentStage()`: `Area`, `Name`, `SceneConstant`, `IsForwardStage`, `Distance` |
| Route | `EventManager.outOfBoundsManager.GetWaypointList()`, children of "Waypoints Parent" named `*Waypoint*` in the game's order, already reversed by the game for reverse stages; `IsForwardStage()` |
| The HUD's progress range | the private `StartingWaypointIndex` and `LastWaypointIndex`, read once by reflection; null if absent |
| Per physics step | the player car's position, `GetCurrentWaypointIndex()`, `EventManager.status` |

The route is read once, on the first step with status `UNDERWAY`. Sampling continues until one of these:
- the finish animation, `FINISHED`, the finished screen or a replay;
- the request's seconds (at most 600);
- 36,000 rows;
- a read fault, which writes nothing.

## Output

Each capture goes to a new private folder, `%LOCALAPPDATA%\Dbce\CourseExtraction\art-<area>-<stage>-<fwd|rev>-<UTC>\`.
It is written to a temporary folder and then renamed. An existing folder is never overwritten, and a failure leaves
nothing partial.

The folder holds three files:
- **`route.json`** (`dbce.art-course-route@1`): the game and its build hash, request id, game mode, area, stage name,
  scene constant, stage and route direction flags, stage distance, the waypoints (Unity world, unconverted), the
  game's start and last indices, the outcome and row counts.
- **`progress.tsv`**: `tick sim_s px py pz waypoint_index status`.
- **`complete.tsv`**: the `dbce-playback-seal-v1` sizes and sha256 of both files.

Limits: at most 20,000 waypoints (more is refused, not truncated), 36,000 rows and 8 MB.

## Stated unknowns (recorded in `route.json`, not guessed)

- **Distance:** `Stage.Distance` is what the game displays as km. It is not checked against the route's length.
- **Axes:** Unity's x east, y up, z north follow the toolkit's mapping and are not verified on screen.
- **Direction:** the waypoint order is the game's list order; driving direction along it is not checked here.
- **Progress:** a coarse waypoint index, not a distance. The HUD shows `(index - start) / (last - start)`.

The schema is game-specific, like iRacing's, because the Woden `dbce.course-route@1` reader requires numeric
`kms`/`unitConversionFactor` that art of rally does not expose. No normalized course sample is produced.

## Checks

`tests/CourseRoute` runs the pure capture (`src/ArtOfSimRally.Mod/CourseRouteCapture.cs`) on temporary files:
- request claim and every refusal case;
- route refusals;
- skipped rows, and the time and row limits;
- the exact sealed write, JSON escaping and folder name;
- never overwriting, no partial folder, and no write after a fault or without rows.

The game glue (`CourseRouteDump.cs`) is compiled with the mod. A runtime capture is a separate, granted step.
