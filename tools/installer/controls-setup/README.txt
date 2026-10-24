art of sim rally - optional controls setup
===========================================================

This optional download sets up your wheel controls for the shared-input package of art of rally
without Wheelkit, and, if you want, your triple-screen measurements and the mod's own settings. For controls it changes
the controls profile and the shared-input switch. It writes only what you preview and apply. It does not launch the
game, change the Windows display mode or send wheel forces. If you use Wheelkit, you do not need it.

1. Install Unity Mod Manager and the shared-input package of art of sim rally first, as that package's README
   describes (its Install.bat), and close the game.
2. Extract this download into its own folder, anywhere outside the game folder.
3. Double-click "Set Up Controls.bat". It finds art of rally the same way the mod installer does and opens the
   helper for art of rally only. If it cannot find the game, run:
   Set Up Controls.bat -GameDir "D:\Games\artofrally"
4. Bind your wheel, pedals and buttons, choose Preview, then Apply.
   Before using the optional Screens or Settings tabs below, start the game once with the mod installed, then close
   it. Those tabs need the mod's saved settings files; if a file is missing, the helper asks you to start the game
   instead of creating it with defaults.
5. Optional: on the Screens tab, choose "Read saved values", change only what you measured (panel size, eye distance,
   left and right screen angle), then Preview and Apply. art of rally uses these only once its triple screens are on
   and "Use measurements entered here" and "Entered measurements accepted" are both on. The helper tells you when they
   are stored but not used yet; saving measurements never changes those switches.
6. Optional: on the Settings tab, choose "Read saved settings", change the rows you want, then Preview and Apply.
   Examples are force feedback, "Triple screens", "Use measurements entered here", "Entered measurements accepted"
   and the panel pixel size. These are the same rows as Wheelkit's Game settings and the mod's own Advanced setup.
   Only the rows you change are written, and the mod applies them at its next start.
7. Start the game normally.

Calibration: the helper does not calibrate axes. Each binding keeps the full range the device reports, and Art's own
Calibrate (F6, Controls) applies only to Art's own bindings, not to this shared profile. If a pedal or handbrake does
not reach its full value, set its travel or range in your wheel or pedal software.

Removing the helper: delete this extracted folder. Its saved profiles and screen setup stay in
%LOCALAPPDATA%\Dbce\StandaloneModSetup; delete that folder too if you no longer want them. The installed mod and its
settings are not affected.

The helper runs from this folder and is never copied into the game folder. It keeps its own profiles in
%LOCALAPPDATA%\Dbce\StandaloneModSetup. Notices and licenses for the helper are in the ModSetup folder.
