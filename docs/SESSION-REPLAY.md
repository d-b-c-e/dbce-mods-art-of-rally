# Whole-session recording and replay

Records a session from game launch (intro, menus, stage choice, the drive, quitting) and
replays it into the game for automated testing. Developer probe only
(`tools/testing/Recorder`, installed with `tools/testing/Install-Recorder.ps1`). Release
packages never include it.

```powershell
.\tools\testing\Session.ps1 -Record -Name <name>    # arms the probe, launches the game
.\tools\testing\Session.ps1 -Stop                    # optional; quitting the game also ends it
.\tools\testing\Session.ps1 -Replay -Name <name>    # launches, replays, waits, closes the game
```

The owner asks for a recorded session. Claude runs `-Record` and tells the owner how to
finish (normally: quit the game from its menu). Afterwards Claude checks the tape. There is
no record button in the mod.

## What is taped

| Layer | Hook | Unit |
|---|---|---|
| Menus, game buttons, paddles | Rewired `Player` getters (`GetButton*`, `GetNegativeButton*`, `GetAxis*`, `GetAnyButton*`) | rendered frame |
| Intro "press any key", raw keys, mouse buttons | the game's global `Input` wrapper | rendered frame |
| Car | final `AxisCarController.GetInput` values (after the mod's wheel override), plus gear and rigidbody pose | physics step |

Each frame and step carries a marker: `scene | top menu panel | event status`. Files go in
`results/sessions/<name>/`: `input.tape`, `car.tape`, `markers.tsv`, `events.log`,
`session.txt`, `shots/` (a screenshot at each new marker) and `playerprefs-at-start.reg`.

## Replay

- Inputs play one marker segment at a time. Replay waits up to two minutes for the live game
  to reach the next segment's marker, so loading-time differences don't shift inputs. A
  segment that never arrives fails the replay.
- Physical wheel, pedals and keyboard are ignored. **All force output is muted.**
- Gear is set from the tape. If the car drifts more than `-PoseThreshold` metres (default 2)
  from the recorded path, it's put back on the recorded pose, velocity included; the count
  goes in the result.
- Output in `results/sessions/<name>/replay-<time>/`: `result.txt`, `replay.log` and
  `shots/`. Compare these screenshots with the recording's, marker by marker.

## Limits

- Use keyboard, wheel or pad buttons in menus, not the mouse. Mouse pointer position isn't
  taped.
- The game's save data (Steam Cloud `cloud\` folder) isn't restored before replay. If the
  game remembers the last stage or car, a replay can start from a different selection; the
  marker check then fails the replay instead of driving the wrong stage. PlayerPrefs are
  exported at record start for reference.
- Car physics isn't deterministic across runs. Pose correction keeps the replay on the
  recorded line, so use the force trace for FFB comparison, not exact equality.
