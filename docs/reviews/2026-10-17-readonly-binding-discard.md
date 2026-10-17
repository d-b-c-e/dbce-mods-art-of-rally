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
