SHARED WHEELKIT PROFILE (PRIVATE CANDIDATE)
-------------------------------------------
This package can also drive the wheel, pedals, menu buttons, shortcuts and gears
from one shared Wheelkit profile instead of Art's own bindings. It is off unless
you turn it on, and is a candidate for an attended owner test, not a release.

What is extra in this package:
* dbce_shim.dll beside the mod: the shared profile reader.
* A force feedback plugin with three added read functions, so the shared reader can
  use the wheel handle Art already holds. The force model, settings and tuning
  are those of your current release.

To try it, with the game closed:
1. Copy a dbce-controls.ini that Wheelkit applied into Mods\ArtOfSimRally.
   Wheelkit cannot write this file for art of rally yet. The profile it applied for
   DRIVE Rally is at BepInEx\config\dbce-controls.ini in that game's folder.
2. Launch, press F6, open Controls and turn on "Use shared Wheelkit profile
   (candidate)". Restart the game; the choice applies at startup.
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

While it is on, Art's editors for controls the profile sets are locked, and device
changes need a restart. A control the profile does not map keeps Art's binding,
unless the profile already uses that physical control for something else.

To stop using it: turn the option off and restart, or remove dbce-controls.ini.
To go back to your previous release, install that release's package: it replaces
the plugin and removes the dbce_shim.dll this package installed. Your settings are
kept either way. The installer never replaces or removes a dbce_shim.dll it did not
install, including one you changed or placed by hand; move such a file aside first.

Not yet tested by a person: driving with the physical wheel and keyboard through
the shared profile, and force feedback together with it.
