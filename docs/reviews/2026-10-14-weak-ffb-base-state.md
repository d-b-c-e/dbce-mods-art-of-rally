# Weak force on October 13: the wheel base preset changed

On October 13 the owner reported that Art (and DRIVE) felt weak. The earlier read-only check found Art's saved
force settings, the shared force library, the native and the output-to-command mapping unchanged from the
accepted baselines (hub `artifacts/ffb-both-weak-20261013`). It could not see the wheel base's live state. MOZA
Pit House's session logs record every preset it writes to the R12. They show that the base preset changed between
the accepted drives and the weak-feeling ones.

Evidence and the read-only script: hub `artifacts/art-weak-ffb-base-20261014` (`README.md`, `timeline.md`). The
script hashed every input before and after reading; nothing changed. No game, device call, setting, install or
force output was involved.

## What the base was running

| owner drive or verdict (CDT) | R12 preset written by Pit House | game FFB × max torque |
|---|---|---:|
| Art 0.4.2-rc.3 (v1 reference) drives 10-08 21:03–21:11 → "A bit too strong … make 30% equate to 50%" (STD-031) | DiRT Rally 2.0 Custom, auto-loaded at 15:16 when DiRT Rally 2.0 started | 50 × 100 |
| DRIVE rc1 owner drive 10-11 20:48, broadly positive | DiRT Rally 2.0 Custom (device connect 10:32), 270° | 50 × 100 |
| DRIVE recording `20261013-002928`, slightly weak | Arcade Xbox 270, owner-selected 10-12 16:40 | 40 × 75 |
| Art recording `20261013-004100` (rc.18, v2), weak | Arcade Xbox 270 | 40 × 75 |

Arcade Xbox 270 is the owner's long-standing preset. It was the only R12 preset written from the first log
(10-04) until 10-07 15:57. Starting a game Pit House recognizes auto-loads that game's preset. Between 10-07 and
10-12 this moved the base to FH6 Official, then to DiRT Rally 2.0 Custom (both 50 × 100). Device reconnects
overwrote the owner's own reductions twice. On 10-12 the owner chose Arcade again and turned Motor auto-load off.
No preset write falls between the listed write and each drive. The only owner slider move inside a drive is a
one-second 40→35→40 during the Art recording.

## What follows, and what does not

- **Observed:** the preset chronology. **Not measured:** torque at the wheel.
- **Not verified:** how MOZA combines maximum torque with game FFB strength. If maximum torque scales output, the
  October 13 drives got **0.60×** the base output of the accepted October 8–11 drives (0.30 against 0.50). If it
  only caps peaks, typical forces got **0.80×**. DRIVE requested 1.24–1.35× its rc1 level on October 13. That
  gives about 0.74–0.81× at the wheel under scaling, which matches "slightly weak", and about 1× under capping,
  which does not.
- **STD-031** was decided on drives at 50 × 100. Under scaling, v2 on that base (0.6 × 0.50) gives the same
  output as v1 on Arcade (0.30), the base Art's earlier acceptance most likely used (the logs start 10-04). v2 on
  Arcade is 0.6× that level, and 0.36× the level the owner called "a bit too strong".
- Art's own request level on October 13 was also lower because of the car (la regina against the Norway M1
  reference) and the intended v2 scale (nene's bands). The base change multiplies that.
- No Art code change is justified. The installed software inputs compared are unchanged, and the
  reference/strength choice belongs to the owner. This is not a claim of physical normalization.

## Attended A/B (owner, about 10 minutes; nothing is changed beforehand)

Before: game closed, Pit House open with R12 Motor auto-load **off** and **Arcade Xbox 270** selected. Use one
car and stage for every drive, the force reference: `Car_M1`, `Norway_Stage_5_Reverse_Dry_80s`. Drive the same
opening section, about 2 minutes each, with effects at their saved values. Record the owner's words per drive:
steering weight (weak / right / too strong), plus crash and landing separately.

1. **A — current:** F6 → FFB, Steering strength **50**. Arcade × v2.
2. **B — v1 level:** Steering strength **83**, with nothing else changed. Gain is applied before the clamp, so
   v2 at 83 equals v1 at 50 within 0.4% (50 × 19,166.67 / 11,500 = 83.3). Arcade × v1 is the pre-October feel.
3. **C — optional, tests the base:** set Strength back to **50**. In Pit House select **R12-DiRT Rally 2.0-Custom**
   and set the steering angle to **270°** (the owner's 10-11 setting). This is the October 8–11 base with
   STD-031. Under scaling, C's force level should match B. C also has more mechanical damping/friction (25/25
   against 10/10) and no spring, so compare weight only. Reselect Arcade Xbox 270 afterwards.

Afterwards, set Strength back to 50 unless the owner prefers otherwise. Then run the hub script to confirm no
preset write fell inside the drives.

**Reading the result.** If B feels right and A weak, the October 13 report is the base change compounded by
STD-031. The owner then chooses one of two options:
- amend STD-031, so Art's standard reference returns to 11,500 N and 50 again means B;
- keep v2 and run the base at 50 × 100 for every game.

If A already feels right on M1/Norway, the October 13 report was mostly the car and route. If B and C differ
clearly in weight, maximum torque does not scale the way assumed above. DRIVE's matched comparison should use the
same base state, confirmed the same way.
