# Whole-session recording and replay

Records a session from game launch (intro, menus, stage choice, the drive, quitting) and
replays it into the game for automated testing. Developer probe only
(`tools/testing/Recorder`, installed with `tools/testing/Install-Recorder.ps1`). Release
packages never include it.

```powershell
.\tools\testing\Session.ps1 -Record -Name <name>    # arms the probe, launches the game
.\tools\testing\Session.ps1 -Stop                    # optional; quitting the game also ends it
.\tools\testing\Session.ps1 -Replay -Name <name>    # strict replay; exit 0 passed, 1 failed, 2 aborted
.\tools\testing\Session.ps1 -Replay -Name <name> -Assist   # eases drift back onto the taped line
```

The owner asks for a recorded session. Claude runs `-Record`, launching the game the way the
Stream Deck button does, and tells the owner how to finish (normally: quit from the game's
menu). Claude then checks the tape. There is no record button in the mod.

## What is taped (format 2)

| Layer | Hook | Unit |
|---|---|---|
| Menus, game buttons, paddles | Rewired `Player` getters (`GetButton*`, `GetNegativeButton*`, `GetAxis*`, `GetAnyButton*`); last value read in the frame | rendered frame |
| Intro "press any key", keys, mouse buttons | the game's global `Input` wrapper | rendered frame |
| Splash screen end | `SplashScreenControl.EndSplashScreen`, the game's only Rewired input-event consumer | rendered frame |
| Mod manager window open/closed | UMM UI state; it opens at startup and blocks stock menus | rendered frame |
| Car input | `CarController` input fields at the start of `FixedUpdate` | physics tick |
| Gear shifts | every `Drivetrain.Shift` call, with the tick `Drivetrain.FixedUpdate` first acts on it | physics tick |
| Car pose | rigidbody position, rotation, velocity (for measuring divergence) | physics tick |

Each frame and tick carries a marker: `scene | top menu panel | event status`. The resolved
scenario (scene, car, weather) is taped at the countdown. Files go in
`results/sessions/<name>/`: `session.txt`, `input.tape`, `car.tape`, `shifts.tape`,
`markers.tsv`, `events.log`, `shots/` (one screenshot per new marker) and
`playerprefs-at-start.reg`. Files are created exclusively, and a write error marks the tape
`incomplete.txt`, which replay refuses.

## Replay

- **Strict by default.** The car gets the taped input fields and shifts on the taped ticks;
  every other shift of the player car (physical shifter, paddles, the game's auto
  first/reverse) is blocked. The car's pose isn't touched. `divergence.tsv` and the result
  report the max and final pose error, the first tick it exceeds 5 cm, and gear mismatches.
  `-Assist` eases the car back once it drifts past 2 m, while underway only.
- Menus play one marker segment at a time, waiting up to 120 s (real time) for the live game
  to reach the next marker. The first few frames of the next segment are played early,
  because a transition's own input is taped there.
- **Force feedback and telemetry are muted from arming until the game exits**, whatever the
  result. Telemetry is muted so SimHub-driven shakers don't react.
- **Passed** means every frame row played, the live game was seen on the final segment, the
  car reached its last segment, and no taped driving row was skipped. A stop or exit
  earlier than about two seconds before the end of the final segment is **aborted**.
  `result.txt` is written last.
- The scenario is checked before driving; a different scene, car or weather fails the replay.
- Output in `results/sessions/<name>/replay-<time>/`: `result.txt`, `replay.log`,
  `divergence.tsv` and `shots/`.

## Known limits

- **Randomness is pinned on purpose.** The game rolls custom-event stage, weather and car with
  `Random.Range`. The probe re-seeds Unity's generator from the screen name at every screen
  change, while recording and replaying alike. Recordings therefore aren't fully natural
  play, and repeated visits to the same screen reuse a seed.
- Mouse pointer position and mod settings edits aren't taped. Use buttons in menus while
  recording (closing the mod manager window with the mouse is fine).
- The save data (Steam Cloud `cloud\` folder) isn't restored; the scenario check catches a
  different selection.
- Format-1 tapes (before 2026-10-04 evening) replay in legacy mode: no shift log, so gears
  are forced from the taped gear column and divergence is expected.
- Recording writes text on the game thread; don't use a session to judge stutter until
  capture moves to a background writer.
- Reviewed in [reviews/2026-10-04-session-replay-review.md](reviews/2026-10-04-session-replay-review.md);
  R1, R2, R4 (scenario check), R5, R7, R8, the frame-count timeout and the multiple-read
  issue are addressed. Moving this into a shared toolkit coordinator waits until a second
  game needs it.
