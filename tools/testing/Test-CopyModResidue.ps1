#Requires -Version 7.0
# Offline checks for CopyModResidue.ps1 (sana 132976): only backups created during the run are archived and removed;
# earlier backups and every other file stay byte-identical. A temporary folder only: no game, copy, owner file or lease.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'CopyModResidue.ps1')
$root = Join-Path ([IO.Path]::GetTempPath()) ('art-copy-residue-test-' + [guid]::NewGuid().ToString('N'))
$checks = 0
function Check([bool]$ok, [string]$why) { if (!$ok) { throw "FAILED: $why" }; $script:checks++ }
function Refused([scriptblock]$block, [string]$like) {
    try { & $block *> $null } catch { if ($_.Exception.Message -notlike "*$like*") { throw "Wrong refusal: $($_.Exception.Message) (wanted $like)" }; $script:checks++; return }
    throw "Not refused: $like"
}
function Hash([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }
function Write-Text([string]$p, [string]$text) { New-Item -ItemType Directory -Path (Split-Path $p) -Force | Out-Null; [IO.File]::WriteAllText($p, $text) }
function New-Mod([string]$name) {
    $mod = Join-Path $root "$name/Mods/ArtOfSimRally"
    Write-Text (Join-Path $mod 'dbce-controls.ini') 'profile after restore'
    Write-Text (Join-Path $mod 'Settings.xml') '<Settings />'
    Write-Text (Join-Path $mod 'dbce-controls.ini.bindings-backup-0000000000000000000000000000old1') 'an earlier backup'
    Write-Text (Join-Path $mod 'notes.bindings-backup-not-a-profile') 'other file with a similar name'
    $mod
}
try {
    # One new backup: archived with its exact bytes and removed; the earlier backup and other files are unchanged.
    $mod = New-Mod 'one-new'
    $others = @{}; foreach ($f in Get-ChildItem -LiteralPath $mod -File) { $others[$f.Name] = Hash $f.FullName }
    $before = @(Get-BindingBackups $mod)
    Check ($before.Count -eq 1) 'one backup recorded before the run'
    $new = Join-Path $mod 'dbce-controls.ini.bindings-backup-1111111111111111111111111111new1'
    Write-Text $new 'the profile the Save replaced'; $newHash = Hash $new
    $archive = Join-Path $root 'one-new-archive'
    $moved = Move-NewBindingBackups $mod $before $archive
    Check ($moved.Count -eq 1 -and $moved[0].name -eq [IO.Path]::GetFileName($new) -and $moved[0].sha256 -eq $newHash) 'the new backup is reported with its hash'
    Check (!(Test-Path -LiteralPath $new)) 'the new backup left the mod folder'
    Check ((Hash (Join-Path $archive ([IO.Path]::GetFileName($new)))) -eq $newHash) 'the archived copy has the exact bytes'
    foreach ($name in $others.Keys) { Check ((Hash (Join-Path $mod $name)) -eq $others[$name]) "unchanged: $name" }
    Check (@(Get-ChildItem -LiteralPath $mod -File).Count -eq $others.Count) 'no other file added or removed'

    # No new backup: nothing archived, nothing removed.
    $mod = New-Mod 'none-new'
    $before = @(Get-BindingBackups $mod)
    $count = @(Get-ChildItem -LiteralPath $mod -File).Count
    $moved = Move-NewBindingBackups $mod $before (Join-Path $root 'none-new-archive')
    Check ($moved.Count -eq 0) 'nothing reported'
    Check (!(Test-Path -LiteralPath (Join-Path $root 'none-new-archive'))) 'no archive created'
    Check (@(Get-ChildItem -LiteralPath $mod -File).Count -eq $count) 'mod folder unchanged'

    # Two new backups in one run: both archived.
    $mod = New-Mod 'two-new'
    $before = @(Get-BindingBackups $mod)
    Write-Text (Join-Path $mod 'dbce-controls.ini.bindings-backup-2222222222222222222222222222new2') 'second save a'
    Write-Text (Join-Path $mod 'dbce-controls.ini.bindings-backup-3333333333333333333333333333new3') 'second save b'
    $moved = Move-NewBindingBackups $mod $before (Join-Path $root 'two-new-archive')
    Check ($moved.Count -eq 2) 'both new backups archived'
    Check ((Get-BindingBackups $mod).Count -eq 1) 'only the earlier backup remains'

    # An earlier backup changed or vanished during the run: refused, and the new backup is left in place.
    $mod = New-Mod 'old-changed'
    $before = @(Get-BindingBackups $mod)
    $new = Join-Path $mod 'dbce-controls.ini.bindings-backup-4444444444444444444444444444new4'; Write-Text $new 'new'
    Write-Text $before[0].path 'rewritten during the run'
    Refused { Move-NewBindingBackups $mod $before (Join-Path $root 'old-changed-archive') } 'changed or vanished'
    Check (Test-Path -LiteralPath $new) 'refusal leaves the new backup in place'
    $mod = New-Mod 'old-gone'
    $before = @(Get-BindingBackups $mod)
    Remove-Item -LiteralPath $before[0].path
    Refused { Move-NewBindingBackups $mod $before (Join-Path $root 'old-gone-archive') } 'changed or vanished'

    # A first run with no earlier backup at all.
    $mod = Join-Path $root 'fresh/Mods/ArtOfSimRally'; Write-Text (Join-Path $mod 'dbce-controls.ini') 'profile'
    $before = @(Get-BindingBackups $mod)
    Check ($before.Count -eq 0) 'no backup before'
    Write-Text (Join-Path $mod 'dbce-controls.ini.bindings-backup-5555555555555555555555555555new5') 'new'
    $moved = Move-NewBindingBackups $mod $before (Join-Path $root 'fresh-archive')
    Check ($moved.Count -eq 1 -and (Get-BindingBackups $mod).Count -eq 0) 'the only backup was new and is archived'
    Write-Output "Copy mod residue: $checks checks passed."
} finally {
    if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
