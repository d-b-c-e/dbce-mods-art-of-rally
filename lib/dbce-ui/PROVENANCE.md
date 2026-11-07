# dbce_ui.dll (developer-only F6 host spike, UI-4)

`dbce_ui.dll` is the x64 build of dbce-wheel-mod-toolkit commit 7552afe7e1e35e7539c61381b6ca9516edab0e7d,
`native/ui-host` (`build.ps1`, which also passed its WARP host check on x64 and x86 and the C# wrapper check), built
from a clean worktree of that commit. SHA-256 47DA1C97DDAB2ED87BEFF62F5271B09E02358126353AF59132EA8D2910F428CB. MIT
(`LICENSE`, the toolkit's).

It is the shared F6 panel core drawn inside the game by its own present hook. The mod loads it only with
`[Dev] UiHost=hook` in `dbce-dev.ini` beside the mod (`docs/UI-HOST-HOOK.md`). It is not in the release ZIP:
`tools/package/package.ps1` copies an explicit file list. Do not edit; rebuild in the toolkit and copy again.
