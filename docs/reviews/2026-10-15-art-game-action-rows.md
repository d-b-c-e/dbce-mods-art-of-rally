# Art native action rows: source-only follow-up

Request162688, after camera-row commit8232bbf. Depends on Niru's actual source
commit a0131c33ba5edc9fa569b5c6a3b40e4dbf97005d (native action/accepted-copy
integration from643bbd0). No compilation, test, game, install or config action
during owner UAT162171. Installed rc25/C955 remains unchanged.

## Presentation and authority

The camera USB row is now ArtButtonBinding(c, channel, label, note), using the
same BeginBindingRow(label,value) shell as Controls. Existing BeginCalibration,
CancelAssign, Clear, explicit pending Save binding/Cancel and capture/driving
locks remain the only edit path. No device reader or binding store is added.

Controls > Other game actions lists:

| Action | Channel | Visibility |
| --- | --- | --- |
| Horn | Horn | Legacy only; shared profile keeps its existing row |
| Look back | LookBack | Legacy only; shared profile keeps its existing row |
| Recover car (hold) | RecoverCar | Legacy only; shared profile keeps reset |
| Repair puncture (hold) | RepairPuncture | Art-only in either input mode |
| Restart stage (hold) | QuickRestart | Art-only in either input mode |
| Look left | LookLeft | Art-only in either input mode |
| Look right | LookRight | Art-only in either input mode |
| Look forward | LookForward | Art-only in either input mode |

The first three Art rows are hidden while SharedReadersRequired, so their
common profile actions never get a second editor. Niru's shared host/consumer
retains their behavior and the original shared IDs horn/lookBack/reset. The
five Art-only channels use his accepted-copy path and duplicate-neutral rule.
Select has no UI row. The hold is the game's existing one-second hold, not a
new timer; repair additionally needs a punctured tire and a spare. Game-mode
restrictions remain native.

Per162745, release-before-Bind guidance is once per section. Only the active
listening row appends transient capture status; normal rows show their current
device/button value and any important hold/eligibility note. Keyboard capture,
reserved-key handling, checked saves, defaults and persistence keys are untouched.

## Verification and deferred checks

Source mapping checked against a0131c3's actual Channel enum, binding fields,
IsButtonChannel and SharedArtOnly. git diff --check passed. No build/test ran.

After UAT handback, run the already-planned normal net48 mod compilation and
existing SharedBindingPresentation/CameraTuning fixtures from the8232 handoff.
The native action implementation's own WheelInput/ShimInput/SharedBindings/
ProbeHooks/package/helper/runtime gates remain in Niru's
2026-10-15-native-actions.md; do not repeat them separately just for the row UI.
The changed camera/action rows have no rendered or physical acceptance yet.
Any private integration merge is review preparation, not canonical integration
or permission to package/install. Final artifact source and schema-3 helper must
be qualified together before local delivery.
