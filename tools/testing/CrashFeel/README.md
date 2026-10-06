# Crash feel comparison (developer tool)

A standalone window that plays candidate crash kicks on the wheel, one per
press, with no game running. It exists for KI-38: crash cues are accepted by
the wheel driver but not felt (owner's MOZA R12, and a T300 at 50%). The point
is to find a kick that is felt before changing the mod.

Every kick goes through the pinned toolkit (`lib/toolkit`) with the same calls
the mod makes: finite constant and sine slots from `WheelFfbNative`, plus the
steering stream at about 60 Hz, the game's force update rate. A winner can move
into the mod without new native code. Not in the solution, never packaged.

```powershell
dotnet build tools/testing/CrashFeel/CrashFeel.csproj -c Release
tools/testing/CrashFeel/bin/Release/net8.0-windows/CrashFeel.exe
CrashFeel.exe --selftest   # plan bounds only: no window, no wheel
```

**First run, 2026-10-05 (owner, MOZA R12, 50%):** F (push + rattle) felt most
realistic and went into the mod as 0.4.1-rc.2. Ratings are in KI-38.

## Candidates

| Key | Kick | Calls |
|---|---|---|
| A | 120 ms push: the mod's crash up to 0.4.0 | constant burst, 120 ms |
| B | 250 ms push | constant burst, 250 ms |
| C | 400 ms push | constant burst, 400 ms |
| D | Knock: 60 ms one way, then 60 ms the other | 60 ms constant burst, replayed with the opposite sign |
| E | Crunch: 12 Hz sine, starts at full push, fades over the last 200 of 300 ms | shaped periodic burst |
| F | A plus a 25 Hz rattle at half strength: **the mod's crash from 0.4.1** | constant + periodic burst together |
| G | B's push added to the steering force instead of a separate effect | steering stream |

**Strength** is the magnitude a full-intensity crash requests at that Crash
strength setting (the mod's default 50% = 0.5). **Direction** picks the sign.
**Cornering load** holds a steady ±20% steering force, which is what a kick
lands on mid-corner. A kick with the load's sign adds to it; the opposite sign
partly cancels it. B against G is the separate-effect versus in-stream question.

## Suggested run (about 10 minutes)

1. Close art of rally. Leave the wheel software (Pit House, Thrustmaster panel)
   exactly as you drive, and don't change it during the run.
2. Find wheels, check the selection, Connect. Connecting acquires the wheel and
   creates idle effects; nothing moves until you press a kick.
3. 50%, load Off: play each of A–G two or three times, rating each with 1–4.
4. Load +20%: try the best two or three with direction + and then −.
5. Try the best one at 100% to see whether it clips or just gets stronger.
6. Close the window. The CSV path is shown at the bottom.

## Safety and limits

- Connect refuses while art of rally is running (it would hold the wheel).
- STOP, Esc, closing the window or the window losing focus stop every effect and
  set the load to Off. Returning focus plays nothing. Kicks are at most 500 ms.
- The native layer only plays a constant burst while this window is in front.
- Ratings are what you felt. "Accepted" in the log means the driver took the
  command, not that the motor delivered a given torque.

The session log is `%LOCALAPPDATA%\ArtOfSimRally\crash-feel\session-*.csv`
(kicks, API results, ratings, notes); the native log is `ffb.log` next to it.
