# Shared binding editor source checkpoint

The F6 host is under implementation on `hera/art-shared-bindings-20261014`, based on the private rc.22 source
`2decf4d418815ca3835bbb51eb83a6e0ab886bc5`. Nothing in this checkpoint has been installed or published.

The existing bridge is the only physical reader. After the whole frame passes Tick and Consumer.Publish, its optional
observer receives borrowed constructor metadata, owned path and that frame's raw bytes. The observer does not Describe,
Read, acquire or enumerate devices. An observer exception detaches that observer without stopping accepted driving input.
The host copies the data it retains, uses object masks and known ranges, rejects invalid/out-of-range frames, and cancels
capture on lost focus, closed settings/main menu, stale data, disconnect, generation change, timeout or explicit Cancel.

`Toolkit/Input/BindingEditorContract.cs` (`Dbce.Wheel.Input.ISharedBindingHost`) is the shared panel seam agreed with Toto.
It is vendored from toolkit `3427ab1dc34ca03f87f3fc905c18a5f76e12314d`; no Art-specific interface is maintained. Values are distinct:

- Active is the frozen startup value.
- NextStart is a saved value waiting for restart; null means the same as Active.
- Draft is an unsaved edit over the saved document; null means no draft. `Bound=false` explicitly means Clear.

The panel reads this model and issues commands. It adds no device polling. Its Editing/CancelPendingEdit integration
must prevent the settings shortcut from closing a capture and give Escape to Cancel before closing the panel. Toto owns
the panel layout and copy; those changes are separate from this host checkpoint.

The shared `ControlProfileDraft` changes only explicitly edited action lines and Revision, keeping other bindings and unknown INI content. It verifies
the expected source hash, writes and flushes a temporary file, checks the source again, atomically replaces it with a unique
backup, and reads the result back. Failed Save keeps the draft. An external writer can require a restart/reread. The running
session keeps its original profile, steering target, backend and force tuning. Saved changes take effect on a new game process.
The host never hot reloads or requalifies the reader session. Art's stricter 64 KiB full-profile inspection limit is retained.

The shared source is in the toolkit's `dotnet/Dbce.Wheel.Input/`, including `ControlBinding`, `BindingCapture`,
`ControlProfileDocument`, `ControlProfileDraft` and `BindingEditorContract`.
The portable setup editor uses the same canonical document/parser and capture detector. Wheelkit.Core is not loaded into
Unity. Art's `SharedBindingDraft` is a thin wrapper supplying the 64 KiB host limit. Binding is available only for
qualified readers already open in this process, within the main-menu settings view.
New or unavailable devices require a restart or the portable Controls Setup download; transmission changes remain in that
setup flow. This limited in-game host is not fresh-device discovery, endpoint calibration or runtime acceptance.

Verification is pending the coordinator's next admitted batch. `tests/SharedBindings` covers the pure host/draft under
net8.0 and net48; `tests/ShimInput` adds Valo's accepted-cache single-read and observer-failure checks. These use fake bytes,
temporary files and clocks only. A successful source build or fixture run will not establish attended in-game acceptance.

The additive lifecycle extraction vendors toolkit `bcfb8522b1583fc61b66aa7ecfc48a733f979f38`,
`BindingCaptureFlow.cs`. Art retains its 144/232-byte decoder and action/menu/focus admission. The shared flow owns
freshness, one-second analog rest, deadlines, cancellation and the result callback into the existing draft. Art now
passes the original native sample sequence at offset 16; duplicate or regressed reads cannot refresh eligibility or
fabricate a button edge, including after reader removal. Production-host fixtures retain their earlier cases and add
those exact sequence checks. This extraction is source-only and awaits its coordinated offline batch; it is separate
from the already-qualified helper ZIP and accepted rc.22 install.

The flow belongs to the prepared reader's lifetime. This host is replaced on Prepare and an attached Art session stops
permanently; it does not recreate a native source behind an existing flow. The capture callback rechecks current
admission and returns a refusal reason without changing the draft, or null after Set accepted it. Success is never
reported for a refused result.

Whole-frame Publish success does not prove every original raw read is fresh. Before supplying an observation, Art
compares its original start/end QPC microseconds with a source-domain QPC clock read after Publish. `BindingSampleClock`
uses the same system origin and integer conversion as native `dbce_shim.cpp::bridge_clock`. A negative/bad/future
timestamp, clock failure or read more than 100000 microseconds old is refused and removes capture eligibility. This
conservative check after Publish dominates Core's earlier 100000-microsecond decision-boundary limit, including when a
slow later device ages an earlier valid sample. It queries only the clock and adds no provider or device read. Fixtures
inject that clock and cover delayed earlier reads, future timestamps and clock failure alongside the original cases.

## Runtime evidence, 2026-10-15 (FFB-off binding host on a private copy)

Two runs, both labeled "FFB-off binding-host evidence (no attended foreground or force claim)" (sana 131300/132808).
The setup was the same for both:
- the private copy, launched once each, at Steam integrity;
- force feedback off in the copy's Settings.xml only, with `DBCE_FFB_MUTE=1` and the latched DD301314 native;
- no activation and no OS input.

Pins: Mod F98D3BDF (built at 11a7b6b; src/tests are unchanged on main since), shim BC9B030A (the mod's own pin), owner
profile 73E78230 (read only). The evidence is in hub `artifacts/art-f6-ui-capture-20261015/RESULT.md`.

What the two runs show:
- **Attach.** The first live attach on BC9B: "Shared input: attached profile … isolated MOZA R12 Base, DS-8X Shifter",
  then "first frame accepted".
- **Editable host.** The host was Editable with Unity `focused True` while the real OS foreground stayed with another
  process (`osForeground False`) in every state line. The editor's own gate is `Application.isFocused`; the force
  connect, which needs the OS foreground, was not involved because FFB was off.
- **Binding steps (run-d4b7959-ffboff, 32 shots):**
  - Listen, cancel, draft and discard work.
  - The close guard warns once, then closes without saving.
  - The footer reads "Bindings unsaved · Settings saved" with a draft and "Saved" without one.
  - The Controls setup guidance renders as agreed.
  - That run's save was refused correctly: the recorder had cleared steering, and the profile document requires it.
- **Save (run-ae46b06-ffboff-save):**
  - The product validator accepted clearing throttle first; then Save returned True.
  - The host showed saved-for-next-start with no draft.
  - The footer read "Bindings saved for next start · Settings saved".
  - The profile file changed in exactly two lines (Revision; the throttle line removed). Steering and every other line
    were unchanged.
  - The runner restored the copy's profile.
- **Force.** Each run's UMM log shows `ffb=False` and no force-device attempt.

Still open (none of these is claimed):
- the attended, focused workflow with FFB on: binding while the game is the OS foreground, plus a real moved-control
  capture;
- force feedback with the shared profile;
- restart consumption (the saved profile becomes active at the next start, and the car follows it);
- rebinding after a disconnect;
- the final shared-input package bytes. `package.ps1 -SharedInputCandidate` builds with the ArtShimProbe flavour and a
  new stamp, so it is different code from F98 (Lola 132795; the separation is under review);
- one shot detail: in the save run, the cleared row's "next start: Unbound" line was half cut by the scroll body, so it
  is shown by the host state and the file, not by that row's pixels.
