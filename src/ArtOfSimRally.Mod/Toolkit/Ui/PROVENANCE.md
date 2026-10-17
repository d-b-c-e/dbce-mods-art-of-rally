# Shared control display names

`SettingsPresentation.cs` is vendored unchanged from toolkit `d07228104ab62ddf903f42912206be679e0a6b01`,
`shared/ui/SettingsPresentation.cs`, under its MIT license. It supplies `Dbce.Wheel.Ui.ControlNames`; it performs no
device I/O or persistence. Toto's panel widgets/styles are a separate UI source pin and must not duplicate this class.

## Shared F6 rows

- `F6Style.cs`: dbce-wheel-mod-toolkit commit e9a839efe212df9fe89b60e3ef15c68a6dc016d8, shared/ui/F6Style.cs (byte-exact, git blob 9fdf61860d42d6f912e9429e6602e8cab1dc039d), SHA-256 02AA4E76635052C7B42F0DDC3FA0824766CC581BEA928D0245AE23F76E9C94CA.
- `F6Widgets.cs`: dbce-wheel-mod-toolkit commit e9a839efe212df9fe89b60e3ef15c68a6dc016d8, shared/ui/unity/F6Widgets.cs (byte-exact, git blob f13cdca71fc92fd90487eb05ba20cc74530f29f7), SHA-256 7B31439F5AB80E2FDECF6B01C8D31B3CF8429E12926D67FF55A696778A02145C.

MIT. The F6 panel palette, row geometry and the Unity IMGUI (GUILayout) row widgets used by SettingsPanel. They draw and
report edits only; they read no device and write no file. Do not edit here: change the toolkit and re-vendor.

## Shared binding Save status (source-only candidate, October15)

BindingSaveStatus.cs is byte-exact from toolkit d342370f0397c041091d2fd8cc77488a9812d48c,
shared/ui/BindingSaveStatus.cs, MIT (root LICENSE), SHA256
D22B954166E904D6FFC11F2309B83ADFC5D5802D1D1692BF1D7EB585543FCAE5.
Optional ISharedBindingSaveStatus does not change ISharedBindingHost. Failed Save
text persists independently of capture/general status; only successful Save,
explicit Discard, or a new host clears it. No writer/capture/ActiveSaved/force
policy changes. Source-linked regressions are prepared; build/run pending.


## October16 small Help repository links (source only)

F6Style.cs now comes byte-exact from toolkit d06aee8a27572c013489981fc4f4b9ee08d28123,
shared/ui/F6Style.cs, MIT; SHA256
FDDD107F1DA5B1511A33FC4ABA1A1C414A464155A2736A5A63AE5FBB544504F8.
Adds shared22-unit text-link height/1-unit underline; all existing palette and layout rules retained.
This supersedes the earlier pin for this file only. No build, suite, game,
report submission, URL change, owner install or rendered acceptance accompanies it.


## October16 small Help repository links (source only)

F6Widgets.cs now comes byte-exact from toolkit d06aee8a27572c013489981fc4f4b9ee08d28123,
shared/ui/unity/F6Widgets.cs, MIT; SHA256
5CB1C7CA3099441D5976E5CB0A73EC0E273EECECA482DF1D500179FF2CE8F2E1.
Link uses help-size accent text, transparent states and an underline; existing Press owns focus/input.
This supersedes the earlier pin for this file only. No build, suite, game,
report submission, URL change, owner install or rendered acceptance accompanies it.

## October17 shared binding presentation

SettingsPresentation.cs is byte-exact from toolkit
9ae7bbe9e9be9732f9207d5aeca76d50264dc902, shared/ui/SettingsPresentation.cs,
MIT; SHA256 EEBCEE5D17293FD9D218CA4DAD087046D1960E0937E92429E78C840EA751FDFC.
This supersedes the old pin for this file only. BindingPresentation adds common
labels, capability-dependent guidance and retained binding Save/draft/next-start
footer text. Art uses its Footer/SaveBindings/BindFrom surface; the existing
BindingSaveStatus lifetime and reader/writer/access policies remain unchanged.
The shared source and Art adoption are source-only pending affected checks.
