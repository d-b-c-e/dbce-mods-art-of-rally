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

## Final private package preparation

Maze221029 assigned packaging, with no owner installation. Ready request221154
is a six-minute total using the existing rc27 `tools/package/package.ps1` route.
It always builds and requires ProductVersion to equal the source-stamped receipt;
the source-only `0.4.2-rc.28+clean` output therefore cannot be relabeled into a
package. One mandatory normal packager build will stamp exact tested source
`eb2316d`, plus its required metadata verifier. Consumer35/UI169 remain retained;
there is no new product test suite, native/helper build or raw-hook repeat.

The ignored `artifacts/art-ui-20261017/Invoke-FinalPackage.ps1` is parser/plan
checked only. After a fresh grant it detaches exact sourceeb2316d under fresh
ignored output, reuses qualified rc27 UMM references and pinned normal force/shim,
creates normal rc28 shared-input and companion archives, verifies exact extracted
membership/hashes/identity pairing, runs existing Test-Installer including
rollback to the retained rc27 package, and checks the companion with Windows
PowerShell5.1 PlanOnly against an inert game/receipt fixture. No helper GUI,
game, device, owner state, installation or public distribution is involved.

Haru221081 located/reverified the unchanged schema3 helper at
`E:/Source/dbce-project-mgmt/artifacts/art-helper-successor-20261015/run-01/helper/StandaloneModSetup-private-a8583c59feef-win-x64.zip`
with its `.sha256` sidecar. ZIP SHA256
`F3CF1345402CD31562B8A6112F440419DC79C6D28DDD276B4DBEDFEA084FC331`,
bundle manifestE9D54FFF,527files. Keep it unchanged; no shared admission update
is needed. Packaging has not entered and no archive is qualified yet.

## Actual final packaging and remaining environment check

Grant221208 entered06:32:46.705Z and released06:33:18.459Z, before its original
06:38:46Z deadline. The exact clean product source remains `eb2316d`. The stock
packager's required source-stamped build passed with zero warnings/errors and
its required metadata verifier passed353 assertions. Both archives were created;
extracted membership, every payload hash, clean identity and companion pairing
passed. Existing disposable installer/rc27 rollback checks passed64 assertions.
No earlier consumer, driving, force or performance check was repeated.

The final Windows PowerShell5.1 companion PlanOnly failed because Get-FileHash
was not recognized. Preserve this original failure, not a product or archive
failure: `artifacts/art-ui-20261017/package-01/result.json`, SHA256
`942C5EDAAFEF18C6B06CD997AC08BDDD00A59A9B0204130405D6E46C2A54618A`.
Logs are in that directory; installer details are under its detached source's
`results/installer-613b449bf8d1424e9522a265868db965`.

Exact private output tuple, all under `package-01`:

- Source-stamped identity:
  `0.4.2-rc.28+eb2316dc5bc951557c9bf1f9c2abbe0c6b2118ab.clean`.
- Normal ZIP: `source/dist/ArtOfSimRally-0.4.2-rc.28-shared-input.zip`;
  SHA256 `B7818C8DF90310D3B4C9BE7E67E0459EB22176B31FE670C40F7B0FF2D5981A2F`.
- Companion ZIP: `source/dist/ArtOfSimRally-0.4.2-rc.28-shared-input-controls-setup.zip`;
  SHA256 `78C27DB09A539FCBF0149CA71895F1E6238C8A68A54B41EFB6E929B41FF97070`.
- Normal manifest SHA256:
  `2FD01BB9CD870A27DE800105579A4574706D022CB886E785BD5A6373BE4DB2F0`.
- Packaged mod SHA256:
  `296C8EFA49A718AF47F22292B3346BFE5260BEB677969B9B24238556FDF6712A`.
- Normal force44E46799, shared shimBC9B030A and schema3 helperF3CF1345 remain
  unchanged. `artShimProbe=false`; the helper was not rebuilt.

The standard tracker recorded46 owned child identities, query-safe quiescence,
no remaining scoped children and exact lease release. Literal lease absence was
verified06:34:17Z and reported directly to Maze before documentation. No game,
device, helper GUI, owner installation or owner configuration action occurred.

Maze221304 and Lena221360 identify the existing DRIVE environment-only repair:
inside the Windows PowerShell child, prepend its own `$PSHOME/Modules` plus the
machine PSModulePath, then explicitly import Microsoft.PowerShell.Utility before
invoking the unchanged Art Setup-Controls.ps1 with PlanOnly. No global setting or
package byte changes. The ignored `Invoke-ArtPS51Plan.ps1` reuses those exact two
lines. `Resume-CompanionPlan.ps1` is parser/plan checked only, pins the original
failure and archive hashes, and requests a fresh two-minute maximum continuation
for that single remaining invocation plus owned-process cleanup. Revo has the
rig next; no continuation has entered and no final overall PASS is claimed yet.

Supported desktop/rendered Discard and persistent error checks, and prior owner
physical acceptance gaps, remain open. Installed rc27 stays unchanged.
