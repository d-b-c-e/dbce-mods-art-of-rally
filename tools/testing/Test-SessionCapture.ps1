<# Opt-in integration fixture. Runs real Unity physics for 12 seconds using synthetic controls,
   records the result with hardware/network output muted, then closes and restores the owner environment.
   Replay the resulting tape separately to keep capture and playback results independent. #>
[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]*$')][string]$Seed,
      [Parameter(Mandatory)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]*$')][string]$Name)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$GameDir = 'D:/Program Files (x86)/Steam/steamapps/common/artofrally'
$customRally = Join-Path $env:USERPROFILE 'AppData/LocalLow/Funselektor Labs/Art of Rally/customrally'
function Assert-GameClosed { if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Close art of rally first.' } }
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
Assert-GameClosed
$source = Join-Path $root "results/sessions/$Seed"
$tape = Join-Path $root "results/sessions/$Name"
if (Test-Path -LiteralPath $tape) { throw 'Integration capture already exists.' }
$backup = Join-Path $root ('results/session-integration-' + [Guid]::NewGuid().ToString('N'))
Save-SessionEnvironment $backup
try {
    $prefs = Join-Path $source 'playerprefs-at-start.reg'
    $sections = @(Get-Content -LiteralPath $prefs | Where-Object { $_ -match '^\[' })
    if ($sections.Count -ne 1 -or $sections[0] -cne '[HKEY_CURRENT_USER\Software\Funselektor Labs\art of rally]') { throw 'Unexpected preference key.' }
    & reg import $prefs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not set seed preferences.' }
    if (Test-Path -LiteralPath (Join-Path $source 'customrally-at-start')) { Copy-Item -LiteralPath (Join-Path $source 'customrally-at-start') -Destination $customRally -Force }
    elseif (Test-Path -LiteralPath (Join-Path $source 'customrally-absent-at-start')) { Remove-Item -LiteralPath $customRally -ErrorAction SilentlyContinue }
    & (Join-Path $PSScriptRoot 'Session.ps1') -Record -Name $Name -ScriptedFrom $Seed
    $deadline = [DateTime]::UtcNow.AddMinutes(3)
    while (-not (Test-Path -LiteralPath (Join-Path $tape 'complete.tsv'))) {
        if (Test-Path -LiteralPath (Join-Path $tape 'incomplete.txt')) { throw (Get-Content -LiteralPath (Join-Path $tape 'incomplete.txt') -Raw) }
        if (-not (Get-Process artofrally -ErrorAction SilentlyContinue)) { throw 'Game exited before capture completed.' }
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Synthetic capture timed out.' }
        Start-Sleep -Seconds 1
    }
    Write-Output "Capture complete: $tape"
} finally {
    & (Join-Path $PSScriptRoot 'Session.ps1') -Stop
    $game = Get-Process artofrally -ErrorAction SilentlyContinue
    if ($game) { $null = $game.CloseMainWindow(); $null = $game.WaitForExit(20000) }
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { Write-Warning "Restore pending: $backup" }
    else { Restore-SessionEnvironment $backup }
}
