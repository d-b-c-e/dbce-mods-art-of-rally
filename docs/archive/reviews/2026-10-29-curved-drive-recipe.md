# First curved drive — owner recipe (0.4.4-rc.7, 2026-10-29)

About 10 minutes. Nothing here changes your saved settings for good: the script backs them up and puts them back
byte-exact afterwards.

## Before (game closed)

```powershell
pwsh -NoProfile -File E:\Source\game-modding\dbce-mods-art-of-rally\tools\testing\Set-ArtCurveRun.ps1 -Apply
```

This sets `CurveRadiusMm` 1500 in `TripleScreen.xml` and turns on "Log detail for support" in `Settings.xml`, after a
hash-verified backup in `E:\Source\temp\art-curve-run`. It refuses to run while the game is open.

## Drive

1. Start art of rally as usual and drive any stage in the bonnet or bumper camera.
2. Press **F6** and open **Cameras**. The status should end with **"Curved screens are corrected."** If it says
   **"Curve correction is off: …"**, note the reason; the screens are flat and the rest still works.
3. Drive for about a minute. Look at the seams: a straight edge (fence, road line, horizon) crossing from one screen
   to the next should stay straight on the glass.
4. Press **F7** a few times on a straight. It switches between your measured perspective and the game's field of
   view; the corner notice says which one is showing. Say which one looks right.
5. Optional: try a chase camera. It keeps the game's framing by design.
6. Quit the game normally.

## Send back

- **Phone photo from your seat** of all three screens on a straight, with the curve on. A screenshot of the window
  cannot show seam straightness, because the correction bends lines by design on a flat capture.
- **A screenshot (Win+Shift+S) of the F6 Cameras status line.**
- Anything odd: black or frozen screens, HUD position, stutter, a screen that looks stretched.

The agent reads the rest:
- `D:\Program Files (x86)\Steam\steamapps\common\artofrally\artofrally_Data\Managed\UnityModManager\Log.txt`:
  `Curved view luminance (0 black, 1 white): left …, center …, right …`. Values near 0 mean a black view; similar
  non-zero values on all three mean the views rendered.
- `render-diagnostics.json` (path in the `Presentation:` log line): `curveLuminance` on the last snapshot.

## After (game closed)

```powershell
pwsh -NoProfile -File E:\Source\game-modding\dbce-mods-art-of-rally\tools\testing\Set-ArtCurveRun.ps1 -Restore
```

It checks that the backup is unchanged, copies both files back, proves each by hash and keeps the backup folder
(renamed `art-curve-run-restored-<time>`). To keep the curve, restore first, then set "Screen curve radius" in
F6 → Cameras → your measurements.

Script self-test (throwaway copy, never the live files): `Set-ArtCurveRun.ps1 -SelfTest` → `SELFTEST PASS`.
