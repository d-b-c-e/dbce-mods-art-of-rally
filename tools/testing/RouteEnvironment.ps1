# Developer-only: the start state of a capture-driver route (a recorder session whose taped menus reach the stage).
# Needs SessionEnvironment.ps1 dot-sourced and the caller's $customRally; the caller saves the owner environment
# before Set-RouteEnvironment and restores it afterwards.

function Assert-RouteSource([string]$Route) {
    foreach ($n in @('input.tape','markers.tsv','session.txt','environment-at-start/state.json','environment-at-start/playerprefs.json')) {
        if (!(Test-Path -LiteralPath (Join-Path $Route $n) -PathType Leaf)) { throw "Route source lacks $n" }
    }
    # The route's menu path depends on the start state it was taped with (2026-10-12 05:45: a custom rally in
    # progress showed RallyInProgress instead of Custom Event). It must record the custom rally state.
    if (!(Test-Path -LiteralPath (Join-Path $Route 'customrally-at-start') -PathType Leaf) -and !(Test-Path -LiteralPath (Join-Path $Route 'customrally-absent-at-start') -PathType Leaf)) { throw 'Route records no custom rally start state.' }
}

# The route's taped start state, exactly as Session.ps1 replay applies a tape's: the game's preferences
# (raw, verified) and its custom rally save; never its mod or triple settings (the staged copy's are pinned).
# Display preferences stay this rig's, from the runner's own environment snapshot $Saved.
function Set-RouteEnvironment([string]$Route, [string]$Saved) {
    $directory = Join-Path $Route 'environment-at-start'
    $state = Get-Content -LiteralPath (Join-Path $directory 'state.json') -Raw | ConvertFrom-Json
    $prefs = Get-SessionPrefs $directory $state
    Restore-RegistryState -Path $prefs.path | Write-Host
    if (-not (Compare-RegistryState -Path $prefs.path)) { throw 'Route preference application not verified.' }
    $taped = Join-Path $Route 'customrally-at-start'
    if (Test-Path -LiteralPath $taped -PathType Leaf) { Copy-Item -LiteralPath $taped -Destination $customRally -Force }
    elseif (Test-Path -LiteralPath $customRally) { Remove-Item -LiteralPath $customRally }
    Restore-SessionPresentation $Saved
}
