SHARED PROFILE
--------------
The mod drives the wheel, pedals, menu buttons, shortcuts and gears from one
shared profile. It looks first for the profile Wheelkit assigns to art of rally,
then for dbce-controls.ini in Mods\ArtOfSimRally. With neither, the mod reads no
wheel controls and the game's own controls apply.

What this package adds for it:
* dbce_shim.dll beside the mod: the shared profile reader.
* A force feedback plugin with three added read functions, so the shared reader can
  use the wheel handle Art already holds. The force model, settings and tuning
  are those of your current release.

1. With the game closed, set up a profile for art of rally in Wheelkit and set the
   mod's gearbox (Controls > Transmission) to match your shifter.
2. Launch the game; the profile is read at startup. Device or profile changes
   need a restart.
3. Stay in the menus for a moment. The Controls page (Ctrl+F10) says which of
   these happened:
   * Attached: the shared profile drives the wheel and its controls.
   * Not used: it refused before attaching (for example no profile, a different
     dbce_shim.dll, or a wheel that did not read neutral), and gives the reason.
     The game's own controls apply; restart to try again. If it also says the
     game's own input for the wheel is still off, restart the game to restore it.
   * Stopped: it attached, then hit a fault (for example the wheel was unplugged).
     Driving with the wheel stays neutral until you restart the game. Keyboard
     driving continues only while the page says the wheel is still isolated;
     otherwise all driving input stays neutral, while the menus still take the
     keyboard.

EDIT SHARED BINDINGS
In the main menu, press Ctrl+F10, open Controls, then find Shared profile:
1. Choose Bind from: Saved device, or an available device already open to the mod.
2. Click Bind on the action, move only the intended control, then release it.
   Cancel or the countdown timeout ends listening without changing the saved file.
3. Review the unsaved draft. Clear removes a binding; Invert reverses an axis.
4. Choose Save bindings. The current session keeps its active bindings; the row
   shows saved next-start values separately. Restart to use the saved changes.
   Discard removes an unsaved draft; it does not undo a successful Save.

If Save fails, nothing is saved: the active bindings stay in use and the draft
remains available to correct or discard. A missing/read-only profile or unavailable
reader explains why editing is unavailable. A new device needs a restart.

To stop using it: remove the game's profile in Wheelkit (or dbce-controls.ini)
and restart; the game's own controls then apply.
To go back to your previous release: first run THIS package's Uninstall.bat (your
settings are kept), then install the previous release's package as usual. Older
release installers do not know about dbce_shim.dll and leave it in place, so do the
uninstall first. Releases before 0.4.6 also had their own wheel bindings in
Settings.xml; this release drops them when it saves, so set them up again there.

This installer replaces or removes dbce_shim.dll only when it installed exactly
those bytes. A copy you changed or placed by hand is left as it is: the uninstall
keeps it, and installing this package over it stops with a message unless the copy
is byte-identical to this package's. Move such a file aside first.
