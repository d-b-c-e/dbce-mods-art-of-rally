# First curved Art drive, for that run only: -Apply sets CurveRadiusMm (TripleScreen.xml) and DiagnosticLogging
# (Settings.xml) after a hash-verified backup; -Restore puts both files back byte-exact and proves it by hash.
# Recipe: docs/reviews/2026-10-29-curved-drive-recipe.md. -SelfTest runs both against a throwaway copy.
param(
    [switch]$Apply,
    [switch]$Restore,
    [switch]$SelfTest,
    [double]$RadiusMm = 1500,
    [string]$ModDir = 'D:\Program Files (x86)\Steam\steamapps\common\artofrally\Mods\ArtOfSimRally',
    [string]$StateDir = 'E:\Source\temp\art-curve-run'
)
$ErrorActionPreference = 'Stop'
$names = 'TripleScreen.xml', 'Settings.xml'

function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }

function Invoke-Apply([string]$mod, [string]$state, [double]$radius) {
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'art of rally is running; close it first.' }
    $backup = Join-Path $state 'backup'
    if (Test-Path $backup) { throw "A run is already applied (restore it first): $backup" }
    $triple = Join-Path $mod 'TripleScreen.xml'; $settings = Join-Path $mod 'Settings.xml'
    $tripleText = [IO.File]::ReadAllText($triple); $settingsText = [IO.File]::ReadAllText($settings)
    if ($tripleText -match '<CurveRadiusMm>') { throw 'TripleScreen.xml already has CurveRadiusMm; set or clear it in F6 instead.' }
    if ($tripleText -notmatch '<UseLocalLayout>true</UseLocalLayout>') { throw 'Curvature needs the local measurements (UseLocalLayout true).' }
    if ($settingsText -notmatch '<DiagnosticLogging>(true|false)</DiagnosticLogging>') { throw 'Settings.xml has no DiagnosticLogging element.' }
    New-Item -ItemType Directory -Force $backup | Out-Null
    $manifest = [ordered]@{}
    foreach ($name in $names) {
        $source = Join-Path $mod $name
        Copy-Item -LiteralPath $source -Destination (Join-Path $backup $name)
        $manifest[$name] = Hash $source
        if ((Hash (Join-Path $backup $name)) -ne $manifest[$name]) { throw "Backup of $name does not match." }
    }
    $manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $state 'manifest.json') -Encoding UTF8
    $value = $radius.ToString([Globalization.CultureInfo]::InvariantCulture)
    [IO.File]::WriteAllText($triple, $tripleText.Replace('</Settings>', "  <CurveRadiusMm>$value</CurveRadiusMm>`r`n</Settings>"), [Text.UTF8Encoding]::new($true))
    [IO.File]::WriteAllText($settings, ($settingsText -replace '<DiagnosticLogging>false</DiagnosticLogging>', '<DiagnosticLogging>true</DiagnosticLogging>'), [Text.UTF8Encoding]::new($true))
    "Applied: CurveRadiusMm $value, DiagnosticLogging true. Backup $backup"
}

function Invoke-Restore([string]$mod, [string]$state) {
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'art of rally is running; close it first.' }
    $backup = Join-Path $state 'backup'
    $manifest = Get-Content -Raw -LiteralPath (Join-Path $state 'manifest.json') | ConvertFrom-Json
    foreach ($name in $names) {
        if ((Hash (Join-Path $backup $name)) -ne $manifest.$name) { throw "Backup of $name changed since -Apply; nothing restored." }
    }
    foreach ($name in $names) {
        Copy-Item -LiteralPath (Join-Path $backup $name) -Destination (Join-Path $mod $name) -Force
        if ((Hash (Join-Path $mod $name)) -ne $manifest.$name) { throw "Restored $name does not match its backup." }
        "Restored $name $($manifest.$name)"
    }
    # The backup is kept (renamed), never deleted, so a later -Apply starts clean.
    Rename-Item -LiteralPath $state -NewName ((Split-Path $state -Leaf) + '-restored-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}

if ($SelfTest) {
    $root = Join-Path ([IO.Path]::GetTempPath()) ('art-curve-selftest-' + [guid]::NewGuid().ToString('N'))
    $mod = Join-Path $root 'mod'; $state = Join-Path $root 'state'
    New-Item -ItemType Directory -Force $mod | Out-Null
    Copy-Item -LiteralPath (Join-Path $ModDir 'TripleScreen.xml'), (Join-Path $ModDir 'Settings.xml') -Destination $mod
    $original = @{}; foreach ($name in $names) { $original[$name] = Hash (Join-Path $mod $name) }
    Invoke-Apply $mod $state 1500 | Out-Null
    $t = [xml](Get-Content -Raw (Join-Path $mod 'TripleScreen.xml')); $s = [xml](Get-Content -Raw (Join-Path $mod 'Settings.xml'))
    $ok = $t.Settings.CurveRadiusMm -eq '1500' -and $s.Settings.DiagnosticLogging -eq 'true'
    $refused = $false; try { Invoke-Apply $mod $state 1500 | Out-Null } catch { $refused = $true }
    Invoke-Restore $mod $state | Out-Null
    $exact = ($names | Where-Object { (Hash (Join-Path $mod $_)) -ne $original[$_] }).Count -eq 0
    Remove-Item -LiteralPath $root -Recurse -Force
    "applied values $ok, second apply refused $refused, restore byte-exact $exact"
    if (-not ($ok -and $refused -and $exact)) { exit 1 }
    'SELFTEST PASS'; exit 0
}
if ($Apply -eq $Restore) { throw 'Pass exactly one of -Apply or -Restore (or -SelfTest).' }
if ($Apply) { Invoke-Apply $ModDir $StateDir $RadiusMm } else { Invoke-Restore $ModDir $StateDir }
