# F6 through the in-game hook (developer switch, UI-4 spike)

Developer-only. Players never see this: without `dbce-dev.ini` the mod behaves exactly as before, and the release ZIP
does not carry `dbce_ui.dll`.

With `[Dev] UiHost=hook` in `Mods\ArtOfSimRally\dbce-dev.ini`, the mod loads `dbce_ui.dll` from the same folder. That
DLL is the toolkit's shared F6 panel core (`native/ui-host`, UI-2), drawn into the game's own frames by its DXGI
present hook, the same way the native games draw F6. The mod hands it the Force feedback and Cameras pages
(`src/ArtOfSimRally.Mod/UiHost/art-hook.settings.json`) and polls changes in its own Update (`UiHost/UiHostHook.cs`).

- **Saving.** Each change sets the existing `Settings` field and writes only that element of `Settings.xml` with
  `SettingsPersistence.WriteElement` (STD-044). The field of view also moves into that camera's offset, as the
  standard F6 does. `tests/UiHost` checks the byte diff for every row.
- **Keys.** F6 opens and closes the core panel. Arrows, Enter or Space, and Esc navigate. F8 keeps the mod's own
  stop path. The UMM window (Ctrl+F10) still shows the standard panel.
- **Wheel.** The wheel's Settings button opens and closes it. While it is open, the menu bindings Up, Down, Left,
  Right, Confirm and Back (Controls > Advanced) go to the panel as navigation.
- **Input block.** While the core panel is open, the mod treats it as settings visible, so the game gets no wheel,
  pedal or menu input and force output stops, the same as the standard F6.
- **Log.** Look for `UiHost=hook` in UMM's `Log.txt`; the core also writes `dbce_ui.log` beside the DLL.

Turn it on or off with the game closed:

```powershell
pwsh tools/testing/Set-UiHostHook.ps1 -On    # copies lib/dbce-ui/dbce_ui.dll and writes dbce-dev.ini
pwsh tools/testing/Set-UiHostHook.ps1 -Off   # removes both: the player default
```

Not covered yet: the Controls, Display, Telemetry and Help rows, the F7 view toast and the camera name. The
measurements of the spike's criteria (focus, FFB, Steam overlay, frame cost) are UI-6.
