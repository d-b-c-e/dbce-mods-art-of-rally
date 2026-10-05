# Real filesystem restoration with an isolated registry double; never touches the game or HKCU.
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('art-session-environment-' + [Guid]::NewGuid().ToString('N'))
$GameDir = Join-Path $fixture 'game'
$customRally = Join-Path $fixture 'customrally'
$registryPath = 'HKCU:\Software\Funselektor Labs\art of rally'
$registryExportPath = 'HKCU\Software\Funselektor Labs\art of rally'
$script:values = @{'Screenmanager Resolution Width_h182942802'='00000a00';'SETTINGS_FULLSCREEN_h1761874848'='00000001';'stage'='00000002'}
$script:checks = 0
function Check([bool]$Condition,[string]$Message) { $script:checks++; if (-not $Condition) { throw $Message } }
function Assert-GameClosed {}
function Get-Item([string]$LiteralPath) {
    if ($LiteralPath -eq $registryPath) {
        $item = [pscustomobject]@{}
        $item | Add-Member ScriptMethod GetValueNames { return [string[]]@($script:values.Keys) }
        return $item
    }
    Microsoft.PowerShell.Management\Get-Item -LiteralPath $LiteralPath
}
function Remove-ItemProperty([string]$LiteralPath,[string]$Name) {
    if ($LiteralPath -ne $registryPath) { throw 'Unexpected registry path' }
    $script:values.Remove($Name)
}
function reg([string]$Operation,[string]$Path,[string]$File,[string]$Overwrite) {
    $global:LASTEXITCODE = 0
    if ($Operation -eq 'export') {
        if ($Path -ne $registryExportPath) { throw 'Unexpected export key' }
        @('Windows Registry Editor Version 5.00','','[HKEY_CURRENT_USER\Software\Funselektor Labs\art of rally]') +
            @($script:values.GetEnumerator() | Where-Object { $_.Key -ne 'SETTINGS_FULLSCREEN_h1761874848' } | ForEach-Object { '"' + $_.Key + '"=dword:' + $_.Value }) |
            Set-Content -LiteralPath $File -Encoding Unicode
    } elseif ($Operation -eq 'import') {
        foreach ($line in Get-Content -LiteralPath $Path) {
            if ($line -match '^"([^"]+)"=dword:([0-9a-f]{8})$') { $script:values[$Matches[1]] = $Matches[2] }
        }
    } else { throw 'Unexpected registry operation' }
}
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
function Get-SessionRegistryValue([string]$Name) {
    [pscustomobject]@{name=$Name;type=4;data=[Convert]::ToBase64String([BitConverter]::GetBytes([Convert]::ToUInt32($script:values[$Name],16)))}
}
function Set-SessionRegistryValue($Value) { $script:values[$Value.name]=[BitConverter]::ToUInt32([Convert]::FromBase64String($Value.data),0).ToString('x8') }
New-Item -ItemType Directory -Path (Join-Path $GameDir 'Mods/ArtOfSimRally') -Force | Out-Null
$files = Get-SessionFiles
foreach ($entry in $files.GetEnumerator()) { Set-Content -LiteralPath $entry.Value -Value ('original ' + $entry.Key) }
$backup = Join-Path $fixture 'backup'
Save-SessionEnvironment $backup
$script:values['stage']='00000007'
$script:values['Screenmanager Resolution Width_h182942802']='00000500'
$script:values['SETTINGS_FULLSCREEN_h1761874848']='00000000'
$script:values['UnitySelectMonitor_h17969598']='00000002'
Set-Content -LiteralPath $files.triplesettings -Value 'recorded triple settings'
Restore-SessionPresentation $backup
Check ($script:values['stage'] -eq '00000007') 'Presentation restore changed the recorded scenario'
Check ($script:values['Screenmanager Resolution Width_h182942802'] -eq '00000a00') 'Owner resolution was not preserved'
Check ($script:values['SETTINGS_FULLSCREEN_h1761874848'] -eq '00000001') 'Owner fullscreen mode was not preserved'
Check (-not $script:values.ContainsKey('UnitySelectMonitor_h17969598')) 'Recorded monitor selection leaked'
Check ((Get-Content -LiteralPath $files.triplesettings -Raw).Trim() -eq 'original triplesettings') 'Owner triple settings were not preserved'
foreach ($path in $files.Values) { Set-Content -LiteralPath $path -Value 'changed' }
Restore-SessionEnvironment $backup
foreach ($entry in $files.GetEnumerator()) { Check ((Get-Content -LiteralPath $entry.Value -Raw).Trim() -eq ('original ' + $entry.Key)) ('Restore failed: ' + $entry.Key) }
Check ($script:values['stage'] -eq '00000002') 'Original game preferences were not restored'
Check (Test-Path -LiteralPath (Join-Path $backup 'restored.txt')) 'Restore receipt missing'
# A corrupt complete-value backup must fail before replacing any owner files.
$rawPath=Join-Path $backup 'playerprefs-raw.json'
$rawBytes=[IO.File]::ReadAllBytes($rawPath)
Add-Content -LiteralPath $rawPath -Value 'corrupt'
Set-Content -LiteralPath $files.modsettings -Value 'before raw rejection'
$rejected=$false
try { Restore-SessionEnvironment $backup } catch { $rejected=$true }
Check $rejected 'Corrupt raw preference backup accepted'
Check ((Get-Content -LiteralPath $files.modsettings -Raw).Trim() -eq 'before raw rejection') 'Corrupt raw snapshot partially restored files'
[IO.File]::WriteAllBytes($rawPath,$rawBytes)
# Reject corruption before changing either registry values or owner files.
$script:values['Screenmanager Resolution Width_h182942802']='00000500'
Add-Content -LiteralPath (Join-Path $backup 'triplesettings') -Value 'corrupt'
$rejected = $false
try { Restore-SessionPresentation $backup } catch { $rejected = $true }
Check $rejected 'Corrupt presentation backup accepted'
Check ($script:values['Screenmanager Resolution Width_h182942802'] -eq '00000500') 'Corrupt backup partially changed preferences'
Set-Content -LiteralPath $files.modsettings -Value 'must survive rejection'
$rejected = $false
try { Restore-SessionEnvironment $backup } catch { $rejected = $true }
Check $rejected 'Corrupt environment accepted'
Check ((Get-Content -LiteralPath $files.modsettings -Raw).Trim() -eq 'must survive rejection') 'Corrupt backup partially restored files'
# Legacy snapshots never owned TripleScreen.xml.
$state = Get-Content -LiteralPath (Join-Path $backup 'state.json') -Raw | ConvertFrom-Json
$state.PSObject.Properties.Remove('schema'); $state.PSObject.Properties.Remove('triplesettings')
$state.PSObject.Properties.Remove('prefsRawHash')
$state | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $backup 'state.json')
Set-Content -LiteralPath $files.triplesettings -Value 'current owner layout'
Restore-SessionEnvironment $backup
Check ((Get-Content -LiteralPath $files.triplesettings -Raw).Trim() -eq 'current owner layout') 'Legacy snapshot replaced owner layout'
# Absence is a state too; remove only a file created by the scoped session.
Remove-Item -LiteralPath $files.triplesettings
$absent = Join-Path $fixture 'absent'
Save-SessionEnvironment $absent
Set-Content -LiteralPath $files.triplesettings -Value 'created during replay'
Restore-SessionEnvironment $absent
Check (-not (Test-Path -LiteralPath $files.triplesettings)) 'Absent owner layout was not restored'
[ordered]@{status='passed';assertions=$script:checks;fixture=$fixture;realRegistryTouched=$false} | ConvertTo-Json -Compress
