# Launch-scoped game state. Keep this adapter's paths explicit; never restore arbitrary paths from a tape.
function Get-SessionFiles {
    @{
        customrally = $customRally
        modsettings = Join-Path $GameDir 'Mods/ArtOfSimRally/Settings.xml'
        triplesettings = Join-Path $GameDir 'Mods/ArtOfSimRally/TripleScreen.xml'
    }
}
function Save-SessionEnvironment([string]$Directory) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $state = @{ schema = 2 }
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
        # Older sessions predate triple-setting snapshots. Leave that file alone.
        if ($null -eq $saved -and $entry.Key -eq 'triplesettings' -and $state.schema -ne 2) { continue }
        if ($null -eq $saved) { throw 'Incomplete environment backup.' }
        if ($saved.exists -and (Get-FileHash -LiteralPath (Join-Path $Directory $entry.Key)).Hash -ne $saved.sha256) { throw "Backup changed: $($entry.Key)" }
    }
    $prefs = Join-Path $Directory 'playerprefs.reg'
    if ((Get-FileHash -LiteralPath $prefs).Hash -ne $state.prefsHash) { throw 'Preference backup changed.' }
    $sections = @(Get-Content -LiteralPath $prefs | Where-Object { $_ -match '^\[' })
    if ($sections.Count -ne 1 -or $sections[0] -cne '[HKEY_CURRENT_USER\Software\Funselektor Labs\art of rally]') { throw 'Unexpected preference key.' }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        if ($null -eq $state.($entry.Key)) { continue }
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

# Stage/car preferences belong to the tape; presentation belongs to this rig.
# Import only explicit DWORD display preferences from the verified owner backup.
function Restore-SessionPresentation([string]$Directory) {
    Assert-GameClosed
    $state = Get-Content -LiteralPath (Join-Path $Directory 'state.json') -Raw | ConvertFrom-Json
    $prefs = Join-Path $Directory 'playerprefs.reg'
    if ((Get-FileHash -LiteralPath $prefs).Hash -ne $state.prefsHash) { throw 'Preference backup changed.' }
    $lines = @(Get-Content -LiteralPath $prefs)
    $sections = @($lines | Where-Object { $_ -match '^\[' })
    if ($sections.Count -ne 1 -or $sections[0] -cne '[HKEY_CURRENT_USER\Software\Funselektor Labs\art of rally]') { throw 'Unexpected preference key.' }
    $pattern = '^(Screenmanager |UnitySelectMonitor_|SETTINGS_(RESOLUTION_|FULLSCREEN_|VSYNC_|FRAMERATE_CAP_))'
    $display = @($lines | Where-Object { $_ -match '^"([^"\\]+)"=dword:[0-9a-fA-F]{8}$' -and $Matches[1] -match $pattern })
    $names = @($display | ForEach-Object { ($_ -split '"')[1] })
    $triple = (Get-SessionFiles).triplesettings
    if ($state.triplesettings.exists -and (Get-FileHash -LiteralPath (Join-Path $Directory 'triplesettings')).Hash -ne $state.triplesettings.sha256) { throw 'Triple settings backup changed.' }
    $key = 'HKCU:\Software\Funselektor Labs\art of rally'
    foreach ($name in (Get-Item -LiteralPath $key).GetValueNames()) {
        if ($name -match $pattern -and $name -notin $names) { Remove-ItemProperty -LiteralPath $key -Name $name }
    }
    $presentation = Join-Path $Directory 'presentation.reg'
    @('Windows Registry Editor Version 5.00','',$sections[0]) + $display | Set-Content -LiteralPath $presentation -Encoding Unicode
    & reg import $presentation | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restore display preferences.' }
    if ($null -ne $state.triplesettings) {
        if ($state.triplesettings.exists) { Copy-Item -LiteralPath (Join-Path $Directory 'triplesettings') -Destination $triple -Force }
        elseif (Test-Path -LiteralPath $triple) { Remove-Item -LiteralPath $triple }
    }
}
