#Requires -Version 7.0
<# Developer-only production Apply + raw-observation slot. Requires a separately
   reviewed native candidate. Holds the lease through normal exit and restoration.
   Sends no raw input itself: use Send-ControlsCommand with command-context.json. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory)][string]$NativeCandidate,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$NativeSha256,
    [string]$GameDir='D:/Program Files (x86)/Steam/steamapps/common/artofrally',
    [string]$WheelkitRepo='E:/Source/toolkits/dbce-wheelkit',
    [string]$ToolkitRepo='E:/Source/toolkits/dbce-wheel-mod-toolkit',
    [string]$HubRepo='E:/Source/dbce-project-mgmt',
    [string]$Profiles=(Join-Path $env:LOCALAPPDATA 'Wheelkit/mapping-profiles.json'),
    [ValidateRange(30,300)][int]$ObserveSeconds=180
)
$ErrorActionPreference='Stop'
function Full([string]$p) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($p) }
$Result=Full $Result; $GameDir=Full $GameDir; $NativeCandidate=Full $NativeCandidate
if(Test-Path -LiteralPath $Result) { throw 'Result directory must be new.' }
function Assert-GameClosed { if(Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Art is running; no installation or restoration.' } }
Assert-GameClosed
if((Get-FileHash -LiteralPath $NativeCandidate).Hash -ne $NativeSha256) { throw 'Native candidate hash mismatch.' }
$probe=Join-Path $PSScriptRoot 'Recorder/bin/Release/net48/ArtOfSimRally.DevRecorder.dll'
if(!(Test-Path -LiteralPath $probe)) { throw 'Build and review the developer probe first.' }
$artRoot=Full (Join-Path $PSScriptRoot '../..')
$customRally=Join-Path $env:USERPROFILE 'AppData/LocalLow/Funselektor Labs/Art of Rally/customrally'
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
. "$ToolkitRepo/tools/playback/Stage-RigLease.ps1"
$request=Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/controls-request.txt'
$switch=Join-Path $env:LOCALAPPDATA 'dbce/art-of-rally/inject.on'
foreach($p in @($request,$switch,(Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/session-request.txt'))) {
    if(Test-Path -LiteralPath $p) { throw "Existing developer request/switch must be resolved first: $p" }
}
if([double](& "$HubRepo/tools/Owner-Input.ps1" -IdleSeconds) -lt 300) { throw 'Owner input is recent.' }
$lease=Enter-StageRigLease -Path (Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt') -Owner hula-art-controls -Purpose 'Production Apply and native raw-input observation; no force; exact restore'
$game=$null; $snapshots=@(); $ready=$false; $applied=$false; $environmentSaved=$false; $restored=$false
function Hash([string]$p) { if(Test-Path -LiteralPath $p -PathType Leaf){return (Get-FileHash -LiteralPath $p).Hash}; return 'absent' }
function Assert-PlainPath([string]$p) {
    $cursor=Full $p
    while($cursor) {
        if((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked test path refused.' }
        $cursor=Split-Path -Parent $cursor
    }
}
function Save-File([string]$p) {
    Assert-PlainPath $p
    if($script:snapshots.path -contains $p){return}
    $index=$script:snapshots.Count; $before=Hash $p; $backup=Join-Path $Result "owner-files/$index.bin"
    if($before -ne 'absent') { Copy-Item -LiteralPath $p -Destination $backup; if((Hash $backup) -ne $before){throw 'Snapshot changed during copy.'} }
    $script:snapshots+=@([pscustomobject]@{path=$p;before=$before;backup=$backup})
}
try {
    Assert-GameClosed
    New-Item -ItemType Directory -Path "$Result/owner-files","$Result/harness","$Result/after" | Out-Null
    Save-SessionEnvironment "$Result/owner-environment"; $environmentSaved=$true
    $payloads=@{
        'Mods/ArtOfSimRally/UnityForceFeedback.dll'=$NativeCandidate
        'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll'=$NativeCandidate
        'Mods/ArtOfSimRally.DevRecorder/ArtOfSimRally.DevRecorder.dll'=$probe
        'Mods/ArtOfSimRally.DevRecorder/Info.json'=(Join-Path $PSScriptRoot 'Recorder/Info.json')
        'Mods/ArtOfSimRally.DevRecorder/Dbce.Wheel.Playback.dll'=(Join-Path $artRoot 'lib/playback/Dbce.Wheel.Playback.dll')
    }
    foreach($relative in $payloads.Keys) { Save-File (Join-Path $GameDir $relative) }
    foreach($relative in @('artofrally_Data/Managed/UnityModManager/Log.txt','artofrally_Data/Managed/UnityModManager/Params.xml')) { Save-File (Join-Path $GameDir $relative) }
    foreach($name in @('ffb.log','ffb.previous.log','last-session-frame-health.xml')) { Save-File (Join-Path $env:LOCALAPPDATA "ArtOfSimRally/$name") }
    $localLow=Split-Path $customRally -Parent
    foreach($p in Get-ChildItem -LiteralPath $localLow -File) { Save-File $p.FullName }
    $cloud=Join-Path $localLow 'cloud'
    $cloudBefore=@(Get-ChildItem -LiteralPath $cloud -File -Recurse | ForEach-Object FullName)
    foreach($p in $cloudBefore) { Save-File $p }
    $snapshots | ConvertTo-Json -Depth 4 | Set-Content "$Result/owner-files.json"
    $ready=$true # all recovery bytes exist before any owner file is written
    # Independent output interlocks also cover a probe that refuses to load.
    $settings=Join-Path $GameDir 'Mods/ArtOfSimRally/Settings.xml'
    [xml]$xml=Get-Content -LiteralPath $settings -Raw
    foreach($key in @('ForceFeedbackEnabled','LandingEffectsEnabled','CrashEffectsEnabled','ShiftEffectsEnabled','TelemetryEnabled')) {
        if(!$xml.Settings.$key) { throw "Missing output interlock $key" }
        $xml.Settings.$key='false'
    }
    $xml.Save($settings)
    $sourceHarness=Join-Path $WheelkitRepo 'tools/ConfigurationQualification/bin/Release/net10.0-windows'
    Copy-Item "$sourceHarness/*" "$Result/harness/" -Recurse
    $harness=Join-Path $Result 'harness/ConfigurationQualification.dll'
    $live=Join-Path $Result 'apply'
    & dotnet $harness prepare-live art-of-rally $GameDir "$WheelkitRepo/src/Wheelkit.App/Data/catalog-seed.json" $Profiles $live
    if($LASTEXITCODE){throw 'Production Apply preparation failed.'}
    & dotnet $harness apply-live $live
    if($LASTEXITCODE){throw 'Production Apply failed.'}
    $applied=$true
    & dotnet $harness raw-workload "$live/profile.json" "$Result/raw-workload.json"
    if($LASTEXITCODE){throw 'Independent raw workload failed.'}
    foreach($relative in $payloads.Keys) {
        $destination=Join-Path $GameDir $relative; Assert-PlainPath $destination
        New-Item -ItemType Directory -Path (Split-Path $destination) -Force | Out-Null
        Copy-Item -LiteralPath $payloads[$relative] -Destination $destination -Force
        if((Hash $destination) -ne (Hash $payloads[$relative])) { throw 'Temporary payload readback failed.' }
        if($payloads[$relative] -eq $NativeCandidate -and (Hash $destination) -ne $NativeSha256) { throw 'Native candidate changed during preparation.' }
    }
    $nonce=[guid]::NewGuid().ToString('N'); $trace=Join-Path $Result 'trace'
    $values=@{expiresUtc=[DateTime]::UtcNow.AddMinutes(5).ToString('o');nonce=$nonce;modSha256=(Hash (Join-Path $GameDir 'Mods/ArtOfSimRally/ArtOfSimRally.Mod.dll'));settingsSha256=(Hash $settings);nativeSha256=$NativeSha256;out=$trace}
    New-Item -ItemType Directory -Path (Split-Path $switch) -Force | Out-Null
    [IO.File]::WriteAllText($switch,$nonce)
    ($values.GetEnumerator()|ForEach-Object {"$($_.Key)=$($_.Value)"}) | Set-Content -LiteralPath $request
    Copy-Item -LiteralPath $request -Destination "$Result/request.txt"
    Start-Process 'steam://rungameid/550320'
    $deadline=[DateTime]::UtcNow.AddSeconds(90)
    while(!$game -and [DateTime]::UtcNow -lt $deadline) { $games=@(Get-Process artofrally -ErrorAction SilentlyContinue); if($games.Count -gt 1){throw 'Multiple Art processes'}; if($games.Count -eq 1){$game=$games[0]}; Start-Sleep -Milliseconds 500 }
    if(!$game){throw 'Game did not launch; do not answer Steam session prompts.'}
    $start=$game.StartTime.ToUniversalTime()
    if(![string]::Equals($game.MainModule.FileName,(Join-Path $GameDir 'artofrally.exe'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected executable launched.' }
    @{GameProcessId=$game.Id;ExpectedStartTimeUtc=$start.ToString('o');Nonce=$nonce;GameDir=$GameDir;Lease=$lease;Result=$Result} | ConvertTo-Json -Depth 4 | Set-Content "$Result/command-context.json"
    $deadline=[DateTime]::UtcNow.AddSeconds(45)
    while(!(Test-Path "$trace/identity.txt") -and !$game.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500; $game.Refresh() }
    if(!(Test-Path "$trace/identity.txt")){throw 'Probe did not confirm its native no-force fence.'}
    & "$PSScriptRoot/Send-ControlsCommand.ps1" -Command Status -Nonce $nonce -GameProcessId $game.Id -ExpectedStartTimeUtc $start -GameDir $GameDir
    $deadline=[DateTime]::UtcNow.AddSeconds($ObserveSeconds)
    Write-Output "Armed. Select independent raw samples after inspecting state. Context: $Result/command-context.json"
    while(!$game.HasExited -and !(Test-Path "$trace/result.txt") -and [DateTime]::UtcNow -lt $deadline) {
        Assert-StageRigLease -Path $lease.path -Token $lease.token
        Start-Sleep -Seconds 2; $game.Refresh()
    }
    if(!$game.HasExited -and !(Test-Path "$trace/result.txt")) {
        & "$PSScriptRoot/Send-ControlsCommand.ps1" -Command Stop -Nonce $nonce -GameProcessId $game.Id -ExpectedStartTimeUtc $start -GameDir $GameDir
    }
} finally {
    if($game -and !$game.HasExited) { $null=$game.CloseMainWindow(); $null=$game.WaitForExit(20000) }
    if(Get-Process artofrally -ErrorAction SilentlyContinue) { throw "Game remains open. Recovery retained at $Result; lease retained. No replacement process was commanded." }
    $restoreErrors=[Collections.Generic.List[string]]::new()
    function Attempt-Restore([scriptblock]$work) {
        try { & $work } catch { $restoreErrors.Add($_.Exception.Message); Write-Warning $_.Exception.Message }
    }
    try {
        if($ready) {
            foreach($p in @($request,$switch)) { Attempt-Restore { if(Test-Path -LiteralPath $p){Copy-Item -LiteralPath $p -Destination "$Result/after/$([IO.Path]::GetFileName($p))";Remove-Item -LiteralPath $p} } }
            if($nonce) { foreach($taken in Get-ChildItem -LiteralPath (Split-Path $request) -Filter 'controls-request.txt.*.taken') { Attempt-Restore {
                if((Get-Content -LiteralPath $taken.FullName -Raw).Contains("nonce=$nonce")) { Copy-Item -LiteralPath $taken.FullName -Destination "$Result/after/"; Remove-Item -LiteralPath $taken.FullName }
            } } }
            if($applied) { Attempt-Restore { & dotnet $harness verify-live $live; "verifyExit=$LASTEXITCODE" | Set-Content "$Result/verify.txt" } }
            if(Test-Path "$Result/apply/apply.json") { Attempt-Restore { & dotnet $harness restore-live $live; if($LASTEXITCODE){throw 'Apply restore failed.'} } }
            foreach($item in $snapshots) { Attempt-Restore {
                Assert-GameClosed; Assert-PlainPath $item.path
                if($item.before -ne 'absent' -and (Hash $item.backup) -ne $item.before) { throw 'Recovery backup mismatch.' }
                if(Test-Path -LiteralPath $item.path -PathType Leaf) { Copy-Item -LiteralPath $item.path -Destination (Join-Path $Result ('after/'+[IO.Path]::GetFileName($item.backup))) }
                if($item.before -eq 'absent') { if(Test-Path -LiteralPath $item.path){Remove-Item -LiteralPath $item.path} }
                else { New-Item -ItemType Directory -Path (Split-Path $item.path) -Force | Out-Null; Copy-Item -LiteralPath $item.backup -Destination $item.path -Force }
                if((Hash $item.path) -ne $item.before){throw 'Owner file restoration mismatch.'}
            } }
            Attempt-Restore { foreach($p in Get-ChildItem -LiteralPath $cloud -File -Recurse | Where-Object FullName -NotIn $cloudBefore) {
                Assert-PlainPath $p.FullName
                Copy-Item -LiteralPath $p.FullName -Destination (Join-Path $Result ('after/new-cloud-'+[guid]::NewGuid().ToString('N')))
                Remove-Item -LiteralPath $p.FullName
            } }
        }
        if($environmentSaved) { Attempt-Restore { Restore-SessionEnvironment "$Result/owner-environment" } }
        if($restoreErrors.Count) {
            $restoreErrors | Set-Content "$Result/restore-errors.txt"
            throw "Restoration incomplete; independent restores attempted. Recovery and lease retained: $Result"
        }
        $restored=$true
        if(Test-Path -LiteralPath $Result){[DateTime]::UtcNow.ToString('o') | Set-Content "$Result/restored.txt"}
    } finally { if($restored){$null=Exit-StageRigLease -Lease $lease} }
}
