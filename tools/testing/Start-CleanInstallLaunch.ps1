# Clean-install lane, Phase B: one first-run launch of a disposable game copy, with the owner's per-user state
# saved, isolated and restored byte for byte. Attended: F6 setup in the copy needs a person. Run only in a granted
# rig slot (Rig-Lease held by the caller). Prepare the copy with Test-CleanInstallCopy.ps1's mod-free recipe and
# install the candidate into it first.
#
# Steam Cloud: the game's saves are Steam Auto-Cloud. A first run from empty saves while Steam syncs could delete the
# owner's cloud saves, so the run refuses unless the operator states which protection is in place.
param(
    [Parameter(Mandatory)][string]$Copy,
    [Parameter(Mandatory)][string]$StateDir,
    [Parameter(Mandatory)][ValidateSet('SteamExited', 'CloudDisabledForArt')][string]$CloudProtection,
    [string]$Installed = 'D:\Program Files (x86)\Steam\steamapps\common\artofrally',
    [int]$MaxMinutes = 30
)
$ErrorActionPreference = 'Stop'
$state = Join-Path $PSScriptRoot 'ArtUserState.ps1'
$exe = [IO.Path]::GetFullPath((Join-Path $Copy 'artofrally.exe'))
$log = [System.Collections.Generic.List[string]]::new()
function Note([string]$text) { $line = (Get-Date -Format 'HH:mm:ss') + ' ' + $text; $log.Add($line); Write-Host $line }

if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'art of rally is already running.' }
if (-not (Test-Path -LiteralPath $exe)) { throw "No game copy at $Copy." }
if ([IO.Path]::GetFullPath($Copy).TrimEnd('\') -ieq [IO.Path]::GetFullPath($Installed).TrimEnd('\')) { throw 'The copy is the installed game.' }
if ($CloudProtection -eq 'SteamExited' -and (Get-Process steam -ErrorAction SilentlyContinue)) { throw 'Steam is running; exit it or choose CloudDisabledForArt.' }
# A steam_appid.txt beside the exe makes SteamAPI_RestartAppIfNecessary return false (Valve's documented developer
# path), so Steam does not quit the copy and start the installed game instead. The process check below proves it.
Set-Content -LiteralPath (Join-Path $Copy 'steam_appid.txt') -Value '550320' -NoNewline
$installedMod = Join-Path $Installed 'Mods\ArtOfSimRally\ArtOfSimRally.Mod.dll'
$installedHash = (Get-FileHash -LiteralPath $installedMod).Hash

# Save, prove the saved copy is complete, then isolate: an empty user state is what a new player has.
& pwsh -NoProfile -File $state -Save $StateDir; if ($LASTEXITCODE) { throw 'state save failed' }
& pwsh -NoProfile -File $state -Compare $StateDir; if ($LASTEXITCODE) { throw 'saved state does not match live state' }
Note "owner state saved to $StateDir"
& reg.exe delete 'HKCU\Software\Funselektor Labs\art of rally' /f *> $null
foreach ($folder in @((Join-Path $env:USERPROFILE 'AppData\LocalLow\Funselektor Labs\art of rally'),
                      (Join-Path $env:LOCALAPPDATA 'ArtOfSimRally'),
                      (Join-Path $env:LOCALAPPDATA 'DBCE\TripleScreen\games\art-of-rally'))) {
    if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
}
Note 'per-user state isolated (first-run)'

$result = [ordered]@{ copy = $exe; cloudProtection = $CloudProtection; wrongProcess = $false; exited = $false }
try {
    Start-Process -FilePath $exe -WorkingDirectory $Copy | Out-Null
    $deadline = (Get-Date).AddMinutes($MaxMinutes)
    $seenCopy = $false
    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2
        $running = @(Get-Process artofrally -ErrorAction SilentlyContinue)
        foreach ($p in $running) {
            if ($p.Path -and ([IO.Path]::GetFullPath($p.Path) -ine $exe)) {
                Note "WRONG PROCESS $($p.Path): stopping it"; $result.wrongProcess = $true
                Stop-Process -Id $p.Id -Force
            }
            elseif ($p.Path) { $seenCopy = $true }
        }
        if ($result.wrongProcess) { break }
        if ($seenCopy -and $running.Count -eq 0) {
            # A Steam relaunch quits the copy first and starts the installed game a moment later: keep watching.
            $grace = (Get-Date).AddSeconds(20)
            while ((Get-Date) -lt $grace -and -not $result.wrongProcess) {
                Start-Sleep -Seconds 1
                foreach ($p in @(Get-Process artofrally -ErrorAction SilentlyContinue)) {
                    if ($p.Path -and ([IO.Path]::GetFullPath($p.Path) -ine $exe)) {
                        Note "WRONG PROCESS after exit $($p.Path): stopping it"; $result.wrongProcess = $true
                        Stop-Process -Id $p.Id -Force
                    }
                }
            }
            $result.exited = -not $result.wrongProcess
            break
        }
    }
    if (-not $result.exited) {
        Get-Process artofrally -ErrorAction SilentlyContinue | Stop-Process -Force
        Note 'time limit or wrong process: game stopped'
    }
    $copyLog = Join-Path $env:USERPROFILE 'AppData\LocalLow\Funselektor Labs\art of rally\Player.log'
    if (Test-Path -LiteralPath $copyLog) {
        Copy-Item -LiteralPath $copyLog -Destination (Join-Path $StateDir 'copy-run-Player.log')
        $result.modLines = @(Select-String -LiteralPath $copyLog -Pattern '\[ArtOfSimRally\]' | Select-Object -First 40 | ForEach-Object Line)
    }
    $umm = Join-Path $Copy 'artofrally_Data\Managed\UnityModManager\Log.txt'
    if (Test-Path -LiteralPath $umm) { Copy-Item -LiteralPath $umm -Destination (Join-Path $StateDir 'copy-run-umm-Log.txt') }
}
finally {
    Get-Process artofrally -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 2
    & pwsh -NoProfile -File $state -Restore $StateDir
    $result.ownerStateRestored = ($LASTEXITCODE -eq 0)
    $result.installedModUnchanged = ((Get-FileHash -LiteralPath $installedMod).Hash -eq $installedHash)
    Note "owner state restored: $($result.ownerStateRestored); installed mod unchanged: $($result.installedModUnchanged)"
    $result.log = $log
    $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $StateDir 'phaseB-result.json') -Encoding UTF8
}
if ($result.wrongProcess -or -not $result.ownerStateRestored -or -not $result.installedModUnchanged) { exit 1 }
