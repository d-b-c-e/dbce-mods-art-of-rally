# DBC-29 private window-landing source milestone

Base: retained DBC-259 rc260 source 3fc49e6, descended from public 0.4.3 e2b08f3.
Private branch egon/art-window-landing-dbc29. Main dirty files, rc260 ZIP,
other retained candidates and accepted installs remain untouched. October 24
handoff's later October 25 publication addendum supersedes its opening baseline.
No existing STD-038 adapter was found in main or the retained rc260 candidate.

WindowLanding.cs is byte-identical to toolkit 7e131c98a27404d8b2c5dda2ea3a9a02cbd6460e,
blob b111307d7ad3bcaca494c17ef7420cf76fa9caaa. Existing RacingRow/WindowsMonitors
pins remain unchanged. Shared toolkit and Wheelkit were not edited.

The mod samples topology every 30 updates while enabled and without the legacy
triple mod. It requires exactly one visible, nonminimized own-process Unity main
window, excluding secondary display windows. Shared policy selects the home
rectangle; independent rendering stays on the center display. No racing row
means no move. Unknown/off-desktop windows wait without consuming the episode.
One windowed SetResolution is allowed per observed topology; placement follows
at least 0.5 seconds later with NOSIZE/NOZORDER/NOACTIVATE. A refused placement
is logged and not retried. No projection, controls, force or saved geometry edits.
The preexisting output-size policy remains unchanged.

Evidence: Release plugin build with -warnaserror PASS, zero warnings/errors;
existing TripleScreen suite plus toolkit landing vectors/grid PASS 548 assertions;
Test-DisplayPin.ps1 PASS all four files. Initial build restore failed because
ProgramData and ProgramFiles(x86) were absent from the adapter environment;
child-only standard Windows values resolved it. First fixture run produced
CS8632 in copied test code; nullable context corrected before final validation.
No game/Windows placement result is inferred from these pure checks.

STD-038 is PARTIAL. Next unfinished source outcome: engine start-monitor
persistence with a qualified test/muted-launch write guard. This candidate
never writes UnitySelectMonitor or any PlayerPrefs. Engine mapping must use
WindowLandingPolicy's observed opening display and engine list, with old/new
logging only after admission; do not infer engine order from Windows order.
Actual asynchronous Unity sizing and native client placement remain unverified.
Darlene review and Sandy affected-display QA remain required before user-ready
claims. No package, install, launch, rig/input, public push or physical acceptance.
KI-52 remains separately scoped to DBC-28.

## October 26 review corrections

Sandy DBC-272 identified asynchronous resize, transient invalidation and client
origin defects in 63a1ff5. The corrected production adapter now uses an injected
host so the actual episode logic runs in the offline suite. It retains pending
state for up to 10 seconds, requires observed Unity windowed mode plus matching
engine/native client dimensions, and translates the observed client-to-outer
origin offset before the sole native placement. A unique validated replacement
HWND may complete the same pending request without another resize. Unavailable
windows wait; deadline/refusal are explicit terminal log outcomes. Topology
changes cancel stale targets. Rectangle/primary tuples are sorted before the
key is constructed; A->B->A is a new observed episode. Projection's output-size
writer is explicitly suspended while landing owns the resize, including the
request frame. Other output policy and accepted projection math are unchanged.

Release plugin -warnaserror PASS: zero warnings/errors. TripleScreen PASS 559
assertions (11 added against production episode code). Display pin PASS all
four files. Diff check PASS. Initial correction build caught a list-interface
mismatch and duplicate test Compile inclusion; both corrected before passing.
The fake host does not test native APIs or rendered output. Native sampling uses
the Unity caller's DPI context; unequal engine/native sizes time out rather than
assuming success. Actual mixed-DPI, post-resize Unity behavior and stable final
client placement remain for Sandy's separately authorized runtime QA. No runtime
or preference-write qualification claimed. STD-038 remains partial; saved-monitor
persistence with qualified test/muted launch classification is still unfinished.
No package, install, launch, rig/input, public push, shared toolkit/Wheelkit edits.
