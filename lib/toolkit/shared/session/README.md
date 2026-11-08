# Full-session timeline (vendorable source)

`FullSessionTimeline.cs` is the data-only record of one recorded launch: boot, menus, loading, one driven stage
(with pause intervals and an observed finish), results, and the sealed stage it refers to. It was extracted
from DRIVE Rally's `DriveRally.Core/FullSessionTimeline.cs` (dbce-mods-drive-rally `9f1b85e`) without changing
field names, enum numbers or rules, so a DRIVE timeline and a consumer timeline serialize the same way. The
schema string is the consumer's (for example `drive.full-session@1`, `woden.full-session@1`).

`FullSessionPackageNames` holds DRIVE's package layout: from the original signal file `source.jsonl` come
`source.session.jsonl` (observation), `source.stage-session.json` (timeline) and `source.stage/` (the game's
existing sealed stage bundle). The outer `complete.tsv` seals `session.json` and those four payloads.

Consumers copy the file byte-identically (`git rev-parse <commit>:shared/session/FullSessionTimeline.cs` equals
the consumer's blob) and record the toolkit commit. Serialization, seals, filesystem and engine calls stay in the
consumer. `tests/FullSession` checks the rules; `tests/FullSession/Compat` compiles the file for netstandard2.0.
