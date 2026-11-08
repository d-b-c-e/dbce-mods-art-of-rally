# Racing-row selector (vendorable source)

`RacingRow.cs` decides which displays a triple-screen mod spans, from the observed monitor rectangles and the primary
display alone. `WindowsMonitors.cs` reads those rectangles (`EnumDisplayMonitors` + `GetMonitorInfoW`); it is kept
apart so the policy stays pure and testable anywhere. Neither file changes a display setting, profile or window.

Three racing screens plus a dashboard or other auxiliary display is a common sim-racing setup, not a one-off: the
owner's rig (2026-10-13) has three 2560x1440 displays at x = -2560, 0, 2560 and a 1920x1080 display at (314, -1080),
which is the fixture. The selector reports the observed racing row only. It is distinct from auxiliary screens and
from any profile geometry a consumer has stored, and a consumer never rewrites a saved rig choice from it
automatically.

The policy:

- **Surround** (the mods' existing wide-primary path). A primary at least 2.9:1 wide is used as one surface: the row
  is the primary alone, and no side display is ever added to it. This is a shape test, not proof that the driver has
  grouped displays (NVIDIA Surround, AMD Eyefinity).
- **Separate.** Otherwise the row is the primary plus exactly one display touching each side, with the same width,
  height, top and bottom. Displays wholly above or below the row, such as the diagnostic display, are ignored.
- **No row** (`None`, with a reason): a missing side, a gap, an overlap, a vertical offset or a different size on a
  side; two candidates for a side; any other display sharing the row's band, including a fourth panel in the same row;
  not exactly one primary. The policy never picks another triple and never spans more than three displays.

This replaces the mods' earlier test, which took the virtual-desktop rectangle as the span and failed once the desktop
was taller than the primary. A mod adopting it spans `RacingRow.X/Y/Width/Height` when `Kind == Separate`, keeps its
Surround path for `Surround`, and does nothing for `None` (log `Reason`).

Consumers copy the files byte-identically (`git rev-parse <commit>:shared/display/RacingRow.cs` equals the consumer's
blob), keep the MIT header and record the toolkit commit. Do not copy the geometry into a third implementation.
`tests/RacingRow` checks the rules, including the rig's actual layout and a read-only print of the live displays;
`tests/RacingRow/Compat` compiles both files for netstandard2.0.

**Unity's legacy Mono 2.0 profile.** `RacingRow.cs` and `WindowsMonitors.cs` also run on Mono 2.0 (.NET 3.5), as used
by Horizon Chase Turbo. They use no LINQ, because a .NET 4.x compile binds Func to mscorlib and Mono 2.0 has Func only
in System.Core. They also avoid IReadOnlyList, Array.Empty and the generic `string.Join`. `RacingRow.Displays` and
`WindowsMonitors.Read()` are `ReadOnlyCollection<MonitorRect>`, which is also an `IReadOnlyList` on .NET 4.5 and later,
so code written against the earlier `IReadOnlyList` surface compiles unchanged. `tests/RacingRow/Compat35/Test-Mono20.ps1`
compiles both files for net35, then checks their net472 build has no Func, LINQ, System.Core, IReadOnly* or ValueTuple
reference. A Mono 2.0 consumer still checks its own build against the game's assemblies. `SpanTarget.cs` uses
ValueTuple and Func, and stays .NET 4.x and netstandard2.0 only.

**Native mods (C++).** `dbce_racing_row.hpp` is the C++ counterpart of `RacingRow.cs`: the same rules and the same
reason sentences, header-only C++17, with no Windows calls. `dbce_windows_monitors.hpp` is the counterpart of
`WindowsMonitors.cs`. It also has `read_primary_size_mm`, the primary display's picture size from
Windows (EDID through GDI), for default triple measurements; call it outside DllMain. The two implementations are kept equal by `racing-row-vectors.txt`. Every vector gives the
monitors, the expected row and the exact reason, and both test harnesses run every vector they can.
`tests/RacingRow` runs the vectors that use the 2.9:1 rule. `tests/RacingRow/Native/build_test.ps1` runs all of them
with MSVC x64 and x86 as C++17, and x64 as C++20, with warnings as errors. A rule change updates both files and the
vectors together.

The C++ selector takes the wide-primary aspect from its caller, `select_racing_row(monitors, aspect)`.
`kSurroundAspect` (2.9) is the toolkit's rule. A mod that already has another rule, such as the 4:1 rule in the Sonic
CrossWorlds renderer and ADRIFT, passes it explicitly, so adopting the row changes only its separate-display test.
Native consumers copy both headers byte-identically, like the C# files, and record the toolkit commit and blob ids.

`SpanTarget.cs` is the optional shared cached decision for consumers that check the span every frame. It preserves
the saved span opt-out and triple-mode gate, selects only a `Separate` row, and reports a changed reason for logging.
The injected observation and monotonic clock make it engine independent. Observations, including failures, are
cached for two seconds; a failed refresh drops the old span. Window creation, size application and placement remain
in the game adapter. Extracted without behavior changes from Woden `a58f8d9`; the frozen Woden package still contains
its original equivalent class. DRIVE adopts this toolkit source rather than another private copy.

Extraction validation, 2026-10-13: source review confirmed the Woden decision logic is unchanged. The combined
selector/cache harness passed 159 assertions and read the active four-display rig as the expected 7680x1440 row at
(-2560,0); the netstandard2.0 compatibility build passed without warnings. Consumer window placement and rendering
still require consumer evidence. A transient failed query is cached too: a one-shot startup caller may keep its
normal window if it runs during that failure interval, rather than using an old or invented span.

## Window landing (source-vendored, Mono 2.0 and later)

`WindowLanding.cs` decides where a game's window belongs on the racing row, and which engine display index to save so
the next launch opens there. Engines remember a start monitor by index: Unity saves the `UnitySelectMonitor`
PlayerPref, stored on Windows as `UnitySelectMonitor_h17969598`. On 2026-10-24, after Surround was turned on, Woden
and DRIVE Rally both had index 1 saved from separate-monitor use. That index named the 1920x1080 dashboard display, and
both games opened there. `WindowLandingPolicy.Decide(monitors, row, spanning, window, openedOn, engine, savedIndex)` returns:

- **Target and Move.** The target is the home display, which is the primary: the whole row under Surround and the
  center display of a separate row. A consumer that spans a Separate row targets the row's span instead. `Move` is set
  when the window's center is on another display, or when a span is not exact. The target is never an auxiliary or
  dashboard display. With no racing row, or a window on no single display (minimized), nothing moves.
- **EngineIndex and Persist.** The engine's own display list (`EngineDisplay`, Unity's `Display.displays` system
  sizes) is in the engine's order, which is not Windows' order, and it has no positions. The home display is mapped by
  size. The index is known only when the lists have the same length and exactly one engine display has the home size.
  `Persist`, the index to save, is set only when that holds and when the saved index (none saved means 0) names an
  engine display of the size the game opened on. That proves the saved index and the list share one order. It is also
  set only when the value differs. Anything ambiguous, such as three equal separate displays, saves nothing and says
  why in `PersistReason`.

The consumer keeps the engine glue: read the window rectangle and the display it opened on, move the window, and save
the preference through the engine's own API (Unity: `PlayerPrefs.SetInt("UnitySelectMonitor", n)` and `Save`). It
logs `Reason`, `PersistReason` and old -> new. It never writes the registry directly, and it skips the save in test
launches that must not change owner state. `window-landing-vectors.txt` holds the cases: Surround with a stale
dashboard index in either Windows or engine order, already landed, unknown or out-of-range indexes, Surround off with
and without a span, one display, and ambiguous rows. `tests/RacingRow` runs every vector and a grid that proves the
only targets are the home display or the span. The Compat and Compat35 builds include the file.

## Triple layout and settings draft (source-vendored, .NET 6+)

TripleLayout.cs and TripleSettingsDraft.cs contain engine-free behavior extracted
from qualified Truckful source 7e8161e3b41ac9efc27c09db100008e946f48574. They use
the existing Dbce.TripleScreen.Core geometry and this folder's unchanged
RacingRow.cs. The geometry core receives no wheel/display dependency.

These two new files require C# 10 and .NET 6+ (record structs, init properties).
This does not change RacingRow/WindowsMonitors legacy Mono 2.0 compatibility or
SpanTarget's existing targets. Older Mono consumers must not copy the new files
as if they had those older compatibility guarantees.

The source namespace is Dbce.Wheel.Display. Copy the two files byte-for-byte,
retain their MIT headers and pin the toolkit commit/blob identities when a
consumer adopts. Link/reference the existing pinned TripleScreen.Core and
RacingRow sources, without copying the projection math into another framework.
No game, Unity, BepInEx, Win32, device, display-application or UI calls exist here.

The shared names retain the qualified selector, validation, projection and
ownership semantics: TripleMode, TripleLayoutChoice, TripleRig, TripleSettings,
PanelFrustum, OffCenterProjection, TriplePanels, TripleGate, DesktopLayout,
SpanAction and SpanPolicy. Physical inputs are millimeters and degrees. Panel
frusta are left/center/right, with yaw and near-plane-one tangents in +Z camera
space; OffCenterProjection uses the existing OpenGL/Unity projection convention.
Near/far require 0 < near < far. The solver keeps the 89-degree side-angle bound.
The width gate uses RacingRowPolicy.SurroundAspect rather than a second constant.

Consumers supply their complete defaults (rig dimensions, Mode/Span and
MatchGameFov). There is no shared owner-rig or chase-framing default.
TripleSettingsDraft takes active settings and the existing save/canEdit/status
callbacks, plus TripleDraftText(Initial, Locked, NoChanges, Saved, Discarded) and
storeAsSinglePrecision. Pass true for a float store such as Truckful/OTH
ConfigEntry<float>; pass false for an actual double store. The supplied active
settings must already reflect the store's precision. Text and comparisons use
the shortest invariant round trip for that precision; opening a page does not
rewrite precise saved geometry.

Save validates before calling the adapter, preserves a refused/failed draft,
and changes Saved only after a successful explicit store callback. Active
stays fixed for the process and SavedPendingRestart remains distinct from an
unsaved draft. Discard/HoldClose/PanelHidden and edit gates retain the original
lifecycle. Five page-specific strings are consumer data, not shared UI rules.
The adapter still owns file names/keys, stale-file checks, persistence/rollback,
focus/menu/solo conditions, the rendered page and report/status integration.

SpanPolicy returns decisions only. An adapter must reach it on initialization
even when the renderer is Off, so an exact persisted owned-span marker can
request one restore. A player-selected size never matches that ownership guard.
Local window, camera, pipeline, HUD and shutdown hooks remain game-specific.
This extraction neither repairs those hooks automatically nor establishes any
rendered or physical acceptance. Consumers adopt in their next coherent
candidate; qualified Truckful 7e8161e bytes stay unchanged.

Qualified extraction provenance (original Truckful blobs):

| Source | Blob |
| --- | --- |
| src/Truckful.Wheel/TripleLayout.cs | e132df60b6eef22b907f352fb7ff288f8b05b024 |
| src/Truckful.Wheel/TripleSettingsDraft.cs | 5c87d22f4fda81809073421dc33d7383fcd248dd |
| tests/Wheel.Offline/TripleChecks.cs | 35f894e557f16bad4386284b60679364f778438f |

Changes from those bodies are the namespace, removal of game defaults, explicit
System/nullable imports, consumer text and float/double storage seams, plus
reuse of the existing aspect constant. Geometry, selector, marker/Off policy
and draft lifecycle are otherwise retained. Core source pin is
866c59f827c2b32e4e38722976b97040c9f47ac5; RacingRow's inherited pin is
f9abfcf9dc295a1da63ef7279bdfaa68ece716ea.

tests/TripleSettings ports all 61 qualified checks with owner defaults as
fixture-local data, plus focused alternate-text/double-store/float-store/failure
cases. The fixture links only pure source, not WindowsMonitors, and performs
no desktop observation or game/device/display action. For a private worktree,
set TripleScreenToolkitRoot to the existing pinned triple-screen toolkit;
canonical sibling checkout layout is the default.

Qualification, 2026-10-17 (grant 226763): source
091eef18978f4a67c05577dea24c048855330b42 built for net6.0/C# 10 with nullable and
warnings as errors, with zero warnings/errors. All 61 retained checks and 14
adapter-seam checks passed. Original logs and result are in the portfolio hub's
artifacts/shared-triples-dani-20261017/run-01. Owned processes were quiescent and
the lease was released at 09:40:48.9164559Z. No consumer build, game, device or
display action occurred. Ordinary merge c8b67d83ca3648537eae72918ef80886a013ad80
preserves this qualified source and canonical process-helper source 0d55d5a;
the eight extraction paths do not overlap that helper change. Consumers still
qualify their own adapters when they next adopt the shared source.
