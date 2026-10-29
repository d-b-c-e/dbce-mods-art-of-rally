# Save, verify, restore and compare art of rally's per-user state around a clean-install launch (Phase B).
# The game keeps its state per Windows user, not per install folder: PlayerPrefs in HKCU, saves and logs in
# LocalLow (its "cloud" folder is Steam Auto-Cloud), mod logs/runs in LOCALAPPDATA. Steam's userdata cache for
# 550320 is snapshotted for comparison only and never restored.
#   -Save <dir>     export to a new directory and verify the backup bytes against the live state
#   -Compare <dir>  exit 0 only when the live state equals the saved one byte for byte
#   -Restore <dir>  verify the backup, put it back (key replaced, folders mirrored), then compare
#   -SelfTest       prove every path, including refusals, on throwaway state under a new -Root; no game state
# Dot-source it (. .\ArtUserState.ps1) to reuse the functions without running an action.
param(
    [string]$Save,
    [string]$Compare,
    [string]$Restore,
    [switch]$SelfTest,
    [string]$Root = (Join-Path $env:TEMP ('art-user-state-selftest-' + [guid]::NewGuid().ToString('n')))
)
$ErrorActionPreference = 'Stop'

function Get-OwnerState {
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

function Compare-Hashes($expected, $actual, [string]$label) {
    $diffs = [System.Collections.Generic.List[string]]::new()
    $names = @($expected.Keys)
    foreach ($name in $names) {
        if (-not $actual.Contains($name)) { $diffs.Add("$label removed $name") }
        elseif ($actual[$name] -ne $expected[$name]) { $diffs.Add("$label changed $name") }
    }
    foreach ($name in $actual.Keys) { if ($names -notcontains $name) { $diffs.Add("$label added $name") } }
    $diffs
}

function As-Map($object) {
    $map = [ordered]@{}
    if ($object) { foreach ($p in $object.PSObject.Properties) { $map[$p.Name] = $p.Value } }
    $map
}

# A restore target must be one of the state's own folders and no part of its path may be a junction or symlink.
function Assert-SafeTarget($state, [string]$path) {
    $full = [IO.Path]::GetFullPath($path).TrimEnd('\')
    $allowed = @($state.Folders.Values | ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\') })
    if ($allowed -notcontains $full) { throw "Refusing restore target outside the known state folders: $full" }
    Assert-NoReparse $full
}

# No component of the path may be a junction or symlink (an alias could reach the owner's install or state).
function Assert-NoReparse([string]$path) {
    $probe = [IO.Path]::GetFullPath($path).TrimEnd('\')
    while ($probe) {
        if (Test-Path -LiteralPath $probe) {
            if ((Get-Item -LiteralPath $probe -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
                throw "Refusing a path through a junction or symlink: $probe"
            }
        }
        $parent = Split-Path -Parent $probe
        if (-not $parent -or $parent -eq $probe) { break }
        $probe = $parent
    }
}

function Save-State($state, [string]$dir) {
    if (Test-Path -LiteralPath $dir) { throw "$dir already exists; never overwrite a saved state." }
    New-Item -ItemType Directory -Path $dir | Out-Null
    Export-Key $state.Key (Join-Path $dir 'key.reg')
    $manifest = [ordered]@{ key = $state.Key; keySha256 = (Get-FileHash -LiteralPath (Join-Path $dir 'key.reg') -Algorithm SHA256).Hash
        folders = [ordered]@{}; compareOnly = [ordered]@{} }
    foreach ($name in $state.Folders.Keys) {
        $folder = $state.Folders[$name]
        Assert-SafeTarget $state $folder
        $present = Test-Path -LiteralPath $folder
        if ($present) {
            & robocopy $folder (Join-Path $dir $name) /MIR /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "robocopy failed for $folder ($LASTEXITCODE)" }
        }
        $manifest.folders[$name] = [ordered]@{ path = $folder; present = $present; hashes = Tree-Hashes $folder }
    }
    foreach ($name in $state.CompareOnly.Keys) {
        $manifest.compareOnly[$name] = [ordered]@{ path = $state.CompareOnly[$name]; hashes = Tree-Hashes $state.CompareOnly[$name] }
    }
    $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $dir 'manifest.json') -Encoding UTF8
    $bad = Test-Backup $state $dir
    if ($bad.Count) { throw "Backup does not match the live state: $($bad -join '; ')" }
    Write-Host "saved $dir"
}

# The saved bytes themselves: every folder copy must equal its manifest and the registry export must be readable.
function Test-Backup($state, [string]$dir) {
    $diffs = [System.Collections.Generic.List[string]]::new()
    $manifestFile = Join-Path $dir 'manifest.json'
    $keyFile = Join-Path $dir 'key.reg'
    if (-not (Test-Path -LiteralPath $manifestFile) -or -not (Test-Path -LiteralPath $keyFile)) { $diffs.Add('backup incomplete'); return $diffs }
    $manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json
    if ($manifest.key -ne $state.Key) { $diffs.Add("backup is for $($manifest.key)") }
    if ((Get-FileHash -LiteralPath $keyFile -Algorithm SHA256).Hash -ne $manifest.keySha256) { $diffs.Add('registry export changed since save') }
    $keyText = [IO.File]::ReadAllText($keyFile)
    if ($keyText -ne 'ABSENT') {
        # The export's first section must be this key (reg export writes HKEY_CURRENT_USER for HKCU).
        $header = '[' + ($state.Key -replace '^HKCU', 'HKEY_CURRENT_USER') + ']'
        $first = $keyText -split "`r?`n" | Where-Object { $_.StartsWith('[') } | Select-Object -First 1
        if (-not $keyText.StartsWith('Windows Registry Editor') -or $first -ne $header) { $diffs.Add('registry export is not for this key') }
    }
    $names = @($manifest.folders.PSObject.Properties | ForEach-Object Name)
    foreach ($name in $state.Folders.Keys) { if ($names -notcontains $name) { $diffs.Add("backup lacks folder $name") } }
    $compareNames = @($manifest.compareOnly.PSObject.Properties | ForEach-Object Name)
    foreach ($name in $state.CompareOnly.Keys) { if ($compareNames -notcontains $name) { $diffs.Add("backup lacks compare entry $name") } }
    foreach ($entry in $manifest.folders.PSObject.Properties) {
        if (-not $state.Folders.Contains($entry.Name)) { $diffs.Add("unknown folder $($entry.Name)"); continue }
        if ([IO.Path]::GetFullPath($entry.Value.path) -ne [IO.Path]::GetFullPath($state.Folders[$entry.Name])) { $diffs.Add("path changed for $($entry.Name)") }
        $copy = Join-Path $dir $entry.Name
        if ([bool]$entry.Value.present -ne (Test-Path -LiteralPath $copy)) { $diffs.Add("presence differs for $($entry.Name)") }
        $saved = if ($entry.Value.present) { Tree-Hashes $copy } else { [ordered]@{} }
        foreach ($d in (Compare-Hashes (As-Map $entry.Value.hashes) $saved "backup $($entry.Name)")) { $diffs.Add($d) }
    }
    $diffs
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
            foreach ($d in (Compare-Hashes (As-Map $entry.Value.hashes) (Tree-Hashes $entry.Value.path) $entry.Name)) { $diffs.Add($d) }
        }
    }
    $diffs
}

function Restore-State($state, [string]$dir) {
    $bad = Test-Backup $state $dir
    if ($bad.Count) { throw "Refusing restore, the backup is not intact: $($bad -join '; ')" }
    $manifest = Get-Content -LiteralPath (Join-Path $dir 'manifest.json') -Raw | ConvertFrom-Json
    foreach ($entry in $manifest.folders.PSObject.Properties) { Assert-SafeTarget $state $entry.Value.path }
    $saved = Join-Path $dir 'key.reg'
    & reg.exe query $state.Key *> $null
    if ($LASTEXITCODE -eq 0) { & reg.exe delete $state.Key /f *> $null; if ($LASTEXITCODE -ne 0) { throw 'reg delete failed' } }
    if ([IO.File]::ReadAllText($saved) -ne 'ABSENT') {
        & reg.exe import $saved *> $null
        if ($LASTEXITCODE -ne 0) { throw "reg import failed; the saved export is still at $saved" }
    }
    foreach ($entry in $manifest.folders.PSObject.Properties) {
        $folder = $entry.Value.path
        if ($entry.Value.present) {
            & robocopy (Join-Path $dir $entry.Name) $folder /MIR /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
            if ($LASTEXITCODE -ge 8) { throw "robocopy restore failed for $folder ($LASTEXITCODE)" }
        }
        elseif (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
    }
    Write-Host "restored from $dir"
}

# Throwaway state for the self-tests: its own key and folders under a new root.
function New-ThrowawayState([string]$root) {
    $live = Join-Path $root 'live'
    [ordered]@{
        Key = 'HKCU\Software\DBCE-ArtUserStateSelfTest-' + (Split-Path -Leaf $root)
        Folders = [ordered]@{ a = (Join-Path $live 'a'); b = (Join-Path $live 'b') }
        CompareOnly = [ordered]@{}
    }
}

function Invoke-SelfTest([string]$root) {
    if (Test-Path -LiteralPath $root) { throw "Self-test root $root already exists; it is never deleted or reused." }
    New-Item -ItemType Directory -Path $root | Out-Null
    $state = New-ThrowawayState $root
    $live = Join-Path $root 'live'
    $failed = [System.Collections.Generic.List[string]]::new()
    function Expect([bool]$ok, [string]$name) { if ($ok) { Write-Host "PASS $name" } else { Write-Host "FAIL $name"; $failed.Add($name) } }
    function Throws([scriptblock]$block) { try { & $block; $false } catch { $true } }
    try {
        & reg.exe add $state.Key /v keep /t REG_SZ /d one /f *> $null
        & reg.exe add $state.Key /v change /t REG_DWORD /d 1 /f *> $null
        New-Item -ItemType Directory -Force (Join-Path $live 'a\sub') | Out-Null
        Set-Content (Join-Path $live 'a\keep.txt') 'keep'; Set-Content (Join-Path $live 'a\change.txt') 'v1'; Set-Content (Join-Path $live 'a\sub\gone.txt') 'x'
        $saved = Join-Path $root 'saved'
        Save-State $state $saved
        Expect ((Compare-State $state $saved).Count -eq 0) 'unchanged state equals backup'
        Expect ((Test-Backup $state $saved).Count -eq 0) 'backup bytes verified'
        Expect (Throws { Save-State $state $saved }) 'existing backup is never overwritten'

        & reg.exe add $state.Key /v change /t REG_DWORD /d 2 /f *> $null
        & reg.exe add $state.Key /v added /t REG_SZ /d new /f *> $null
        & reg.exe delete $state.Key /v keep /f *> $null
        Set-Content (Join-Path $live 'a\change.txt') 'v2'; Remove-Item (Join-Path $live 'a\sub\gone.txt'); Set-Content (Join-Path $live 'a\new.txt') 'n'
        New-Item -ItemType Directory -Force (Join-Path $live 'b') | Out-Null; Set-Content (Join-Path $live 'b\appeared.txt') 'n'
        Expect ((Compare-State $state $saved).Count -ge 4) 'mutations detected'
        Restore-State $state $saved
        Expect ((Compare-State $state $saved).Count -eq 0) 'restore is exact'

        # A damaged backup is refused before anything live is touched.
        $broken = Join-Path $root 'broken'
        Copy-Item -LiteralPath $saved -Destination $broken -Recurse
        Set-Content (Join-Path $broken 'a\keep.txt') 'tampered'
        Set-Content (Join-Path $live 'a\marker.txt') 'live'
        Expect (Throws { Restore-State $state $broken }) 'tampered backup refused'
        Expect (Test-Path (Join-Path $live 'a\marker.txt')) 'refused restore left live state alone'
        Remove-Item (Join-Path $live 'a\marker.txt')

        # A valid-looking registry export for another key is refused, even with its hash re-pinned.
        $otherKey = Join-Path $root 'other-key'
        Copy-Item -LiteralPath $saved -Destination $otherKey -Recurse
        $keyCopy = Join-Path $otherKey 'key.reg'
        [IO.File]::WriteAllText($keyCopy, [IO.File]::ReadAllText($keyCopy).Replace('DBCE-ArtUserStateSelfTest', 'DBCE-SomethingElse'), [Text.Encoding]::Unicode)
        $m = Get-Content (Join-Path $otherKey 'manifest.json') -Raw | ConvertFrom-Json
        $m.keySha256 = (Get-FileHash -LiteralPath $keyCopy -Algorithm SHA256).Hash
        $m | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $otherKey 'manifest.json') -Encoding UTF8
        Expect ((Test-Backup $state $otherKey).Count -gt 0) 'registry export for another key refused'
        Expect (Throws { Restore-State $state $otherKey }) 'restore from another key''s export refused'

        # A registry export edited after save (valid content, wrong hash) is refused.
        $edited = Join-Path $root 'edited-key'
        Copy-Item -LiteralPath $saved -Destination $edited -Recurse
        $keyCopy = Join-Path $edited 'key.reg'
        [IO.File]::WriteAllText($keyCopy, [IO.File]::ReadAllText($keyCopy).Replace('"keep"="one"', '"keep"="two"'), [Text.Encoding]::Unicode)
        Expect ((Test-Backup $state $edited) -contains 'registry export changed since save') 'edited registry export refused'

        # A manifest that drops the expected folders is refused.
        $partial = Join-Path $root 'partial'
        Copy-Item -LiteralPath $saved -Destination $partial -Recurse
        $m = Get-Content (Join-Path $partial 'manifest.json') -Raw | ConvertFrom-Json
        $m.folders = [pscustomobject]@{}
        $m | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $partial 'manifest.json') -Encoding UTF8
        Expect ((Test-Backup $state $partial).Count -ge 2) 'manifest missing expected folders refused'

        # A manifest pointing elsewhere is refused.
        $moved = Join-Path $root 'moved'
        Copy-Item -LiteralPath $saved -Destination $moved -Recurse
        $m = Get-Content (Join-Path $moved 'manifest.json') -Raw | ConvertFrom-Json
        $m.folders.a.path = (Join-Path $root 'elsewhere')
        $m | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $moved 'manifest.json') -Encoding UTF8
        Expect (Throws { Restore-State $state $moved }) 'restore target outside the state folders refused'

        # A junction in a state folder's path is refused.
        $target = Join-Path $root 'junction-target'
        New-Item -ItemType Directory -Path $target | Out-Null
        if (Test-Path -LiteralPath (Join-Path $live 'b')) { Remove-Item -LiteralPath (Join-Path $live 'b') -Recurse -Force }
        New-Item -ItemType Junction -Path (Join-Path $live 'b') -Target $target | Out-Null
        Expect (Throws { Restore-State $state $saved }) 'junction restore target refused'
        Expect (Throws { Save-State $state (Join-Path $root 'saved-junction') }) 'junction save source refused'
        Remove-Item -LiteralPath (Join-Path $live 'b') -Force

        Expect (Throws { Invoke-SelfTest $root }) 'existing self-test root refused'
    }
    finally {
        & reg.exe delete $state.Key /f *> $null
        $leftover = Join-Path $live 'b'
        if (Test-Path -LiteralPath $leftover) { (Get-Item -LiteralPath $leftover -Force).Delete() }
        Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
    }
    $failed
}

if ($MyInvocation.InvocationName -eq '.') { return }

if ($SelfTest) {
    $failed = Invoke-SelfTest $Root
    if ($failed.Count) { Write-Host "SELFTEST FAIL ($($failed.Count))"; exit 1 }
    Write-Host 'SELFTEST PASS'
    exit 0
}

$state = Get-OwnerState
if ($Save) { Save-State $state $Save; exit 0 }
if ($Compare) {
    $diffs = Compare-State $state $Compare
    if ($diffs.Count) { $diffs | ForEach-Object { Write-Host "DIFF $_" }; exit 1 }
    Write-Host 'state equal'; exit 0
}
if ($Restore) {
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'art of rally is running; restore only with the game closed.' }
    Restore-State $state $Restore
    $diffs = Compare-State $state $Restore
    if ($diffs.Count) { $diffs | ForEach-Object { Write-Host "DIFF $_" }; exit 1 }
    Write-Host 'restored state equal'; exit 0
}
Write-Host 'Use -Save, -Compare, -Restore or -SelfTest.'; exit 2
