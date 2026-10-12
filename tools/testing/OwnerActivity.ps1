# Developer-only owner-activity gate for the disposable-copy runners (sana 34111/34141).
# Dot-source after the toolkit's tools/playback/Stage-OwnerIdle.ps1. Idle is always read and recorded as observed;
# the canonical local policy (Test-StageOwnerActivityAllowed: %LOCALAPPDATA%/dbce/rig-activity-policy.json) only
# decides whether recent input refuses a launch or ends an observation. It never waives the rig lease, the one-game
# queue, force muting or restoration.

$script:ownerActivity = [ordered]@{ launchChecks = [Collections.Generic.List[object]]::new(); activeDuringObservation = 0; firstActiveUtc = $null; lastActiveUtc = $null }

# True when the run may proceed: the owner was idle for MinimumSeconds, or the owner's standing direction allows
# activity. Launch checks are all recorded; during an observation only readings below the minimum are counted.
function Test-OwnerGate([string]$OwnerInput, [string]$Phase, [double]$MinimumSeconds, [string]$Record) {
    $idle = [double](& $OwnerInput -IdleSeconds)
    $allowed = Test-StageOwnerActivityAllowed
    $now = [DateTime]::UtcNow.ToString('o')
    if ($Phase -ne 'observation') {
        $ownerActivity.launchChecks.Add([pscustomobject]@{ utc = $now; phase = $Phase; idleSeconds = $idle; minimumSeconds = $MinimumSeconds; ownerActivityAllowed = $allowed })
    } elseif ($idle -lt $MinimumSeconds) {
        $ownerActivity.activeDuringObservation++
        if (!$ownerActivity.firstActiveUtc) { $ownerActivity.firstActiveUtc = $now }
        $ownerActivity.lastActiveUtc = $now
    }
    if ($Record -and ($Phase -ne 'observation' -or $idle -lt $MinimumSeconds)) { $ownerActivity | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $Record }
    return ($allowed -or $idle -ge $MinimumSeconds)
}
