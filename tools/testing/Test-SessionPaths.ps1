# Exercises the actual launcher with process, registry and environment doubles.
# No game, registry key, display, input device or owner file is accessed.
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('art-session-paths-' + [Guid]::NewGuid().ToString('N'))
$runner = Join-Path $fixture 'source/tools/testing/Session.ps1'
$priorLocalData = $env:LOCALAPPDATA
$priorLocation = Get-Location
$global:ArtPathTest = @{ fixture=$fixture; running=$false; launches=@(); saves=0; restores=0; checks=0; replayResult='passed fixture' }
function Check([bool]$Value, [string]$Message) { $global:ArtPathTest.checks++; if (-not $Value) { throw $Message } }
function Get-Process { param([string[]]$Name, $ErrorAction)
    if ('artofrally' -in $Name -and $global:ArtPathTest.running) {
        $p = [pscustomobject]@{ Id=123; ProcessName='artofrally' }
        $p | Add-Member ScriptMethod CloseMainWindow { $global:ArtPathTest.running=$false; return $true }
        $p | Add-Member ScriptMethod WaitForExit { param($milliseconds) return $true }
        $p
    }
}
function reg { param($Operation, $Path, $File, $Overwrite)
    if ($Operation -ne 'export' -or $Path -ne 'HKCU\Software\Funselektor Labs\art of rally') { throw 'Unexpected registry double call' }
    Set-Content -LiteralPath $File -Value 'fixture preferences'
    $global:LASTEXITCODE = 0
}
function Start-Process { param([string]$FilePath)
    if ($FilePath -ne 'steam://rungameid/550320') { throw 'Unexpected process launch double' }
    $request = @{}
    foreach ($line in Get-Content -LiteralPath (Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/session-request.txt')) {
        if ($line -match '^([^=]+)=(.*)$') { $request[$Matches[1]]=$Matches[2] }
    }
    $global:ArtPathTest.launches += $request
    # The mod settings the launched game would read.
    $settings = $global:ArtPathTest.settings
    $global:ArtPathTest.settingsAtLaunch += ,$(if ($settings -and (Test-Path -LiteralPath $settings)) { Get-Content -LiteralPath $settings -Raw } else { $null })
    $global:ArtPathTest.running=$true
    if ($request.mode -eq 'record') {
        foreach ($f in 'input.tape','car.tape','markers.tsv') { Set-Content -LiteralPath (Join-Path $request.tape $f) -Value 'fixture' }
        Set-Content -LiteralPath (Join-Path $request.tape 'session.txt') -Value 'format=2'
    } elseif ($request.mode -eq 'replay') {
        # A one-line result must be interpreted as a line, not the first character.
        Set-Content -LiteralPath (Join-Path $request.out 'result.txt') -Value $global:ArtPathTest.replayResult
    } else { throw 'Unexpected session request mode' }
}
try {
    New-Item -ItemType Directory -Path (Split-Path $runner) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Session.ps1') -Destination $runner
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ReplayInputIsolation.ps1') -Destination (Split-Path $runner)
    @'
$customRally = Join-Path $global:ArtPathTest.fixture 'customrally'
function Save-SessionEnvironment([string]$Path) {
    if (-not $Path.StartsWith($global:ArtPathTest.fixture, [StringComparison]::OrdinalIgnoreCase)) { throw 'Environment double escaped fixture' }
    $global:ArtPathTest.saves++
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $Path 'state.json') -Value '{}'
}
function Restore-SessionEnvironment([string]$Path, [switch]$NoReceipt) {
    if (-not $Path.StartsWith($global:ArtPathTest.fixture, [StringComparison]::OrdinalIgnoreCase)) { throw 'Restore double escaped fixture' }
    $global:ArtPathTest.restores++
    if (-not $NoReceipt) { Set-Content -LiteralPath (Join-Path $Path 'restored.txt') -Value 'fixture restored' }
}
function Restore-SessionPresentation([string]$Path) {}
'@ | Set-Content -LiteralPath (Join-Path (Split-Path $runner) 'SessionEnvironment.ps1')
    $env:LOCALAPPDATA = Join-Path $fixture 'local data'
    Set-Location -LiteralPath $fixture
    $game = Join-Path $fixture "game's & copy"
    # Owner settings with every input path on: shared profile, direct wheel, shifter, device shortcuts, force.
    $settingsPath = Join-Path $game 'Mods/ArtOfSimRally/Settings.xml'
    New-Item -ItemType Directory -Path (Split-Path $settingsPath) -Force | Out-Null
    $ownerSettings = "<?xml version=`"1.0`" encoding=`"utf-8`"?>`r`n<Settings><!--owner--><SharedInput>true</SharedInput><WheelInputEnabled>true</WheelInputEnabled>" +
        "<SettingsButtonBinding>wheel|0|button:3|0|1</SettingsButtonBinding><StopFfbButtonBinding /><SteerBinding>native wheel binding</SteerBinding>" +
        "<CameraButtonBindings><string>wheel|0|button:4|0|1</string><string /><string>wheel|0|button:5|0|1</string></CameraButtonBindings>" +
        "<ShifterEnabled>true</ShifterEnabled><Unknown a=`"keep`"><Part>data</Part></Unknown></Settings>"
    Set-Content -LiteralPath $settingsPath -Value $ownerSettings -NoNewline
    $global:ArtPathTest.settings = $settingsPath; $global:ArtPathTest.settingsAtLaunch = @()
    & $runner -Record -MuteOutputs -Name owner-drive -SessionsRoot './my tape library' -GameDir $game
    Check ($global:ArtPathTest.settingsAtLaunch[-1] -ceq $ownerSettings) 'Recording changed the input settings it launches with'
    $tape = Join-Path $fixture 'my tape library/owner-drive'
    Check ($global:ArtPathTest.launches[-1].tape -eq $tape) 'Selected recording library was not used'
    Check ($global:ArtPathTest.launches[-1].muteOutputs -eq '1') 'Record lost its output mute'
    Check (Test-Path -LiteralPath (Join-Path $tape 'environment-at-start/state.json')) 'Starting environment was not captured with the tape'
    Check (-not (Test-Path -LiteralPath (Join-Path $fixture 'source/results/sessions'))) 'Custom library wrote into the source checkout'
    $global:ArtPathTest.running=$false
    & $runner -Replay -Name owner-drive -SessionsRoot './my tape library' -OutputDirectory './separate playback result' -GameDir $game
    Check ($LASTEXITCODE -eq 0) 'Single-line successful replay returned failure'
    $result = Join-Path $fixture 'separate playback result'
    Check ($global:ArtPathTest.launches[-1].tape -eq $tape) 'Replay used a different same-named tape'
    Check ($global:ArtPathTest.launches[-1].out -eq $result) 'Selected replay output was not used'
    Check ($global:ArtPathTest.launches[-1].playback -eq 'trajectory') 'Replay did not retain trajectory mode'
    Check (Test-Path -LiteralPath (Join-Path $result 'owner-environment/restored.txt')) 'Replay did not restore the saved environment'
    Check (-not $global:ArtPathTest.running) 'Replay did not close its game'
    # sana 36236: the replay launch reads no input or force device, and nothing else in the settings changes.
    $launched = [xml]$global:ArtPathTest.settingsAtLaunch[-1]
    foreach ($flag in 'SharedInput', 'WheelInputEnabled', 'ShifterEnabled', 'ForceFeedbackEnabled') {
        Check (@($launched.Settings.ChildNodes | Where-Object { $_.LocalName -eq $flag }).Count -eq 1 -and $launched.Settings.$flag -eq 'false') "Replay launched with $flag not off"
    }
    Check ([string]$launched.Settings.SettingsButtonBinding -eq '' -and [string]$launched.Settings.StopFfbButtonBinding -eq '') 'Replay launched with a device shortcut bound'
    Check (@($launched.Settings.CameraButtonBindings.ChildNodes | Where-Object { $_.InnerText -ne '' }).Count -eq 0 -and
        @($launched.Settings.CameraButtonBindings.ChildNodes).Count -eq 3) 'Replay launched with a camera shortcut bound'
    Check ($launched.Settings.SteerBinding -eq 'native wheel binding' -and $launched.Settings.Unknown.a -eq 'keep' -and $launched.Settings.Unknown.Part -eq 'data' -and
        $global:ArtPathTest.settingsAtLaunch[-1].Contains('<!--owner-->')) 'Replay isolation changed unrelated settings'
    $evidence = Get-Content -LiteralPath (Join-Path $result 'replay-input.json') -Raw | ConvertFrom-Json
    Check ($evidence.recorded.SharedInput -eq 'true' -and $evidence.recorded.WheelInputEnabled -eq 'true' -and $evidence.recorded.ShifterEnabled -eq 'true' -and
        $evidence.recorded.ForceFeedbackEnabled -eq '(default)' -and $evidence.recorded.SettingsButtonBinding -eq 'bound' -and $evidence.recorded.StopFfbButtonBinding -eq 'unbound' -and
        $evidence.recorded.CameraButtonBindings -eq '2 bound' -and $evidence.applied.SharedInput -eq 'false') 'Replay input evidence differs'
    # A malformed settings file is refused before any launch, and is left as it was.
    $global:ArtPathTest.running = $false
    Set-Content -LiteralPath $settingsPath -Value '<Settings><SharedInput>true</SharedInput><SharedInput>false</SharedInput></Settings>' -NoNewline
    $launches = $global:ArtPathTest.launches.Count; $refusedAmbiguous = $false
    try { & $runner -Replay -Name owner-drive -SessionsRoot './my tape library' -OutputDirectory './ambiguous playback result' -GameDir $game } catch { $refusedAmbiguous = $_.Exception.Message -like '*Ambiguous Art setting SharedInput*' }
    Check ($refusedAmbiguous -and $global:ArtPathTest.launches.Count -eq $launches) 'Ambiguous settings were not refused before launch'
    Check ((Get-Content -LiteralPath $settingsPath -Raw) -ceq '<Settings><SharedInput>true</SharedInput><SharedInput>false</SharedInput></Settings>') 'Refused isolation changed the settings'
    Set-Content -LiteralPath $settingsPath -Value $ownerSettings -NoNewline
    # sana 36495: the partial outcome exits 0 after the normal cleanup and restore; failed stays 1.
    foreach ($case in @(@{ text = "partial:post-stage-divergence: all 9 driving rows played (fixture); frames waited 120 s for 'x'`nstatus`nscope=stage-only`nfullSessionPassed=false"; code = 0; dir = 'partial result' },
                        @{ text = "failed: frames waited 120 s for 'x'"; code = 1; dir = 'failed result' })) {
        $global:ArtPathTest.running = $false; $global:ArtPathTest.replayResult = $case.text
        & $runner -Replay -Name owner-drive -SessionsRoot './my tape library' -OutputDirectory "./$($case.dir)" -GameDir $game
        Check ($LASTEXITCODE -eq $case.code) "Replay '$($case.dir)' returned $LASTEXITCODE"
        Check (Test-Path -LiteralPath (Join-Path $fixture "$($case.dir)/owner-environment/restored.txt")) "Replay '$($case.dir)' did not restore the saved environment"
        Set-Content -LiteralPath $settingsPath -Value $ownerSettings -NoNewline
    }
    $global:ArtPathTest.replayResult = 'passed fixture'
    $before = $global:ArtPathTest.saves; $launchesBefore = $global:ArtPathTest.launches.Count
    $refused = $false
    try { & $runner -Replay -Name owner-drive -SessionsRoot './my tape library' -OutputDirectory './separate playback result' -GameDir $game } catch { $refused=$_.Exception.Message -like '*existing evidence*' }
    Check $refused 'An existing result was not refused'
    Check ($global:ArtPathTest.saves -eq $before) 'Existing-result refusal happened after snapshotting'
    Check ($global:ArtPathTest.launches.Count -eq $launchesBefore) 'Existing-result refusal launched a game'
    & $runner -Record -Name default-location -GameDir $game
    Check ($global:ArtPathTest.launches[-1].tape -eq (Join-Path $fixture 'source/results/sessions/default-location')) 'Legacy default recording location changed'
    "$($global:ArtPathTest.checks) launcher path checks passed; isolated evidence: $fixture"
} finally {
    $env:LOCALAPPDATA = $priorLocalData
    Set-Location -LiteralPath $priorLocation.Path
    Remove-Variable ArtPathTest -Scope Global
}
