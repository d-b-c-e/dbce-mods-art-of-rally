# dbce_ui.dll (developer-only F6 host spike, UI-4)

`dbce_ui.dll` is the x64 build of dbce-wheel-mod-toolkit commit 1cafe717d48b5341ec3176d8d7ec62f58e664b44,
`native/ui-host` (`build.ps1`, which also passed its WARP host check on x64 and x86 and the C# wrapper check), built
from a clean worktree of that commit. SHA-256 68DDF97AC953BEC1D9233692E47ECA789F092152870FFE354D6583015EB27A14. MIT
(`LICENSE`, the toolkit's).

It is the shared F6 panel core drawn inside the game by its own present hook. The mod loads it only with
`[Dev] UiHost=hook` in `dbce-dev.ini` beside the mod (`docs/UI-HOST-HOOK.md`). It is not in the release ZIP:
`tools/package/package.ps1` copies an explicit file list. Do not edit; rebuild in the toolkit and copy again.
