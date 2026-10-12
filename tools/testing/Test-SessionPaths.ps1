# Exercises the actual launcher with process, registry and environment doubles.
# No game, registry key, display, input device or owner file is accessed.
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('art-session-paths-' + [Guid]::NewGuid().ToString('N'))
$runner = Join-Path $fixture 'source/tools/testing/Session.ps1'
$priorLocalData = $env:LOCALAPPDATA
$priorLocation = Get-Location
$global:ArtPathTest = @{ fixture=$fixture; running=$false; launches=@(); saves=0; restores=0; checks=0; replayResult='passed fixture'; calls=@() }
function Check([bool]$Value, [string]$Message) { $global:ArtPathTest.checks++; if (-not $Value) { throw $Message } }
function Get-Process { param([string[]]$Name, $ErrorAction)
    # dieAfter: the game exits after this many more process checks (an automated capture that never sealed).
    if ('artofrally' -in $Name -and $global:ArtPathTest.dieAfter -gt 0) { $global:ArtPathTest.dieAfter--; if ($global:ArtPathTest.dieAfter -eq 0) { $global:ArtPathTest.running = $false } }
    if ('artofrally' -in $Name -and $global:ArtPathTest.running) {
        $p = [pscustomobject]@{ Id=123; ProcessName='artofrally' }
        # A stubborn game ignores the close request (sana 36649).
        $p | Add-Member ScriptMethod CloseMainWindow { if (-not $global:ArtPathTest.stubborn) { $global:ArtPathTest.running=$false }; return $true }
        $p | Add-Member ScriptMethod WaitForExit { param($milliseconds) return -not $global:ArtPathTest.running }
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
        # An automated capture seals by itself, or the game exits without a seal.
        if ($request.captureDriver) {
            if ($global:ArtPathTest.exitWithoutSeal) { $global:ArtPathTest.dieAfter = 2 }
            else { Set-Content -LiteralPath (Join-Path $request.tape 'complete.tsv') -Value 'dbce-playback-seal-v1' }
        }
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
    $global:ArtPathTest.saves++; $global:ArtPathTest.calls += 'save:' + $Path
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $Path 'state.json') -Value '{}'
    # snapshot: the mod settings and the preferences really are saved and restored (the failure controls).
    if ($global:ArtPathTest.snapshot) { foreach ($k in 'settings', 'prefs') { Copy-Item -LiteralPath $global:ArtPathTest.$k (Join-Path $Path $k) } }
}
function Restore-SessionEnvironment([string]$Path, [switch]$NoReceipt) {
    if (-not $Path.StartsWith($global:ArtPathTest.fixture, [StringComparison]::OrdinalIgnoreCase)) { throw 'Restore double escaped fixture' }
    if ($global:ArtPathTest.running) { throw 'Restore double: the game is running' }
    $global:ArtPathTest.restores++; $global:ArtPathTest.calls += 'restore:' + $Path
    if ($global:ArtPathTest.snapshot) { foreach ($k in 'settings', 'prefs') { Copy-Item -LiteralPath (Join-Path $Path $k) $global:ArtPathTest.$k -Force } }
    if (-not $NoReceipt) { Set-Content -LiteralPath (Join-Path $Path 'restored.txt') -Value 'fixture restored' }
}
# A game that someone else starts while a replay is being set up, before this run launches anything.
function Restore-SessionPresentation([string]$Path) { if ($global:ArtPathTest.foreignGame) { $global:ArtPathTest.running = $true; throw 'fixture: a game started during setup' } }
'@ | Set-Content -LiteralPath (Join-Path (Split-Path $runner) 'SessionEnvironment.ps1')
    @'
function Assert-RouteSource([string]$Route) {
    if (-not (Test-Path -LiteralPath (Join-Path $Route 'environment-at-start/state.json') -PathType Leaf)) { throw "Route source lacks environment-at-start/state.json" }
}
function Set-RouteEnvironment([string]$Route, [string]$Saved) {
    $global:ArtPathTest.calls += 'route:' + $Route + '|' + $Saved
    # routeFailure: the route's preferences are written, then 'throw' fails, or 'foreign-game' sees a game start.
    if ($global:ArtPathTest.routeFailure) { Set-Content -LiteralPath $global:ArtPathTest.prefs -Value 'route preferences' -NoNewline }
    if ($global:ArtPathTest.routeFailure -eq 'foreign-game') { $global:ArtPathTest.running = $true }
    if ($global:ArtPathTest.routeFailure -in 'throw', 'foreign-game') { throw 'fixture: route failure after a partial write' }
}
'@ | Set-Content -LiteralPath (Join-Path (Split-Path $runner) 'RouteEnvironment.ps1')
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
    # sana 36649: a game still running after replay (it would not close, or -KeepGameOpen) leaves restoration pending:
    # exit 4 with restore-pending.txt and no restore receipt, even for a passed replay.
    foreach ($case in @(@{ dir = 'stubborn result'; stubborn = $true; keep = $false }, @{ dir = 'kept open result'; stubborn = $false; keep = $true })) {
        $global:ArtPathTest.running = $false; $global:ArtPathTest.stubborn = $case.stubborn
        $keepArgs = if ($case.keep) { @{ KeepGameOpen = $true } } else { @{} }
        & $runner -Replay -Name owner-drive -SessionsRoot './my tape library' -OutputDirectory "./$($case.dir)" -GameDir $game @keepArgs 3>$null
        Check ($LASTEXITCODE -eq 4) "Replay '$($case.dir)' returned $LASTEXITCODE with restoration pending"
        Check ((Test-Path -LiteralPath (Join-Path $fixture "$($case.dir)/restore-pending.txt")) -and
            -not (Test-Path -LiteralPath (Join-Path $fixture "$($case.dir)/owner-environment/restored.txt"))) "Replay '$($case.dir)' did not state pending restoration"
        $global:ArtPathTest.stubborn = $false; $global:ArtPathTest.running = $false
        Set-Content -LiteralPath $settingsPath -Value $ownerSettings -NoNewline
    }
    # sana 37706: an automated recording is muted, starts from a verified route in this library under that route's
    # start state, and closes the game and restores the owner environment itself. A player recording is unchanged.
    $library = Join-Path $fixture 'my tape library'
    New-Item -ItemType Directory -Path (Join-Path $library 'route-a/environment-at-start') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $library 'route-a/environment-at-start/state.json') -Value '{}'
    $global:ArtPathTest.running = $false; $launches = $global:ArtPathTest.launches.Count
    $refusedMute = $false
    try { & $runner -Record -ScriptedFrom route-a -Name auto-unmuted -SessionsRoot './my tape library' -GameDir $game } catch { $refusedMute = $_.Exception.Message -like '*must mute outputs*' }
    Check ($refusedMute -and $global:ArtPathTest.launches.Count -eq $launches -and -not (Test-Path -LiteralPath (Join-Path $library 'auto-unmuted'))) 'Unmuted automated recording was not refused before launch'
    $refusedRoute = $false
    try { & $runner -Record -MuteOutputs -ScriptedFrom missing-route -Name auto-missing -SessionsRoot './my tape library' -GameDir $game } catch { $refusedRoute = $_.Exception.Message -like '*Route source lacks*' }
    Check ($refusedRoute -and $global:ArtPathTest.launches.Count -eq $launches -and -not (Test-Path -LiteralPath (Join-Path $library 'auto-missing'))) 'Missing route was not refused before launch'
    $global:ArtPathTest.calls = @()
    & $runner -Record -MuteOutputs -IsolateDevices -ScriptedFrom route-a -Name auto-1 -SessionsRoot './my tape library' -GameDir $game
    $auto = Join-Path $library 'auto-1'; $ownerEnv = Join-Path $auto 'automation/owner-environment'
    Check ($LASTEXITCODE -eq 0 -and -not $global:ArtPathTest.running) "Automated recording returned $LASTEXITCODE or left the game running"
    Check ($global:ArtPathTest.launches[-1].muteOutputs -eq '1' -and $global:ArtPathTest.launches[-1].captureDriver -eq (Join-Path $library 'route-a')) 'Automated request lost its mute or route'
    $order = @("save:$ownerEnv", "route:$(Join-Path $library 'route-a')|$ownerEnv", "save:$(Join-Path $auto 'environment-at-start')", "restore:$ownerEnv")
    Check (($global:ArtPathTest.calls -join ';') -eq ($order -join ';')) "Automated environment order: $($global:ArtPathTest.calls -join '; ')"
    # The synthetic route is the only input and no force device starts: the replay isolation, before the launch.
    $autoLaunch = [xml]$global:ArtPathTest.settingsAtLaunch[-1]
    foreach ($flag in 'SharedInput', 'WheelInputEnabled', 'ShifterEnabled', 'ForceFeedbackEnabled') { Check ($autoLaunch.Settings.$flag -eq 'false') "Automated recording launched with $flag not off" }
    $autoIsolation = Get-Content -LiteralPath (Join-Path $auto 'automation/replay-input.json') -Raw | ConvertFrom-Json
    Check ($autoIsolation.recorded.SharedInput -eq 'true' -and $autoIsolation.applied.ForceFeedbackEnabled -eq 'false') 'Automated isolation evidence differs'
    Set-Content -LiteralPath $settingsPath -Value $ownerSettings -NoNewline
    Check (Test-Path -LiteralPath (Join-Path $ownerEnv 'restored.txt')) 'Automated recording did not restore the owner environment'
    # Isolation is opt-in for plumbing checks (sana 37927): a plain automated recording keeps the owner's settings and
    # the muted force path; -IsolateDevices needs a route.
    & $runner -Record -MuteOutputs -ScriptedFrom route-a -Name auto-plain -SessionsRoot './my tape library' -GameDir $game
    Check ($LASTEXITCODE -eq 0 -and $global:ArtPathTest.settingsAtLaunch[-1] -ceq $ownerSettings -and
        -not (Test-Path -LiteralPath (Join-Path $library 'auto-plain/automation/replay-input.json'))) 'A plain automated recording changed the input settings'
    $refusedIsolate = $false
    try { & $runner -Record -IsolateDevices -Name isolate-only -SessionsRoot './my tape library' -GameDir $game } catch { $refusedIsolate = $_.Exception.Message -like '*-IsolateDevices is for an automated recording*' }
    Check ($refusedIsolate -and -not (Test-Path -LiteralPath (Join-Path $library 'isolate-only'))) '-IsolateDevices without a route was not refused'
    $global:ArtPathTest.exitWithoutSeal = $true
    & $runner -Record -MuteOutputs -ScriptedFrom route-a -Name auto-unsealed -SessionsRoot './my tape library' -GameDir $game
    Check ($LASTEXITCODE -eq 1 -and (Test-Path -LiteralPath (Join-Path $library 'auto-unsealed/automation/owner-environment/restored.txt'))) "Unsealed automated recording returned $LASTEXITCODE or was not restored"
    $global:ArtPathTest.exitWithoutSeal = $false; $global:ArtPathTest.running = $false; $global:ArtPathTest.stubborn = $true
    & $runner -Record -MuteOutputs -ScriptedFrom route-a -Name auto-stubborn -SessionsRoot './my tape library' -GameDir $game 3>$null
    Check ($LASTEXITCODE -eq 4 -and (Test-Path -LiteralPath (Join-Path $library 'auto-stubborn/automation/restore-pending.txt')) -and
        -not (Test-Path -LiteralPath (Join-Path $library 'auto-stubborn/automation/owner-environment/restored.txt'))) "Automated recording with the game still open returned $LASTEXITCODE"
    $global:ArtPathTest.stubborn = $false; $global:ArtPathTest.running = $false
    # sana 38425: every change after the owner snapshot is inside the restore. A route that fails after writing the
    # preferences, and an isolation refused after the route's write, both leave the owner's preferences and settings
    # as they were, with the restore receipt, and launch nothing.
    $prefs = Join-Path $fixture 'preferences.txt'; Set-Content -LiteralPath $prefs -Value 'owner preferences' -NoNewline
    $global:ArtPathTest.prefs = $prefs; $global:ArtPathTest.snapshot = $true
    $ambiguous = '<Settings><SharedInput>true</SharedInput><SharedInput>false</SharedInput></Settings>'
    foreach ($case in @(@{ name = 'auto-route-throw'; failure = 'throw'; settings = $ownerSettings; message = '*route failure after a partial write*' },
                        @{ name = 'auto-isolation-throw'; failure = 'write'; settings = $ambiguous; message = '*Ambiguous Art setting SharedInput*' })) {
        Set-Content -LiteralPath $settingsPath -Value $case.settings -NoNewline
        $global:ArtPathTest.routeFailure = $case.failure; $launches = $global:ArtPathTest.launches.Count; $failed = ''
        try { & $runner -Record -MuteOutputs -IsolateDevices -ScriptedFrom route-a -Name $case.name -SessionsRoot './my tape library' -GameDir $game } catch { $failed = $_.Exception.Message }
        Check ($failed -like $case.message) "$($case.name) did not report its failure: $failed"
        Check ((Get-Content -LiteralPath $prefs -Raw) -ceq 'owner preferences' -and (Get-Content -LiteralPath $settingsPath -Raw) -ceq $case.settings) "$($case.name) left the owner preferences or settings changed"
        Check (Test-Path -LiteralPath (Join-Path $library "$($case.name)/automation/owner-environment/restored.txt")) "$($case.name) has no restore receipt"
        Check ($global:ArtPathTest.launches.Count -eq $launches -and -not (Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/session-request.txt'))) "$($case.name) launched or left a request"
    }
    # A game this run never launched is not closed: restoration stays pending (exit 4), for a recording and a replay.
    Set-Content -LiteralPath $settingsPath -Value $ownerSettings -NoNewline
    $global:ArtPathTest.routeFailure = 'foreign-game'
    & $runner -Record -MuteOutputs -IsolateDevices -ScriptedFrom route-a -Name auto-foreign -SessionsRoot './my tape library' -GameDir $game 3>$null
    Check ($LASTEXITCODE -eq 4 -and $global:ArtPathTest.running -and (Test-Path -LiteralPath (Join-Path $library 'auto-foreign/automation/restore-pending.txt')) -and
        -not (Test-Path -LiteralPath (Join-Path $library 'auto-foreign/automation/owner-environment/restored.txt'))) "A recording closed a game it did not launch, or returned $LASTEXITCODE"
    $global:ArtPathTest.routeFailure = $null; $global:ArtPathTest.running = $false; $global:ArtPathTest.snapshot = $false
    $global:ArtPathTest.foreignGame = $true; $replayFailed = ''
    try { & $runner -Replay -Name owner-drive -SessionsRoot './my tape library' -OutputDirectory './foreign game result' -GameDir $game 3>$null } catch { $replayFailed = $_.Exception.Message }
    Check ($replayFailed -like '*a game started during setup*' -and $global:ArtPathTest.running -and (Test-Path -LiteralPath (Join-Path $fixture 'foreign game result/restore-pending.txt'))) 'A replay closed a game it did not launch'
    $global:ArtPathTest.foreignGame = $false; $global:ArtPathTest.running = $false
    Set-Content -LiteralPath $settingsPath -Value $ownerSettings -NoNewline
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
