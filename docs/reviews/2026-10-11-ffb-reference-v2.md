# Art steering reference v2 (STD-031)

Owner direction: new Steering strength 50 should reproduce the former 30;
finalize this reference before normalizing DRIVE and releasing it privately.

The candidate keeps `GainFromStrength = Strength / 50` and changes the default
front-force reference from 11,500 N to `11500f / .6f` (float value
19166.666015625 N). The existing production curve uses this reference before
speed fade, clamp and smoothing. Curve shape, smoothing default, sign, native
delivery and the independent crash/landing/shift strengths are unchanged.
Multiplying an already clipped output by 0.6 would not reproduce old 30.

New settings and the explicit FFB reset use the new reference. Explicit saved
references and strengths retain their values, including legacy 11,500 N.
Advanced FFB identifies them as customized and offers **Use standard reference**.
That action changes only the reference; the steering slider remains independent.
Owner adoption will deliberately set Strength 50 and the standard reference,
with the existing backup/install procedure. No installed setting was changed by
this source/offline qualification. Do not label a saved legacy tune as v2 merely
because a new DLL is installed.

Recording already reads the actual reference and gain from the loaded Settings
and writes both per force row. Replay retains the recorded values; no historical
capture is interpreted using the new defaults. The production recorder-access
check now verifies both the new default and an explicit legacy30/11500 tune.

## Checks

- Mod Release build: zero warnings/errors.
- Existing regression suite: 478,849 assertions pass, including 14,400 paired
  time steps with both signs, zero/fade speeds, saturation, reversal, release,
  resets, inversion and three smoothing levels. Old30 uses frozen game math;
  new50 uses production Settings and ForceCurve. Float tolerance is 1e-6 and
  native-command quantization differs by at most one unit out of 10,000.
  Existing unrelated nullable warnings remain in the regression project.
- Settings UI policy/serialization: 140 assertions pass. Legacy save/load is
  unchanged; defaults and explicit reset adopt the new calibration.
- Recorder and actual built-mod reflection contract: 75 assertions pass.
- Two sealed original owner recordings replay successfully before paired trials:
  Norway 8,974 rows and owner crash-baseline 2,947 rows. Both begin at zero filter
  history; recorded reset epochs are used. New50 versus old30 maximum differences
  are 1e-7 and 4e-8 respectively, at most one native command unit. All source
  capture files remain hash-identical. This compares steering, not crash effects.
- Independent focused source review: no blocker (romi/art_reference_review).

Private evidence: hub `artifacts/art-reference-v2-20261011` (build/regression/
settings/recorder logs, retained Replay exporter, paired trials, receipt and
`analysis/report.md`). The first comparison manifest used an invalid evidence
enum and was refused; `comparison-v2.json` corrects only the label to the
analyzer's existing `counterfactual` value. Trial outputs were not changed.

Software reference identity: `art-of-rally-50-v2`; toolkit profile follows this
source commit. Historical `art-of-rally-50-v1` and its comparison corpus stay
unchanged. Use a new comparison manifest and regenerate from original inputs;
do not relabel v1 CSV output or scale clipped requests.

## Remaining

Package and closed-game adoption, muted runtime/capture check, and the owner's
attended feel check remain. No new physical output, game launch, installation,
or public release occurred. DRIVE normalization follows this baseline once its
adoption is qualified; the old quick-comparison wrapper still selects v1 until
explicitly updated with the matching v2 corpus.
