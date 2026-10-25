# Unity Mod Manager, bundled (STD-040)

The mod loader art of sim rally runs on. Install.bat puts these files into the game only when the game has no Unity
Mod Manager yet, exactly where Unity Mod Manager's own installer puts them in its "DoorstopProxy" mode:

- `winhttp.dll`, `doorstop_config.ini` -> the game folder (next to `artofrally.exe`)
- `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll`, `Config.xml`, `Params.xml` -> `artofrally_Data\Managed\UnityModManager`

An existing Unity Mod Manager is never changed. Another loader's `winhttp.dll` or `doorstop_config.ini` makes the
installer stop without writing anything.

Source: the official Unity Mod Manager 0.33.0 download (https://www.nexusmods.com/site/mods/21), byte-identical to
the copy its installer put in the owner's game on 2026-08-31. `winhttp.dll` is that download's `winhttp_x64.dll`
(Unity Doorstop, LGPL-2.1; source: https://github.com/NeighTools/UnityDoorstop). `Config.xml` and
`doorstop_config.ini` are the files Unity Mod Manager's installer wrote for art of rally. `Params.xml` is ours: the
manager's window does not open at every start and does not check for updates (players use F6).

Licenses: `licenses/` (Unity Mod Manager MIT, Unity Doorstop LGPL-2.1, Harmony MIT, dnlib MIT).

SHA-256:
- `winhttp.dll` 8C6CDBC38836DEE87E3368F5DE1994D7C0CCEBF29E4CE7ABA3C0981F9375412C
- `doorstop_config.ini` 4F0DE4444B8577E9FF173AAC7E70161ACBC9AACE87BE39181341B8754C102B68
- `UnityModManager.dll` 63E5BAF7B1738E4091B5FD17CCB738ECDB4D1DBF246061DFD55DC52835D52691
- `0Harmony.dll` DF043F05C6E43602FA8E8F38F52A3A2CF962AAB7A65A9D85573EA25F01A63E80
- `dnlib.dll` 54C7968CFBDDA0E9FDDFA0F844D2DBEF3BB3A5A4D2D7462D5413EAC3FD0614CA
- `Config.xml` 8A959494C3821A34F72DEF2AF763E044637AF221E9FC94EACF70AFA89775A49E
- `Params.xml` 7B8491EFA228C4050226D0FA78372DA7529B2686096C3769FCA9B000A5CBB951
