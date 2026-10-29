# DBC-29 candidate-specific runtime qualification handoff

Candidate: f2a770491dad80b5c5c0088f55bb20c04e7d6cdf, branch egon/art-window-landing-dbc29, E:/Source/games/.worktrees/egon-art-landing-dbc29. This wake verified exact HEAD and a clean worktree. No rebuild or runtime action was repeated.

## Cleared source gates
Sandy DBC-279 and Darlene DBC-280 PASS at this exact commit. Earlier review findings are resolved. Recorded candidate evidence: Release build zero warnings/errors; 596 offline assertions; display source pin and diff checks PASS. Offline checks do not execute Unity preference APIs or native placement. Public baseline remains 0.4.3; retained rc260 and accepted installations remain separate.

## Proposed next authorized scope
After explicit approval only: prepare a private package from this exact source, run existing required package checks, record source and payload/ZIP hashes, and give Sandy one serialized affected-display qualification slot. Install only while the game is closed, preserving settings and a restorable accepted-install backup. Do not close an existing game session. No public push/release, shared toolkit/Wheelkit edits, force testing, rig/input injection, or physical acceptance is included. Any unavailable topology or required input case stays untested.

## Sandy evidence matrix
1. Capture baseline topology rectangles, primary flags, DPI/scaling, Unity engine display list/order, opening main-window client/outer rectangles, mode/dimensions, and UnitySelectMonitor presence/value. Preserve settings and preference state before the session.
2. Ordinary launch with a supported racing row: validate one visible/nonminimized own-process Unity main window, observed Unity Windowed mode and matching engine/native client sizes before placement, then independently measure final client origin and size against the selected home rectangle. A native API success message alone does not prove stable final geometry. Independent side-display windows must be excluded.
3. Observe stable final placement after Unity settles; preserve accepted projection, force and controls. No row should produce no move. Transient minimize/ambiguous window observations should wait within the 10-second deadline; timeout/refusal terminates the episode without repeated placement. Exercise topology/replacement-window cases only within the separately approved display slot; otherwise mark untested.
4. Repeat the affected placement case with available mixed-DPI topology. Capture both engine and native dimensions in the actual caller DPI context. Any mismatch must time out rather than claim placement. Do not change global DPI awareness.
5. For an ordinary launch with uniquely proven engine-order mapping and a changed target index, record the preference before/after a successful SetInt/Save and the old -> new diagnostic. Exit normally and relaunch, recording the persisted value and actual opening display. Absent, unchanged, ambiguous-engine-size and topology-invalidated evidence should not produce an unsupported write.
6. Separate muted/test classification from ordinary persistence: any nonempty DBCE_FFB_MUTE or DBCE_ART_SHIM_REQUEST, installed/loaded DevRecorder, session-request marker, editor or batch/nographics/runTests suppresses writes. Do not remove an installed recorder merely to force the ordinary case. A muted launch cannot qualify successful ordinary-launch persistence. These routes may establish suppression only. Inspection failures also suppress writes.
7. Preference API failure containment is currently offline-qualified: initial read failure suppresses persistence while landing completes; write failure attempts only in-memory restoration and leaves disk state unconfirmed. Do not induce access failures or corrupt preferences on the accepted install. Report native failure paths as offline-only unless a separately authorized isolated fixture exists.
8. Restore the accepted install and exact pre-session settings/preferences after the bounded slot, record hashes and restoration outcome, and distinguish source/build/package/install/runtime evidence from owner physical acceptance.

## Exit criteria and retained gaps
Sandy records candidate hash, topology, measured placement, actual preference flush/relaunch result and restoration. Darlene's final diagnostic review is complete; new user-facing changes require renewed review. STD-038 remains PARTIAL until applicable runtime evidence is complete; skipped matrix rows stay open. Existing unrelated unchecked/partial standards and missing adoption entries remain unchanged, including previously recorded STD-037 and STD-025/026/028/029/032. No duplicate F6 implementation; KI-52 remains DBC-28.

Next executable source outcome is correction of any exact candidate-specific qualification finding. No additional defect is currently supported by the passed source reviews; do not invent another implementation lane.
## Source-owned handoff reconciliation (DBC-281)

This file retains parent document `window-runtime-handoff`, revision
`b806442c-da07-4715-91ed-ac87f6d239f0`, with these execution details. It is a
handoff only: its documentation commit is not a new runtime candidate. Build or
package identity must continue to name f2a770491dad80b5c5c0088f55bb20c04e7d6cdf;
record any later source change separately and renew affected review.

### Expected visible behavior and preconditions

The adapter must be enabled and the legacy triple mod absent. For an auxiliary-
display start with a supported Separate racing row, the main client lands on the
center home display; under Surround it lands on the home Surround rectangle.
Independent side windows remain excluded. Already-home and no-row cases do not
request a corrective move. This feature adds no player control and must not
activate, raise or focus the window. Preserve the accepted independent projection
and Surround settings; do not infer geometry acceptance from the landing log.

After a move decision, there is one windowed resize request. Placement waits for
observed Windowed mode and matching Unity/native client dimensions, with a
10-second deadline and no minimum half-second delay. Native placement accounts
for the observed client-to-outer offset. Projection output resizing resumes when
the landing episode finishes; inspect the final client after that handback too.
There is no continuous correction loop. A late Unity reposition is a finding,
not permission to repeat placement or change the candidate during the slot.

Use one evidence row per case, recording: candidate source and installed payload
hashes; package hash when separately authorized; classification/marker state;
Windows rectangles/primary/scaling and Unity display order; original preference
presence/value; opening client and outer rectangles; resize/placement timestamps;
final client rectangle immediately and after settling; foreground window and
visible stacking before/after; exact landing diagnostics; preference after flush
and after normal exit/relaunch; restoration receipt; PASS/FAIL/UNTESTED and reason.
A screenshot supports visible behavior; it does not establish engine display
order, native coordinates, or a durable preference write by itself.

### Rollback boundary

Before any separately authorized install, Sandy records the accepted install's
payload hashes and retains its restorable package/backup, owner settings, and the
actual Unity preference store with key presence as well as value. Discover that
store from the real install; do not guess a registry path or treat this key's
absence as a saved zero. Preserve unrelated preference keys because PlayerPrefs.Save
flushes the store. Record the original launch environment and markers too.

On wrong placement, unexpected preference mutation, focus change or output-size
oscillation, capture evidence and stop the bounded case. Close normally only as
allowed by the runtime authorization. Restore the accepted payload and exact
settings/preferences while closed; remove only slot-created markers/environment
changes and retain preexisting recorder files. Verify payload/settings hashes and
preference presence/value, then report restoration. If restoration is incomplete,
record the exact remaining difference rather than claiming rollback succeeded.
A caught Save failure's in-memory restoration is never evidence of disk rollback.

### Next source allocation from the actual roadmap

`docs/ROADMAP.md` is dated September 30 and still lists historical release work;
read it with `docs/KNOWN-ISSUES.md` and the October 24 hub handoff's October 25
publication addendum. The latter's next steps name KI-52 then F6 refusal text,
followed by the STD-038 addendum. F6 is already delivered on DBC-259; persistence
is already implemented here. Do not allocate either again.

The next unfinished Art source milestone is the named, reversible KI-52
canvas/background adjustment under the existing Surround guard (DBC-28). Retained
12b69aa at `../egon-art-ki52-dbc28/docs/reviews/2026-10-26-ki52-source-prerequisite.md`
records the missing exact hierarchy/RectTransform/render-mode target evidence;
The retained report is prerequisite evidence, not target discovery. No broad canvas rewrite is
supported. Capture that affected state through Sandy before selecting the target.
The older roadmap's KI-6 snapback investigation also requires steering/slip
captures before adding a damper; it is not a justified code-change substitute.

If these capture/runtime dependencies remain, the independent portfolio source
allocation to reconcile is GRID 2019's catalog/setup parity for the F6-only
Normalize effect gain/Strength controls. GRID's current README explicitly records
that gap. Check the retained DBC-233 advanced-FFB work and current manager
assignments first; it may already contain the implementation. Shared Wheelkit
changes need a separately coordinated task. This handoff starts no duplicate
implementation, changes no shared source, and grants no new runtime permission.