# Owned controls glyph presentation - October 15

Source-only candidate for Sana request 162432, based on `643bbd0`. No build,
fixture execution, launch, install, settings change or input automation during
owner UAT boundary 162171.

## Diagnosis

`GlyphFallback` returned whenever a sprite was nonnull. The retained native
`ControlsRemapper.RefreshDisplayedGlyph` asks for controller artwork, then falls
back to generic artwork with GUID `cd9718bf-a87a-44bc-8716-60a0def28a9f`.
Missing generic entries can leave text badges beside other rows that still show
gamepad artwork. A nonnull image does not prove its physical binding is correct.
The menu fallback likewise trusted any returned artwork.

The native remapper walks Rewired maps, matching action ID and split-axis
polarity. It does not inspect the active shared profile. A native map can describe
a different control from the effective shared input. Rewriting stock maps for
cosmetics would change another input authority and is unnecessary.

Native-method evidence is Niru's retained reconstruction in
`%LOCALAPPDATA%/Temp/claude/E--Source-dbce-project-mgmt/94b35c50-1f1b-4817-b63c-3e91de4434c6/scratchpad/art-actions/ControlsRemapper.cs`
and `RewiredConsts.Action.cs`, from
`E:/art-ui-research/controls-offline-01/references/Managed/Assembly-CSharp.dll`
(reported SHA prefix `7807A1D674C8214F`, coordination 162702). The original method
resets `activeController` to null on its keyboard path; our postfix uses that
exact selected controller field rather than guessing again.

The historical closed qualification snapshot
`E:/art-ui-research/controls-first-shot-01/before/copy-mod/dbce-controls.ini`
has SHA-256 `002347077864493E8429817F0A93F3F21E2875E85A7AD1BACFEB2BF845F5C0C0`.
It binds steering X, brake Rz, clutch Slider 1, handbrake Slider 2 and camera
button 32 (display B33). Throttle and sequential shifts are absent; transmission
is H-pattern. Its paired `Settings.xml`, SHA-256
`1186E327AD4D0E44893ABC64BA76E51D59D97DFE0955CE7728256607A3B79C8F`, enables
shared/assigned input and retains legacy throttle axis 2 (Z). Existing
`ApplyAxisFallback` consumes that loaded Art binding when shared throttle is
unowned and its physical object has no shared collision.

There is no frozen native map from current owner UAT. Neither that screenshot
nor these historical settings establishes current effective assignments. The
proven source defect is generic/stale artwork bypassing the effective binding.
This change does not claim an input-mapping correction.

## Presentation change

- Both existing postfixes try the effective owned binding before accepting
  artwork. Action ID and polarity use Niru's single `NativeAction` table;
  the glyph code has no separate action-name or dispatch table.
- Shared labels use `TryActiveBinding` and the startup Active document. Saved
  and Draft never become prompt sources before restart. An owned shared action
  without an Active witness cannot substitute an Art fallback.
- Unowned shared actions and Art-only routes can label the already loaded Art
  binding through the new `WheelInput.Glyphs.cs` accessor. It requires a GUID,
  valid reader cache, no shared collision and no latched delivery. No polling,
  PrepareGameQuery, EnsureSharedFrame, parsing of settings or file read occurs.
- `SharedInputStartup.Glyphs.cs` requires an attached, started, nonstopped
  session. The displayed joystick must be the exact admitted isolated object.
  The binding device separately joins the existing owned reader to an isolated
  Rewired object by exact path/product with unchanged connection epoch. Missing,
  ambiguous, disconnected or restored witnesses refuse. DirectInput and
  Rewired GUID namespaces are not equated. No fresh census or acquisition.
- A wheel-selected row can label the actual separate pedal control. Shared
  zero-based button 12 displays B13; a Rewired name Button 13 stays B13.
  Rx/Ry/Rz and S1/S2 remain distinct. Hat directions are not rounded into
  another assignment.
- Keyboard, unrelated gamepad/native artwork and unrouted actions retain their
  stock presentation. Existing native missing-artwork fallback remains. No
  stock mapping, settings, input/FFB dispatch, reader or native binary changes.

## Dependency and deferred checks

Requires Niru's agreed 162702 seam, to integrate before any build:

```csharp
internal static bool GameButtonInput.NativeAction(int rewiredId, bool negative,
    out string sharedId, out WheelInput.Channel? artChannel);
internal bool SharedBindingCaptureHost.TryActiveBinding(string sharedAction,
    out Dbce.Wheel.Input.ControlBinding binding);
```

`SharedInputStartup.cs` changes only its declaration to `partial`. New partial
files avoid Niru's dispatch, settings and capture-host claims. Lola's UI files
are untouched.

`tests/PromptGlyphs` links the production postfixes, ownership resolver, loaded
legacy fallback accessor, formatter and legacy parser. Cases cover generic
nonnull sprites, no native map, separate pedals, keyboard/gamepad preservation,
wrong product/path/epoch, disconnect/restore/ambiguity, legacy throttle,
collision suppression and no legacy substitution for an owned shared action.
Router stubs only supply fixture routes; Niru owns actual action-ID coverage.

Only source diff/whitespace inspection has run. Defer the net8/net48 fixture,
affected mod compile and actual-game Harmony seam checks until an authorized
post-UAT offline slot. Later rendered acceptance must check badge fit, native
keyboard/gamepad transitions and the reported controls rows. No runtime,
rendered, physical, package or install acceptance is claimed. Broad baselines
are not needed for this presentation slice.
