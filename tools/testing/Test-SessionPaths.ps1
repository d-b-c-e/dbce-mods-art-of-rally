# Exercises the actual launcher with process, registry and environment doubles.
# No game, registry key, display, input device or owner file is accessed.
$ErrorActionPreference = 'Stop'
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('art-session-paths-' + [Guid]::NewGuid().ToString('N'))
$runner = Join-Path $fixture 'source/tools/testing/Session.ps1'
$priorLocalData = $env:LOCALAPPDATA
$priorLocation = Get-Location
$global:ArtPathTest = @{ fixture=$fixture; running=$false; launches=@(); saves=0; restores=0; checks=0 }
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
    $global:ArtPathTest.running=$true
    if ($request.mode -eq 'record') {
        foreach ($f in 'input.tape','car.tape','markers.tsv') { Set-Content -LiteralPath (Join-Path $request.tape $f) -Value 'fixture' }
        Set-Content -LiteralPath (Join-Path $request.tape 'session.txt') -Value 'format=2'
    } elseif ($request.mode -eq 'replay') {
        # A one-line result must be interpreted as a line, not the first character.
        Set-Content -LiteralPath (Join-Path $request.out 'result.txt') -Value 'passed fixture'
    } else { throw 'Unexpected session request mode' }
}
try {
    New-Item -ItemType Directory -Path (Split-Path $runner) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Session.ps1') -Destination $runner
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
    & $runner -Record -MuteOutputs -Name owner-drive -SessionsRoot './my tape library' -GameDir $game
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
    $before = $global:ArtPathTest.saves
    $refused = $false
    try { & $runner -Replay -Name owner-drive -SessionsRoot './my tape library' -OutputDirectory './separate playback result' -GameDir $game } catch { $refused=$_.Exception.Message -like '*existing evidence*' }
    Check $refused 'An existing result was not refused'
    Check ($global:ArtPathTest.saves -eq $before) 'Existing-result refusal happened after snapshotting'
    Check ($global:ArtPathTest.launches.Count -eq 2) 'Existing-result refusal launched a game'
    & $runner -Record -Name default-location -GameDir $game
    Check ($global:ArtPathTest.launches[-1].tape -eq (Join-Path $fixture 'source/results/sessions/default-location')) 'Legacy default recording location changed'
    "$($global:ArtPathTest.checks) launcher path checks passed; isolated evidence: $fixture"
} finally {
    $env:LOCALAPPDATA = $priorLocalData
    Set-Location -LiteralPath $priorLocation.Path
    Remove-Variable ArtPathTest -Scope Global
}
