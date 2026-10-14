# Known issues

This register separates observed behavior from unverified modes. Version **0.3.11**, Steam game build **1.5.8b**, UMM **0.27.0** on the owner's rig.

| Issue | Current evidence | Status |
|---|---|---|
| Tearing in NVIDIA Surround | Visible during the attended wide-output drive at 7680×1440, roughly 59 updates/s, Unity VSync request/effective count 1. A stock Surround comparison was started but the mode changed before a conclusive visual baseline. | **Open.** Cause and fix unknown; use separate displays if this is distracting. |
| Colored fringe around vegetation | Reported on all three separate-display views in one stage. It was not obvious in a different Surround stage, so the comparison does not isolate a cause. | **Open, minor.** Capture the same stage/time of day in both modes before changing image effects. |
| Other monitor and UI arrangements | Tested on three matching 2560×1440 displays, center primary, with Unity side indices 1 and 2. Some title/main-menu centering was smoke-tested. | **Not fully qualified.** Mixed resolution/DPI, all menus, pointer focus, replay, and photo mode need attended checks. |
| Separate displays with a dashboard or other extra display | Three racing displays plus a dashboard is a common setup (the owner's rig, 2026-10-13: a 1920×1080 display above the 2560×1440 row). Independent displays use the saved Unity indices (`LeftDisplayIndex`/`RightDisplayIndex`) and check only that both report the panel's resolution, so a dashboard of another resolution is refused. Unity's display list gives no positions, and its order is not a reliable link to Windows' displays, so the mod cannot tell left from right or a same-size dashboard from a side display. `render-diagnostics.json` now reports `observedRacingRow` (the toolkit's shared selector, `lib/display`: the row Windows shows and the displays outside it) beside `unityDisplays`, as evidence only. | **Open.** The saved Unity indices stay authoritative; check them when a display is added. Swapped sides and a same-size dashboard are not detected. A guard needs a real Unity-index-to-display mapping, not an observed order. |
| Mode changes while game runs | Unity secondary windows cannot be deactivated until process exit. | **Known behavior.** Close the game before changing Windows topology; restart after changing mod output mode. |

### Fixed in the tested build

- Side-camera vegetation disappearance and object popping were corrected by widening the shared vegetation culling range. The 0.3.8 separate-display drive confirmed stable vegetation.
- The 0.3.10 separate-display drive appeared to keep the side views moving through the finish-line Cinemachine handoff.
- The 0.3.11 Surround viewport path restored the vegetation and lighting that the earlier render-texture compositor lost; the user reported it was much better. This does not resolve Surround tearing.

See [experiment 008](experiments/008-attended-separate-display-check.md) for the version-by-version observations and the limits of the runtime diagnostics.
