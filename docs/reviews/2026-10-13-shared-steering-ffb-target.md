# Shared steering force target: source candidate

A fresh user of the shared controls setup has `dbce-controls.ini` but no legacy
`Settings.SteerBinding`. Following steering previously found no FFB target.
The active shared session locks the FFB device picker because changing devices
closes its readers. Resolving the target after attachment would stop shared input.

The candidate inspects the startup profile before force recovery. It asks the
shared Core's optional `dbce_shim_profile_action_info` export for the configured
steering identity; the native call uses Core's parser and pure binding admission
without creating a host, worker, provider, read handle or force effect. The same
byte array is retained for `SharedInputSession.Begin`. The report's full-file
SHA-256 must match those bytes. Art checks the response schema, action, axis kind
and nonzero instance/product GUIDs; it never reparses a binding.

`FfbSelection` captures whether shared input was selected for this launch and its
resolved target. Explicit selections retain precedence, including legacy explicit
GUIDs. In shared-follow mode, malformed, conflicting or unbound profiles and a
missing optional export provide a correction/restart reason. They never select
legacy steering, a display name, an index or the first available wheel. The native
FFB owner still performs strict instance-GUID and force-capability selection.
Configured identity is not connected hardware or physical force evidence.

Changing the saved shared-input flag or file applies next launch. Qualification
and attachment do not request a force reconnect or retarget the selected device.
No legacy binding is written and no effect, gain, reference or default is changed.
The existing locked picker remains; this slice does not enable a device change
while shared readers are attached. The owner's installed rc18 is untouched.

The inspector requires a fully consistent profile before returning an automatic
force target. Core ordinary input retains its existing ability to use valid
actions beside invalid ones; the force target query returns the invalid action's
name instead of selecting from that partial configuration. Unknown/unsupported
schema and cross-action product conflicts are checked by the same admission code.

## Review and validation status

Source base: Art `95203fd114af33a3459d35e50015039b2c6f1aba`; native copied provider
`f38b85566251cad742f68a0057cd166944bd5a9f`. The extension is additive and optional:
base ABI 2, 296-byte frames, 128-byte open arguments, bridge ABI 1 and 144/232-byte
metadata/samples are unchanged. Existing exports remain.

Source and targeted tests are prepared. No build or test suite has run for this
slice yet; the coordinator owns the next shared build lease. The current Art
native pin and `ExpectedShimSha256` still name the earlier qualified artifact.
This is intentionally reviewable source, not a repinned usable package. Existing
packages without the optional export give the matching-package message in shared
follow mode; explicit force selections and shared input remain available.

Prepared checks:

- Native pure plan and C ABI: full grammar, duplicate sections/keys, unsupported
  schema, invalid non-steering action, cross-action product conflict, exact hash,
  unbound action, bounds, truncation and the added export alongside old exports.
- Art `SettingsUi`: legacy follow, explicit selection precedence, failed explicit
  selection, shared follow, no settings/tune write and no stale-target fallback.
- Art `WheelInput --shared-ffb-only`: production Prepare/selection/qualification
  order with fake inspection and qualification boundaries, fresh shared-only
  settings, pre-recovery target, force-settle wait, byte-array identity at Begin,
  startup-latched shared mode, refusal and no late target resolution.
- Art `ShimInput --profile-info <candidate DLL>` on .NET and Unity Mono: actual
  pure export/response validation without a Core host, device or force output.

After source review, build native and managed candidates under the shared lease,
run the targeted checks plus existing Core/ABI/bridge regression coverage, then
review the resulting native hash before changing the source pin. A separate
muted runtime slot must establish fresh shared-only startup and continued reader
attachment. Normal physical FFB acceptance remains owner-attended.
