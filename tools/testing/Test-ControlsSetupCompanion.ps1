<# Tests the optional controls setup companion (package.ps1 -PrivateStandaloneSetup) with a shared-input candidate
   installed by its real Install.bat into an isolated fake game folder, using the same batch entry points players run.
   No game, hardware or owner install is used. SettingsOnly retains integrity refusals and checks only first-launch Settings.
   Logs/fixtures use fresh EvidenceDirectory, or results/controls-setup-* by default. #>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory,
      [Parameter(Mandatory)][string]$CompanionDirectory,
      [switch]$SettingsOnly,
      [string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$package = [IO.Path]::GetFullPath($PackageDirectory)
$companion = [IO.Path]::GetFullPath($CompanionDirectory).TrimEnd('\')
. (Join-Path $root 'tools/installer/verify.ps1')
$manifest = Assert-Payload $package
if (-not $manifest.files.'ArtOfSimRally/dbce_shim.dll') { throw 'Use an extracted shared-input candidate package.' }
if ($SettingsOnly -and -not $EvidenceDirectory) { throw '-SettingsOnly requires a fresh -EvidenceDirectory' }
$run = if ($EvidenceDirectory) { [IO.Path]::GetFullPath($EvidenceDirectory) } else { Join-Path $root ('results/controls-setup-' + [Guid]::NewGuid().ToString('N')) }
if (Test-Path -LiteralPath $run) { throw 'Evidence directory already exists; preserve its results' }
$game = Join-Path $run 'game [USB] with spaces'
$mod = Join-Path $game 'Mods/ArtOfSimRally'
$checks = 0
$passed = [Collections.Generic.List[string]]::new()
$cleanFailures = [Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $game -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'artofrally.exe') -Value 'fixture; never executed'
New-Item -ItemType Directory -Path (Join-Path $game 'artofrally_Data/Managed/UnityModManager') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'artofrally_Data/Managed/UnityModManager/UnityModManager.dll') -Value 'fixture; never loaded'

function Assert([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; $script:checks++ }
function Pass([string]$Name) { $passed.Add($Name); Write-Host "PASS $Name" }
function File-Snapshot([string]$Directory) {
    return (@(Get-ChildItem -LiteralPath $Directory -Recurse -File | Sort-Object FullName | ForEach-Object {
        $_.FullName.Substring($Directory.Length) + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }) -join "`n")
}
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

if ($SettingsOnly) {
    $wrongSource = Copy-Companion 'wrong helper source'
    $wrongPath = Join-Path $wrongSource 'companion-manifest.json'
    $wrongRecord = Get-Content -LiteralPath $wrongPath -Raw | ConvertFrom-Json
    $wrongRecord.standaloneSetup.sourceCommit = '0000000000000000000000000000000000000000'
    $wrongRecord | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $wrongPath -Encoding UTF8
    $log = Invoke-Setup 'wrong-source' $wrongSource $game -Fails $true
    Assert ($log -match 'Unexpected helper identity' -and $log -notmatch 'PLAN ONLY') 'Wrong helper source was accepted'
    Pass 'Set Up Controls refuses a helper source mismatch'
    $before = File-Snapshot $game
    $request = Join-Path $run 'first-launch-settings.json'
    [IO.File]::WriteAllText($request, '{"schema":1,"settings":[{"section":"","key":"ShowFrameRate","value":"true"}]}')
    foreach ($action in @('preview', 'apply')) {
        $result = Invoke-Cli ("settings-only-$action") @($action, '--game', 'art-of-rally', '--game-dir', $game, '--settings', $request) 'APPLY'
        Assert ($result.code -eq 1 -and $result.text -match 'Start .+ once with the mod installed' -and $result.text -match 'Nothing was written') "Settings $action lacks actionable first-launch refusal: $($result.text)"
        Assert (-not (Test-Path -LiteralPath (Join-Path $mod 'Settings.xml'))) "Settings $action created Settings.xml"
        Assert ((File-Snapshot $game) -ceq $before) "Settings $action changed the target inventory"
    }
    $before | Set-Content -LiteralPath (Join-Path $run 'target-before.txt') -Encoding UTF8
    (File-Snapshot $game) | Set-Content -LiteralPath (Join-Path $run 'target-after.txt') -Encoding UTF8
    Pass 'Settings Preview/Apply gives actionable first-launch refusal and preserves the full target inventory'
    [ordered]@{ mode='settings-only'; status='PASS'; package=$package; companion=$companion; checks=$checks; cases=$passed.ToArray()
        companionManifestSha256=(Get-FileHash -LiteralPath (Join-Path $companion 'companion-manifest.json')).Hash
        gameLaunched=$false; guiLaunched=$false; hardwareOpened=$false } |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'checks.json') -Encoding UTF8
    Write-Host "$checks focused checks passed; evidence: $run"
    return
}

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

# A separate real install has never run: optional scopes must refuse before controls can create Settings.xml.
$clean = Join-Path $run 'never launched game'
New-Item -ItemType Directory -Path (Join-Path $clean 'artofrally_Data/Managed/UnityModManager') -Force | Out-Null
Set-Content -LiteralPath (Join-Path $clean 'artofrally.exe') -Value 'fixture; never executed'
Set-Content -LiteralPath (Join-Path $clean 'artofrally_Data/Managed/UnityModManager/UnityModManager.dll') -Value 'fixture; never loaded'
$null = Invoke-Batch 'clean-install' (Join-Path $package 'Install.bat') ('-GameDir "' + $clean + '"')
$cleanMod = Join-Path $clean 'Mods/ArtOfSimRally'
foreach ($file in @('Settings.xml', 'TripleScreen.xml', 'dbce-controls.ini')) {
    Assert (-not (Test-Path -LiteralPath (Join-Path $cleanMod $file))) "Fresh install unexpectedly contains $file"
}
$cleanBefore = File-Snapshot $clean
$cleanScreens = Join-Path $run 'clean-screens.json'
$cleanSettings = Join-Path $run 'clean-settings.json'
[IO.File]::WriteAllText($cleanScreens, '{"schema":1,"values":{"RightYawDegrees":48}}')
[IO.File]::WriteAllText($cleanSettings, '{"schema":1,"settings":[{"section":"","key":"Strength","value":"15"}]}')
foreach ($scope in @('screens', 'settings')) {
    $failuresBefore = $cleanFailures.Count
    $request = if ($scope -eq 'screens') { $cleanScreens } else { $cleanSettings }
    foreach ($action in @('preview', 'apply')) {
        $result = Invoke-Cli ("clean-$scope-$action") @($action, '--game', 'art-of-rally', '--game-dir', $clean, "--$scope", $request) 'APPLY'
        if ($result.code -eq 0 -or $result.text -notmatch 'Start .+ once with the mod installed') {
            $cleanFailures.Add("Clean $scope $action did not ask for first launch; see cli-clean-$scope-$action")
        } else { $checks++ }
        Assert ((File-Snapshot $clean) -ceq $cleanBefore) "Clean $scope $action changed the target"
    }
    if ($cleanFailures.Count -eq $failuresBefore) { Pass "clean $scope Preview/Apply asks for first launch and leaves every target file unchanged" }
}
$cleanPreview = Invoke-Cli 'clean-controls-preview' @('preview', '--game', 'art-of-rally', '--game-dir', $clean, '--profile', $profilePath)
Assert ((File-Snapshot $clean) -ceq $cleanBefore) 'Clean controls Preview changed the target'
$cleanApply = Invoke-Cli 'clean-controls-apply' @('apply', '--game', 'art-of-rally', '--game-dir', $clean, '--profile', $profilePath) 'APPLY'
if ($cleanApply.code -eq 0) {
    Assert ($cleanPreview.code -eq 0 -and $cleanPreview.text.Contains('dbce-controls.ini')) "Clean controls Preview failed: $($cleanPreview.text)"
    $cleanXml = [xml](Get-Content -LiteralPath (Join-Path $cleanMod 'Settings.xml') -Raw)
    Assert ($cleanXml.DocumentElement.Name -ceq 'Settings' -and $cleanXml.DocumentElement.NamespaceURI -eq '') 'Clean Apply wrote an XML root the mod cannot load'
    Assert ($cleanXml.Settings.SharedInput -eq 'true' -and $cleanXml.Settings.TransmissionMode -eq 'Automatic') 'Clean Apply omitted the shared-input/profile gearbox settings'
    Assert ((Get-Content -LiteralPath (Join-Path $cleanMod 'dbce-controls.ini') -Raw).Contains('steer =')) 'Clean Apply omitted the controls binding'
    $allowed = @((Join-Path $cleanMod 'Settings.xml'), (Join-Path $cleanMod 'dbce-controls.ini'))
    $unchanged = @(Get-ChildItem -LiteralPath $clean -Recurse -File | Where-Object { $_.FullName -notin $allowed } | Sort-Object FullName | ForEach-Object {
        $_.FullName.Substring($clean.Length) + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    }) -join "`n"
    Assert ($unchanged -ceq $cleanBefore) 'Clean Apply changed files beyond Settings.xml and dbce-controls.ini'
    Pass 'controls Preview/Apply on the never-launched install creates only the shared controls and loadable Settings XML with the profile gearbox'
} else {
    Assert ($cleanApply.text -match 'Start .+ once with the mod installed') "Clean controls Apply failed without actionable first-launch guidance: $($cleanApply.text)"
    Assert ((File-Snapshot $clean) -ceq $cleanBefore) 'Refused clean controls Apply partially wrote the target'
    Pass 'controls Apply refuses the never-launched install with first-launch guidance and leaves every target file unchanged'
}
$cleanUserFiles = @{}
foreach ($file in @('Settings.xml', 'TripleScreen.xml', 'dbce-controls.ini')) {
    $path = Join-Path $cleanMod $file
    if (Test-Path -LiteralPath $path) { $cleanUserFiles[$file] = (Get-FileHash -LiteralPath $path).Hash }
}
$null = Invoke-Batch 'clean-uninstall' (Join-Path $package 'Uninstall.bat') ('-GameDir "' + $clean + '"')
foreach ($file in $cleanUserFiles.Keys) {
    Assert ((Get-FileHash -LiteralPath (Join-Path $cleanMod $file)).Hash -eq $cleanUserFiles[$file]) "Clean uninstall changed $file"
}
Pass 'uninstall preserves user files from the never-launched target'

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
    status=$(if ($cleanFailures.Count) { 'FAIL' } else { 'PASS' })
    companionManifestSha256=(Get-FileHash -LiteralPath (Join-Path $companion 'companion-manifest.json')).Hash; checks=$checks; cases=$passed.ToArray(); failures=$cleanFailures.ToArray()
    evidence='Disposable folders and the real Install.bat, Uninstall.bat and Set Up Controls.bat; no game, loader, device or owner install' } |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $run 'checks.json') -Encoding UTF8
Write-Host "$($passed.Count) controls setup companion cases ($checks checks) passed; $($cleanFailures.Count) failures. Evidence: $run"
if ($cleanFailures.Count) { throw ($cleanFailures -join '; ') }
