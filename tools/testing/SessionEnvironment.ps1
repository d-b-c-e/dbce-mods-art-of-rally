# Launch-scoped game state. Keep this adapter's paths explicit; never restore arbitrary paths from a tape.
function Get-SessionFiles {
    @{
        customrally = $customRally
        modsettings = Join-Path $GameDir 'Mods/ArtOfSimRally/Settings.xml'
    }
}
function Save-SessionEnvironment([string]$Directory) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $state = @{}
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        $exists = Test-Path -LiteralPath $entry.Value -PathType Leaf
        $hash = $null
        if ($exists) {
            Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $Directory $entry.Key)
            $hash = (Get-FileHash -LiteralPath (Join-Path $Directory $entry.Key)).Hash
        }
        $state[$entry.Key] = @{ exists = $exists; sha256 = $hash }
    }
    & reg export 'HKCU\Software\Funselektor Labs\art of rally' (Join-Path $Directory 'playerprefs.reg') /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not snapshot game preferences.' }
    $state['prefsHash'] = (Get-FileHash -LiteralPath (Join-Path $Directory 'playerprefs.reg')).Hash
    $state['prefsNames'] = @((Get-Item -LiteralPath 'HKCU:\Software\Funselektor Labs\art of rally').GetValueNames())
    $state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Directory 'state.json') -Encoding UTF8
}
function Restore-SessionEnvironment([string]$Directory, [switch]$NoReceipt) {
    Assert-GameClosed
    $state = Get-Content -LiteralPath (Join-Path $Directory 'state.json') -Raw | ConvertFrom-Json
    # Validate every backup before touching the live environment.
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        $saved = $state.($entry.Key)
        if ($null -eq $saved) { throw 'Incomplete environment backup.' }
        if ($saved.exists -and (Get-FileHash -LiteralPath (Join-Path $Directory $entry.Key)).Hash -ne $saved.sha256) { throw "Backup changed: $($entry.Key)" }
    }
    $prefs = Join-Path $Directory 'playerprefs.reg'
    if ((Get-FileHash -LiteralPath $prefs).Hash -ne $state.prefsHash) { throw 'Preference backup changed.' }
    $sections = @(Get-Content -LiteralPath $prefs | Where-Object { $_ -match '^\[' })
    if ($sections.Count -ne 1 -or $sections[0] -cne '[HKEY_CURRENT_USER\Software\Funselektor Labs\art of rally]') { throw 'Unexpected preference key.' }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        if ($state.($entry.Key).exists) { Copy-Item -LiteralPath (Join-Path $Directory $entry.Key) -Destination $entry.Value -Force }
        elseif (Test-Path -LiteralPath $entry.Value) { Remove-Item -LiteralPath $entry.Value }
    }
    $key = Get-Item -LiteralPath 'HKCU:\Software\Funselektor Labs\art of rally'
    foreach ($name in $key.GetValueNames()) {
        if ($name -notin $state.prefsNames) { Remove-ItemProperty -LiteralPath 'HKCU:\Software\Funselektor Labs\art of rally' -Name $name }
    }
    & reg import $prefs | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore game preferences; backup retained.' }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        if ($state.($entry.Key).exists -and (Get-FileHash -LiteralPath $entry.Value).Hash -ne $state.($entry.Key).sha256) { throw "Restore verification failed: $($entry.Key)" }
    }
    if (-not $NoReceipt) { Set-Content -LiteralPath (Join-Path $Directory 'restored.txt') -Value ([DateTime]::UtcNow.ToString('o')) }
}
