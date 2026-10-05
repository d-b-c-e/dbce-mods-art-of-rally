# Toolkit standards adoption

Which entries of the wheel toolkit's standards ledger
(`E:\Source\toolkits\dbce-wheel-mod-toolkit\STANDARDS.md`) this mod has brought in.
Update a row in the same commit that adopts it. Statuses: `adopted`, `partial`,
`pending`, `n/a` (say why), `unchecked` (nobody has looked yet).
Audited 2026-10-04 (read-only standards audit); statuses below are from that audit.

| Standard | Title | Status | Notes |
|---|---|---|---|
| STD-001 | One mod per game | adopted | wheel and triple in src\ArtOfSimRally.Mod (Triple\) |
| STD-002 | Recording and playback from launch | partial | recorder tools\testing\Recorder; replay on codex/session-playback (Astra/Codex) |
| STD-003 | Normalized FFB strength | partial | default 50 (Settings.cs:67) and shared ForceCurve, but FyReference 11500 N is per-game; awaits a family 50% calibration |
| STD-004 | Consistent settings UX | adopted | Simple/Advanced pages, F8 Stop FFB; KI-44 bindings lock-up still open |
| STD-005 | Camera numpad layout 8/2 9/3 4/6 7/1 +/- 5 | pending | Settings.cs:158-168 original layout (8/2 up-down, 9/7 fwd-back, 1/3 tilt, 0 reset); no camera key migration |
| STD-006 | Camera step sizes are settings | partial | held speeds (TuneMoveSpeed 0.4 m/s, TuneAngleSpeed 20 deg/s; FOV shares angle speed), not per-press steps; no defaults button |
| STD-007 | Triple screens in one wide window | partial | wide one-window mode exists but off by default (Triple\Settings.cs:18); no Auto; game pillarbox not hidden |
| STD-008 | Display changes: game applies once | partial | no SetResolution; Surround size not added to the game list; separate-display mode calls Display.Activate (needs watchdog test) |
| STD-009 | Dashboard telemetry matches the HUD | partial | HUD = 0.6 x physics; fixed in b5c8e46 (v0.4.0, codex/session-playback) but not on main, ignores speedo type, unverified at rig; docs\TELEMETRY.md:93 stale |
| STD-010 | Install the latest build for testing | adopted | owner Stream Deck target (not re-verified in audit) |
