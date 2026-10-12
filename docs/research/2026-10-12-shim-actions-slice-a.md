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
