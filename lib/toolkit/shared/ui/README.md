# Shared settings presentation, first slice

`SettingsPresentation.cs` is MIT source intended to be vendored into managed consumers. It depends only on the
base library, and may be compiled by Unity Mono and ordinary .NET hosts. It neither opens devices nor writes files.

The first DRIVE/Wheelkit UX pilot shares player-facing axis/button/hat names and explicit numeric edit rules.
Hardware indices in stored profiles remain zero based; buttons and hats shown to players are one based. Numeric
values are finite, clamped and stepped from the minimum. Never normalize a saved value merely while displaying it.

Copy the source with its license into the consumer. This small slice is not the
full mod-published setting descriptor/edit-session contract.

## Recording status

`SessionStatusPresentation.cs` owns recording/playback states, American English
wording, elapsed time and center-display placement. `unity/SessionStatusHud.cs`
is the optional Unity IMGUI renderer; it must not be added to a portable managed
assembly. Vendor the source with this repo's MIT license.
Recording is a developer tool (STD-050); player builds omit it. Native/WPF adapters use the same state contract with their own renderer.

For IL2CPP games whose generated interop lacks managed `GUIStyle` setters,
`unity/StrippedSessionStatusHud.cs` supplies the same presentation/layout with
a host-provided `Action<GUIStyle, int, bool>` style configurator. The host must
pass its verified font-size and wrap setter. This variant avoids `GUI.depth`,
`GUI.skin` and `GUIStyle.CalcHeight`; it preserves matrix, color and enabled
state around drawing. Its fixed minimum height is sized for two wrapped lines;
check the text visually at the host's actual screen resolution.

Game adapters provide real lifecycle state, output mute state and elapsed time,
and show only a stop action that the host actually supports. **Saved means every recording file
was written and closed**, not a key press or signal footer. An error never displays Saved.
`Limited` is a distinct warning for a time/size/row-limited partial capture;
retained partial data does not turn that outcome into Saved.
`DataWritten` means a source was closed but a host-specific companion or case
finalizer has not yet established a complete package. Hosts with a separate
finalizer can replace the active stop hint without changing other consumers.
Render through an always-visible hook, outside the F6/settings window. The
component does not start/stop a recorder, write config, enable forces or register
global input. Public builds that exclude internal recording omit the adapter.
The host controls OnGUI ordering; setting and restoring GUI.depth inside the
renderer does not establish ordering between MonoBehaviour callbacks.

A recording shows REC, a timer and the stop hint through title, menus, loading,
driving and pause, on the center display with triples. No armed session means no
banner. Tests: `dotnet run --project tests/SessionStatus`.

## F6 panel rows

`SharedBindingRowView.Applicability` defaults to `Supported` for existing hosts.
Use `Unsupported` only for actions permanently absent from this adapter's
declaration; the generic panel puts only those rows in its optional unsupported
group. `Note` remains a visible temporary refusal and continues to disable edits
without hiding a supported row. RowState still describes binding/capture/error
state; do not infer applicability from its value or from wording in Note.

Hosts that already filter unsupported actions need no change; others mark
genuinely undeclared actions explicitly. Save/Discard remain reachable even if no
supported row is selectable; their access/capture/draft gates remain.

`F6Style.cs` holds the shared F6 presentation rules. It is engine-free C# 7.3, so it works in Unity 2019 Mono and
in newer hosts:
- the palette, taken from Wheelkit's dark theme (`F6Palette`);
- logical-unit metrics (`F6Metrics`);
- Wheelkit's two-column row geometry with a stacked fallback when narrow (`F6RowLayout`);
- line wrapping for segments and commands (`F6Flow`).

`unity/F6Widgets.cs` is the Unity IMGUI (GUILayout) renderer for Mono hosts (#DRIVE Rally, art of rally):
- tabs, segments and measured commands;
- toggle, number and choice rows;
- a status card;
- Tab / Enter / arrow keyboard focus, with scroll-to-focus inside the body.

Hosts keep their own semantics:
- the window, scrolling, saving and every action;
- their shortcuts and their copy (for example, F8 toggles in DRIVE and stops in art of rally);
- their value grids. A host snaps a proposed value with `NumberEdit` and keeps a saved off-grid value until an
  explicit edit.

Rows show no default, custom or changed badges. "Unsaved" belongs only to binding drafts, which the host compares
with its loaded state. The widgets read no device and write no file. Stripped IL2CPP hosts without GUILayout use
the same `F6Style` rules with their own renderer. Offline checks: `dotnet run --project tests/F6Style`.

### Rectangle binding panel candidate

`SharedBindingPanel.cs` extracts the existing iRacing binding panel's contract-driven
presentation and device selection into a bounded, one-action-at-a-time view.
`unity/RectBindingPanel.cs` adapts its labels/buttons to rectangle IMGUI without
GUILayout, following that consumer's stripped IL2CPP route. Compile with
`DBCE_F6_IL2CPP` and an `interop` alias for IL2CPP; otherwise it uses ordinary Unity
types. The host owns clipping/scrolling, focus/menu eligibility, close confirmation,
cursor restoration and the Unity message bridge. It must preserve GUI state around
the complete drawing callback. The adapter restores enabled state around each button.

The panel consumes `ISharedBindingHost`, `SharedBindingDeviceSelection` and
`SharedBindingUiPolicy`. It never enumerates devices, parses a binding, captures an
input or writes a profile. Save and Discard call the host explicitly. Active,
next-start and draft values remain separate; Return/Close never implies Apply.
The device list contains only the existing host's fresh accepted snapshot rows.
A disconnected explicit choice never silently selects another device.

Delivered actions are direct choices; unsupported reasons are collapsed separately.
Revision/source information is under Details. The caller must pass its persistent
Save status separately from transient capture text to Draw, so another capture
cannot hide a refused Save. Rectangle buttons clear all inherited skin backgrounds
and draw flat palette normal/hover/pressed/disabled/text states with existing GUI primitives.

Help page members (`Heading`, `Link`, `TextInput`, `Toggle`) serve a host's
shared problem report (`shared/support`):
- `Link` is the small underlined repository link (STD-030 October 16), using
  `F6Metrics.LinkHeight`/`LinkUnderline`. It is as wide as its estimated text and
  has no button surface. The host opens the URL only when it returns true.
- `TextInput` is a labeled one-line field or 120-unit area. It uses
  `GUI.TextField`/`GUI.TextArea`, and shows plain text when disabled.
- `Toggle` is a flat square plus label, and returns the new value.
- `Tabs` draws page tabs in the shared tab presentation (as `F6Widgets.Tab`):
  - text is secondary when unselected, and bold primary with the accent line
    when selected;
  - hover shows the tab-focus tint, and a divider follows the row;
  - tabs wrap at tab boundaries.

  It returns the clicked index. The host switches pages, and keeps its title
  and tabs fixed above a scrolled page body.

They use the same cursor, own plain styles and restore `GUI.enabled`/`GUI.color`.
Label, Help, Button and the binding panel are unchanged. Hosts check their own
interop for the IMGUI calls they use, and close text input while the game takes
typed keys.

Missing native snapshot exports disable capture; a UI adapter cannot provide
those exports.

## Shared binding Save status

BindingSaveStatus.cs is renderer-neutral C#7.3 source. One instance belongs to one
binding host. Record the actual checked Save result, including access refusals;
render SaveFailure independently from capture/general messages every frame.
Successful Save and explicit Discard resolve the warning; a new host starts clear.
The optional ISharedBindingSaveStatus companion leaves ISharedBindingHost
compatible. No parser, writer, reader, native/force or file calls are added.
Vendor the helper with its MIT license.


`DBCE_INPUT_NET35` also enables the legacy binding panel/status whitespace checks
and predicate-free row grouping/selection. The modern LINQ path is unchanged.
Some Unity consumer builds expose newer implicit reference assemblies even when
supplying game references: a LINQ predicate can then compile to mscorlib Func,
which old Mono defines only in System.Core. The legacy panel avoids that emitted
reference.
