#Requires -Version 7.0
# Developer-only, coordinator-serialized Drive probe (plan a73b4fe, romi 30460). The prepared request
# (Prepare-ShimProbe -Mode Drive -Recorder -StageCopy) is the probe's configuration. The dev recorder's capture
# driver replays its taped menu route to the stage, leaves the car to the shared shim (carInput=shared) and tapes
# it; the probe drives bounded raw stimuli at the stage start. Muted (DBCE_FFB_MUTE=1 plus the recorder's output and
# save/upload guards). Never installs into the owner game, sends OS input or answers Steam prompts.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Result,
      # The recorder session whose taped menu route reaches the stage (rc13 muted synthetic capture: Japan6, CUSTOM).
      [Parameter(Mandatory)][string]$RouteSource,
      [switch]$PreflightOnly,
      [ValidateRange(60,600)][int]$ObserveSeconds=300,
      [ValidateRange(20,120)][int]$CaptureSeconds=60,
      [string]$ToolkitRepo='E:/Source/toolkits/dbce-wheel-mod-toolkit',
      [string]$HubRepo='E:/Source/dbce-project-mgmt')
$ErrorActionPreference='Stop'
function Full([string]$p) { [IO.Path]::GetFullPath($p).TrimEnd('\') }
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
$RouteSource=Full $RouteSource; Plain $RouteSource
foreach($n in @('input.tape','markers.tsv','session.txt')) { if(!(Test-Path -LiteralPath (Join-Path $RouteSource $n) -PathType Leaf)) { throw "Route source lacks $n" } }
$requestPath=Join-Path $Result 'request.json'
$request=Get-Content -LiteralPath $requestPath -Raw | ConvertFrom-Json
$prepared=Get-Content -LiteralPath (Join-Path $Result 'prepared.json') -Raw | ConvertFrom-Json
$GameDir=Full $request.GameRoot; $owner=Full $request.SourceGameRoot
foreach($p in @($GameDir,$owner)) { Plain $p }
if($request.Schema -ne 1 -or $request.Mode -ne 'Drive' -or (Full $request.Result) -ne $Result -or
    (Within $GameDir $owner) -or (Within $owner $GameDir) -or (Within $Result $GameDir) -or (Within $Result $owner) -or (Within $RouteSource $GameDir)) { throw 'Invalid disposable Drive request scope.' }
if([DateTime]::Parse($request.ExpiresUtc).ToUniversalTime() -lt [DateTime]::UtcNow.AddSeconds($ObserveSeconds+30)) { throw 'Prepare a new request with enough time for the complete drive.' }
if(Test-Path -LiteralPath "$Result/launch.json") { throw 'Request has already been launched; use a fresh result.' }
if(@($prepared.RecorderFiles).Count -ne 3) { throw 'Prepare with -Recorder: the Drive needs the dev recorder staged.' }
$mod=Join-Path $GameDir 'Mods/ArtOfSimRally'; $recorderDir=Join-Path $GameDir 'Mods/ArtOfSimRally.DevRecorder'
$pins=@{
    (Join-Path $mod 'ArtOfSimRally.Mod.dll')=$request.ModSha256
    (Join-Path $mod 'UnityForceFeedback.dll')=$request.NativeSha256
    (Join-Path $GameDir 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')=$request.NativeSha256
    (Join-Path $mod 'Settings.xml')=$request.SettingsSha256
    ([string]$request.ShimPath)=$request.ShimSha256
    ([string]$request.ProfilePath)=$request.ProfileSha256
}
foreach($file in $prepared.RecorderFiles) { $pins[(Join-Path $recorderDir $file.Name)]=$file.Sha256 }
function Assert-Pins {
    foreach($p in $pins.Keys) { Plain $p; if((Hash $p) -ne $pins[$p]) { throw "Candidate pin differs: $p" } }
    if((Hash (Join-Path $owner 'Mods/ArtOfSimRally/Settings.xml')) -ne $prepared.SettingsSourceSha256) { throw 'Owner settings changed since preparation.' }
    if((Get-Content -LiteralPath (Join-Path $GameDir 'steam_appid.txt') -Raw).Trim() -cne '550320') { throw 'Copy-only Steam app id missing; direct launch could redirect through Steam.' }
}
Assert-Pins
$otherMods=@(Get-ChildItem -LiteralPath (Join-Path $GameDir 'Mods') -Directory | Where-Object Name -NotIn @('ArtOfSimRally','ArtOfSimRally.DevRecorder'))
if($otherMods.Count) { throw 'Disposable copy contains optional mod folders; stage only ArtOfSimRally and the recorder.' }
[xml]$umm=Get-Content -LiteralPath (Join-Path $GameDir 'artofrally_Data/Managed/UnityModManager/Params.xml') -Raw
if(@($umm.Param.ModParams.Mod | Where-Object { $_.Id -eq 'ArtOfSimRally' -and $_.Enabled -eq 'true' }).Count -ne 1) { throw 'Candidate UMM mod is not enabled.' }
if(@($umm.Param.ModParams.Mod | Where-Object { $_.Id -eq 'ArtOfSimRally.DevRecorder' -and $_.Enabled -eq 'false' }).Count) { throw 'The recorder is disabled in the copy UMM parameters.' }
$sessionRequest=Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/session-request.txt'
foreach($p in @('ArtOfSimRally/controls-request.txt','ArtOfSimRally/session-request.txt','dbce/art-of-rally/inject.on')) {
    if(Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA $p)) { throw "Existing developer marker refused: $p" }
}
Assert-GameClosed
. (Join-Path $PSScriptRoot 'SteamLaunchContext.ps1')
$context=Get-SteamLaunchContext
$context | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$Result/preflight-steam-context.json"
if($context.decision -eq 'refuse') { throw "Steam launch context refused: $($context.reason)" }
if($PreflightOnly) { Write-Output "Drive pins, recorder, settings, app id, UMM, markers and Steam context ($($context.decision)) checks passed. No launch or shared-state mutation."; return }
$ownerInput=Join-Path $HubRepo 'tools/Owner-Input.ps1'
if([double](& $ownerInput -IdleSeconds) -lt 300) { throw 'Owner input is recent; no launch or shared-state mutation.' }
$localLow=Join-Path $env:USERPROFILE 'AppData/LocalLow/Funselektor Labs/Art of Rally'
$customRally=Join-Path $localLow 'customrally' # read by SessionEnvironment.ps1's Get-SessionFiles
$artLocal=Join-Path $env:LOCALAPPDATA 'ArtOfSimRally'
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
. (Join-Path $ToolkitRepo 'tools/playback/Stage-RigLease.ps1')
$snapshots=[Collections.Generic.List[object]]::new(); $ownerPins=[Collections.Generic.List[object]]::new()
$lease=$null; $game=$null; $ready=$false; $environmentSaved=$false; $launchAttempted=$false; $localBefore=@(); $artBefore=@(); $processStart=$null
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
$lease=Enter-StageRigLease -Path (Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt') -Owner art-shim-drive -Purpose 'Disposable Art shared-input Drive at a recorder-reached stage; muted, no force or OS input; restore shared state'
try {
    Assert-GameClosed
    New-Item -ItemType Directory -Path "$Result/shared-files","$Result/after" | Out-Null
    foreach($p in @((Files (Join-Path $owner 'Mods') -Recurse)) + @(
        (Join-Path $owner 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll'),
        (Join-Path $owner 'artofrally_Data/Managed/UnityModManager/Params.xml'),
        (Join-Path $owner 'steam_appid.txt'))) { Plain $p; $ownerPins.Add([pscustomobject]@{path=$p;sha256=(Hash $p)}) }
    $ownerPins | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/owner-pins.json"
    Save-SessionEnvironment "$Result/shared-environment"; $environmentSaved=$true
    # Art's local folder (logs, frame health, the recorder's request marker) and LocalLow (Unity, saves).
    $artBefore=@(Files $artLocal -Recurse | Where-Object { $_ -notmatch '\\(dev-captures|crash-feel)\\' })
    foreach($p in $artBefore) { Save-File $p }
    foreach($p in @('DBCE/TripleScreen/games/art-of-rally/status.json','DBCE/TripleScreen/games/art-of-rally/render-diagnostics.json')) { Save-File (Join-Path $env:LOCALAPPDATA $p) }
    $localBefore=@(Files $localLow -Recurse)
    foreach($p in $localBefore) { Save-File $p }
    $snapshots | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/shared-files.json"
    @{schema=1;gameDir=$GameDir;owner=$owner;lease=$lease;localBefore=$localBefore;artBefore=$artBefore} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$Result/recovery.json"
    $ready=$true
    Assert-StageRigLease -Path $lease.path -Token $lease.token
    if([double](& $ownerInput -IdleSeconds) -lt 300) { throw 'Owner input resumed during preparation; no launch.' }
    Assert-Pins # close the preparation-to-launch gap while holding the lease
    $context=Get-SteamLaunchContext
    if($context.decision -eq 'refuse') { $context | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$Result/launch-steam-context.json"; throw "Steam launch context refused: $($context.reason)" }
    # The recorder's request (a shared marker, absent before; restoration removes it and its .taken rename).
    $capture=Join-Path $Result 'capture'
    @("mode=record","tape=$capture","captureDriver=$RouteSource","carInput=shared","captureSeconds=$CaptureSeconds",
      "stopWhenFile=$(Join-Path $Result 'probe-receipt.json')","muteOutputs=1","expiresUtc=$([DateTime]::UtcNow.AddMinutes(10).ToString('o'))") |
        Set-Content -LiteralPath $sessionRequest -Encoding ascii
    Copy-Item -LiteralPath $sessionRequest -Destination (Join-Path $Result 'session-request.txt')
    $exe=Join-Path $GameDir 'artofrally.exe'
    $childEnvironment=[ordered]@{
        DBCE_ART_SHIM_REQUEST=$requestPath
        SteamAppId='550320'; SteamGameId='550320'
        DBCE_FFB_MUTE='1' # native load-time latch precedes managed request validation
        DBCE_FFB_LOG_PATH=(Join-Path $Result 'native.log')
    }
    $brokerSha256=$null
    if($context.decision -eq 'direct') {
        $start=[Diagnostics.ProcessStartInfo]::new()
        $start.FileName=$exe; $start.WorkingDirectory=$GameDir; $start.UseShellExecute=$false
        foreach($name in $childEnvironment.Keys) { $start.EnvironmentVariables[$name]=$childEnvironment[$name] }
        $launchAttempted=$true
        $game=[Diagnostics.Process]::Start($start)
    } else {
        $broker=Join-Path $Result 'launch-broker.cmd'
        $brokerSha256=New-ChildEnvironmentBroker -Path $broker -Executable $exe -WorkingDirectory $GameDir -Environment $childEnvironment
        $launchUtc=[DateTime]::UtcNow; $launchAttempted=$true
        Start-MediumBroker $broker
        $adoptDeadline=[DateTime]::UtcNow.AddSeconds(15)
        while(!$game -and [DateTime]::UtcNow -lt $adoptDeadline) {
            $candidates=@(Get-Process artofrally -ErrorAction SilentlyContinue)
            if($candidates.Count -gt 1) { throw 'More than one Art process appeared after the broker launch; do not answer Steam session dialogs.' }
            if($candidates.Count -eq 1 -and $candidates[0].StartTime.ToUniversalTime() -ge $launchUtc.AddSeconds(-1)) { $game=$candidates[0]; $null=$game.SafeHandle }
            else { Start-Sleep -Milliseconds 100 }
        }
        if(!$game) { throw 'Broker launch produced no Art process; nothing to close.' }
    }
    $processStart=$game.StartTime.ToUniversalTime(); $imageDeadline=[DateTime]::UtcNow.AddSeconds(5); $imagePath=$null
    while(!$imagePath -and !$game.HasExited -and [DateTime]::UtcNow -lt $imageDeadline) {
        try { $game.Refresh(); $imagePath=$game.MainModule.FileName } catch [ComponentModel.Win32Exception] { }
        if(!$imagePath) { $null=$game.WaitForExit(100) }
    }
    if(![string]::Equals($imagePath,$exe,[StringComparison]::OrdinalIgnoreCase)) { throw 'Launched executable path unavailable or differs; no replacement process action authorized.' }
    $gameIntegrity=try { Get-ProcessIntegrity $game.Id } catch { $null }
    $matchesSteam=$null -ne $gameIntegrity -and $gameIntegrity.integrity -eq $context.steam[0].integrity
    @{pid=$game.Id;startUtc=$processStart.ToString('o');executable=$imagePath;requestSha256=(Hash $requestPath);routeSource=$RouteSource;lease=$lease
        launchMode=$context.decision;brokerSha256=$brokerSha256;steam=$context.steam[0];launcher=$context.launcher;game=$gameIntegrity;integrityMatchesSteam=$matchesSteam} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/launch.json"
    if(!$matchesSteam) { throw 'Launched copy integrity differs from Steam; SteamAPI_Init cannot succeed. Closing the exact child without observation.' }
    Write-Output "Disposable Art started (Drive): pid=$($game.Id), path=$imagePath. No OS input sent."
    # Done when the probe has its receipt and the recorder has closed its capture (or the bound).
    $deadline=[DateTime]::UtcNow.AddSeconds($ObserveSeconds)
    while(!$game.HasExited -and [DateTime]::UtcNow -lt $deadline -and !((Test-Path -LiteralPath "$Result/probe-receipt.json") -and (Test-Path -LiteralPath (Join-Path $capture 'end.txt')))) {
        Assert-StageRigLease -Path $lease.path -Token $lease.token
        if(@(Get-Process artofrally -ErrorAction SilentlyContinue | Where-Object Id -NE $game.Id).Count) { throw 'Another Art process appeared; do not answer Steam session dialogs.' }
        if([double](& $ownerInput -IdleSeconds) -lt 5) { 'Owner input resumed; ending observation.' | Set-Content -LiteralPath "$Result/interrupted.txt"; break }
        $null=$game.WaitForExit(500); $game.Refresh()
    }
    @{receiptPresent=(Test-Path -LiteralPath "$Result/probe-receipt.json");captureEnded=(Test-Path -LiteralPath (Join-Path $capture 'end.txt'));exited=$game.HasExited;windowTitle=$game.MainWindowTitle;observedUtc=[DateTime]::UtcNow.ToString('o')} |
        ConvertTo-Json | Set-Content -LiteralPath "$Result/observation.json"
    if(!(Test-Path -LiteralPath "$Result/probe-receipt.json")) { Write-Warning 'No probe receipt; inspect the retained candidate/Unity logs. No OS input sent.' }
} finally {
    if($game -and !$game.HasExited) {
        $game.Refresh()
        # Close only this exact child identity. Never command a Steam replacement.
        if($game.StartTime.ToUniversalTime() -ne $processStart -or ![string]::Equals($game.MainModule.FileName,(Join-Path $GameDir 'artofrally.exe'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Child identity changed; backup and lease retained.' }
        $null=$game.CloseMainWindow(); $null=$game.WaitForExit(20000)
    }
    Assert-GameClosed
    $umLog=Join-Path $GameDir 'artofrally_Data/Managed/UnityModManager/Log.txt'
    if(Test-Path -LiteralPath $umLog) { Copy-Item -LiteralPath $umLog -Destination (Join-Path $Result 'umm-log.txt') }
    $errors=[Collections.Generic.List[string]]::new()
    function Attempt([scriptblock]$work) { try { & $work } catch { $errors.Add($_.Exception.Message); Write-Warning $_.Exception.Message } }
    if($ready) {
        # The recorder's request marker never outlives the run, launched or not.
        Attempt { if(Test-Path -LiteralPath $sessionRequest) { Remove-Item -LiteralPath $sessionRequest } }
    }
    if($ready -and $launchAttempted) {
        foreach($item in $snapshots) { Attempt {
            Plain $item.path; Plain $item.backup
            if($item.before -ne 'absent' -and (Hash $item.backup) -ne $item.before) { throw 'Recovery backup mismatch.' }
            if(Test-Path -LiteralPath $item.path -PathType Leaf) { Copy-Item -LiteralPath $item.path -Destination (Join-Path $Result ('after/'+[IO.Path]::GetFileName($item.backup))) }
            if($item.before -eq 'absent') { if(Test-Path -LiteralPath $item.path) { Remove-Item -LiteralPath $item.path } }
            else { New-Item -ItemType Directory -Path (Split-Path $item.path) -Force | Out-Null; Copy-Item -LiteralPath $item.backup -Destination $item.path -Force }
            if((Hash $item.path) -ne $item.before) { throw "Shared file restoration mismatch: $($item.path)" }
        } }
        # New files (the recorder's .taken request, Unity output) are kept as evidence, then removed.
        foreach($scope in @(@{root=$localLow;before=$localBefore}, @{root=$artLocal;before=$artBefore})) {
            foreach($p in @(Files $scope.root -Recurse | Where-Object { $_ -notin $scope.before -and $_ -notmatch '\\(dev-captures|crash-feel)\\' })) { Attempt {
                Plain $p
                if(!(Within $p $scope.root)) { throw 'New file escaped Art shared scope.' }
                $keep=Join-Path $Result ('after/new/'+[IO.Path]::GetRelativePath($scope.root,$p).Replace('\','__'))
                New-Item -ItemType Directory -Path (Split-Path $keep) -Force | Out-Null
                Copy-Item -LiteralPath $p -Destination $keep
                Remove-Item -LiteralPath $p
            } }
        }
    }
    if($environmentSaved -and $launchAttempted) { Attempt { Restore-SessionEnvironment "$Result/shared-environment" } }
    foreach($pin in $ownerPins) { Attempt { if((Hash $pin.path) -ne $pin.sha256) { throw "Owner file differs; retained for review, never overwritten: $($pin.path)" } } }
    if($errors.Count) { $errors | Set-Content -LiteralPath "$Result/restore-errors.txt"; throw "Restoration incomplete; backup and lease retained: $Result" }
    [DateTime]::UtcNow.ToString('o') | Set-Content -LiteralPath "$Result/restored.txt"
    $null=Exit-StageRigLease -Lease $lease
}
