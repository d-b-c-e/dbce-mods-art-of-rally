# Real filesystem and registry restoration on a scratch key (HKCU\Software\dbce-test-<random>), removed afterwards.
# The game's own key is never read or written.
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('art-session-environment-' + [Guid]::NewGuid().ToString('N'))
$GameDir = Join-Path $fixture 'game'
$customRally = Join-Path $fixture 'customrally'
$scratch = 'Software\dbce-test-' + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$script:checks = 0
function Check([bool]$Condition,[string]$Message) { $script:checks++; if (-not $Condition) { throw $Message } }
function Assert-GameClosed {}
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
$script:PrefsKey = 'HKCU\' + $scratch
$hive = [Microsoft.Win32.Registry]::CurrentUser
function Set-Pref([string]$Name,[int]$Value) { $k = $hive.CreateSubKey($scratch); try { $k.SetValue($Name, $Value, [Microsoft.Win32.RegistryValueKind]::DWord) } finally { $k.Dispose() } }
function Get-Pref([string]$Name) { $k = $hive.OpenSubKey($scratch); try { $k.GetValue($Name) } finally { $k.Dispose() } }
function Test-Pref([string]$Name) { $k = $hive.OpenSubKey($scratch); try { $k.GetValueNames() -contains $Name } finally { $k.Dispose() } }
function Get-Blob { $k = $hive.OpenSubKey($scratch); try { ($k.GetValue('Blob') -join ',') } finally { $k.Dispose() } }
try {
    Set-Pref 'Screenmanager Resolution Width_h182942802' 0xa00
    Set-Pref 'SETTINGS_FULLSCREEN_h1761874848' 1
    Set-Pref 'stage' 2
    $k = $hive.OpenSubKey($scratch, $true); $k.SetValue('Blob', [byte[]](1, 0, 0, 0, 2, 0, 0, 0), [Microsoft.Win32.RegistryValueKind]::Binary); $k.Dispose()
    New-Item -ItemType Directory -Path (Join-Path $GameDir 'Mods/ArtOfSimRally') -Force | Out-Null
    $files = Get-SessionFiles
    foreach ($entry in $files.GetEnumerator()) { Set-Content -LiteralPath $entry.Value -Value ('original ' + $entry.Key) }
    $backup = Join-Path $fixture 'backup'
    Save-SessionEnvironment $backup
    Check (Test-Path -LiteralPath (Join-Path $backup 'playerprefs.json')) 'Registry snapshot missing'
    Check (-not (Test-Path -LiteralPath (Join-Path $backup 'playerprefs.reg'))) 'A reg.exe export was written'
    Set-Pref 'stage' 7
    Set-Pref 'Screenmanager Resolution Width_h182942802' 0x500
    Set-Pref 'SETTINGS_FULLSCREEN_h1761874848' 0
    Set-Pref 'UnitySelectMonitor_h17969598' 2
    Set-Content -LiteralPath $files.triplesettings -Value 'recorded triple settings'
    Restore-SessionPresentation $backup
    Check ((Get-Pref 'stage') -eq 7) 'Presentation restore changed the recorded scenario'
    Check ((Get-Pref 'Screenmanager Resolution Width_h182942802') -eq 0xa00) 'Owner resolution was not preserved'
    Check ((Get-Pref 'SETTINGS_FULLSCREEN_h1761874848') -eq 1) 'Owner fullscreen mode was not preserved'
    Check (-not (Test-Pref 'UnitySelectMonitor_h17969598')) 'Recorded monitor selection leaked'
    Check ((Get-Content -LiteralPath $files.triplesettings -Raw).Trim() -eq 'original triplesettings') 'Owner triple settings were not preserved'
    foreach ($path in $files.Values) { Set-Content -LiteralPath $path -Value 'changed' }
    $k = $hive.OpenSubKey($scratch, $true); $k.SetValue('Blob', [byte[]](9), [Microsoft.Win32.RegistryValueKind]::Binary); $k.Dispose()
    Restore-SessionEnvironment $backup
    foreach ($entry in $files.GetEnumerator()) { Check ((Get-Content -LiteralPath $entry.Value -Raw).Trim() -eq ('original ' + $entry.Key)) ('Restore failed: ' + $entry.Key) }
    Check ((Get-Pref 'stage') -eq 2) 'Original game preferences were not restored'
    Check ((Get-Pref 'SETTINGS_FULLSCREEN_h1761874848') -eq 1) 'Original fullscreen preference was not restored'
    Check (-not (Test-Pref 'UnitySelectMonitor_h17969598')) 'Value added by the run was not removed'
    Check ((Get-Blob) -eq '1,0,0,0,2,0,0,0') 'Binary preference was not restored byte for byte'
    Check (Test-Path -LiteralPath (Join-Path $backup 'restored.txt')) 'Restore receipt missing'
    # A corrupt snapshot must fail before replacing any owner files.
    $snapPath = Join-Path $backup 'playerprefs.json'
    $snapBytes = [IO.File]::ReadAllBytes($snapPath)
    Add-Content -LiteralPath $snapPath -Value 'corrupt'
    Set-Content -LiteralPath $files.modsettings -Value 'before snapshot rejection'
    $rejected = $false
    try { Restore-SessionEnvironment $backup } catch { $rejected = $true }
    Check $rejected 'Corrupt registry snapshot accepted'
    Check ((Get-Content -LiteralPath $files.modsettings -Raw).Trim() -eq 'before snapshot rejection') 'Corrupt snapshot partially restored files'
    [IO.File]::WriteAllBytes($snapPath, $snapBytes)
    # Reject corruption before changing either registry values or owner files.
    Set-Pref 'Screenmanager Resolution Width_h182942802' 0x500
    Add-Content -LiteralPath (Join-Path $backup 'triplesettings') -Value 'corrupt'
    $rejected = $false
    try { Restore-SessionPresentation $backup } catch { $rejected = $true }
    Check $rejected 'Corrupt presentation backup accepted'
    Check ((Get-Pref 'Screenmanager Resolution Width_h182942802') -eq 0x500) 'Corrupt backup partially changed preferences'
    Set-Content -LiteralPath $files.modsettings -Value 'must survive rejection'
    $rejected = $false
    try { Restore-SessionEnvironment $backup } catch { $rejected = $true }
    Check $rejected 'Corrupt environment accepted'
    Check ((Get-Content -LiteralPath $files.modsettings -Raw).Trim() -eq 'must survive rejection') 'Corrupt backup partially restored files'
    # An old-format backup (reg export) is refused, never imported, and touches nothing.
    $state = Get-Content -LiteralPath (Join-Path $backup 'state.json') -Raw | ConvertFrom-Json
    $state.schema = 3
    $state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backup 'state.json')
    $rejected = $false
    try { Restore-SessionEnvironment $backup } catch { $rejected = $true }
    Check $rejected 'Old-format backup accepted'
    Check ((Get-Content -LiteralPath $files.modsettings -Raw).Trim() -eq 'must survive rejection') 'Old-format backup partially restored files'
    # Absence is a state too; remove only a file created by the scoped session.
    Remove-Item -LiteralPath $files.triplesettings
    $absent = Join-Path $fixture 'absent'
    Save-SessionEnvironment $absent
    Set-Content -LiteralPath $files.triplesettings -Value 'created during replay'
    Restore-SessionEnvironment $absent
    Check (-not (Test-Path -LiteralPath $files.triplesettings)) 'Absent owner layout was not restored'
    Check (Test-Path -LiteralPath "HKCU:\$scratch") 'The key itself was deleted'
    [ordered]@{status='passed';assertions=$script:checks;fixture=$fixture;scratchKey=$scratch;realRegistryTouched=$false} | ConvertTo-Json -Compress
}
finally {
    if ($scratch -like 'Software\dbce-test-*') { $hive.DeleteSubKeyTree($scratch, $false) }
    Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue
}
