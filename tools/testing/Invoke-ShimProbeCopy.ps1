#Requires -Version 7.0
# Developer-only, coordinator-serialized diagnostic. The prepared request is the
# sole configuration. This runner never installs into SourceGameRoot or sends input.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Result,
      [switch]$PreflightOnly,
      [ValidateRange(30,180)][int]$ObserveSeconds=90,
      [string]$ToolkitRepo='E:/Source/toolkits/dbce-wheel-mod-toolkit',
      [string]$HubRepo='E:/Source/dbce-project-mgmt')
$ErrorActionPreference='Stop'
function Full([string]$p) { [IO.Path]::GetFullPath($p) }
function Hash([string]$p) { if(Test-Path -LiteralPath $p -PathType Leaf) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash } else { 'absent' } }
function Plain([string]$p) {
    $cursor=Full $p
    while($cursor) {
        if((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked path refused: $cursor" }
        $cursor=[IO.Path]::GetDirectoryName($cursor)
    }
}
function Within([string]$p,[string]$parent) { $p.Equals($parent,[StringComparison]::OrdinalIgnoreCase) -or $p.StartsWith($parent.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase) }
function Assert-GameClosed { if(Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Art remains open; retain backup and lease, no restoration.' } }
$Result=Full $Result; Plain $Result
$requestPath=Join-Path $Result 'request.json'
$request=Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
$GameDir=Full $request.GameRoot; $owner=Full $request.SourceGameRoot
foreach($p in @($GameDir,$owner)) { Plain $p }
if($request.Schema -ne 1 -or $request.Mode -notin @('Observe','Apply') -or (Full $request.Result) -ne $Result -or
    (Within $GameDir $owner) -or (Within $owner $GameDir) -or (Within $Result $GameDir) -or (Within $Result $owner)) { throw 'Invalid disposable request scope.' }
if([DateTime]::Parse($request.ExpiresUtc).ToUniversalTime() -lt [DateTime]::UtcNow.AddSeconds($ObserveSeconds+30)) { throw 'Prepare a new request with enough time for the complete observation.' }
if(Test-Path -LiteralPath "$Result/launch.json") { throw 'Request has already been launched; use a fresh result.' }
$mod=Join-Path $GameDir 'Mods/ArtOfSimRally'
$pins=@{
    (Join-Path $mod 'ArtOfSimRally.Mod.dll')=$request.ModSha256
    (Join-Path $mod 'UnityForceFeedback.dll')=$request.NativeSha256
    (Join-Path $GameDir 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')=$request.NativeSha256
    (Join-Path $mod 'Settings.xml')=$request.SettingsSha256
    ([string]$request.ShimPath)=$request.ShimSha256
    ([string]$request.ProfilePath)=$request.ProfileSha256
}
foreach($p in $pins.Keys) { Plain $p; if((Hash $p) -ne $pins[$p]) { throw "Candidate pin differs: $p" } }
if((Hash (Join-Path $owner 'Mods/ArtOfSimRally/Settings.xml')) -ne $request.SettingsSha256) { throw 'Owner settings changed since preparation.' }
if((Get-Content -LiteralPath (Join-Path $GameDir 'steam_appid.txt') -Raw).Trim() -cne '550320') { throw 'Copy-only Steam app id missing; direct launch could redirect through Steam.' }
$otherMods=@(Get-ChildItem -LiteralPath (Join-Path $GameDir 'Mods') -Directory | Where-Object Name -NE 'ArtOfSimRally')
if($otherMods.Count) { throw 'Disposable copy contains optional mod folders; stage only ArtOfSimRally.' }
[xml]$umm=Get-Content -LiteralPath (Join-Path $GameDir 'artofrally_Data/Managed/UnityModManager/Params.xml') -Raw
if(@($umm.Param.ModParams.Mod | Where-Object { $_.Id -eq 'ArtOfSimRally' -and $_.Enabled -eq 'true' }).Count -ne 1) { throw 'Candidate UMM mod is not enabled.' }
foreach($p in @('ArtOfSimRally/controls-request.txt','ArtOfSimRally/session-request.txt','dbce/art-of-rally/inject.on')) {
    if(Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA $p)) { throw "Existing developer marker refused: $p" }
}
Assert-GameClosed
if($PreflightOnly) { Write-Output 'Disposable candidate pins, settings, app id, UMM and marker checks passed. No launch or shared-state mutation.'; return }
$ownerInput=Join-Path $HubRepo 'tools/Owner-Input.ps1'
if([double](& $ownerInput -IdleSeconds) -lt 300) { throw 'Owner input is recent; no launch or shared-state mutation.' }
$customRally=Join-Path $env:USERPROFILE 'AppData/LocalLow/Funselektor Labs/Art of Rally/customrally'
$localLow=Split-Path $customRally -Parent; $cloud=Join-Path $localLow 'cloud'
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
. (Join-Path $ToolkitRepo 'tools/playback/Stage-RigLease.ps1')
$snapshots=[Collections.Generic.List[object]]::new(); $ownerPins=[Collections.Generic.List[object]]::new()
$lease=$null; $game=$null; $ready=$false; $environmentSaved=$false; $launchAttempted=$false
function Save-File([string]$p) {
    Plain $p; $p=Full $p
    if($snapshots.path -contains $p) { return }
    $before=Hash $p; $backup=Join-Path $Result ('shared-files/'+$snapshots.Count+'.bin')
    if($before -ne 'absent') { Copy-Item -LiteralPath $p -Destination $backup; if((Hash $backup) -ne $before) { throw 'Snapshot changed during copy.' } }
    $snapshots.Add([pscustomobject]@{path=$p;before=$before;backup=$backup})
}
function Files([string]$p,[switch]$Recurse) {
    if(Test-Path -LiteralPath $p) { Get-ChildItem -LiteralPath $p -File -Recurse:$Recurse | ForEach-Object { Plain $_.FullName; $_.FullName } }
}
$lease=Enter-StageRigLease -Path (Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt') -Owner art-shim-observe -Purpose 'Disposable Art exact identity/cache diagnostic; no force or input; restore shared state'
try {
    Assert-GameClosed
    New-Item -ItemType Directory -Path "$Result/shared-files","$Result/after" | Out-Null
    foreach($p in @((Files (Join-Path $owner 'Mods') -Recurse)) + @(
        (Join-Path $owner 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll'),
        (Join-Path $owner 'artofrally_Data/Managed/UnityModManager/Params.xml'),
        (Join-Path $owner 'steam_appid.txt'))) { Plain $p; $ownerPins.Add([pscustomobject]@{path=$p;sha256=(Hash $p)}) }
    $ownerPins | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/owner-pins.json"
    Save-SessionEnvironment "$Result/shared-environment"; $environmentSaved=$true
    foreach($p in @('ArtOfSimRally/ffb.log','ArtOfSimRally/ffb.previous.log','ArtOfSimRally/last-session-frame-health.xml',
        'DBCE/TripleScreen/games/art-of-rally/status.json','DBCE/TripleScreen/games/art-of-rally/render-diagnostics.json')) { Save-File (Join-Path $env:LOCALAPPDATA $p) }
    $rootBefore=@(Files $localLow); $cloudBefore=@(Files $cloud -Recurse)
    foreach($p in $rootBefore+$cloudBefore) { Save-File $p }
    $snapshots | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/shared-files.json"
    @{schema=1;gameDir=$GameDir;owner=$owner;lease=$lease;rootBefore=$rootBefore;cloudBefore=$cloudBefore} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$Result/recovery.json"
    $ready=$true
    Assert-StageRigLease -Path $lease.path -Token $lease.token
    if([double](& $ownerInput -IdleSeconds) -lt 300) { throw 'Owner input resumed during preparation; no launch.' }
    $start=[Diagnostics.ProcessStartInfo]::new()
    $start.FileName=Join-Path $GameDir 'artofrally.exe'; $start.WorkingDirectory=$GameDir; $start.UseShellExecute=$false
    $start.EnvironmentVariables['DBCE_ART_SHIM_REQUEST']=$requestPath
    $start.EnvironmentVariables['SteamAppId']='550320'; $start.EnvironmentVariables['SteamGameId']='550320'
    $launchAttempted=$true
    $game=[Diagnostics.Process]::Start($start)
    $processStart=$game.StartTime.ToUniversalTime(); $game.Refresh()
    if(![string]::Equals($game.MainModule.FileName,$start.FileName,[StringComparison]::OrdinalIgnoreCase)) { throw 'Launched executable path differs; no process action authorized.' }
    @{pid=$game.Id;startUtc=$processStart.ToString('o');executable=$game.MainModule.FileName;requestSha256=(Hash $requestPath);lease=$lease} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/launch.json"
    Write-Output "Disposable Art started: pid=$($game.Id), path=$($game.MainModule.FileName). No input sent."
    $deadline=[DateTime]::UtcNow.AddSeconds($ObserveSeconds)
    while(!$game.HasExited -and !(Test-Path -LiteralPath "$Result/probe-receipt.json") -and [DateTime]::UtcNow -lt $deadline) {
        Assert-StageRigLease -Path $lease.path -Token $lease.token
        if(@(Get-Process artofrally -ErrorAction SilentlyContinue | Where-Object Id -NE $game.Id).Count) { throw 'Another Art process appeared; do not answer Steam session dialogs.' }
        if([double](& $ownerInput -IdleSeconds) -lt 5) { 'Owner input resumed; ending observation.' | Set-Content -LiteralPath "$Result/interrupted.txt"; break }
        $null=$game.WaitForExit(500); $game.Refresh()
    }
    @{receiptPresent=(Test-Path -LiteralPath "$Result/probe-receipt.json");exited=$game.HasExited;windowTitle=$game.MainWindowTitle;windowHandle=$game.MainWindowHandle.ToInt64();observedUtc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath "$Result/observation.json"
    if(!(Test-Path -LiteralPath "$Result/probe-receipt.json")) { Write-Warning 'No probe receipt; inspect the retained candidate/Unity logs. No input sent.' }
} finally {
    if($game -and !$game.HasExited) {
        $game.Refresh()
        # Close only this exact child identity. Never command a Steam replacement.
        if($game.StartTime.ToUniversalTime() -ne $processStart -or ![string]::Equals($game.MainModule.FileName,(Join-Path $GameDir 'artofrally.exe'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Child identity changed; backup and lease retained.' }
        $null=$game.CloseMainWindow(); $null=$game.WaitForExit(20000)
    }
    Assert-GameClosed
    $errors=[Collections.Generic.List[string]]::new()
    function Attempt([scriptblock]$work) { try { & $work } catch { $errors.Add($_.Exception.Message); Write-Warning $_.Exception.Message } }
    if($ready -and $launchAttempted) {
        foreach($item in $snapshots) { Attempt {
            Plain $item.path; Plain $item.backup
            if($item.before -ne 'absent' -and (Hash $item.backup) -ne $item.before) { throw 'Recovery backup mismatch.' }
            if(Test-Path -LiteralPath $item.path -PathType Leaf) { Copy-Item -LiteralPath $item.path -Destination (Join-Path $Result ('after/'+[IO.Path]::GetFileName($item.backup))) }
            if($item.before -eq 'absent') { if(Test-Path -LiteralPath $item.path) { Remove-Item -LiteralPath $item.path } }
            else { New-Item -ItemType Directory -Path (Split-Path $item.path) -Force | Out-Null; Copy-Item -LiteralPath $item.backup -Destination $item.path -Force }
            if((Hash $item.path) -ne $item.before) { throw "Shared file restoration mismatch: $($item.path)" }
        } }
        foreach($p in @((Files $localLow | Where-Object { $_ -notin $rootBefore })) + @((Files $cloud -Recurse | Where-Object { $_ -notin $cloudBefore }))) { Attempt {
            Plain $p
            if((Split-Path $p -Parent) -ne $localLow -and !(Within $p $cloud)) { throw 'New file escaped Art shared scope.' }
            Copy-Item -LiteralPath $p -Destination (Join-Path $Result ('after/new-'+[guid]::NewGuid().ToString('N')))
            Remove-Item -LiteralPath $p
        } }
    }
    if($environmentSaved -and $launchAttempted) { Attempt { Restore-SessionEnvironment "$Result/shared-environment" } }
    foreach($pin in $ownerPins) { Attempt { if((Hash $pin.path) -ne $pin.sha256) { throw "Owner file differs; retained for review, never overwritten: $($pin.path)" } } }
    if($errors.Count) { $errors | Set-Content -LiteralPath "$Result/restore-errors.txt"; throw "Restoration incomplete; backup and lease retained: $Result" }
    # A pre-launch refusal wrote no shared state and must not overwrite a newer
    # owner preference with an unnecessary restoration.
    [DateTime]::UtcNow.ToString('o') | Set-Content -LiteralPath "$Result/restored.txt"
    $null=Exit-StageRigLease -Lease $lease
}
