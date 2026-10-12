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

Update (romi 28531): art-shim-bridge f38b855 (`dbce_shim.dll` 0D10F913) admits a
fenced latched bridge open. `--bridge-latched` against it passes (613): admission,
action-level navDown injected through the applied profile, masked physical
confirm, keyboard excluded in `Compose`, `end` → Completed, resolved stop. The
`--bridge` gate is now hash-aware: the ordinary pin 7DDC9621 must still refuse
latched opens, 0D10F913 must admit them, any other build fails as unreviewed
(609 each). Core reads the batch file without write sharing, so batch writers
must replace the file whole (temp file, then `File.Replace`/`Move`); the test
writer does, and the in-game Trial writer must too. The ordinary candidate stays
pinned to 7DDC9621.

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

## Startup lifecycle fixes (romi 28533, source)

- Shared input alone works: while the shared profile is pending or attached,
  Art's readers stay open as its only physical source even with Use assigned
  controls Off and no shortcut bindings (`WheelInput.SharedReadersRequired`).
  Previously Ready waited forever because `UpdateCore` closed the readers.
- Editors (F6): controls the shared profile may set show a read-only line ("Set
  by the shared profile", or what applies when the profile does not map them)
  instead of Bind/Calibrate/Clear; `BeginAssign`/`Clear` refuse them too. Art-only
  channels (handbrake button, camera tuning buttons) stay editable, binding from
  the shared copy with no reader reopen and no second read; a device change is
  refused with "Device changes need a restart". The separate-shifter editor and
  the FFB device picker/Refresh are replaced with the same restart statement.
- The handbrake button really keeps Art's binding beside a started shared
  session (it was skipped before), except during latched delivery.
- Ordering: qualification waits for force feedback's startup acquisition or any
  pending reconnect (`!ForceReconnect.Pending`), so the attach is never closed
  straight away. While the session has pinned the readers (isolation, a few
  frames) neither discovery nor force recovery may reopen them. A later force
  reconnect stops the attached session for good with the stated cause "a force
  feedback reconnect closed the wheel readers"; nothing reattaches, readers reopen
  neutral, and read health reports unknown rather than a failure.
- After any stop, driving input (wheel and keyboard alike) stays neutral until
  restart; menus still take the keyboard. The previous text claimed the keyboard
  was unaffected, which was false for driving.
- Cleanup failure: if the joystick restore fails, the session is kept
  (`CleanupFailed`), Art's shifter stays closed, the panel says the game's own
  input is still off, and the restore is retried in the menus about once a second
  up to 10 times (then "restart the game"). Only a complete cleanup hands Art's
  bindings and shifter back.

Tests: `tests/WheelInput --shared-lifecycle-only` 33 drive the production
startup, reader, bridge and consumer code in the watchdog's order with only the
hardware-facing session scripted (`ISharedQualification`); `--shared-startup-only`
14 now runs with Art's assigned controls Off against the real qualified DLL;
`--shim-latched-only` 10; `tests/ShimInput` 570 (net8 and Unity Mono). 23 mutants
(each fix removed or weakened) all fail. Also fixed: `tests/Lifecycle` stubs
(broken by 54f687a), and the reasoned close is not an overload of `Close`, which
the dev recorder looks up by name.

## Muted ordinary startup smoke (2026-10-12 03:23, romi 28934)

Disposable copy `art-shim-probe-game-20261011` staged by
`Prepare-SharedInputCopy.ps1` at 0adda89 (mod source = 8f9b945), shim 7DDC9621,
native DD301314, the owner's Wheelkit profile 5206EDFB, the owner's settings plus
`SharedInput=true`; run by `Invoke-SharedInputCopy.ps1` under the rig lease,
broker launch at Medium, `DBCE_FFB_MUTE=1`, no input or force, no owner install.

- Startup: profile found, Art's shifter reader kept closed. FFB init failed five
  times (0x80070005, under the mute latch); after "FFB recovery exhausted" the
  shared profile qualified, which is the intended force-settle order.
- Attached profile 'Moza KS Pro Triple' (Sequential), isolating MOZA R12 Base and
  DS-8X Shifter. First frame accepted with 14 owned actions: steer, throttle,
  brake, handbrake, shiftUp, shiftDown, confirm, back, start, navUp/Down/Left/Right,
  camera. No stop during the 10 s settle.
- Normal exit; Core host-shutdown resolved with inputs released; shared state
  restored; owner pins unchanged; lease released.
- Not covered: physical input, driving, and first-force coexistence (FFB never
  acquired in this run). Keyboard driving after a terminal fault stays neutral
  and is not qualified (scoped follow-up).

Evidence (ignored): `E:/Source/games/.worktrees/art-shim-probe-evidence/shared-smoke-20261012-032314`.
