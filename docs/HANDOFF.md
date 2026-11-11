# Handoff

2026-11-10, AD-6 (toolkit Unity layer). Source 0.4.6-rc.3 (`939bd98`); Test-Rc passed offline (20 checks) and built `dist/ArtOfSimRally-0.4.6-rc.3.zip` (SHA-256 `6B709130…44FD2D2`). Not installed or launch-checked; the rig has 0.4.6-rc.1.

- No change in play: the span window, window landing and menu centering now come from the toolkit (`lib/toolkit/shared/display/unity/RacingWindow.cs` and `RacingWindowLanding.cs`); art keeps only when each runs (`TripleScreen.DeveloperLaunch`, `ThreePanelOutput`). #DRIVE Rally consumes the same code.
- Own source 13,621 → 13,174 lines.
- Launch check to run: the span or landing log lines (`Span window:`, `Window landing:`) as before, and menus on the middle screen with "Keep menus and HUD on the middle screen" on.
- Next for AD-6: cameras onto `shared/cameras/CameraSets.cs` (not started; plan in the hub's CLEANUP-QUEUE AD-6 note), then the Help report page and the curved presenter.
