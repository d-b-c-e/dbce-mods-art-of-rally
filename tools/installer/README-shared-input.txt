SHARED WHEELKIT PROFILE
-----------------------
This package can also drive the wheel, pedals, menu buttons, shortcuts and gears
from one shared Wheelkit profile instead of Art's own bindings. It is off unless
you turn it on.

What is extra in this package:
* dbce_shim.dll beside the mod: the shared profile reader.
* A force feedback plugin with three added read functions, so the shared reader can
  use the wheel handle Art already holds. The force model, settings and tuning
  are those of your current release.

To try it, with the game closed:
1. In Wheelkit 0.6.23 or later, select art of rally, review the preview and
   Apply your profile. Wheelkit writes Mods\ArtOfSimRally\dbce-controls.ini, turns
   on "Use shared profile" and sets the mod's gearbox to match
   the profile. Your Art bindings are kept, and the preview lists any control it
   leaves out, with the reason.
   Without Wheelkit you do not need this: bind everything in F6 > Controls.
2. Launch the game; the choice applies at startup. To turn the option on or off
   by hand, press F6, open Controls, change it and restart the game.
3. Stay in the menus for a moment. The Controls page says which of these happened:
   * Attached: the shared profile drives the wheel and its controls.
   * Not used: it refused before attaching (for example no profile, a different
     dbce_shim.dll, or a wheel that did not read neutral), and gives the reason.
     Art's own bindings stay in charge; restart to try again. If it also says the
     game's own input for the wheel is still off, restart the game to restore it.
   * Stopped: it attached, then hit a fault (for example the wheel was unplugged).
     Art's bindings do NOT take over: driving with the wheel stays neutral until
     you restart the game. Keyboard driving continues only while the page says the
     wheel is still isolated; otherwise all driving input stays neutral, while the
     menus still take the keyboard.

EDIT SHARED BINDINGS
In the main menu, press F6, open Controls, then find Shared profile:
1. Choose Bind from: Saved device, or an available device already open to the mod.
2. Click Bind on the action, move only the intended control, then release it.
   Cancel or the countdown timeout ends listening without changing the saved file.
3. Review the unsaved draft. Clear removes a binding; Invert reverses an axis.
4. Choose Save bindings. The current session keeps its active bindings; the row
   shows saved next-start values separately. Restart to use the saved changes.
   Discard removes an unsaved draft; it does not undo a successful Save.

If Save fails, nothing is saved: the active bindings stay in use and the draft
remains available to correct or discard. A missing/read-only profile or unavailable
reader explains why editing is unavailable. A new device may need a restart or
the matching standalone Controls Setup download; Wheelkit is optional.

Art's separate editors for controls owned by the shared profile stay locked;
edit those bindings in Shared profile instead. A control the profile does not map
keeps Art's binding, unless the profile already uses that physical control for
something else. Device and saved binding changes take effect after a restart.

To stop using it: turn the option off and restart, or remove dbce-controls.ini.
To go back to your previous release: first run THIS package's Uninstall.bat (your
settings are kept), then install the previous release's package as usual. Older
release installers do not know about dbce_shim.dll and leave it in place, so do the
uninstall first.

This installer replaces or removes dbce_shim.dll only when it installed exactly
those bytes. A copy you changed or placed by hand is left as it is: the uninstall
keeps it, and installing this package over it stops with a message unless the copy
is byte-identical to this package's. Move such a file aside first.
