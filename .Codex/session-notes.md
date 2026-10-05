# Session notes - 2026-10-05 UTC (October 4 local)

Art 0.4.0 is published and installed. Source/tag: 30d421b26eccebc564cd50bf0733c1ed6a82d849 / v0.4.0.
The normal and optional SessionTools ZIPs were downloaded from GitHub and matched.
All 20 final gates passed; exact final owner-session-5 replay 210753 passed 13,455
poses with zero detected position error, finish/results/menu, normal close and
owner environment restoration. The owner accepted preceding RC10 replay 204543
visually and explicitly requested shipping and moving to iRacing.

## Start playback without conversation history

From E:/Source/games/dbce-mods-art-of-rally on codex/session-playback, game closed:
`pwsh -NoProfile -File tools/testing/Session.ps1 -Replay -Name 2026-10-04-owner-session-5 -TimeoutMinutes 8`
Use the original recording, not synthetic-roundtrip fixtures. The frozen optional
archive is extracted at results/session-tools-040-final. Toolkit default master
has docs/RECORDING-PLAYBACK-RUNBOOK.md and knowledge/RECORDING-PLAYBACK-LESSONS.md,
linked from AGENTS.md and CLAUDE.md. No transcript reconstruction is required.

## Evidence and constraints

See docs/reviews/2026-10-05-release-0.4.0.md and docs/LOCAL-DEPLOYMENT.md for exact
hashes, reports and backups. The final source fixed raw game-preference backup:
reg export silently omitted 21/58 values. Raw snapshot hashes, types/bytes, count
and restored readback are verified. Preserve current display layout and settings;
no more small-resolution test overrides. Final log used native borderless mode.

Keep physical output muted while retaining original force/telemetry producers.
Kinematic presentation is not deterministic input physics or fresh force evidence.
The NVIDIA timeout/hard-restart cause remains unknown; owner authorized controlled
live tests after reboot. Computer Use native pipe is unavailable. Owner watches
and handles menus when necessary. Stop on new display/input failure.

## Integration / next work

This branch contains the tested release. Concurrent origin/main has a different
component-layout migration. Do not reset it, overwrite it or equate its builds
with this release. Integrate only with new validation; the published tag is fixed.

User authorized continuing iRacing first, then Woden and DRIVE. Isolated checkouts
are under C:/Users/antho/.codex/worktrees/session-playback/. Their docs/STAGE-PLAYBACK.md
record status and limitations; Art success does not qualify the other adapters.
Do not overwrite concurrent triple-screen source or owner bindings. New iRacing
candidate includes Claude's fc35102 camera/triple work. Preserve its previously
installed unreceipted plugin before deployment. Runtime qualification is ongoing;
read the latest iRacing notes before launching any other game on this shared rig.

KI-44 bounded handoff recovery has offline coverage but exact live retest remains
open. Broader hardware, stage/car and camera transition cases stay pending.
