# Shared binding capture and profile edits

`BindingCapture.cs` contains the reusable, device-free capture detector. Hosts feed snapshots from their own reader;
the detector owns no clock, cancellation, acquisition or force output. Held buttons and hats need release/repress,
axes use a fixed rest, and ambiguous changes never guess. Optional per-axis known minimum/maximum values scale the
threshold to the reader's range; absent/unknown ranges are excluded. Existing callers without range metadata keep
the original 12000 threshold. A rest sample is not endpoint calibration.

`ControlProfileDocument.cs` reads Schema 1 with the canonical `ControlBinding` parser. It preserves action, device,
product, inversion, range/rest/travel, transmission and revision, and retains unrelated INI lines. Only explicitly
changed actions and Revision are edited. A null edit is an explicit Clear; it is not a missing value. Unrepresentable
extended binding values, duplicate keys/sections, mixed line endings, and a Controls section above 64 KiB are refused.
No device or file is opened. Hosts own expected-source checks, backups, the write transaction and restart semantics.

`ControlBinding.FromCapture` validates captured parts through that same parser; `WithInverted` changes only axis
inversion on a copy. There is no host-specific binding grammar or second profile format.

`ControlProfileDraft.cs` adds the shared in-game active/saved/draft lifecycle. It performs file I/O only on explicit
Save: bounded expected-source reads, a flushed temporary file, atomic replacement with a unique backup, and readback.
Failed Save retains the draft; successful Save changes the next-start document without changing Active. Hosts supply
their maximum full-profile byte limit and own access, focus and input suppression. This small source is for
Mono/IL2CPP hosts that cannot load Wheelkit.Core.

`BindingEditorContract.cs` is the renderer-neutral `ISharedBindingHost` and snapshot view model contract. Active,
NextStart and Draft are distinct typed values; null means no delta and `Bound=false` is an explicit unbound value.
Each host supplies labels, reasons, device views and capture commands. A panel consumes this contract and
opens no devices. Vendor this shared contract rather than maintaining game-namespace copies.

# Optional POV reader for existing managed pins

`PovSnapshotReader.cs` is MIT source that can be vendored when a consumer keeps
an older managed wrapper but its existing native module provides
`ReadDeviceStateWithPov` (native 0.9.0+). This is not a native DLL update.

The consumer supplies the export from the exact already-loaded module and a
slot owned by its normal input lifecycle. The helper never loads/unloads a
module or opens/acquires/releases a device. Serialize it with that owner.
Clear the binding before unloading its module. An absent export is a separate
capability decision; a failed coherent read must not retry through the legacy
reader. Gate all mapped values on success; raw zero is not pedal release.

Tests: `dotnet run --project tests/PovReader -c Release` (fake delegate only).
They cover coherent counts/data, absent export, rejected/partial/throwing reads,
invalid arrays/slot and cleared outputs. They send no hardware input or force.
The runtime ABI and lifecycle requirements remain in `docs/POV-INPUT.md`.
# Shared steering identity for FFB targets

`ShimSteering.cs` is MIT source to vendor beside `ControlBinding.cs` (netstandard and Unity Mono consumers). It
returns the steering binding (device instance GUID, product GUID, display name, ...) from a shared `[Controls]`
section only after native Core accepted those exact bytes (`dbce_shim_profile_info`: `ok=1` and the section's
SHA-256). It reads the single `steer` line with `ControlBinding`, the only grammar. Product policy is the consumer's:
an empty product GUID is returned as written (DRIVE accepts it; iRacing's adapter refuses it). Several steer lines, a value that does not fit steer, no device, or bytes that are
not UTF-8 give no identity and a reason; they never throw into the caller's input open. A wrong Core report throws
`InvalidDataException`. It opens no device and sends no force. The consumer's route policy (explicit selection,
pending, owned, stopped, actual refusal) is in `knowledge/SHIM-IDENTITY-AND-NATIVE-ACTIONS-2026-10-11.md`.

Tests: `dotnet run --project dotnet/Dbce.Wheel.Input.Tests -c Release` (bytes and reports only).

# Value shaping for declared extras

`ExtraControlValue.cs` is MIT source to vendor beside `ControlBinding.cs`. It holds the arithmetic native Core applies
to a game's declared extras (native/shim extension 2, `dbce_shim_extras.h`) after its status checks, for managed
previews in Wheelkit, the setup helper and F6.
- `Pair(neg, pos)`: a centred component from two digital halves. Each half is `ControlBinding.Normalize(ControlShape.Digital, raw)`. The result is pos minus neg, an unbound half passes 0, and opposite halves together give 0.
- `Stick(x, y, StickRange)`: each component is clamped to -1..1. `Circle` scales any vector longer than 1 back to
  length 1; `Square` keeps both components. +X is right and +Y up.
- `TryParseRange` accepts the declaration's words, exactly `circle` or `square`.

It reads no device and knows no context or status. Unit neutrality (a configured part that is not Live leaves the whole
destination neutral) stays in native Core. Non-finite input throws, and it is plain C# with no tuples, so netstandard
and Unity Mono consumers can vendor it. The tests in `Dbce.Wheel.Input.Tests` match native `test_extras`.

### Clock-injected capture lifecycle

`SharedBindingCaptureFlow` consumes `AcceptedBindingObservation` from the host's reader. The adapter keeps
its binary decoder, applies metadata masks, chooses the action and supplies its menu/focus policy. Observations carry
exact instance/product identities, an opaque identity key, a per-instance generation/epoch, native read sequence and
the injected local-clock `Seen`. Only new valid, non-stale reads are observations; loss/ended/closed/decode refusal calls
`Remove` with an actionable reason. Repeated or regressed sequences never refresh eligibility. No reader is opened.

The flow owns 0.25-second freshness, one-second analog rest, 10/11-second deadlines, held-input edges, cancellation,
canonical `FromCapture` and exactly one result callback into the host's draft. Call `Refresh(canEdit)` before rendering;
the callback returns null only after accepting the draft edit, or a refusal reason without changing prior state. A
refusal cannot show a captured-success message. Save/restart remain separate. Art and DRIVE retain their different ABI decoders.

One flow belongs to one reader handle lifetime, with per-instance epochs and read sequences that only advance.
`Remove` and `ReaderStopped` keep watermarks against cached replay, so a recreated reader needs a new flow.
Observe accepted ticks while the editable F6 binding editor is visible, including before Begin, to supply the device
list. This copies the existing cache; it never activates another reader.

### Legacy Mono binding consumers

Define `DBCE_INPUT_NET35` only when source-vendoring the binding components into a
legacy Mono host. Modern builds keep their existing public signatures and defaults.
The legacy surface uses IList/IDictionary, read-only list wrappers and protective
binding/draft dictionary copies, domain clock/capture callbacks, and a named
snapshot identity struct. GUID constructor fallbacks retain the same exact braced
round-trip grammar. Parse refusal uses IOException where the shipped framework
lacks InvalidDataException. Explicit Save flushes the managed stream, then Windows
FlushFileBuffers on its SafeFileHandle before the existing guarded Replace and
readback. This Windows-only path is opt-in, not a general cross-platform fallback.
