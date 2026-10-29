# Art clean-install lane, Phase A: file level only, on a disposable copy. No game launch.
# install -> verify -> uninstall (must equal pristine) -> reinstall -> rollback to a public ZIP -> candidate again.
# Prepare <Root>\game first as a mod-free copy of the game, e.g. robocopy <game> <Root>\game /E /XD <game>\Mods
# <game>\Mods-retired <game>\artofrally_Data\Managed\UnityModManager /XF winhttp.dll doorstop_config.ini
# UnityForceFeedback.dll (the last is the mod's Plugins copy). Result: <Root>\phaseA-result.json.
param(
    [Parameter(Mandatory)][string]$Root,
    [Parameter(Mandatory)][string]$Candidate,
    [Parameter(Mandatory)][string]$Public
)
$ErrorActionPreference = 'Stop'
$game = Join-Path $Root 'game'
$results = [ordered]@{}
$failures = [System.Collections.Generic.List[string]]::new()
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $results[$name] = [ordered]@{ ok = $ok; detail = $detail }
    if (-not $ok) { $failures.Add("$name $detail") }
    Write-Host ("{0} {1} {2}" -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $name, $detail)
}
function Snapshot {
    $map = @{}
    Get-ChildItem -LiteralPath $game -Recurse -File | ForEach-Object {
        $map[$_.FullName.Substring($game.Length + 1)] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
    $map
}
function Compare-Snapshot($a, $b) {
    $added = @($b.Keys | Where-Object { -not $a.ContainsKey($_) })
    $removed = @($a.Keys | Where-Object { -not $b.ContainsKey($_) })
    $changed = @($a.Keys | Where-Object { $b.ContainsKey($_) -and $a[$_] -ne $b[$_] })
    [ordered]@{ added = $added; removed = $removed; changed = $changed }
}
function Extract([string]$zip, [string]$name) {
    $dir = Join-Path $Root "pkg-$name"
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    Expand-Archive -LiteralPath $zip -DestinationPath $dir
    $dir
}
function Run([string]$pkg, [switch]$Uninstall) {
    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', (Join-Path $pkg 'install.ps1'), '-GameDir', $game)
    if ($Uninstall) { $args += '-Uninstall' }
    $out = & powershell @args 2>&1 | Out-String
    [ordered]@{ exit = $LASTEXITCODE; output = $out }
}
function PayloadMatches([string]$pkg) {
    $bad = @(Get-ChildItem -LiteralPath (Join-Path $pkg 'ArtOfSimRally') -File | Where-Object {
        $installed = Join-Path $game ('Mods\ArtOfSimRally\' + $_.Name)
        -not (Test-Path $installed) -or (Get-FileHash $_.FullName).Hash -ne (Get-FileHash $installed).Hash })
    $bad.Count -eq 0
}

if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'art of rally is running; Phase A never runs beside the game.' }
$results['candidateSha256'] = (Get-FileHash $Candidate).Hash
$results['publicSha256'] = (Get-FileHash $Public).Hash
$pristine = Snapshot
Check 'pristine-has-no-loader' (-not (Test-Path (Join-Path $game 'winhttp.dll')) -and -not (Test-Path (Join-Path $game 'Mods'))) "$($pristine.Count) files"

$rc = Extract $Candidate 'rc5'
$pub = Extract $Public 'public043'

$r = Run $rc
Check 'install-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
Check 'install-placed-loader' ((Test-Path (Join-Path $game 'winhttp.dll')) -and (Test-Path (Join-Path $game 'doorstop_config.ini')) -and
    (Test-Path (Join-Path $game 'artofrally_Data\Managed\UnityModManager\UnityModManager.dll')))
Check 'install-payload-matches' (PayloadMatches $rc)
$verify = & powershell -NoProfile -ExecutionPolicy Bypass -Command ". '$(Join-Path $rc 'verify.ps1')'" 2>&1 | Out-String
Check 'verify-script-loads' ($LASTEXITCODE -eq 0) ''
$installed = Snapshot
$d = Compare-Snapshot $pristine $installed
Check 'install-touches-no-game-file' ($d.added.Count -ge 10 -and $d.removed.Count -eq 0 -and $d.changed.Count -eq 0) "added $($d.added.Count)"
$results['installAdded'] = $d.added

$r = Run $rc -Uninstall
Check 'uninstall-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
$after = Snapshot
$d = Compare-Snapshot $pristine $after
Check 'uninstall-restores-pristine' ($d.added.Count -eq 0 -and $d.removed.Count -eq 0 -and $d.changed.Count -eq 0) (
    "added [$($d.added -join ', ')] removed [$($d.removed -join ', ')] changed [$($d.changed -join ', ')]")
$results['uninstallLeftovers'] = $d

$r = Run $rc
Check 'reinstall-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
Check 'reinstall-payload-matches' (PayloadMatches $rc)

$r = Run $pub
Check 'rollback-to-0.4.3-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
Check 'rollback-payload-is-0.4.3' (PayloadMatches $pub)
Check 'rollback-removed-candidate-only-files' (-not (Test-Path (Join-Path $game 'Mods\ArtOfSimRally\dbce_shim.dll')) -or
    (Test-Path (Join-Path $pub 'ArtOfSimRally\dbce_shim.dll'))) 'dbce_shim.dll matches the 0.4.3 payload set'

$r = Run $rc
Check 'forward-to-rc5-exit-0' ($r.exit -eq 0) "exit $($r.exit)"
Check 'forward-payload-is-rc5' (PayloadMatches $rc)

$results['failures'] = $failures
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $Root 'phaseA-result.json') -Encoding UTF8
if ($failures.Count) { Write-Host "Phase A FAIL ($($failures.Count))"; exit 1 }
Write-Host 'Phase A PASS'
