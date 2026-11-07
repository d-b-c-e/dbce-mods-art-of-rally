<# Developer switch for the UI-4 spike: F6 drawn by the toolkit core (dbce_ui.dll) through the game's own present hook.
   -On copies lib/dbce-ui/dbce_ui.dll into Mods\ArtOfSimRally and writes dbce-dev.ini with [Dev] UiHost=hook.
   -Off removes both, which is the player default. Run with the game closed. docs/UI-HOST-HOOK.md #>
[CmdletBinding()]
param([switch]$On, [switch]$Off, [string]$GameDir = 'D:\Program Files (x86)\Steam\steamapps\common\artofrally')
$ErrorActionPreference = 'Stop'
if ($On -eq $Off) { throw 'Use -On or -Off' }
$modDir = Join-Path $GameDir 'Mods\ArtOfSimRally'
if (-not (Test-Path -LiteralPath (Join-Path $modDir 'ArtOfSimRally.Mod.dll'))) { throw "The mod is not installed in $modDir" }
if (Get-Process -Name 'artofrally' -ErrorAction SilentlyContinue) { throw 'Close the game first; its DLLs are locked while it runs' }
$dll = Join-Path $modDir 'dbce_ui.dll'
$ini = Join-Path $modDir 'dbce-dev.ini'
if ($On) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '..\..\lib\dbce-ui\dbce_ui.dll') -Destination $dll -Force
    # The panel keeps its own [Panel] View, Scale and Page in this file; [Dev] stays as written.
    if (-not (Test-Path -LiteralPath $ini)) { [IO.File]::WriteAllText($ini, "[Dev]`r`nUiHost=hook`r`n") }
    "UiHost=hook on: $dll, $ini"
} else {
    foreach ($path in $dll, $ini) { if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
    'UiHost=hook off: F6 is the standard panel'
}
