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

## Rig check, 2026-11-07 (rc.43, single screen)

Launched through Steam with force feedback muted and the mod's triple view off (2560x1440 window). The UMM log showed
`dbce_ui: Art of Rally Wheel Mod 0.4.4, 14 rows`, the DXGI present hook (Present1 too), drawing on the game's swap
chain and F6 input attached; the core coexisted with another hook already on `dxgi.dll` Present and ResizeBuffers.

- F6 posted to the game window opened the core panel inside the game's frame (window capture by PID): Force
  feedback, Display, Cameras and Help tabs, the pushed values (FFB Off, Strength 50%).
- Down, Down, Right set Strength to 55: Settings.xml changed only `<Strength>50</Strength>` to `55`, same length.
- Up, Up, Right, Right, Down, Right on Cameras set the field of view to 35 with "Saved" shown: Settings.xml changed
  only `<BonnetFOV>33.6</BonnetFOV>` to `35`.
- F6 again closed it; the capture passed `Test-GamePicture -Menu`. WM_CLOSE ended the game in 0.4 s.

Evidence (captures, the before and after Settings.xml, logs) is in
`E:/Source/temp/art-0.4.4-rc.43/launch-20261107-110613`.

**Owner check still open:** the FOV change in the next frame needs a stage in the bonnet view, which posted input
cannot start (the game ignores injected input). With the switch on, drive a stage in the bonnet view, press F6, go to
Cameras and press Right on Field of view: the view should widen a little at once. The wheel D-pad and buttons
are untested for the same reason.

Not covered yet: the Controls, Display, Telemetry and Help rows, the F7 view toast and the camera name. The
measurements of the spike's criteria (focus, FFB, Steam overlay, frame cost) are in [F6-HOST-SPIKE.md](F6-HOST-SPIKE.md)
(UI-6).
