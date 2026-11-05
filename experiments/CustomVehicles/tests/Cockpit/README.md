# Cockpit persistence and dispatch fixtures

The executable links the actual Core sources and Runtime/CockpitTuning.cs. It
checks finite saved values without arbitrary limits, pose isolation by vehicle
and Game/Realistic set, shared FOV offsets, measured-seat changes, scoped reset,
legacy migration, failed-save retry, and imported-key dispatch. Disk assertions
reload real JSON. Legacy data and failed-attempt receipts are preserved.

Host.cs supplies simulated keyboard/focus/time/UI and optional Art reflection
endpoints. It uses the game's real Unity KeyCode and Vector3 types, without
calling Unity native APIs. It does not establish live Unity callback order,
rendered camera geometry, keyboard reachability, device behavior, or acceptance.

For a regression witness, build with Baseline=true and CockpitSourceRoot pointing
to a frozen baseline directory. Its Core files are copied from the named commit;
Runtime/CockpitTuning.cs wraps the unchanged ArtKey and TuneCockpit method bodies
extracted from that commit's actual Runtime/Main.cs. The extraction recipe and
full original source/method hashes belong to the frozen CPU packet. Host.cs only
supplies the original methods' surrounding plumbing. Run --bounds, --shared-fov,
and --imported-f7 separately: each must build successfully and then fail at its
named behavior assertion on the old source. The current source must pass all.

CPU execution follows the coordinator's frozen-packet GO and shared lease. Run
the plugin compile and existing CustomVehicles.Tests metadata/guard checks in
the same bounded batch. RuntimeQualified remains false; no install or public push.
