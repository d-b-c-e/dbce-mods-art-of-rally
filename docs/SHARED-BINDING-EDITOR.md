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

The additive lifecycle extraction vendors toolkit `4425c06a69745c171ed7f025bf96104acf8184d5`,
`BindingCaptureFlow.cs`. Art retains its 144/232-byte decoder and action/menu/focus admission. The shared flow owns
freshness, one-second analog rest, deadlines, cancellation and the result callback into the existing draft. Art now
passes the original native sample sequence at offset 16; duplicate or regressed reads cannot refresh eligibility or
fabricate a button edge, including after reader removal. Production-host fixtures retain their earlier cases and add
those exact sequence checks. This extraction is source-only and awaits its coordinated offline batch; it is separate
from the already-qualified helper ZIP and accepted rc.22 install.
