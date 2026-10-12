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
1. In Wheelkit 0.6.23 or later, select art of rally, review the preview and
   Apply your profile. Wheelkit writes Mods\ArtOfSimRally\dbce-controls.ini, turns
   on "Use shared Wheelkit profile (candidate)" and sets the mod's gearbox to match
   the profile. Your Art bindings are kept, and the preview lists any control it
   leaves out, with the reason.
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

While it is on, Art's editors for controls the profile sets are locked, and device
changes need a restart. A control the profile does not map keeps Art's binding,
unless the profile already uses that physical control for something else.

To stop using it: turn the option off and restart, or remove dbce-controls.ini.
To go back to your previous release: first run THIS package's Uninstall.bat (your
settings are kept), then install the previous release's package as usual. Older
release installers do not know about dbce_shim.dll and leave it in place, so do the
uninstall first.

This installer replaces or removes dbce_shim.dll only when it installed exactly
those bytes. A copy you changed or placed by hand is left as it is: the uninstall
keeps it, and installing this package over it stops with a message unless the copy
is byte-identical to this package's. Move such a file aside first.

Not yet tested by a person: driving with the physical wheel, pedals (clutch
included), H-pattern shifter and keyboard through the shared profile, and force
feedback together with it.
