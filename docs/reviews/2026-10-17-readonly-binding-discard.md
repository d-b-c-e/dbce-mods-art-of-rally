# Discard after shared-reader loss

Private successor of rc27 source `75e3a6d76b230c682cd9b2bb1973fc28ce4878bb`,
assigned by Maze 219947. Worktree
`E:/Source/games/.worktrees/zori-art-readonly-discard-20261017`, branch
`zori/art-readonly-discard-20261017`.

The Art renderer previously placed Save and Discard inside its Editable access
branch. A reader becoming unavailable preserved the draft but hid Discard.
The toolbar now remains visible for a draft or saved next-start changes. Save
still requires Editable access and idle capture; Discard requires only a draft
and no shared/legacy capture. This adopts the existing canonical
`shared/ui/SharedBindingPanel.cs` policy, confirmed by Haru 219987/220017.
There is no new shared API, reader, writer, profile format or force change.

After Discard, the panel shows restart guidance only if a saved next-start
change still exists. Discarding unsaved edits alone does not claim a restart is
needed. Active/saved bindings and an earlier successful Save remain untouched.

The existing source-linked SharedBindingPresentation cases now cover read-only
Discard after failed Save, capture/legacy edit locks, disabled Save, preserved
saved next-start state and no false restart message. Whitespace checks pass.
The affected production build and this existing fixture are pending a fresh
serialized source-only handoff. No runtime, rendered, package, installation or
physical acceptance is claimed. Preserve qualified installed rc27 and its
passing package/driving/force/performance evidence.

Owned files: `SettingsPanel.SharedBindings.cs`, the existing presentation
fixture, this note and the corresponding TOOLKIT-ADOPTION entry. No other Art
source or canonical checkout is changed. Broader F6 UX recommendations are
recorded separately in the portfolio hub; HCT remains clean at `3489460`.

## Coherent shared status adoption

Maze220395 extends this same private successor with Haru's shared presentation
source `0dae6b3`. Its `SettingsPresentation.cs` is copied byte-exact (pin/hash in
Toolkit/Ui/PROVENANCE.md). Art's persistent footer passes the existing retained
SaveFailure to BindingPresentation together with draft/next-start state, then
appends the ordinary settings save status. Binding errors therefore remain
visible when changing pages. Bind from/Save bindings labels use shared constants.
The previous game-specific two-paragraph setup guidance remains accurate for
already-open profile devices; this consumer does not advertise Find devices.

Additional claims are the footer in SettingsPanel.cs, SettingsViewPolicy's thin
formatter adapter, the exact shared SettingsPresentation vendor/provenance and
existing SettingsUi fixture/project references. The existing panel fixture now
links the same shared presentation file. Focused status cases retain ordinary
settings errors beside a binding error and prefer binding failure over draft or
next-start text. No reader/writer or automatic Save is added.

One affected check should build the production plugin and run existing
SharedBindingPresentation plus SettingsUi after final source is committed.
The ignored artifacts/art-ui-20261017/Invoke-Affected.ps1 reuses the standard
lease/owned-process helpers, qualified rc27 UMM reference assemblies and serial
build-server-disabled commands; plan/parser only has run. Preserve its outputs
and original failures. Installed rc27, packaging, runtime and physical acceptance
remain unchanged. The shared formatter's own fixture is Haru's separate check.

## Actual affected qualification

Fresh grant220993 ran once on clean source
`eb2316dc5bc951557c9bf1f9c2abbe0c6b2118ab` from06:27:16.570Z through verified
release06:27:34.557Z, before the original06:31:16Z deadline. Release plugin build
passed with zero warnings/errors, SharedBindingPresentation passed35 checks and
SettingsUi passed169 assertions. No failures or retries occurred. Haru's shared
F6Style28 qualification is retained rather than repeated.

Actual logs/result are under this worktree's ignored
`artifacts/art-ui-20261017/run-01/`; result SHA256
`99FAE6BCB2D6664E373C6986B8B5CF9047845DDC648570875BA5EBCD82DC99E9`.
The source-only mod output SHA256 is
`82C448DA82FD3031DD60BE630EB52E366C27DCFC3112A53B441C5A06B808C13C`.
Its informational identity is `0.4.2-rc.28+clean`; the exact source is established
by this batch receipt, not by a commit embedded in that identity. This is not a
qualified release package or installable helper tuple.

Standard qualified trackerA28B713D reports16owned child identities, query-safe
quiescence and no remaining scoped children. Exact own-token lease release
passed and literal absence was checked before direct handback221020, prior to
documentation. Only private reference/output files were written; no game,
device, force, package, installation or owner configuration action occurred.

Next: retain these passing affected checks. Package/delivery, actual rendered
read-only Discard and every-page error visibility need their own assigned scope;
do not repeat accepted driving/force/performance or the unchanged UI tour.
The current installed rc27 and its owner acceptance gaps remain unchanged.
