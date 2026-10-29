# Save, restore and compare art of rally's per-user state around a clean-install launch (Phase B).
# The game keeps its state per Windows user, not per install folder: PlayerPrefs in HKCU, saves and logs in
# LocalLow (its "cloud" folder is Steam Auto-Cloud), mod logs/runs in LOCALAPPDATA. Steam's own userdata cache for
# 550320 is snapshotted for comparison only and never restored by this script.
#   -Save <dir>     export everything to a new directory
#   -Compare <dir>  exit 0 only when the live state equals the saved one byte for byte
#   -Restore <dir>  put the saved state back (registry key replaced, folders mirrored), then compare
#   -SelfTest       prove save/mutate/restore/compare on a throwaway key and folders under -Root; touches no game state
param(
    [string]$Save,
    [string]$Compare,
    [string]$Restore,
    [switch]$SelfTest,
    [string]$Root = (Join-Path $env:TEMP 'art-user-state-selftest')
)
$ErrorActionPreference = 'Stop'

function Default-State {
    $steamUserdata = 'D:\Program Files (x86)\Steam\userdata'
    $cloud = @(Get-ChildItem -LiteralPath $steamUserdata -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName '550320' } | Where-Object { Test-Path -LiteralPath $_ })
    [ordered]@{
        Key = 'HKCU\Software\Funselektor Labs\art of rally'
        Folders = [ordered]@{
            locallow = Join-Path $env:USERPROFILE 'AppData\LocalLow\Funselektor Labs\art of rally'
            modlocal = Join-Path $env:LOCALAPPDATA 'ArtOfSimRally'
            triple = Join-Path $env:LOCALAPPDATA 'DBCE\TripleScreen\games\art-of-rally'
        }
        CompareOnly = [ordered]@{ steamcloud = ($cloud | Select-Object -First 1) }
    }
}

function Export-Key([string]$key, [string]$file) {
    & reg.exe query $key *> $null
    if ($LASTEXITCODE -ne 0) { Set-Content -LiteralPath $file -Value 'ABSENT' -NoNewline; return }
    & reg.exe export $key $file /y *> $null
    if ($LASTEXITCODE -ne 0) { throw "reg export failed for $key" }
}

function Tree-Hashes([string]$folder) {
    $map = [ordered]@{}
    if (-not $folder -or -not (Test-Path -LiteralPath $folder)) { return $map }
    Get-ChildItem -LiteralPath $folder -Recurse -Force -File | Sort-Object FullName | ForEach-Object {
        $map[$_.FullName.Substring($folder.Length).TrimStart('\')] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }
    $map
}

function Save-State($state, [string]$dir) {
    if (Test-Path -LiteralPath $dir) { throw "$dir already exists; never overwrite a saved state." }
    New-Item -ItemType Directory -Path $dir | Out-Null
    Export-Key $state.Key (Join-Path $dir 'key.reg')
    $manifest = [ordered]@{ key = $state.Key; folders = [ordered]@{}; compareOnly = [ordered]@{} }
    foreach ($name in $state.Folders.Keys) {
        $folder = $state.Folders[$name]
        $present = Test-Path -LiteralPath $folder
        if ($present) {
            & robocopy $folder (Join-Path $dir $name) /MIR /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $folder ($LASTEXITCODE)" }
        }
        $manifest.folders[$name] = [ordered]@{ path = $folder; present = $present; hashes = Tree-Hashes $folder }
    }
    foreach ($name in $state.CompareOnly.Keys) {
        $manifest.compareOnly[$name] = [ordered]@{ path = $state.CompareOnly[$name]; hashes = Tree-Hashes $state.CompareOnly[$name] }
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dir 'manifest.json') -Encoding UTF8
    Write-Host "saved $dir"
}

function Compare-State($state, [string]$dir) {
    $manifest = Get-Content -LiteralPath (Join-Path $dir 'manifest.json') -Raw | ConvertFrom-Json
    $diffs = [System.Collections.Generic.List[string]]::new()
    $now = Join-Path $env:TEMP ('art-key-' + [guid]::NewGuid().ToString('n') + '.reg')
    try {
        Export-Key $state.Key $now
        if ([IO.File]::ReadAllText($now) -cne [IO.File]::ReadAllText((Join-Path $dir 'key.reg'))) { $diffs.Add("registry $($state.Key)") }
    }
    finally { Remove-Item -LiteralPath $now -ErrorAction SilentlyContinue }
    foreach ($group in @('folders', 'compareOnly')) {
        foreach ($entry in $manifest.$group.PSObject.Properties) {
            $saved = $entry.Value.hashes.PSObject.Properties
            $live = Tree-Hashes $entry.Value.path
            foreach ($file in $saved) {
                if (-not $live.Contains($file.Name)) { $diffs.Add("$($entry.Name) removed $($file.Name)") }
                elseif ($live[$file.Name] -ne $file.Value) { $diffs.Add("$($entry.Name) changed $($file.Name)") }
            }
            $savedNames = @($saved | ForEach-Object Name)
            foreach ($name in $live.Keys) { if ($savedNames -notcontains $name) { $diffs.Add("$($entry.Name) added $name") } }
        }
    }
    $diffs
}

function Restore-State($state, [string]$dir) {
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'art of rally is running; restore only with the game closed.' }
    $manifest = Get-Content -LiteralPath (Join-Path $dir 'manifest.json') -Raw | ConvertFrom-Json
    $saved = Join-Path $dir 'key.reg'
    & reg.exe query $state.Key *> $null
    if ($LASTEXITCODE -eq 0) { & reg.exe delete $state.Key /f *> $null; if ($LASTEXITCODE -ne 0) { throw "reg delete failed" } }
    if ([IO.File]::ReadAllText($saved) -ne 'ABSENT') {
        & reg.exe import $saved *> $null
        if ($LASTEXITCODE -ne 0) { throw "reg import failed; the saved export is still at $saved" }
    }
    foreach ($entry in $manifest.folders.PSObject.Properties) {
        $folder = $entry.Value.path
        if ($entry.Value.present) {
            & robocopy (Join-Path $dir $entry.Name) $folder /MIR /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "robocopy restore failed for $folder ($LASTEXITCODE)" }
        }
        elseif (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
    }
    Write-Host "restored from $dir"
}

if ($SelfTest) {
    # Throwaway key and folders only: save, mutate every way, restore, and require an exact match.
    if (Test-Path -LiteralPath $Root) { Remove-Item -LiteralPath $Root -Recurse -Force }
    $live = Join-Path $Root 'live'
    $state = [ordered]@{
        Key = 'HKCU\Software\DBCE-ArtUserStateSelfTest'
        Folders = [ordered]@{ a = (Join-Path $live 'a'); b = (Join-Path $live 'b') }
        CompareOnly = [ordered]@{}
    }
    & reg.exe add $state.Key /v keep /t REG_SZ /d one /f *> $null
    & reg.exe add $state.Key /v change /t REG_DWORD /d 1 /f *> $null
    New-Item -ItemType Directory -Force (Join-Path $live 'a\sub') | Out-Null
    Set-Content (Join-Path $live 'a\keep.txt') 'keep'; Set-Content (Join-Path $live 'a\change.txt') 'v1'; Set-Content (Join-Path $live 'a\sub\gone.txt') 'x'
    $failed = 0
    try {
        Save-State $state (Join-Path $Root 'saved')
        if ((Compare-State $state (Join-Path $Root 'saved')).Count) { Write-Host 'FAIL unchanged state differs'; $failed++ }
        & reg.exe add $state.Key /v change /t REG_DWORD /d 2 /f *> $null
        & reg.exe add $state.Key /v added /t REG_SZ /d new /f *> $null
        & reg.exe delete $state.Key /v keep /f *> $null
        Set-Content (Join-Path $live 'a\change.txt') 'v2'; Remove-Item (Join-Path $live 'a\sub\gone.txt'); Set-Content (Join-Path $live 'a\new.txt') 'n'
        New-Item -ItemType Directory -Force (Join-Path $live 'b') | Out-Null; Set-Content (Join-Path $live 'b\appeared.txt') 'n'
        $seen = Compare-State $state (Join-Path $Root 'saved')
        if ($seen.Count -lt 5) { Write-Host "FAIL mutations not all detected: $($seen -join '; ')"; $failed++ }
        Restore-State $state (Join-Path $Root 'saved')
        $after = Compare-State $state (Join-Path $Root 'saved')
        if ($after.Count) { Write-Host "FAIL after restore: $($after -join '; ')"; $failed++ }
    }
    finally {
        & reg.exe delete $state.Key /f *> $null
        Remove-Item -LiteralPath $Root -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($failed) { Write-Host "SELFTEST FAIL ($failed)"; exit 1 }
    Write-Host 'SELFTEST PASS (unchanged equal; 7 mutations detected; restore exact; throwaway state removed)'
    exit 0
}

$state = Default-State
if ($Save) { Save-State $state $Save; exit 0 }
if ($Compare) {
    $diffs = Compare-State $state $Compare
    if ($diffs.Count) { $diffs | ForEach-Object { Write-Host "DIFF $_" }; exit 1 }
    Write-Host 'state equal'; exit 0
}
if ($Restore) {
    Restore-State $state $Restore
    $diffs = Compare-State $state $Restore
    if ($diffs.Count) { $diffs | ForEach-Object { Write-Host "DIFF $_" }; exit 1 }
    Write-Host 'restored state equal'; exit 0
}
Write-Host 'Use -Save, -Compare, -Restore or -SelfTest.'; exit 2
