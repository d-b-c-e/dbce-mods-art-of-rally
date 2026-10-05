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
    # Kept only to reject obsolete automation with an actionable error.
    [Parameter(ParameterSetName = 'Replay')][switch]$Assist,
    [Parameter(ParameterSetName = 'Replay')][double]$PoseThreshold = 2,
    [Parameter(ParameterSetName = 'Replay')][int]$TimeoutMinutes = 30,
    [Parameter(ParameterSetName = 'Replay')][switch]$KeepGameOpen
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sessions = Join-Path $root 'results/sessions'
$request = Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/session-request.txt'
# Custom rally progress lives outside the Steam Cloud folder; it decides which
# menus appear, so a replay needs the recorded copy.
$customRally = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData/LocalLow/Funselektor Labs/Art of Rally/customrally'
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')

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
        Assert-GameClosed
        Assert-TestSlot
        New-Item -ItemType Directory -Force -Path $tape | Out-Null
        Save-SessionEnvironment (Join-Path $tape 'environment-at-start')
        # Reference copy of the game's local preferences (stage/car choices may live here).
        & reg export 'HKCU\Software\Funselektor Labs\art of rally' (Join-Path $tape 'playerprefs-at-start.reg') /y | Out-Null
        if (Test-Path -LiteralPath $customRally) { Copy-Item -LiteralPath $customRally (Join-Path $tape 'customrally-at-start') }
        else { New-Item -ItemType File (Join-Path $tape 'customrally-absent-at-start') | Out-Null }
        $values = @{ mode = 'record'; tape = $tape }
        if ($MuteOutputs) { $values['muteOutputs'] = '1' }
        if ($ScriptedFrom) { $values['captureDriver'] = Join-Path $sessions $ScriptedFrom }
        Write-Request $values
        Start-Game
        Write-Output "Recording session '$Name' from launch into $tape"
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
        $out = Join-Path $tape ('replay-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
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
        $result = Get-Content -LiteralPath $resultFile
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
            if (Get-Process artofrally -ErrorAction SilentlyContinue) { Write-Warning "Environment restore pending until game closes: $ownerState" }
            else { Restore-SessionEnvironment $ownerState }
        }
        Write-Output "Evidence: $out"
        # 0 passed, 1 failed, 2 aborted.
        if ($result[0] -like 'passed*') { exit 0 } elseif ($result[0] -like 'aborted*') { exit 2 } else { exit 1 }
    }
}
