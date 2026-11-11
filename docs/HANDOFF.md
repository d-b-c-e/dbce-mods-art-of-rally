# Handoff

2026-11-10, AD-6 (toolkit Unity layer, first piece). Source 0.4.6-rc.2 (`ffefc02`); Test-Rc passed offline (20 checks) and built `dist/ArtOfSimRally-0.4.6-rc.2.zip` (SHA-256 `EA159E63…595685`). Not installed or launch-checked; the rig has 0.4.6-rc.1.

- No change in play: the frame-rate monitor and counter come from the toolkit (`lib/toolkit/shared/ui/unity/FrameRateReadout.cs`); `FrameRate.cs` keeps the setting, the 30 s log line and the center-screen edge. `lib/toolkit` is re-synced (shared and triple) to toolkit `48cfd39`, triple `42da140`; the triple core is byte-identical, the rest additive.
- DRIVE Rally is the second consumer of every extracted piece; it now compiles the same `lib/toolkit` sources.
- Next for AD-6 (each changes what one game shows, so each needs an owner check): span window and window landing, menu centering, the curved presenter, the Help report page, FFB runtime last. Hub `CLEANUP-QUEUE.md` AD-6 `progress` has the reasons.
- Own source 13,630 → 13,621 lines.
