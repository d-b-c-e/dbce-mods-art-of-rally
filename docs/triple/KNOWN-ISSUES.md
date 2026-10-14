# Known issues

This register separates observed behavior from unverified modes. Version **0.3.11**, Steam game build **1.5.8b**, UMM **0.27.0** on the owner's rig.

| Issue | Current evidence | Status |
|---|---|---|
| Tearing in NVIDIA Surround | Visible during the attended wide-output drive at 7680×1440, roughly 59 updates/s, Unity VSync request/effective count 1. A stock Surround comparison was started but the mode changed before a conclusive visual baseline. | **Open.** Cause and fix unknown; use separate displays if this is distracting. |
| Colored fringe around vegetation | Reported on all three separate-display views in one stage. It was not obvious in a different Surround stage, so the comparison does not isolate a cause. | **Open, minor.** Capture the same stage/time of day in both modes before changing image effects. |
| Other monitor and UI arrangements | Tested on three matching 2560×1440 displays, center primary, with Unity side indices 1 and 2. Some title/main-menu centering was smoke-tested. | **Not fully qualified.** Mixed resolution/DPI, all menus, pointer focus, replay, and photo mode need attended checks. |
| Separate displays with a dashboard or other extra display | Three racing displays plus a dashboard is a common setup (the owner's rig, 2026-10-13: a 1920×1080 DisplayLink display above the 2560×1440 row). The mod used to read the saved Unity indices (`LeftDisplayIndex`/`RightDisplayIndex`), and on this rig index 2 had become the dashboard. A runtime diagnostic found no way to identify a side display before activation: Unity's `nativeDisplay` is only its index, and `RelativeMouseAt` resolves only the active primary. Unity 2019.4 sorts its display list by virtual-desktop x, y unless the game runs with `-multidisplay` (Unity Manual: Multi-display). | **Fixed.** Runtime proof on this rig (2026-10-13, one muted stage run on a disposable copy, hub `artifacts/art-separate-display-stage-20261013`): Unity 1 was proven on the left display and Unity 3 on the right by their windows. The dashboard (Unity 2) was never activated, and the row showed one continuous scene left to right. The sides now come from the racing row: the equal-size displays directly left and right of the primary. Their Unity indices are predicted from that sort and each side is proven by the window its activation opens before side views render. A mismatch, ambiguity, missing window or `-multidisplay` turns side views off with a named code. The saved index fields are no longer shown or used, and their stored values are untouched. Unity cannot deactivate a display, so if the prediction were ever wrong, one window could stay black until exit. |
| Mode changes while game runs | Unity secondary windows cannot be deactivated until process exit. | **Known behavior.** Close the game before changing Windows topology; restart after changing mod output mode. |

### Fixed in the tested build

- Side-camera vegetation disappearance and object popping were corrected by widening the shared vegetation culling range. The 0.3.8 separate-display drive confirmed stable vegetation.
- The 0.3.10 separate-display drive appeared to keep the side views moving through the finish-line Cinemachine handoff.
- The 0.3.11 Surround viewport path restored the vegetation and lighting that the earlier render-texture compositor lost; the user reported it was much better. This does not resolve Surround tearing.

See [experiment 008](experiments/008-attended-separate-display-check.md) for the version-by-version observations and the limits of the runtime diagnostics.
