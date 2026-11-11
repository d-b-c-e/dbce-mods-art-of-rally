# Handoff

2026-11-10, ART-G (one input route). Source 0.4.6-rc.1 (`14dba12`); Test-Rc passed offline and built `dist/ArtOfSimRally-0.4.6-rc.1.zip` (SHA-256 `0B5D32D0…7DF4B5`). Not yet installed or launch-checked; the rig still has 0.4.5-rc.1.

The shared profile (Wheelkit's store, or `dbce-controls.ini` beside the mod) is the only wheel input route: `SharedInputStartup` → `SharedInputSession` → `WheelReaders` → `ShimBridge` → `ShimAxisConsumer` → `CarInputPatch` / `ProfileButtons` / `GameButtonInput` / `ProfileGears`. No profile means the game's own controls. Art's own reader, bindings, calibration and shifter (`WheelInput*`, `Shifter*`, `WheelPovInput`), the probe-stage files, the camera migrations, `DisableSteerAssist`, `GameButtonCompatibility` and the glyph-label switch are deleted; `tests/SharedInput` runs 11 scenarios, one process each.
- Own source 15,924 → 13,630 lines; the < 9,000 target needs work outside ART-G (mod core 8,286, Triple 2,959, Shim 1,600).
- Left from ART-L: `lib/dbce-ui` waits for UI-10; `src/Dbce.TripleScreen.Protocol` is art's own code; `experiments/` holds only ignored build output; local branch `napo/art-std046-audit-20261104` left in place.
