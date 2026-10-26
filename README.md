# art of sim rally

## Install

For the **64-bit Windows game**. Tested on the Steam version; other stores are
not confirmed. You need the game and your wheel's Windows driver. Everything else
is in the one download (including the Unity Mod Manager loader). SimHub is optional.

1. **Close the game.** Download **ArtOfSimRally-0.4.3.zip** from the latest
   release's **Assets** section (not GitHub's **Source code** downloads).
2. Right-click the ZIP → **Extract All**. Open the extracted folder and
   double-click **Install.bat**. Wait for the successful verification message.
3. Launch art of rally through Steam and press **F6** to set up your wheel.

The installer finds Steam libraries on other drives, adds Unity Mod Manager if the
game does not have it yet (an existing one is used as it is), and preserves existing
mod settings. If it cannot find your game, see [custom folders](docs/SETUP.md#custom-game-folder).
If you already use Unity Mod Manager, its [Mods-tab installation](docs/SETUP.md#installing-through-umm) also works.

**Updating:** close the game, extract the new ZIP into a fresh folder, and run
its `Install.bat`. No uninstall is needed. **Removing:** close the game and run
`Uninstall.bat` from the extracted download. Your settings and game bindings stay.

## First drive

Pause before assigning controls or selecting devices. Keep the game window
focused while force feedback initializes.

- **Wheel:** in **FFB**, choose **On** and select your real wheel. **Follow steering
  binding** uses a wheel assigned in this mod's Controls page; otherwise choose
  the FFB wheel explicitly. Strength defaults to **50**. Force builds between
  3 and 12 km/h, so test while moving. Advanced has smoothing and direction.
- **Controls:** keep game bindings that work. For a missing axis, centre or
  release it, click **Bind** in Controls, move it through full travel, check the
  preview, then **Save calibration**. Use **Invert** if it reads backwards.
  Wheel buttons can also drive the game's menus (Confirm, Back, Start, arrows) and
  actions such as horn, look back and recover car: bind them on the same page.
- **Separate USB handbrake or pedals:** bind them on their Controls rows.
  Each row can use a different device; leave working game-controlled rows unbound.
  [TSS handbrake steps](docs/SETUP.md#separate-usb-handbrake).
- **Shifter:** expand **shifter bindings** under Controls, enable **Separate
  shifter**, choose its device and mode, then bind its gears or shift buttons.
- **Landing vibration:** on by default at **5%**, independently of steering
  Strength. Existing saved opt-outs/strengths are kept. Requires wheel sine-effect
  support. This setting controls the wheel; tune a ButtKicker in SimHub.

The mod also removes hidden wheel deadzones and gamepad steering smoothing.
Leave **Disable steering limiter on car spawn (legacy)** off and use the game's
own assist settings.

## Optional cameras and telemetry

**Cameras:** use the game's change-view button to reach bonnet and bumper views.
Adjust the active view with the numpad, or rebind the tuning keys in **Cameras**.
Close the panel to use those keys. [Camera setup](docs/SETUP.md#cameras).
If Nexus Camera Mod is loaded, it keeps control and our mounted views are suspended.

**SimHub:** in **Telemetry**, choose **On** while paused. Use the local preset
**127.0.0.1:8000** on the same PC, and select **Forza Horizon 5** in
SimHub with the same UDP port. No extra SimHub helper is required.
[Dashboard and ButtKicker setup](docs/SETUP.md#simhub-and-buttkicker).

## Getting help

For a problem: enable **Log detail for support**, reproduce briefly,
pause, then use **Help → Create support file**. Turn detail off afterward. Ordinary
errors/settings can be collected without detailed logging.

Attach the Desktop `art-of-sim-rally-support-*.txt` to a
[GitHub issue](https://github.com/d-b-c-e/dbce-mods-art-of-rally/issues), with your wheel,
driver, stage/car and what happened. See [troubleshooting](docs/TROUBLESHOOTING.md).

You can also use **F6 > Help > Submit GitHub Issue** while paused. Enter a
title and description, optionally select logs, then choose **Review report**.
Review the preview for private information (redaction is best effort), tick
**I reviewed this report**, and choose **Open reviewed GitHub draft**. Check and
submit the issue on GitHub. If asked, manually attach the reviewed
`support-report.txt` saved in a report folder on your Desktop; attaching uploads
it immediately.

Tested on the owner's **MOZA R12**, with positive reports from **MOZA R5** and
**Thrustmaster T300 RS GT** users. Fanatec/TSS-specific behavior and the full
hardware transition matrix still need confirmation. Known limits and pending
checks are in [KNOWN-ISSUES.md](docs/KNOWN-ISSUES.md).

## Development

Version 0.2.6 added an experimental [constant-force crash kick](docs/CRASH-EFFECTS.md),
off by default at strength **50** with range **0–100**. Landing stays on by
default at strength **5**, range **0–40**. Saved settings are preserved. Crash
feel varies by hardware; a higher nominal setting does not guarantee spare
wheel headroom. Both effects are separate from SimHub telemetry.

Version 0.2.7 improves USB binding recovery and adds optional shift vibration,
off by default. Fresh T300/TSS binding and physical shift feel still need
feedback; see the [0.2.7 release notes](docs/releases/0.2.7.md).

[Build instructions](docs/BUILDING.md) · [Documentation index](docs/README.md) ·
[Roadmap](docs/ROADMAP.md) · [Release procedure](docs/RELEASING.md)

The native driver, managed FFB wrapper/force curve and telemetry encoder come
from **dbce-wheel-mod-toolkit**. The exact release pin and hashes are recorded
in [Local deployment](docs/LOCAL-DEPLOYMENT.md). Developer recording/replay tools
are a separate optional release asset, excluded from the normal player ZIP.

## Licence

MIT. No game or UMM assemblies are redistributed. The native force-feedback
plugin is our own DirectInput implementation.
