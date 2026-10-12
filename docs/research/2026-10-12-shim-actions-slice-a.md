# Art shared input, slice A: menu buttons, shortcuts and gears (source)

Source and offline tests only; no rig run. The qualified axes-only candidate stays
pinned on `codex/art-shim-input` (Observe and Apply passed, see
`2026-10-12-shim-probe-result.md`). Normal launches still never construct the shim;
FFB math, tune and the owner install are unchanged.

## Mapping onto Art's existing consumers

| Shared action | Art consumer (unchanged hook) |
|---|---|
| steer, throttle, brake, clutch, handbrake | `AxisCarController.GetInput` postfix → `Compose` |
| confirm, back, start, navUp/Down/Left/Right, camera | Channels Confirm/Back/Start/Nav*/CameraSwitch → `GameButtonInput` Rewired postfixes, title release, continue |
| modMenu, muteFfb | SettingsButton / StopFfbButton shortcut edges |
| shiftUp, shiftDown | Core press edges, latched per Unity frame, one step per `CarDynamics.FixedUpdate` → `Drivetrain.Shift(gear ± 1, true)` with `SkipNeutral` |
| neutral, reverse, gear1–8 | Held gate → gear index (reverse 0, neutral 1, gearN = N + 1; no gate is neutral), applied on change with `Drivetrain.Shift` |

Still reported Unsupported to Core at open: `select`, `lookBack`, `reset`, `horn`.

- A channel uses the shared frame only while a started session **owns** its action;
  availability then requires the action to be Live in this Unity frame, and a
  terminal session keeps it neutral rather than falling back. Channels without an
  owned action (camera tuning, handbrake button, or actions the profile leaves
  unbound) keep Art's own bindings on the same copied read. There is no second read.
- Gear indices are checked against the current car's `gearRatios` every physics
  step; a gate or sequential step the car lacks is ignored, never clamped.
  Art's own shifter reader is not opened alongside a started shared session.
- Transmission policy (Core parses the profile's `Transmission` but does not
  enforce it, so the adapter does): the session's mode is Core's parsed value,
  read once through `dbce_shim_profile_info`. `Sequential` applies only press
  edges; `H-pattern` applies only gates; `Automatic` or anything else applies
  neither. Nothing shifts while the car's gearbox is automatic (Art's
  `TransmissionMode` still sets that flag), and at most one change happens per
  physics step; the other mode's queued presses are discarded.
- Exact HID isolation is unchanged: `NativeInputIsolation` disables only devices
  owning bound actions (with the owner's profile now the R12 and the DS-8X).
  Keyboard is a separate Rewired controller: `GameButtonPatch` ORs shared buttons
  into native results and `Compose` keeps independent keyboard values.

## Freshness fix for live driving

`Compose` only uses an accepted frame from the current Unity frame, and the
watchdog that published it can run after game scripts. The first shared consumer
in a frame (car input, the physics gear step, a Rewired query or the watchdog) now
publishes this frame's snapshot (`EnsureSharedFrame`); later consumers reuse it,
still one read per frame. Menu probes could not reveal this; car input could have
composed a previous frame and read neutral. Snapshot and button projection are
stamped separately: a Rewired query after a car or gear consumer already read the
frame still projects this frame's buttons once, without another read (review
finding on 4774fc9).

## Validation

- `tests/ShimInput` (net8.0, net48, game Unity Mono): 539 assertions, including
  the supported set, Live/current/owned-only button state, ownership changes after
  open, Core press edges latched and capped at two, simultaneous up/down, non-Live
  edges, clear/terminal, H-gate decisions (reverse first, explicit and implicit
  neutral, sixth gate, stale, terminal) and never-clamping sequential targets.
- Actual `dbce_shim.dll` (`--bridge`, net8.0 and net48): 576 assertions. With the
  pinned profile's shapes (R12 button 31 confirm, hat 0 navDown, button 5 select,
  DS-8X buttons 9/10 shiftUp/Down) Core binds both devices, reports select
  Unsupported, resolves confirm/hat presses with edges, and one DS-8X press queues
  exactly one sequential step each way.
- `tests/WheelInput --shim-actions-only` (17): owned confirm ignores Art's pressed
  binding while unowned back keeps it on the same read; shared press/hold/release;
  shared shortcut edge; car input publishes and uses the current frame with one
  read; a refused frame leaves owned channels neutral with no fallback. Mutants
  removing the shared button branch, the shortcut branch or car-input freshness
  each fail it. Existing WheelInput (428) and shared-read (12) suites pass; the
  WheelInput stubs gained the Rewired event types `RewiredInputIsolation` needs.

## Next (slice B, muted copy, menu-only)

Prepare a fresh Observe and Apply for this build (pins change). After attach,
submit a fixed script through the resident `SubmitTestInjection` derived from the
pinned profile's exact bindings, and read back the actual game consumers:
`Player.GetButtonDown` for the game's own submit/cancel/vertical ids, the
`PanelManager` stack top, and shared action status/value/injected per step.
Keyboard observations are recorded independently. Gears follow in slice C with a
parked car.

## Slice B status: latched Core trial prepared, blocked by the bridge gate

The WheelFfb `inject raw` path cannot drive the copied provider: its snapshot read
is physical-only and refuses while legacy injection is active
(`read_snapshot_impl.h`, `WHEEL_READ_INJECTION_ACTIVE`), which the bridge treats
as terminal. The reviewed test path for shim consumers is Core's latched control
session (session file `nonce=`/`expires=`, batches `nonce=`/`batch=<n>`/commands
or `end`, `inject action <id> <n> ms=…` resolved through the applied profile,
`Injected` provenance, `suppress_native` for the whole session, `Waiting →
Running → Completed`).

Prepared in source and tested offline:

- `ShimBridgeNative` latched open (flags, fences/force bytes, session nonce,
  UTF-16 session/batch paths).
- `ShimAxisConsumer.AcceptLatched` (fixed at open; latched sessions require every
  frame latched, ordinary sessions refuse latched frames), `DeliverOnly` (owned
  actions outside the mask are never delivered, including sequential edges and
  axes, and never fall back to Art's bindings), `LatchedDelivery` and `Injected`.
  `Compose` excludes native/keyboard values while a latched frame suppresses native.
- Rewired postfixes: during latched delivery the primary player's button, axis and
  continue queries return only shared values (native keyboard replaced); ordinary
  sessions still OR shared buttons into native results; other players untouched.

Blocked: `dbce_shim_bridge_open` (art-shim-bridge 574bb88) refuses every open flag
("bridge control/capture provenance is not qualified"), although `open_impl`
already supports a latched bridge host. `--bridge` now pins that refusal;
`--bridge-latched` runs the full actual-Core latched trial (admission, injected
navDown through the applied profile, masked physical confirm, keyboard
suppression in `Compose`, `end` → Completed) once a shim admits latched bridge
sessions. The in-game Trial flow (fences computed from the probe's actual guards,
scene/driving refusal, selection readback) is deliberately not wired until then.

## Ordinary opt-in candidate (source; owner testing not started)

`Settings.SharedInput` (default off; panel: "Use shared Wheelkit profile
(candidate)", read at startup, restart required) is the only switch. Off keeps
Art's bindings exactly; nothing migrates bindings, tune or force settings.

- Preflight at mod load (files only): `dbce-controls.ini` beside the mod (empty or
  over 1 MiB refused), `dbce_shim.dll` beside the mod pinned to the qualified
  copied-provider build `7DDC9621…` (art-shim-bridge 574bb88). Any refusal states a
  reason and keeps Art's bindings and Art's own shifter.
- Qualification idle in the menus only, after Rewired and Art's readers are ready,
  using the probe's exact sequence (now shared as `SharedInputSession`, which the
  probe also uses): Core profile identities, metadata/path pins of the owned
  slots, exact path/product join, identity lease, exact-object isolation, then a
  neutral-cache check before `AttachSharedInput` with Core's transmission.
- A refusal during qualification stops the opened Core host, restores only the
  flags it changed, ends the Rewired observation and hands Art's shifter back.
  After the first accepted frame a fault keeps the wheel neutral until restart.
- Art's own shifter reader never opens while the shared profile is pending or
  attached (also on FFB recovery).
- The candidate needs a WheelFfb exposing the shared read exports (toolkit
  snapshot candidate). With the accepted native the shared path refuses
  ("WheelFfb owner has not loaded a module" or a missing export) and Art's
  bindings stay in charge.
- Panel status (`SharedInputStatus`, pure): every phase, the refusal reason, the
  profile name, isolated devices, unsupported actions, Art-binding channels, and an
  explicit gear line: Automatic or unknown profile → no shared gears; Art
  Transmission Automatic → ignored; Follow game → only while the game's option is
  manual; the current car's automatic gearbox → ignored. Force feedback with the
  shared profile is flagged as not qualified (first attended test).

Tests: `tests/ShimInput` 567 (status truth table and gear lines);
`tests/WheelInput --shared-startup-only` 13 with the qualified DLL (off, missing
profile, missing/wrong/empty DLL and profile refusals, no qualification while
driving or before Rewired is ready, real Core opened then refused without a WheelFfb
owner: host stopped, no leaked Rewired subscription, shifter handed back, no retry).
Mutants removing the cleanup, the shifter hand-back or the driving gate each fail.
