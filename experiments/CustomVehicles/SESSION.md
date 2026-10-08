# Unattended framework checkpoint — 2026-10-08

Owner scope: keep iterating on the design, consult Astra and Claude over hcom,
and advance a serious reusable custom-vehicle system. This was independent of
the installed cosmetic Turtle Van 0.1.4 and the core Art of Sim Rally/FFB work.

## Delivered offline

- New UMM development module `ArtOfSimRally.CustomVehicles`, version 0.2.0.
- Schema-2 data-only packages, strict runtime/CLI validation, catalog budgets,
  stable ID selection storage, removal/reordering fallback and content identity.
- Generic indexed Blender exporter, dry-run checks, corrected sRGB palette,
  complete Trail Scout example and converted Turtle Van with the 0.1.4 windows.
- Per-player-instance visuals/cockpit, chooser preview transaction/cleanup,
  native donor save compatibility and failed-preview fallback.
- Proposed physical definitions and guarded initialization implementation;
  ambiguity-rejecting body collider selection, owned axle/collision/COM data,
  rollback journal and failed-instance deactivation.
- Network/local result guards, explicit process-lifetime taint, opt-in native
  initialization reports and no-op report comparison utility.
- Standalone Windows creator kit with editable examples and validate/pack tools.

141 managed assertions, 38 native Harmony contracts, actual guard-prefix/callback
checks, five CLI/palette/link/report tests and Blender dry-run checks passed.
Both examples validate; exported Turtle exterior/cockpit renders were inspected.
See QUALIFICATION.md for the evidence boundaries and reviews.

## Preserved / unfinished

No game launch, install, native/FFB change, public upload, push or physical test.
All 21 payload/protected hashes from the accepted install receipt still match.
Physical writes are **hard-locked** by RuntimeQualified=false. The native no-op,
guard/save coverage, handling, restart/failure, collision/contact alignment and
camera lifecycle need attended qualification. Source hooks and passing tests are
not a claim that arbitrary custom physics works in game.

Independent stats, drive-layout/differential/tire-curve authoring, sound/light/damage
integration, native replay identity and public distribution remain future work.
No Steam Workshop uploader was produced. Creator packages are data-only ZIPs for
this experimental runtime.

## Resume

Use the newest `artifacts/CustomVehicles-0.2.0-offline-*-receipt.json` (excluding the
CreatorSDK receipt) and matching runtime/creator ZIPs plus SHA256 files. Artifacts
are ignored; source and examples are committed. `tools/build.ps1` reproduces them.
Do not install over the main mod or run alongside the cosmetic Turtle Van plugin.
Follow QUALIFICATION.md's attended sequence before unlocking physical changes.

Hcom tara used manual delivery. Hula supplied two completed read-only reviews;
nene supplied the completed creator review and was asked for a second runtime
presentation pass, which remains pending at this checkpoint. Check messages on
resume; peer acknowledgement is not runtime certification. No memory-store edits.
