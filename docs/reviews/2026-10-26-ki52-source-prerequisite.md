# KI-52 source reconciliation — October 26, 2026

## Disposition

No runtime correction is proposed without the missing target evidence. KI-52 remains open. This is an engineering handoff, not a playable candidate or acceptance claim.

## Reconciled baseline

- Main HEAD is e2b08f3530310e69bb8136ee4b898277033da90a (0.4.3). The October 24 handoff's older release/install details are superseded by the issue's October 25 baseline.
- Main contains unrelated uncommitted settings, shared-input, packaging and installer changes; these are preserved. This report lives in isolated branch egon/art-ki52-prerequisite-dbc28 based on the release commit. Held branches and installs are untouched.
- MenuCenteringController currently moves only IntroSplashScreen/Text, Main Menu/Race/VerticalLayoutGroup and Main Menu/Race/GameLogo, preserving their original anchored positions for Release. Its output guard requires an exact three-panel Surround/borderless span.
- docs/triple/experiments/006-centered-menu-and-presentation.md records the actual hierarchy evidence for those same three targets. Its acceptance explicitly retained the full-width background; pointer/focus and restoration were inconclusive. This is not evidence for a missing background/canvas target.
- The all-ref history search found the KI-52 registration (acb039b), but no later KI-52 implementation commit. Compared MenuCenteringController across 86 retained worktrees: no differing centering implementation was found (checkout line endings aside). No retained candidate is being promoted.

## Exact prerequisite

Through Sandy's serialized runtime admission, obtain one reproducible affected menu/loading state on the accepted Surround setup: a screenshot identifying the spill, the owning Canvas render mode/scaler/camera, and the exact transform hierarchy and RectTransform anchors, pivot, offsets and size for that element and its parent. Distinguish a UI image from a world-camera backdrop. Existing evidence does not establish which is responsible. Do not repeat renderer discovery or alter all canvases to guess.

Then implement one named reversible target adjustment behind the existing centering guard. Darlene must review any player-facing change. Sandy must validate that affected state, pointer/focus, toggle-off restoration, and accepted Surround/Separate behavior on the exact artifact. No rig slot, install, launch or input action was requested or performed in this reconciliation.

## Alternative for manager allocation

The October 24 Art handoff documents a controller without a usable Rewired identity causing shared-profile refusal (Steam Input's Xbox-labelled controller was observed). RewiredInputIsolation.JoystickView.Identity returns no identity when neither supported extension supplies one. SharedInputStartup retains the refusal reason, and Shim/SharedInputStatus.Lines already presents it for Phase.Refused.

A bounded alternative is clearer F6 refusal text explaining that Art bindings remain for this session because a controller could not be identified, with the documented restart/Steam Input workaround. Keep the actual device/refusal detail; do not claim every unidentified controller is Steam Input. Do not change identity admission, isolation, bindings or shared toolkit code. Reconcile the dirty main settings/input work before implementation; use the existing status presentation checks and Darlene review. This alternative is identified for allocation, not silently substituted for KI-52.

## Standards and validation limits

Read-only ledger comparison: STD-019 remains unchecked; STD-011/012/013/014/016/018 are also unchecked, with existing partial standards unchanged. STD-037/038/039/040 have no adoption rows in this baseline; 0.4.3 separately implements the simplified package behavior. This report adopts no standard and does not amend toolkit source.

Checks: reviewed release HEAD, all-ref matching history, current centering source, known issue, experiment 006, handoff and refusal presentation route. No code changed; no build, package, install, runtime or physical validation claimed. Public pushes and shared Wheelkit changes were not performed.
