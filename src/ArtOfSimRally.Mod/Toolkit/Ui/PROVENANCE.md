# Shared control display names

`SettingsPresentation.cs` is vendored unchanged from toolkit `d07228104ab62ddf903f42912206be679e0a6b01`,
`shared/ui/SettingsPresentation.cs`, under its MIT license. It supplies `Dbce.Wheel.Ui.ControlNames`; it performs no
device I/O or persistence. Toto's panel widgets/styles are a separate UI source pin and must not duplicate this class.

## Shared F6 rows

- `F6Style.cs`: dbce-wheel-mod-toolkit commit e9a839efe212df9fe89b60e3ef15c68a6dc016d8, shared/ui/F6Style.cs (byte-exact, git blob 9fdf61860d42d6f912e9429e6602e8cab1dc039d), SHA-256 02AA4E76635052C7B42F0DDC3FA0824766CC581BEA928D0245AE23F76E9C94CA.
- `F6Widgets.cs`: dbce-wheel-mod-toolkit commit e9a839efe212df9fe89b60e3ef15c68a6dc016d8, shared/ui/unity/F6Widgets.cs (byte-exact, git blob f13cdca71fc92fd90487eb05ba20cc74530f29f7), SHA-256 7B31439F5AB80E2FDECF6B01C8D31B3CF8429E12926D67FF55A696778A02145C.

MIT. The F6 panel palette, row geometry and the Unity IMGUI (GUILayout) row widgets used by SettingsPanel. They draw and
report edits only; they read no device and write no file. Do not edit here: change the toolkit and re-vendor.
