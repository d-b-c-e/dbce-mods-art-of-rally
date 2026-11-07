# Native in-stage actions: horn, look, recover, repair, restart (source handoff)

niru, 2026-10-15, for sana 162429. Source only. Nothing was built, run, installed or written to an owner
configuration while owner UAT 162171 runs.

## What the game does

All from the reference `Assembly-CSharp.dll` (SHA-256 `7807A1D674C8214F…`, `E:\art-ui-research\controls-offline-01\references\Managed`).
Sources: `RewiredConsts.Action`, plus a read-only IL scan of constant `Player.Get*` ids in every method.

| Action | Rewired id | Game read |
|---|---|---|
| Horn | 30 | `SoundController.Update`: `GetButtonDown` plays a looping horn, `GetButtonUp` stops it. Skipped in replay and on the controls panel. |
| Recover car | 44 | `ShortcutManager`: `GetButtonDown` arms, then `GetButton` must be held 1 s. |
| Repair puncture | 45 | Same hold; armed only with a punctured tire and a spare. |
| Quick restart | 46 | Same hold in career; immediate elsewhere, never in freeroam, daily or weekly. |
| Look left / right | 22 − / + | `CarCameras.Update` `GetAxis(22)` (HorizontalOrbit). |
| Look back / forward | 63 − / + | `GetAxis(63)` (VerticalOrbit). |

The look poles come from the yaw formula `atan2(-axis63, axis22) + 90°`: 63 negative turns 180°, which is look back.
`ControlsRemapper` splits 0, 22 and 63 into negative and positive rows.

Two things are not routed:
- `ActivateView` 64 is the view-collectible trigger, not a select or view button. Art has no select action, so
  the shared Select row is hidden.
- 8 (`Start`) stays unrouted, as before: 19 already toggles pause.

## Design (sana 162623; zele 162553 A + B)

**A. Shared, no ABI change.** The base frame already carries these 31 actions:
- lookBack 18 → `Channel.LookBack` → 63 negative;
- reset 19 → `Channel.RecoverCar` → 44;
- horn 20 → `Channel.Horn` → 30.

`ShimAxisConsumer` now marks only select unsupported. `lib/input-shim/CONTROLS.json` moves to schema 3
(`art-shim-ini-controls-3`):
- delivered = the 31 actions minus select;
- the three new actions are listed under `fixtureOnly`;
- `package.ps1` requires schema 3.

Wheelkit must admit schema 3 before any install (zele a8583c5, branch `zele/art-schema3-admission-20261015`).
Today's helper refuses it.

**B. Art-only.** Repair puncture, quick restart and look left, right and forward have new channels and
`Settings.xml` `*Binding` fields:
- During a shared session they read this frame's shared copy (`ArtOnlyValue`).
- A control that the profile maps stays neutral (`SharedDuplicate`).
- They never take a second device read.
- No writer sets them.

**Dispatch.** `GameButtonInput.NativeAction(rewiredId, negative, out sharedId, out Channel? artChannel)` is the
only table. `Button`, `Released`, `Axis` and kiki's prompt labels all use it.
- `GameButtonPatch` adds `GetButtonUp`, and `InputHooks.Missing` fences it.
- During a latched test session, native `GetButtonUp` is replaced too, like the other button queries.
- `Axis` adds the orbit axes 22 and 63.
- The game's own context checks, the 1 s hold timer and the horn loop stay native. Nothing performs an action
  directly.

**Horn release.** A horn press can be interrupted by the settings panel, focus loss, a read failure, a binding
reload or a stopped session. Each interrupted press still delivers exactly one `GetButtonUp` (`OweUp`), so the
looping horn cannot be left on. It sounds again only after a fresh press.

**Host and status.**
- `SharedBindingCaptureHost` hides select: no row, capture, clear or invert.
- The "does not consume" note is gone.
- The reset row is labeled "Recover car (hold 1 s)".
- `TryActiveBinding(id, out ControlBinding)` reads only the startup Active profile, for kiki's prompts.
- `SharedInputStatus` drops the "Not available in Art" line and names the Art-only actions.

## Files

- `GameButtonInput.cs`, `InputHooks.cs`, `WheelInput.cs`, `WheelInput.GameButtons.cs`, `WheelInput.Shim.cs`
- `Settings.cs` (additive fields only)
- `SharedBindingCaptureHost.cs`, `Shim/ShimAxisConsumer.cs`, `Shim/SharedInputStatus.cs`
- `lib/input-shim/CONTROLS.json`, `tools/package/package.ps1`, `CHANGELOG.md`
- Tests:
  - `tests/WheelInput/GameButtonTests.cs` (`NativeActions`);
  - `tests/WheelInput/Program.cs` (`OrdinaryFallback`: shared horn and Art-only rows);
  - `tests/ShimInput/Program.cs` (partition, status, contract 3);
  - `tests/SharedBindings/Program.cs` (hidden select, accessor);
  - `tests/ProbeHooks/Program.cs` (six Rewired seams).

Not touched, by ownership:
- `SettingsPanel*.cs` belongs to Lola. The 8 Art rows go through the existing `Axis(c, channel, label)`.
- Glyphs belong to kiki.
- `tests/SharedBindingPresentation` still has the old "does not consume" text as a generic disabled-row fixture
  string. No host emits it now.

## Deferred until UAT handback (not run)

1. The normal net48 mod build with `/warningsaserrors`.
2. The `WheelInput`, `ShimInput` (net8 and net48), `SharedBindings` and `ProbeHooks` suites. ProbeHooks needs the
   Unity Mono collision mode and checks the 6 seams.
3. A schema 3 package with the Wheelkit schema 3 helper successor and its companion repin, in one slot (zele).
4. In-game run:
   - horn press, hold and release;
   - look back, and left, right and forward;
   - recover, repair and restart with the 1 s hold;
   - the same from a shared profile with horn, lookBack and reset bound.

   Only after that can they move from `fixtureOnly` to `runtimeShown`.

Not claimed: orbiting in the mod's bonnet view. That view is an extra `CarCameras` angle (`BonnetCamera.cs`), so the
orbit axes should apply, but nobody has seen it in the game.
