<# Tests the optional controls setup companion (package.ps1 -PrivateStandaloneSetup) with a shared-input candidate
   installed by its real Install.bat into an isolated fake game folder, using the same batch entry points players run.
   No game, hardware or owner install is used. Keeps logs and fixtures in results/controls-setup-* for inspection. #>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory,
      [Parameter(Mandatory)][string]$CompanionDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$package = [IO.Path]::GetFullPath($PackageDirectory)
$companion = [IO.Path]::GetFullPath($CompanionDirectory).TrimEnd('\')
. (Join-Path $root 'tools/installer/verify.ps1')
$manifest = Assert-Payload $package
if (-not $manifest.files.'ArtOfSimRally/dbce_shim.dll') { throw 'Use an extracted shared-input candidate package.' }
$run = Join-Path $root ('results/controls-setup-' + [Guid]::NewGuid().ToString('N'))
$game = Join-Path $run 'game [USB] with spaces'
$mod = Join-Path $game 'Mods/ArtOfSimRally'
$checks = 0
$passed = [Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $game -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'artofrally.exe') -Value 'fixture; never executed'
New-Item -ItemType Directory -Path (Join-Path $game 'artofrally_Data/Managed/UnityModManager') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'artofrally_Data/Managed/UnityModManager/UnityModManager.dll') -Value 'fixture; never loaded'

function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; $script:checks++ }
function Pass([string]$Name) { $passed.Add($Name); Write-Host "PASS $Name" }
function Invoke-Batch([string]$Name, [string]$Batch, [string]$Arguments, [bool]$Fails = $false) {
    # Quoted paths exercise %* forwarding; redirected stdin makes pause nonblocking.
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $env:ComSpec
    $start.Arguments = '/d /s /c ""{0}" {1} <nul"' -f $Batch, $Arguments
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(60000)) { $process.Kill(); throw "$Name timed out; see $run" }
    $output = @($stdout.Result, $stderr.Result) -join "`n"; $code = $process.ExitCode; $process.Dispose()
    $output | Out-File -LiteralPath (Join-Path $run ($Name + '.log')) -Encoding utf8
    Assert $(if ($Fails) { $code -ne 0 } else { $code -eq 0 }) "$Name returned $code; see $run"
    return $output
}
function Invoke-Setup([string]$Name, [string]$From, [string]$GameDir, [bool]$Fails = $false) {
    Invoke-Batch $Name (Join-Path $From 'Set Up Controls.bat') ('-GameDir "' + $GameDir + '" -PlanOnly') $Fails
}
function Copy-Companion([string]$Name) {
    $copy = Join-Path $run $Name
    New-Item -ItemType Directory -Path $copy | Out-Null
    Get-ChildItem -LiteralPath $companion | Copy-Item -Destination $copy -Recurse
    return $copy
}
function Update-CompanionManifest([string]$Directory) {
    # Rehash every file, as an attacker or a careless repack would; the pinned bundle check must still refuse.
    $path = Join-Path $Directory 'companion-manifest.json'
    $record = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $files = [ordered]@{}
    foreach ($file in Get-ChildItem -LiteralPath $Directory -File -Recurse | Sort-Object FullName) {
        $relative = $file.FullName.Substring($Directory.Length + 1).Replace('\', '/')
        if ($relative -ne 'companion-manifest.json') { $files[$relative] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
    }
    $record.files = [pscustomobject]$files
    $record | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $path -Encoding UTF8
}
function Invoke-Cli([string]$Name, [string[]]$Arguments, [string]$Stdin = '') {
    # The CLI confirmation is read from standard input; a file is the dependable way to give it from any shell.
    $io = Join-Path $run ('cli-' + $Name); New-Item -ItemType Directory -Path $io | Out-Null
    [IO.File]::WriteAllText((Join-Path $io 'in.txt'), $(if ($Stdin) { $Stdin + "`r`n" } else { '' }), [Text.Encoding]::ASCII)
    $line = ($Arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } }) -join ' '
    $process = Start-Process -FilePath (Join-Path $companion 'ModSetup/Wheelkit.StandaloneSetup.exe') -ArgumentList $line -RedirectStandardInput (Join-Path $io 'in.txt') `
        -RedirectStandardOutput (Join-Path $io 'out.txt') -RedirectStandardError (Join-Path $io 'err.txt') -NoNewWindow -Wait -PassThru
    return @{ code=$process.ExitCode; text=((Get-Content -LiteralPath (Join-Path $io 'out.txt') -Raw) + (Get-Content -LiteralPath (Join-Path $io 'err.txt') -Raw)) }
}

$log = Invoke-Setup 'before-install' $companion $game -Fails $true
Assert ($log -match 'Install art of sim rally first') 'Setup ran before the mod was installed'
$null = Invoke-Batch 'install' (Join-Path $package 'Install.bat') ('-GameDir "' + $game + '"')
$plan = Invoke-Setup 'plan' $companion $game
$expected = 'PLAN ONLY: "' + (Join-Path $companion 'ModSetup\DbceModSetup.exe') + '" --game art-of-rally --game-dir "' + [IO.Path]::GetFullPath($game) + '"'
Assert ($plan.Contains($expected)) "Unexpected helper command: $plan"
Pass 'Set Up Controls verifies the companion and plans the game-locked art of rally helper; it refuses before the mod is installed'

$baseline = Join-Path $run 'baseline game'
Copy-Item -LiteralPath $game -Destination $baseline -Recurse
$baselineBuild = Join-Path $baseline 'Mods/ArtOfSimRally/build.json'
$record = Get-Content -LiteralPath $baselineBuild -Raw | ConvertFrom-Json
$record.PSObject.Properties.Remove('sharedInputCandidate')
$record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $baselineBuild -Encoding UTF8
$log = Invoke-Setup 'baseline' $companion $baseline -Fails $true
Assert ($log -match 'needs the shared-input package') 'Setup accepted a baseline (non-candidate) install'
Pass 'Set Up Controls refuses the baseline package with a clear message'

foreach ($rehash in @($false, $true)) {
    $changed = Copy-Companion ('changed managed dll ' + $rehash)
    Add-Content -LiteralPath (Join-Path $changed 'ModSetup/DbceModSetup.dll') -Value 'changed'
    if ($rehash) { Update-CompanionManifest $changed }
    $log = Invoke-Setup ('changed-dll-' + $rehash) $changed $game -Fails $true
    Assert ($log -match 'incomplete or has changed' -and $log -notmatch 'PLAN ONLY') "Changed managed helper DLL was accepted (rehash $rehash)"
}
$extra = Copy-Companion 'unlisted file'
Set-Content -LiteralPath (Join-Path $extra 'ModSetup/unlisted.dll') -Value 'not in the bundle'
$log = Invoke-Setup 'unlisted' $extra $game -Fails $true
Assert ($log -match 'missing or unexpected files') 'Unlisted companion file was accepted'
Pass 'Set Up Controls refuses a changed managed helper DLL beside an unchanged executable, with or without a rehashed manifest, and an unlisted file'

# Controls only: the helper may write the controls profile and the shared-input opt-in; nothing else changes.
$settings = Join-Path $mod 'Settings.xml'
Set-Content -LiteralPath $settings -Value '<Settings><SharedInput>false</SharedInput><SteerBinding>native-keep</SteerBinding><Strength>15</Strength><LandingEffectsEnabled>false</LandingEffectsEnabled><Unknown>keep</Unknown></Settings>'
$fixed = @{}
foreach ($relative in @($manifest.files.PSObject.Properties.Name | Where-Object { $_.StartsWith('ArtOfSimRally/') })) { $fixed[$relative] = (Get-FileHash -LiteralPath (Join-Path $game ('Mods/' + $relative))).Hash }
$nativeHash = (Get-FileHash -LiteralPath (Join-Path $game 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')).Hash
$profilePath = Join-Path $run 'controls-setup-profile.json'
$binding = @{ InstanceGuid='11111111-1111-1111-1111-111111111111'; ProductGuid='22222222-2222-2222-2222-222222222222'; DeviceName='Fixture wheel'; Kind='Axis'; Index=0; Direction=-1; Rest=32767; Minimum=0; Maximum=65535; Calibrated=$true }
$profile = @{ Schema=2; ActiveId='fixture'; Profiles=@(@{ Id='fixture'; Profile=@{ Schema=1; Name='Companion fixture'; Transmission='Automatic'; Bindings=@{ steer=$binding } } }) }
[IO.File]::WriteAllText($profilePath, ($profile | ConvertTo-Json -Depth 12))
$preview = Invoke-Cli 'preview' @('preview', '--game', 'art-of-rally', '--game-dir', $game, '--profile', $profilePath)
Assert ($preview.code -eq 0 -and $preview.text.Contains('dbce-controls.ini')) "Preview failed: $($preview.text)"
Assert (-not (Test-Path -LiteralPath (Join-Path $mod 'dbce-controls.ini'))) 'Preview wrote the controls profile'
$applied = Invoke-Cli 'apply' @('apply', '--game', 'art-of-rally', '--game-dir', $game, '--profile', $profilePath) 'APPLY'
Assert ($applied.code -eq 0) "Apply failed: $($applied.text)"
Assert ((Get-Content -LiteralPath (Join-Path $mod 'dbce-controls.ini') -Raw).Contains('steer =')) 'Apply did not write the controls profile'
$saved = [xml](Get-Content -LiteralPath $settings -Raw)
Assert ($saved.Settings.SharedInput -eq 'true' -and $saved.Settings.TransmissionMode -eq 'Automatic') 'Apply did not opt in to shared input with the profile gearbox'
Assert ($saved.Settings.SteerBinding -eq 'native-keep') 'Apply changed the native Art binding'
Assert ($saved.Settings.Strength -eq '15' -and $saved.Settings.LandingEffectsEnabled -eq 'false' -and $saved.Settings.Unknown -eq 'keep') 'Apply changed force or other saved settings'
foreach ($relative in $fixed.Keys) { Assert ((Get-FileHash -LiteralPath (Join-Path $game ('Mods/' + $relative))).Hash -eq $fixed[$relative]) "Setup changed the installed $relative" }
Assert ((Get-FileHash -LiteralPath (Join-Path $game 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')).Hash -eq $nativeHash) 'Setup changed the native plugin copy'
Pass 'the companion CLI previews then applies an art of rally controls profile on the installed candidate, changing only the controls profile, the shared-input opt-in and the profile gearbox'

# The same helper's screens scope: one given yaw, the other kept; art of rally's own switches reported and never set.
$triple = Join-Path $mod 'TripleScreen.xml'
Set-Content -LiteralPath $triple -Value '<Settings><UseLocalLayout>true</UseLocalLayout><ManualSetupConfirmed>false</ManualSetupConfirmed><PanelWidthPx>2560</PanelWidthPx><PanelWidthMm>598</PanelWidthMm><EyeDistanceMm>600</EyeDistanceMm><LeftYawDegrees>40</LeftYawDegrees><RightYawDegrees>50</RightYawDegrees></Settings>'
$settingsBefore = (Get-FileHash -LiteralPath $settings).Hash
$screensPath = Join-Path $run 'controls-setup-screens.json'
[IO.File]::WriteAllText($screensPath, '{"schema":1,"values":{"RightYawDegrees":48}}')
$screened = Invoke-Cli 'screens-apply' @('apply', '--game', 'art-of-rally', '--game-dir', $game, '--screens', $screensPath) 'APPLY'
Assert ($screened.code -eq 0 -and $screened.text.Contains('Stored, not used yet') -and -not $screened.text.Contains('uses them from its next start')) "Screens apply failed: $($screened.text)"
$tripleSaved = [xml](Get-Content -LiteralPath $triple -Raw)
Assert ($tripleSaved.Settings.LeftYawDegrees -eq '40' -and $tripleSaved.Settings.RightYawDegrees -eq '48') 'Screens apply changed the wrong yaw'
Assert ($tripleSaved.Settings.UseLocalLayout -eq 'true' -and $tripleSaved.Settings.ManualSetupConfirmed -eq 'false' -and $tripleSaved.Settings.PanelWidthPx -eq '2560') 'Screens apply changed a switch or pixel size'
Assert ((Get-FileHash -LiteralPath $settings).Hash -eq $settingsBefore) 'Screens apply changed Settings.xml'
foreach ($relative in $fixed.Keys) { Assert ((Get-FileHash -LiteralPath (Join-Path $game ('Mods/' + $relative))).Hash -eq $fixed[$relative]) "Screens setup changed the installed $relative" }
Pass 'the same companion helper stores one given art of rally yaw, keeps the other, and says the values are not used yet'

$controlsHash = (Get-FileHash -LiteralPath (Join-Path $mod 'dbce-controls.ini')).Hash
$settingsHash = (Get-FileHash -LiteralPath $settings).Hash
$null = Invoke-Batch 'uninstall' (Join-Path $package 'Uninstall.bat') ('-GameDir "' + $game + '"')
Assert ((Get-FileHash -LiteralPath (Join-Path $mod 'dbce-controls.ini')).Hash -eq $controlsHash -and (Get-FileHash -LiteralPath $settings).Hash -eq $settingsHash) 'Uninstall changed the written controls or settings'
$helperInGame = @(Get-ChildItem -LiteralPath $game -Recurse -File | Where-Object { $_.Name -match '^(DbceModSetup|Wheelkit\.|companion-manifest|Setup-Controls|Set Up Controls)' })
Assert (-not $helperInGame.Count) "Helper files reached the game folder: $($helperInGame.FullName -join ', ')"
Pass 'uninstall keeps the written controls and settings, and no helper file ever reached the game folder'

[ordered]@{ powerShell=$PSVersionTable.PSVersion.ToString(); package=$package; companion=$companion; release=$manifest.release
    companionManifestSha256=(Get-FileHash -LiteralPath (Join-Path $companion 'companion-manifest.json')).Hash; checks=$checks; cases=$passed.ToArray()
    evidence='Disposable folders and the real Install.bat, Uninstall.bat and Set Up Controls.bat; no game, loader, device or owner install' } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'checks.json') -Encoding UTF8
Write-Host "$($passed.Count) controls setup companion cases ($checks checks) passed. Evidence: $run"
