# Weak force on October 13: the base preset differed from the accepted drives

On October 13 the owner reported that Art (and DRIVE) felt weak. The earlier read-only check found Art's saved
force settings, the shared force library, the native and the output-to-command mapping unchanged from the
accepted baselines (hub `artifacts/ffb-both-weak-20261013`). It could not see the wheel base's live state. MOZA
Pit House's session logs record every preset it writes to the R12. They show that the October 13 drives and the
accepted drives ran on different base presets.

Evidence and the read-only script: hub `artifacts/art-weak-ffb-base-20261014` (`README.md`, historical
`timeline.md`). The script hashes every input before and after reading; nothing changed. No game, device call,
setting, install or force output was involved. Revised after the coordinator's review (sana 106938).

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

## What this does and does not establish

- **Observed:** the preset chronology.
- **Not established:** that the base change caused the reports. Car, route, the intended v2 scale and
  expectations also differ between these drives.
- **Not measured:** torque at the wheel.
- **Not verified:** how MOZA combines maximum torque with game FFB strength. If it scales output, the presets give
  0.30 against 0.50. If it only caps peaks, typical commands give 0.40 against 0.50.
- **Software:** the claim that it is unchanged covers only the paths the earlier check compared. Physical
  normalization stays pending with the owner.
- No Art software retune is justified on those paths. The reference/strength choice belongs to the owner
  (STD-031).

## Attended A/B candidate (owner, about 5 minutes; nothing is changed beforehand)

Before: game closed, Pit House open with R12 Motor auto-load **off**. **Arcade Xbox 270** is selected,
unchanged, as the owner's preferred base. Use one car and stage for both drives, the force reference: `Car_M1`,
`Norway_Stage_5_Reverse_Dry_80s`. Drive the same opening section, about 2 minutes each, with effects at their
saved values. Note each drive's start and end time. Record the owner's words per drive: steering weight
(weak / right / too strong), plus crash and landing separately.

1. **A:** F6 → FFB, Steering strength **50** (current).
2. **B:** Steering strength **83**, with nothing else changed. Gain is applied before the clamp, so v2 at 83 equals
   v1 at 50 within 0.4% (50 × 19,166.67 / 11,500 = 83.3).

Afterwards, set Strength back to 50 unless the owner says otherwise. Then run the hub script in fresh mode with
the two drive windows and a new output folder, and require the verdict `clean` for both. Clean needs all of these:
- Pit House's log covers the whole drive, so Pit House must stay running;
- the starting preset is known;
- nothing changed inside the drive.

An `unknown-*` verdict is not a pass.

A preference between A and B shows the preferred steering gain on this base. It does not establish why the
October 13 drives felt weak. What to do with the preference, including whether to revisit STD-031, is for the
owner to decide. DRIVE's matched comparison should use the same unchanged base, confirmed the same way.
