# Camera adjustment binding rows: source-only UAT follow-up

Request162426, based on643bbd055a2ca3652ccf8ab2ba94710a351fb72c.
Private branch lola/art-camera-binding-rows-20261015. Installed rc25/C955 remains
unchanged during owner UAT162171; no game, install, config write, build or test ran.

## Changes

- Extract the existing Controls BeginRow(label, value,300) plus responsive spacer
  into BeginBindingRow. Controls action rendering and callbacks remain identical.
- Replace Cameras' caption/inline keyboard block and separate legacy USB block
  with two consistent binding rows per action. Labels identify keyboard versus
  USB button; values retain the current key or WheelInput device/button description.
- Each row shows Bind/Cancel and Clear beside its current value. A listening row
  shows its existing capture status. The USB row retains the release-before-Bind
  hint and device-input bar. Existing CalibrationEditor still owns pending
  digital capture, explicit Save binding and cancellation; Bind does not save.
- CameraKeys.Begin/Cancel/Clear, reserved-key validation, key handling and checked
  save implementation are unchanged. Existing Editing/driving guards remain.
- SharedBindingCaptureHost, Shim consumers, action tables and persisted fields
  are untouched. Camera channels already use SharedArtOnly; missing native/shared
  action integration remains Niru's separate claim. No new input or reader path.
- Existing source-linked SharedBindingPresentation stub now names F6RowLayout to
  match the extracted common helper. It does not change test behavior.

## Verification now

Source diff and git diff --check only. No compiler, fixture, runtime, packaging
or owner query was executed. No rendered or physical acceptance is claimed.

## Precise checks after UAT handback

Use a fresh private checkout and the ordinary shared lease/input/reference pins.
No package, recorder or force/performance baseline is required for these source checks.

1. Build src/ArtOfSimRally.Mod/ArtOfSimRally.Mod.csproj in Release with
   ArtShimProbe=false and warnings as errors. This compiles both changed partials
   against actual F6Widgets/F6RowLayout and the existing camera/USB owners.
2. Build tests/SharedBindingPresentation/SharedBindingPresentation.csproj and
   execute the resulting net8.0 DLL. Its existing30 production-linked row checks
   cover unchanged Controls routing, capture locks, unavailable devices and
   failed-Save visibility after common-row extraction.
3. Build tests/CameraTuning/CameraTuning.csproj and execute its existing fixture.
   It covers keyboard conflicts/reserved keys/modifiers, cancellation/timeout,
   clear/reset, checked-save failures and persisted custom mappings.

For both net8.0 fixtures use the exact installed runtime explicitly, e.g.
`dotnet exec --fx-version 8.0.31 <fixture.dll>`, preserving the existing process
roll-forward environment. Do not run the broad Test-Rc or WheelInput baseline
solely for this presentation delta. If a later authorized rendered check occurs,
observe the changed camera disclosure at the actual host width and verify Bind,
listening/Cancel and explicit USB Save visually; source checks do not prove that.
