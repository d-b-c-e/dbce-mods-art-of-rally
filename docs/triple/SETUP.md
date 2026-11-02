# Setup guide

Start with the [install steps](../README.md#install). This mod works without Triple Screen Optimizer or art of sim rally. It does require Unity Mod Manager (UMM) installed for art of rally.

## Check the installation

Launch the game through Steam, press **Ctrl+F10**, and find **DBCE triple-screen for art of rally 0.3.11** in UMM. The game folder should contain `Mods/DbceTripleScreenArtOfRally/Info.json` and three DLLs, with no extra `Mods/Mods` level. The mod starts **Off** on a fresh install.

If automatic game detection fails, Steam's **Manage → Browse local files** opens the folder containing `artofrally.exe`. Run this from the extracted ZIP, replacing the example path:

```powershell
.\Install.bat -GameDir "D:\Games\artofrally"
```

Drag-and-drop installation through UMM's Mods tab is also supported. Updating by either method preserves `Settings.xml` and any local `desired-layout.json`. Keep the extracted download if you want to run `Uninstall.bat` later. The uninstaller removes only this mod's DLLs and metadata, leaving settings, other mods, and UMM in place.

## Measure your rig

Open **Advanced setup and diagnostics** in the mod's UMM panel. Enter:

1. The native pixel width and height of **one** monitor, not the total Surround width.
2. The visible physical width and height of one panel in millimetres. Measure the lit image area inside the bezel.
3. Your eye distance from the center of the middle screen in millimetres.
4. Each side screen's angle relative to the center screen plane, in degrees. A straight three-screen row is 0°; inward-angled side screens use positive angles. Enter left and right independently if they differ.

Choose **Use these measurements**. The values shown initially are examples and do nothing until accepted. You can instead use an existing optimizer `desired-layout.json` as a measurement source and leave **Use measurements entered here** off. The mod copies neither the optimizer app nor its UI. The imported layout is optional and is read from `%LOCALAPPDATA%\DBCE\TripleScreen\games\art-of-rally\desired-layout.json` or a staged copy beside the mod.

The checked-in [32-inch example](../examples/triple-32-1440p-1500r.json) now
uses the owner's updated 660 mm eye distance and 70° angle for each side panel.
It is a measurement example, not a new attended runtime validation; measure
your own rig before using it.

The mod uses these measurements for the three off-axis projections. Use the **Field of view** slider for a visual preference after setup; it moves all three projections together so seams stay aligned. **Reset field of view** restores the measured view. It does not change the saved physical measurements.

The upcoming 0.3.12 source candidate adds **Override field of view**. Turn it
off to follow the game's live camera FOV on all three views; turn it on to use
the saved slider value. Existing settings retain slider control by default.
This option is not in the published 0.3.11 ZIP yet.

## Turning triple screens on

Three monitors side by side work with or without NVIDIA Surround. With the game closed, either set Windows to **Extend these displays** with the center screen as primary, or enable a 3-panel-wide Surround output. All three screens need the same native resolution (for three 2560×1440 screens the span is **7680×1440**); bezel-corrected Surround widths are not supported. Then turn on **Triple screens: one window across the monitors** (F6, Cameras).

Without Surround, the game becomes one borderless window across the three monitors, as Assetto Corsa does. The first launch after turning it on may briefly show the game squeezed onto one screen while it switches; later launches start across all three. Menus and the HUD stay on the middle screen. Each screen gets its own off-axis view of your measured rig. A dashboard or other display above the row is ignored.

## Updating, removing, and compatibility

Close the game before installation, updating, removal, or changing Windows display topology. To update, extract a new release to a fresh folder and run its `Install.bat`. To remove, run `Uninstall.bat` with the game closed. Your UMM settings and screen measurements remain for a later reinstall.

This mod was exercised alongside art of sim rally on one rig. It does not install or depend on that mod and does not modify its files. Other camera mods may change the source camera; report the other mod name and version with any conflict. For a bad image, choose **Off** in this mod and restart the game; [troubleshooting](TROUBLESHOOTING.md) has more checks.

## Comparing views (F7)

While driving with triple screens on, **F7** switches between the measured perspective (your screen
sizes and eye-to-glass distance) and the game's own field of view. A label on the center screen says
which is showing. It lasts until the game restarts, never changes saved settings, and uses the same
screen layout and curve setting for both.
