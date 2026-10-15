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
| STD-003 | Normalized FFB strength | partial | default 50 and shared ForceCurve; STD-031 source candidate uses 11500/0.6 N. Cross-game calibration follows qualified adoption of this Art reference. |
| STD-004 | Consistent settings UX | adopted | Simple/Advanced pages, F8 Stop FFB; KI-44 bindings lock-up still open |
| STD-005 | Camera numpad layout 8/2 9/3 4/6 7/1 +/- 5 | adopted | bf23221 on claude/camera-standards: Settings.cs defaults; CameraKeys.MigratePreviousDefaults moves exact old default sets (either tilt order, or family interim layout) on load, logged; customised sets kept; owner accepted at the rig 2026-10-06, released in v0.4.1 |
| STD-006 | Camera step sizes are settings | adopted | bf23221 on claude/camera-standards: CameraMoveStep 0.02 m / CameraTiltStep 1 deg / CameraFovStep 2 deg per press, held repeat 0.35 s then 0.1 s (CameraRepeat); Advanced > Cameras sliders + Default steps; replaces held speeds; owner accepted at the rig 2026-10-06, released in v0.4.1 |
| STD-007 | Triple screens in one wide window | partial | wide one-window mode exists but off by default (Triple\Settings.cs:18); no Auto; game pillarbox not hidden |
| STD-008 | Display changes: game applies once | partial | no SetResolution; Surround size not added to the game list; separate-display mode calls Display.Activate (needs watchdog test) |
| STD-009 | Dashboard telemetry matches the HUD | partial | HUD = 0.6 x physics; fixed in b5c8e46 (v0.4.0, codex/session-playback) but not on main, ignores speedo type, unverified at rig; docs\TELEMETRY.md:93 stale |
| STD-010 | Install the latest build for testing | adopted | owner Stream Deck target (not re-verified in audit) |
| STD-011 | Work lands on main | unchecked | added 2026-10-05; remote still has codex/* side branches to review |
| STD-012 | Reproduce the route and preserve original signals | unchecked | added 2026-10-05; session playback (v0.4.0) is the likely fit |
| STD-013 | The installed build launches plainly | unchecked | added 2026-10-05 |
| STD-014 | Request reciprocal review when progress stalls | unchecked | added 2026-10-05; process standard |
| STD-015 | Triples on Surround and on separate monitors | partial | added 2026-10-05; both modes exist (wide display and three separate displays). Separate displays take their sides from the shared racing-row selector (see below); Art spans no desktop window, so the virtual-desktop-height assumption fixed elsewhere does not apply. The shared racing-row selector (toolkit db0383c, exact source in lib/display) chooses the sides: Unity's position-sorted display order predicts the side indices, and each side is proven by the window it opens. The saved indices are unused (rc.22; stage proof in hub artifacts/art-separate-display-stage-20261013). |
| STD-016 | Telemetry: Forza Horizon layout, on by default | unchecked | added 2026-10-05 |
| STD-017 | Hide settings pages that have nothing to offer | unchecked | added 2026-10-05 |
| STD-018 | Handling changes never reach online scores | unchecked | added 2026-10-05; DisableSteerAssist is off by default |
| STD-019 | Menus on the centre screen; side screens only in gameplay | unchecked | added 2026-10-05 |
| STD-020 | Standard feature checklist per game | adopted | row in dbce-project-mgmt PROJECTS.md feature checklist |
| STD-021 | art of rally is the FFB reference | adopted | historical v1 retained; owner-requested v2 steering calibration has source/original-input checks below. Independent crash shape/strength unchanged; runtime adoption and feel pending. |
| STD-022 | One triple-screen selector | adopted | F6 Cameras shows "Off / Surround / Separate monitors" on the shared rows (2026-10-15, branch toto/art-f6-triple-rows); stored flags unchanged. docs/triple uses the same names (branch toto/art-triple-docs-labels) |
| STD-023 | Frame-rate readout in the settings panel and the log | adopted | 0.4.2 candidate: Cameras page shows avg/1% low/worst of the last 10 s; log line every 30 s (toolkit FrameRateMonitor, vendored) |
| STD-024 | On-screen frame-rate counter | adopted | 0.4.2 candidate: "Show frame rate on screen", top right of the centre screen, off by default |
| STD-027 | Independent steering and effect strengths | partial | Existing steering gain and crash/landing/shift amplitudes are separate. October 8 source labels Steering strength, explains independence and always exposes the Advanced tuning/effects link in Simple. No force arithmetic, saved tune or native change. Build checked; packaging and rendered label/link acceptance remain pending. |
| STD-033 | Rig profile controls reach the game | partial | rc12 / f6165bc plus candidate Wheelkit writer: production Apply, actual raw reader, named/any-button game queries and real car inputs qualified in October 10 runs06/07. Title, menu Up/Down/Confirm/Back/Start, camera, steering/throttle/brake/handbrake observed. Left/right navigation, clutch and auxiliary shifter still open. No user release of the Wheelkit writer yet. |
| STD-034 | Closed-loop Apply qualification | partial | Frozen writer/profile, independent raw workload, process no-force latch, actual consumer observations and exact restoration in runs06/07. Config verification accepts only complete semantic XML equivalence after the native serializer reorders fields. All-binding staged-fault matrix and omitted shifter actions remain open. See docs/reviews/2026-10-10-raw-controls-title.md. |
| STD-035 | Controls identity, provenance and failure rules | partial | Source-only copied provider validates identity/caps/generation/sequence and original QPC freshness; actual Core-to-Art sink tested on .NET8/Unity Mono. Native candidates 64ccf67/574bb88 add exact owned HID paths; the process-only probe joins path plus product to Rewired, preserving both GUID namespaces. No new profile parser/acquisition. Runtime identity/cache/restore and production activation remain open. See docs/reviews/2026-10-11-shared-input-axis-slice.md. |
| STD-036 | Every binding reaches its own device | partial | Existing legacy readers preserved when dormant. Internal source bridge borrows their slots and shares one physical read with Core and the legacy cache; fake production-path tests pass. No runtime attachment. Menus, shifter and shortcuts remain on existing paths; live parity is pending. |

## Shared input source slice (2026-10-11)

October 13 source follow-up for STD-035/036: fresh shared-only FFB selection now
uses a pure optional Core profile inspection, with a frozen startup identity and
strict native GUID selection. Explicit device overrides and accepted force tuning
are unchanged. Private candidates now pin the canonical toolkit shim `c6594fa`
(built at `421bd4b`, hash `BC9B030A`), which carries `5b96ba8`'s inspection plus the
fenced latched bridge; toolkit native and Art .NET/Unity Mono checks passed (see
[the shim provenance](lib/input-shim/PROVENANCE.md)). Private rc.22 (with that shim since rc.21) is
installed locally (not launched; see [LOCAL-DEPLOYMENT](docs/LOCAL-DEPLOYMENT.md)). The
earlier `723E8A38` from `5b96ba8` first passed Core, ABI, bridge, fake-COM and .NET/Unity
Mono tests. The October 13 disposable startup (with `723E8A38`) resolved the configured target before
qualification, attached and accepted a shared frame using the test-only muted
DD301314 native; exact owner/copy restoration passed. Production 44E46799 force
acquisition, input/FFB coexistence and physical behavior remain unqualified. See
[the bounded review](docs/reviews/2026-10-13-shared-steering-ffb-target.md).
This narrow result does not upgrade general parity or the historical ledger rows.

The shared frame consumer and copied bridge stay dormant in normal launches.
A process-only, menu-only diagnostic now observes exact Rewired identity,
disable/restore and raw-axis cache facts independently, using the existing native
no-force latch. An explicit second Apply probe requires a matching receipt;
unknown identities/paths refuse. The exact owned HID path plus product join now
has source/fake checks and keeps Core/RawInput GUID namespaces separate; its
first muted compatibility proof remains open. See the
[copy-only probe recipe](docs/research/2026-10-11-shim-muted-probe.md).
All existing WheelInput tests pass before any publication. Source checks do not
upgrade STD-033/034's live qualification. There is no settings/backend migration,
installation or change to the accepted Art v2 force producer or original captures.
The [slice review](docs/reviews/2026-10-11-shared-input-axis-slice.md) records the
outstanding ledger rows and [native bridge implementation](docs/research/2026-10-11-shared-input-bridge.md).

## Steering reference update (2026-10-11)

STD-031 is **partial**: the new default reference makes new50 match legacy30
before clamp/filter; explicit saved tunes and historical replay stay unchanged.
Build, settings/recorder checks and paired trials on 11,921 original owner force
rows pass. Package/runtime adoption and physical feel remain. See
[reference v2](docs/reviews/2026-10-11-ffb-reference-v2.md).

## Custom-vehicle experiment evidence (2026-10-08)

The separate, uninstalled [Custom Vehicles candidate](experiments/CustomVehicles/README.md)
adds process-latched network and local result/ghost/progression guards for proposed
physical overrides. Production prefixes and native hook contracts are checked
offline; physical activation remains hard-locked pending attended donor/guard
qualification. See [the evidence and remaining checks](experiments/CustomVehicles/QUALIFICATION.md).
This is experiment-scoped STD-018 progress, not adoption by the parent release;
the global STD-018 row remains unchecked. No main-mod settings, native FFB, game
preferences or installed payloads changed for this candidate.

## Final managed-payload test preparation, 2026-10-15

STD-002/012/028/033/034 remain scoped to their existing status. The explicit
`Prepare-SharedInputCopy -PackageZip -ForceFeedbackOff -MutedNative` test-copy
route validates the original package before replacing only the disposable
copy's two natives with pinned DD301. The original package receipt and extracted
bytes remain intact; `nativeSubstitution` records packaged and exercised hashes
and paths, and ordinary staged-file hash checks cover the exercised bytes.
This prepares muted managed-binding Save/restart evidence; it does not qualify
the production native or physical controls. Focused temporary-copy checks are in
`tools/testing/Test-MutedPackageCopy.ps1`; 18 checks passed in the named offline
slot, followed by the two-start normal managed-payload Save/restart PASS. Both
starts restored and the shared lease was released. See the final section of
`docs/SHARED-BINDING-EDITOR.md` for the exact pins, remaining controls checks and
native substitution limit. Existing accepted force/performance results remain
accepted; this tooling-only change does not reopen them. This is scoped workflow
evidence, not general parity or attended sign-off for new controls.
Historical unchecked STD-011/012/013/014/016/017/018/019 rows remain open. The
toolkit's STD-037 schema-2 endpoint standard is missing from the adoption table;
this staging change does not enable or change schema-2 controls.

## October 15 shared binding presentation policy

Source-only adoption of toolkit `7c51cb8e857026dcbeb6de3c300be18f59e67466`
`BindingEditorPolicy.cs`, working-byte SHA-256 `66B3FE7634BB690807101C0AA7FB210A13D7FBEC93CACA381FB8123DF8663D9C`.
Missing explicit selection or host replacement remains visibly unavailable until
an explicit current-device/Saved choice; reappearance does not confirm it.
Unavailable-action/profile-mode rows show the host reason and disable Bind,
Clear and Invert. Initial intentional Saved mode and host/profile/force behavior
remain unchanged. Working Woden selection is the reference, not a rewrite target.
F6-SHARED-PENDING-0635 passed 24 source-linked presentation checks
and the affected plugin compile with zero warnings/errors. No new runtime, physical, package or install claim.
This addresses the scoped STD-004/030 presentation gap; broader partial STD-033/036
game-delivery/physical checks remain separate from this source change.

### STD-004 / STD-030 - shared binding Save warnings (October15 source candidate)

Independent binding Save-failure presentation adopts toolkit d342370/BindingSaveStatus.
Warning survives capture/general messages and resolves on successful Save/Discard
or a new host. Existing F6/capture/writer/ActiveSaved/FFB behavior stays in place.
Focused host/presentation fixtures are updated but not run; candidate not installed.
Other preexisting unchecked ledger items remain outside this status-only scope.
