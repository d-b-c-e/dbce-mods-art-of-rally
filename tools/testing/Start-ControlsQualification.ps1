#Requires -Version 7.0
<# Developer-only production Apply + raw-observation slot. Requires a separately
   reviewed native candidate. Holds the lease through normal exit and restoration.
   Sends no raw input itself: use Send-ControlsCommand with command-context.json. #>
[CmdletBinding(DefaultParameterSetName='Run')]
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory,ParameterSetName='Run')][string]$NativeCandidate,
    [Parameter(Mandatory,ParameterSetName='Run')][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$NativeSha256,
    [Parameter(Mandatory,ParameterSetName='Recover')][switch]$Recover,
    [string]$GameDir='D:/Program Files (x86)/Steam/steamapps/common/artofrally',
    [string]$WheelkitRepo='E:/Source/toolkits/dbce-wheelkit',
    [string]$ToolkitRepo='E:/Source/toolkits/dbce-wheel-mod-toolkit',
    [string]$HubRepo='E:/Source/dbce-project-mgmt',
    [string]$Profiles=(Join-Path $env:LOCALAPPDATA 'Wheelkit/mapping-profiles.json'),
    [ValidateRange(30,300)][int]$ObserveSeconds=180
)
$ErrorActionPreference='Stop'
function Full([string]$p) { $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($p) }
$Result=Full $Result; $GameDir=Full $GameDir
if(!$Recover -and (Test-Path -LiteralPath $Result)) { throw 'Result directory must be new.' }
function Assert-GameClosed { if(Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Art is running; no installation or restoration.' } }
Assert-GameClosed
if(!$Recover) {
    $NativeCandidate=Full $NativeCandidate
    if((Get-FileHash -LiteralPath $NativeCandidate).Hash -ne $NativeSha256) { throw 'Native candidate hash mismatch.' }
}
$probe=Join-Path $PSScriptRoot 'Recorder/bin/Release/net48/ArtOfSimRally.DevRecorder.dll'
if(!$Recover -and !(Test-Path -LiteralPath $probe)) { throw 'Build and review the developer probe first.' }
$artRoot=Full (Join-Path $PSScriptRoot '../..')
$customRally=Join-Path $env:USERPROFILE 'AppData/LocalLow/Funselektor Labs/Art of Rally/customrally'
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
. "$ToolkitRepo/tools/playback/Stage-RigLease.ps1"
$request=Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/controls-request.txt'
$switch=Join-Path $env:LOCALAPPDATA 'dbce/art-of-rally/inject.on'
foreach($p in @($request,$switch,(Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/session-request.txt'))) {
    if(!$Recover -and (Test-Path -LiteralPath $p)) { throw "Existing developer request/switch must be resolved first: $p" }
}
if(!$Recover -and [double](& "$HubRepo/tools/Owner-Input.ps1" -IdleSeconds) -lt 300) { throw 'Owner input is recent.' }
$lease=$null
$game=$null; $snapshots=@(); $ready=$false; $applied=$false; $environmentSaved=$false; $restored=$false; $verificationError=$null
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
if($Recover) {
    Assert-PlainPath $Result
    if(Test-Path "$Result/restored.txt") { throw 'This run is already restored; refusing to overwrite newer owner settings.' }
    $recovery=Get-Content -LiteralPath "$Result/recovery.json" -Raw | ConvertFrom-Json
    if($recovery.schema -ne 1 -or $recovery.gameDir -ne $GameDir) { throw 'Recovery game scope differs from -GameDir.' }
    $localLow=Split-Path $customRally -Parent; $cloud=Join-Path $localLow 'cloud'
    $rootBefore=@($recovery.rootBefore); $cloudBefore=@($recovery.cloudBefore); $newPayloadDirs=@($recovery.newPayloadDirs)
    $allowedGame=@('Mods/ArtOfSimRally/UnityForceFeedback.dll','artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll',
        'Mods/ArtOfSimRally.DevRecorder/ArtOfSimRally.DevRecorder.dll','Mods/ArtOfSimRally.DevRecorder/Info.json',
        'Mods/ArtOfSimRally.DevRecorder/Dbce.Wheel.Playback.dll','artofrally_Data/Managed/UnityModManager/Log.txt',
        'artofrally_Data/Managed/UnityModManager/Params.xml') | ForEach-Object { Full (Join-Path $GameDir $_) }
    $allowedLogs=@('ffb.log','ffb.previous.log','last-session-frame-health.xml') | ForEach-Object { Full (Join-Path $env:LOCALAPPDATA "ArtOfSimRally/$_") }
    foreach($p in $rootBefore) { if((Split-Path -Parent (Full $p)) -ne $localLow) { throw 'Invalid LocalLow root recovery scope.' } }
    foreach($p in $cloudBefore) { if(!(Full $p).StartsWith($cloud+'\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid cloud recovery scope.' } }
    foreach($dir in $newPayloadDirs) { if((Full $dir) -notin @($allowedGame | ForEach-Object { Split-Path -Parent $_ })) { throw 'Invalid new payload directory.' } }
    $snapshots=@(Get-Content -LiteralPath "$Result/owner-files.json" -Raw | ConvertFrom-Json)
    if($snapshots.Count -gt 10000 -or @($snapshots.path | Sort-Object -Unique).Count -ne $snapshots.Count) { throw 'Invalid recovery inventory.' }
    for($i=0;$i -lt $snapshots.Count;$i++) {
        $item=$snapshots[$i]; Assert-PlainPath $item.path; Assert-PlainPath $item.backup
        if((Full $item.path) -notin ($allowedGame+$allowedLogs+$rootBefore+$cloudBefore) -or
            (Full $item.backup) -ne (Full "$Result/owner-files/$i.bin") -or
            ($item.before -ne 'absent' -and (Hash $item.backup) -ne $item.before)) { throw 'Invalid recovery path or backup hash.' }
    }
    $leasePath=Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt'
    if($recovery.lease.path -ne $leasePath) { throw 'Invalid recovery lease path.' }
    if(Test-Path -LiteralPath $leasePath) {
        $slot=Get-Content -LiteralPath $leasePath -Raw
        if(![string]::IsNullOrWhiteSpace($slot)) {
            if($slot.TrimEnd("`r","`n") -cne $recovery.lease.token) { throw 'Another test holds the recovery lease.' }
            if([IO.File]::GetLastWriteTimeUtc($leasePath) -gt [DateTime]::UtcNow.AddHours(-2)) {
                $lease=$recovery.lease; Assert-StageRigLease -Path $leasePath -Token $lease.token
            }
            # An unchanged but expired token may be reacquired by the shared helper below.
        }
    }
    $live=Join-Path $Result 'apply'; $harness=Join-Path $Result 'harness/ConfigurationQualification.dll'
    $nonce=if(Test-Path "$Result/request.txt") { ((Get-Content "$Result/request.txt" | Where-Object { $_ -match '^nonce=([a-f0-9]{32})$' }) -replace '^nonce=','') } else { $null }
    foreach($p in @($request,$switch)) {
        if(Test-Path -LiteralPath $p) {
            $text=Get-Content -LiteralPath $p -Raw
            if(!$nonce -or !($text -eq $nonce -or $text.Contains("nonce=$nonce"))) { throw 'A different controls request is present; refusing recovery.' }
        }
    }
    $ready=$true; $environmentSaved=$true
}
if(!$lease) { $lease=Enter-StageRigLease -Path (Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt') -Owner hula-art-controls -Purpose 'Production Apply and native raw-input observation; no force; exact restore' }
try {
    Assert-GameClosed
    if($Recover) {
        # Keep retries tied to a renewed token after an expired lease was reacquired.
        $recovery.lease=$lease
        $recovery | ConvertTo-Json -Depth 5 | Set-Content "$Result/recovery.next.json"
        [IO.File]::Move("$Result/recovery.next.json","$Result/recovery.json",$true)
        Write-Output 'Recovering original files/preferences; no launch or input.'; return
    }
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
    $newPayloadDirs=@($payloads.Keys | ForEach-Object { Split-Path -Parent (Join-Path $GameDir $_) } | Sort-Object -Unique | Where-Object { !(Test-Path -LiteralPath $_) })
    foreach($relative in @('artofrally_Data/Managed/UnityModManager/Log.txt','artofrally_Data/Managed/UnityModManager/Params.xml')) { Save-File (Join-Path $GameDir $relative) }
    foreach($name in @('ffb.log','ffb.previous.log','last-session-frame-health.xml')) { Save-File (Join-Path $env:LOCALAPPDATA "ArtOfSimRally/$name") }
    $localLow=Split-Path $customRally -Parent
    $rootBefore=@(Get-ChildItem -LiteralPath $localLow -File | ForEach-Object FullName)
    foreach($p in $rootBefore) { Save-File $p }
    $cloud=Join-Path $localLow 'cloud'
    $cloudBefore=@(Get-ChildItem -LiteralPath $cloud -File -Recurse | ForEach-Object FullName)
    foreach($p in $cloudBefore) { Save-File $p }
    $snapshots | ConvertTo-Json -Depth 4 | Set-Content "$Result/owner-files.json"
    @{schema=1;gameDir=$GameDir;lease=$lease;rootBefore=$rootBefore;cloudBefore=$cloudBefore;newPayloadDirs=$newPayloadDirs} | ConvertTo-Json -Depth 5 | Set-Content "$Result/recovery.json"
    $ready=$true # all recovery bytes exist before any owner file is written
    # Keep the loader window from covering the title. Its exact prior file is restored.
    $ummParams=Join-Path $GameDir 'artofrally_Data/Managed/UnityModManager/Params.xml'
    [xml]$paramsXml=Get-Content -LiteralPath $ummParams -Raw
    if(!$paramsXml.Param.ShowOnStart) { throw 'Missing UMM startup-window setting.' }
    $paramsXml.Param.ShowOnStart='0'; $paramsXml.Save($ummParams)
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
    Copy-Item -LiteralPath $settings -Destination "$Result/applied-settings.xml"
    [xml]$appliedXml=Get-Content -LiteralPath $settings -Raw
    foreach($key in @('ForceFeedbackEnabled','LandingEffectsEnabled','CrashEffectsEnabled','ShiftEffectsEnabled','TelemetryEnabled')) {
        if($appliedXml.Settings.$key -ne 'false') { throw "Apply changed output interlock $key" }
    }
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
    $identityDeadline=[DateTime]::UtcNow.AddSeconds(5); $gamePath=$null
    while(!$gamePath) {
        try { $game.Refresh(); $gamePath=$game.MainModule.FileName }
        catch [ComponentModel.Win32Exception] { if([DateTime]::UtcNow -ge $identityDeadline){throw}; Start-Sleep -Milliseconds 200 }
        if(!$gamePath -and [DateTime]::UtcNow -ge $identityDeadline){throw 'Game executable identity unavailable.'}
    }
    if(![string]::Equals($gamePath,(Join-Path $GameDir 'artofrally.exe'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected executable launched.' }
    @{GameProcessId=$game.Id;ExpectedStartTimeUtc=$start.ToString('o');Nonce=$nonce;GameDir=$GameDir;Lease=$lease;Result=$Result} | ConvertTo-Json -Depth 4 | Set-Content "$Result/command-context.json"
    $deadline=[DateTime]::UtcNow.AddSeconds(45)
    while(!(Test-Path "$trace/identity.txt") -and !$game.HasExited -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 500; $game.Refresh() }
    if(!(Test-Path "$trace/identity.txt")){throw 'Probe did not confirm its native no-force fence.'}
    & "$PSScriptRoot/Send-ControlsCommand.ps1" -Command Status -Nonce $nonce -GameProcessId $game.Id -ExpectedStartTimeUtc $start -GameDir $GameDir
    $deadline=[DateTime]::UtcNow.AddSeconds($ObserveSeconds)
    Write-Output "Armed. Select independent raw samples after inspecting state. Context: $Result/command-context.json"
    while(!$game.HasExited -and !(Test-Path "$trace/result.txt") -and [DateTime]::UtcNow -lt $deadline) {
        Assert-StageRigLease -Path $lease.path -Token $lease.token
        if([double](& "$HubRepo/tools/Owner-Input.ps1" -IdleSeconds) -lt 5) {
            'Owner input resumed; stopping controls test.' | Set-Content "$Result/interrupted.txt"
            break
        }
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
            if($applied) {
                try {
                    & dotnet $harness verify-art-runtime $live
                    "verifyExit=$LASTEXITCODE" | Set-Content "$Result/verify.txt"
                    if($LASTEXITCODE) { throw 'Applied settings verification failed; inspect retained runtime bytes.' }
                } catch { $verificationError=$_.Exception.Message; $verificationError | Set-Content "$Result/verification-error.txt" }
            }
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
            Attempt-Restore { foreach($p in Get-ChildItem -LiteralPath $localLow -File | Where-Object FullName -NotIn $rootBefore) {
                Assert-PlainPath $p.FullName
                Copy-Item -LiteralPath $p.FullName -Destination (Join-Path $Result ('after/new-root-'+[guid]::NewGuid().ToString('N')))
                Remove-Item -LiteralPath $p.FullName
            } }
            foreach($dir in $newPayloadDirs) { Attempt-Restore {
                Assert-PlainPath $dir
                if(Test-Path -LiteralPath $dir) {
                    if(@(Get-ChildItem -LiteralPath $dir -Force).Count) { throw "New probe directory contains unexpected files; retained: $dir" }
                    Remove-Item -LiteralPath $dir
                }
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
    if($verificationError) { throw "$verificationError Owner state restored; lease released." }
}
