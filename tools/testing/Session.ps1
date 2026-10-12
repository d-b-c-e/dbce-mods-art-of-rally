<#
.SYNOPSIS
    Records or replays a whole art of rally session, from game launch through the menus,
    the stage and the drive. Developer probe only (tools/testing/Install-Recorder.ps1).

.DESCRIPTION
    Record: arms the probe, launches the game the way the Stream Deck button does
    (steam://rungameid/550320), and tapes everything until the game exits or -Stop.
    Replay: arms the probe with a tape, launches the game and lets the probe drive it.
    Force output is muted during replay. Waits for the result and closes the game.

.EXAMPLE
    .\tools\testing\Session.ps1 -Record -Name norway-stage1
    .\tools\testing\Session.ps1 -Stop
    .\tools\testing\Session.ps1 -Replay -Name norway-stage1
#>
[CmdletBinding(DefaultParameterSetName = 'Status')]
param(
    [Parameter(ParameterSetName = 'Record', Mandatory)][switch]$Record,
    [Parameter(ParameterSetName = 'Record')][switch]$MuteOutputs,
    [Parameter(ParameterSetName = 'Record')][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]*$')][string]$ScriptedFrom,
    [Parameter(ParameterSetName = 'Replay', Mandatory)][switch]$Replay,
    [Parameter(ParameterSetName = 'Stop', Mandatory)][switch]$Stop,
    [Parameter(ParameterSetName = 'Restore', Mandatory)][string]$RestoreEnvironment,
    [Parameter(ParameterSetName = 'Record', Mandatory)][Parameter(ParameterSetName = 'Replay', Mandatory)][ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9._-]*$')][string]$Name,
    [Parameter(ParameterSetName = 'Replay')][ValidateSet('trajectory','input-diagnostic')][string]$Playback = 'trajectory',
    [string]$GameDir = 'D:/Program Files (x86)/Steam/steamapps/common/artofrally',
    # Optional library/result locations for Wheelkit; old command lines retain their paths.
    [string]$SessionsRoot,
    [Parameter(ParameterSetName = 'Replay')][string]$OutputDirectory,
    # Kept only to reject obsolete automation with an actionable error.
    [Parameter(ParameterSetName = 'Replay')][switch]$Assist,
    [Parameter(ParameterSetName = 'Replay')][double]$PoseThreshold = 2,
    [Parameter(ParameterSetName = 'Replay')][Parameter(ParameterSetName = 'Record')][int]$TimeoutMinutes = 30,
    [Parameter(ParameterSetName = 'Replay')][switch]$KeepGameOpen
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sessions = if ($SessionsRoot) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($SessionsRoot) } else { Join-Path $root 'results/sessions' }
$request = Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/session-request.txt'
# Custom rally progress lives outside the Steam Cloud folder; it decides which
# menus appear, so a replay needs the recorded copy.
$customRally = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/Funselektor Labs/Art of Rally/customrally'
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
. (Join-Path $PSScriptRoot 'ReplayInputIsolation.ps1')

function Send-Probe([string]$Command) {
    $game = Get-Process artofrally -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $game) { return 'game not running' }
    $pipe = [IO.Pipes.NamedPipeClientStream]::new('.', "ArtOfSimRally.DevRecorder.$($game.Id)", [IO.Pipes.PipeDirection]::InOut)
    try {
        $pipe.Connect(3000)
        $writer = [IO.StreamWriter]::new($pipe, [Text.UTF8Encoding]::new($false), 1024, $true); $writer.AutoFlush = $true
        $reader = [IO.StreamReader]::new($pipe, [Text.Encoding]::UTF8, $false, 1024, $true)
        $writer.WriteLine($Command)
        $reply = $reader.ReadLineAsync()
        if (-not $reply.Wait(5000)) { return 'no reply from probe within 5 s' }
        return $reply.Result
    } finally { $pipe.Dispose() }
}

function Assert-GameClosed {
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Close art of rally first; the session starts at game launch.' }
}
function Assert-TestSlot {
    # These games share the rig. Do not launch a test over another active game.
    $other = Get-Process -Name 'Super Woden Rally Edge','DRIVERally','iracing-arcade' -ErrorAction SilentlyContinue
    if ($other) { throw ('Another game owns the desktop test slot: ' + (($other | Select-Object -ExpandProperty ProcessName) -join ', ')) }
}

function Start-Game {
    try { Start-Process 'steam://rungameid/550320' }
    catch { Remove-Item -LiteralPath $request -ErrorAction SilentlyContinue; throw }
    $deadline = (Get-Date).AddMinutes(2)
    while (-not (Get-Process artofrally -ErrorAction SilentlyContinue)) {
        if ((Get-Date) -gt $deadline) { Remove-Item -LiteralPath $request -ErrorAction SilentlyContinue; throw 'art of rally did not start within 2 minutes.' }
        Start-Sleep -Seconds 1
    }
}

function Write-Request([hashtable]$Values) {
    New-Item -ItemType Directory -Force -Path (Split-Path $request) | Out-Null
    # One launch only: the probe ignores a request that has expired.
    $Values['expiresUtc'] = [DateTime]::UtcNow.AddMinutes(5).ToString('o')
    $tmp = "$request.tmp"
    ($Values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) | Set-Content -LiteralPath $tmp -Encoding UTF8
    Move-Item -LiteralPath $tmp -Destination $request -Force
}

switch ($PSCmdlet.ParameterSetName) {
    'Restore' { Restore-SessionEnvironment ([IO.Path]::GetFullPath($RestoreEnvironment)); Write-Output 'Saved owner environment restored.' }
    'Record' {
        $tape = Join-Path $sessions $Name
        if (Test-Path -LiteralPath $tape) { throw "Session '$Name' already exists: $tape" }
        # An automated recording (sana 37706): muted, from a verified route in this library, under that route's own
        # start state. Unlike a player recording it runs to completion: it closes the game and restores the owner's
        # environment itself.
        $route = $null
        if ($ScriptedFrom) {
            if (-not $MuteOutputs) { throw 'An automated recording must mute outputs: add -MuteOutputs.' }
            $route = Join-Path $sessions $ScriptedFrom
            . (Join-Path $PSScriptRoot 'RouteEnvironment.ps1')
            Assert-RouteSource $route
            if (Test-Path -LiteralPath (Join-Path $route 'incomplete.txt')) { throw 'The route recording is marked incomplete.' }
        }
        Assert-GameClosed
        Assert-TestSlot
        New-Item -ItemType Directory -Force -Path $tape | Out-Null
        if ($route) {
            # The owner's state first, then the route's start state (its preferences and custom rally; this rig's
            # display and the owner's mod settings stay). environment-at-start then records where this tape began.
            $ownerState = Join-Path $tape 'automation/owner-environment'
            Save-SessionEnvironment $ownerState
            Set-RouteEnvironment $route $ownerState
            # The route's synthetic input is the only input, and no force device is initialised (sana 37706): the
            # same isolation as replay, recorded in automation/replay-input.json. The owner restore returns the settings.
            Set-ReplayInputIsolation (Join-Path $GameDir 'Mods/ArtOfSimRally/Settings.xml') (Join-Path $tape 'automation')
        }
        Save-SessionEnvironment (Join-Path $tape 'environment-at-start')
        # Reference copy of the game's local preferences (stage/car choices may live here).
        & reg export 'HKCU\Software\Funselektor Labs\art of rally' (Join-Path $tape 'playerprefs-at-start.reg') /y | Out-Null
        if (Test-Path -LiteralPath $customRally) { Copy-Item -LiteralPath $customRally (Join-Path $tape 'customrally-at-start') }
        else { New-Item -ItemType File (Join-Path $tape 'customrally-absent-at-start') | Out-Null }
        $values = @{ mode = 'record'; tape = $tape }
        if ($MuteOutputs) { $values['muteOutputs'] = '1' }
        if ($ScriptedFrom) { $values['captureDriver'] = Join-Path $sessions $ScriptedFrom }
        if (-not $route) {
            Write-Request $values
            Start-Game
            Write-Output "Recording session '$Name' from launch into $tape"
            break
        }
        $sealed = $false; $restored = $false
        try {
            Write-Request $values
            Start-Game
            Write-Output "Recording automated session '$Name' from route '$ScriptedFrom' into $tape (outputs muted)"
            $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
            while (-not (Test-Path -LiteralPath (Join-Path $tape 'complete.tsv')) -and -not (Test-Path -LiteralPath (Join-Path $tape 'incomplete.txt'))) {
                if (-not (Get-Process artofrally -ErrorAction SilentlyContinue)) { Start-Sleep 2; break }
                if ((Get-Date) -gt $deadline) {
                    $status = try { Send-Probe 'SESSION-STATUS' } catch { 'status unavailable: ' + $_.Exception.Message }
                    Write-Warning "No sealed recording after $TimeoutMinutes minutes; $status"; break
                }
                Start-Sleep -Seconds 2
            }
            $sealed = (Test-Path -LiteralPath (Join-Path $tape 'complete.tsv')) -and -not (Test-Path -LiteralPath (Join-Path $tape 'incomplete.txt'))
        } finally {
            Remove-Item -LiteralPath $request -ErrorAction SilentlyContinue
            $game = Get-Process artofrally -ErrorAction SilentlyContinue
            if ($game) { $null = $game.CloseMainWindow(); $null = $game.WaitForExit(20000) }
            if (Get-Process artofrally -ErrorAction SilentlyContinue) {
                $pending = "Environment restore pending until the game closes. Close it, then run: Session.ps1 -RestoreEnvironment `"$ownerState`""
                Set-Content -LiteralPath (Join-Path $tape 'automation/restore-pending.txt') -Value $pending
                Write-Warning $pending
            }
            else { Restore-SessionEnvironment $ownerState; $restored = $true }
        }
        Write-Output "Evidence: $tape"
        # 0 sealed and the owner environment restored; 1 not sealed (restored); 4 restoration pending. A restore failure throws.
        if (-not $restored) { exit 4 }
        if ($sealed) { exit 0 } else { exit 1 }
    }
    'Stop' { Write-Output (Send-Probe 'SESSION-STOP') }
    'Status' { Write-Output (Send-Probe 'SESSION-STATUS') }
    'Replay' {
        if ($Assist) { throw 'Use -Playback trajectory. Assisted physics replay has been removed.' }
        $tape = Join-Path $sessions $Name
        foreach ($file in 'input.tape', 'car.tape', 'markers.tsv') {
            if (-not (Test-Path -LiteralPath (Join-Path $tape $file))) { throw "Tape is incomplete, missing $file" }
        }
        if (Test-Path -LiteralPath (Join-Path $tape 'incomplete.txt')) { throw "Tape is marked incomplete: $(Get-Content -LiteralPath (Join-Path $tape 'incomplete.txt'))" }
        $metadata = Get-Content -LiteralPath (Join-Path $tape 'session.txt') -Raw
        if ($metadata -match '(?m)^format=3\s*$') {
            $null = [Reflection.Assembly]::LoadFrom((Join-Path $root 'lib/playback/Dbce.Wheel.Playback.dll'))
            [Dbce.Wheel.Playback.ArtifactSeal]::Verify($tape, [string[]]@('session.txt','input.tape','car.tape','markers.tsv','end.txt'))
        }
        Assert-GameClosed
        Assert-TestSlot
        $out = if ($OutputDirectory) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory) } else { Join-Path $tape ('replay-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
        if (Test-Path -LiteralPath $out) { throw 'Choose a new playback result directory; existing evidence is never overwritten.' }
        $threshold = if ($Assist) { $PoseThreshold } else { 0 }
        New-Item -ItemType Directory -Force -Path $out | Out-Null
        $ownerState = Join-Path $out 'owner-environment'
        Save-SessionEnvironment $ownerState
        try {
        if (Test-Path -LiteralPath (Join-Path $tape 'environment-at-start/state.json')) {
            Restore-SessionEnvironment (Join-Path $tape 'environment-at-start') -NoReceipt
        }
        elseif (Test-Path -LiteralPath (Join-Path $tape 'playerprefs-at-start.reg')) {
            $recordedPrefs = Join-Path $tape 'playerprefs-at-start.reg'
            $registrySections = @(Get-Content -LiteralPath $recordedPrefs | Where-Object { $_ -match '^\[' })
            if ($registrySections.Count -ne 1 -or $registrySections[0] -cne '[HKEY_CURRENT_USER\Software\Funselektor Labs\art of rally]') { throw 'Tape preference snapshot contains unexpected registry keys.' }
            & reg import $recordedPrefs | Out-Null
            if ($LASTEXITCODE -ne 0) { throw 'Could not apply the recorded starting preferences.' }
        }
        $ownerRally = Join-Path $out 'customrally-before-replay'
        $hadRally = Test-Path -LiteralPath $customRally
        if ($hadRally) { Copy-Item -LiteralPath $customRally $ownerRally }
        $taped = Join-Path $tape 'customrally-at-start'
        if (Test-Path -LiteralPath $taped) { Copy-Item -LiteralPath $taped $customRally -Force }
        elseif (Test-Path -LiteralPath (Join-Path $tape 'customrally-absent-at-start')) { Remove-Item -LiteralPath $customRally -ErrorAction SilentlyContinue }
        else { Write-Warning 'Tape predates custom rally snapshots; replaying with the current custom rally state.' }
        Restore-SessionPresentation $ownerState
        # The tape supplies every input: the replay process reads no input or force device (replay-input.json).
        Set-ReplayInputIsolation (Join-Path $GameDir 'Mods/ArtOfSimRally/Settings.xml') $out
        Write-Request @{ mode = 'replay'; playback = $Playback; tape = $tape; out = $out; poseThreshold = $threshold.ToString([Globalization.CultureInfo]::InvariantCulture) }
        Start-Game
        Write-Output "Replaying '$Name' into $out"
        $deadline = (Get-Date).AddMinutes($TimeoutMinutes)
        $resultFile = Join-Path $out 'result.txt'
        while (-not (Test-Path -LiteralPath $resultFile)) {
            if (-not (Get-Process artofrally -ErrorAction SilentlyContinue)) { Start-Sleep 2; if (-not (Test-Path -LiteralPath $resultFile)) { throw 'Game exited without a replay result.' } else { break } }
            if ((Get-Date) -gt $deadline) { throw "No replay result after $TimeoutMinutes minutes; status: $(Send-Probe 'SESSION-STATUS')" }
            Start-Sleep -Seconds 5
        }
        $result = @(Get-Content -LiteralPath $resultFile)
        $result
        if (-not $KeepGameOpen) {
            $game = Get-Process artofrally -ErrorAction SilentlyContinue
            if ($game) { $null = $game.CloseMainWindow(); if (-not $game.WaitForExit(20000)) { Write-Warning 'Game did not close; leaving it running.' } }
        }
        } finally {
            Remove-Item -LiteralPath $request -ErrorAction SilentlyContinue
            if (-not $KeepGameOpen) {
                $game = Get-Process artofrally -ErrorAction SilentlyContinue
                if ($game) { $null = $game.CloseMainWindow(); $null = $game.WaitForExit(20000) }
            }
            $restored = $false
            if (Get-Process artofrally -ErrorAction SilentlyContinue) {
                # Explicit pending state (KeepGameOpen, or a game that did not close): never a restored success.
                $pending = "Environment restore pending until the game closes. Close it, then run: Session.ps1 -RestoreEnvironment `"$ownerState`""
                Set-Content -LiteralPath (Join-Path $out 'restore-pending.txt') -Value $pending
                Write-Warning $pending
            }
            else { Restore-SessionEnvironment $ownerState; $restored = $true }
        }
        Write-Output "Evidence: $out"
        # 0 passed, or partial:post-stage-divergence (the stage replayed; result.txt says fullSessionPassed=false),
        # both only after a confirmed restore; 1 failed, 2 aborted; 4 restoration pending (restore-pending.txt), whatever
        # the replay result. A restore failure throws.
        if (-not $restored) { exit 4 }
        if ($result[0] -like 'passed*' -or $result[0] -like 'partial:post-stage-divergence*') { exit 0 } elseif ($result[0] -like 'aborted*') { exit 2 } else { exit 1 }
    }
}
