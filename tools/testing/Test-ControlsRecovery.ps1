#Requires -Version 7.0
<# Runs the production recovery entry in child processes with disposable game,
   profile/AppData roots and a file-only environment-restoration stub. No game,
   registry, wheel, Steam or real rig lease is accessed. #>
[CmdletBinding()]
param([string]$ToolkitRepo='E:/Source/toolkits/dbce-wheel-mod-toolkit')
$ErrorActionPreference='Stop'
$root=Join-Path ([IO.Path]::GetTempPath()) ('art-controls-recovery-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $root | Out-Null
$checks=0
function Check($ok,[string]$why) { $script:checks++; if(!$ok){throw $why} }
function Text([string]$path,[string]$value) { New-Item -ItemType Directory -Path (Split-Path $path) -Force | Out-Null; [IO.File]::WriteAllText($path,$value) }
foreach($case in @('restore','expired-lease','already-restored','bad-backup','outside-path','other-lease','unexpected-file','environment-failure')) {
    $test=Join-Path $root $case; $game=Join-Path $test 'game'; $result=Join-Path $test 'result'
    $user=Join-Path $test 'user'; $app=Join-Path $test 'app'; $copy=Join-Path $test 'tools/testing'
    $localLow=Join-Path $user 'AppData/LocalLow/Funselektor Labs/Art of Rally'; $cloud=Join-Path $localLow 'cloud'
    New-Item -ItemType Directory -Path "$result/owner-files","$result/owner-environment","$result/after",$copy,$cloud -Force|Out-Null
    Copy-Item "$PSScriptRoot/Start-ControlsQualification.ps1" $copy
    # Replace only the platform boundary; all production file/lease recovery executes unchanged.
    Text "$copy/SessionEnvironment.ps1" @'
function Restore-SessionEnvironment([string]$Directory) {
    if(Test-Path "$Directory/fail") { throw 'simulated environment failure' }
    [IO.File]::WriteAllText((Join-Path $GameDir 'environment-restored.txt'),'owner environment')
}
'@
    $target=Join-Path $game 'Mods/ArtOfSimRally/UnityForceFeedback.dll'
    $probeDir=Join-Path $game 'Mods/ArtOfSimRally.DevRecorder'; $probe=Join-Path $probeDir 'Info.json'
    Text $target 'temporary native'; Text $probe 'temporary probe'; Text "$result/owner-files/0.bin" 'original native'
    Text "$localLow/new.log" 'new runtime log'; Text "$cloud/new.sav" 'new runtime save'
    $leasePath=Join-Path $app 'dbce/test-slot.txt'; $token='test recovery lease'; Text $leasePath $token
    $lease=@{path=$leasePath;token=$token;previous=''}
    $snapshot=@(@{path=$target;backup=(Join-Path $result 'owner-files/0.bin');before=(Get-FileHash "$result/owner-files/0.bin").Hash},
        @{path=$probe;backup=(Join-Path $result 'owner-files/1.bin');before='absent'})
    if($case -eq 'already-restored') { Text "$result/restored.txt" 'completed' }
    if($case -eq 'bad-backup') { Text "$result/owner-files/0.bin" 'corrupt' }
    if($case -eq 'outside-path') { $snapshot[0].path=Join-Path $test 'unrelated.txt'; Text $snapshot[0].path 'do not change' }
    if($case -eq 'other-lease') { Text $leasePath 'another test owner' }
    if($case -eq 'expired-lease') { [IO.File]::SetLastWriteTimeUtc($leasePath,[DateTime]::UtcNow.AddHours(-3)) }
    if($case -eq 'unexpected-file') { Text "$probeDir/unknown.txt" 'retain' }
    if($case -eq 'environment-failure') { Text "$result/owner-environment/fail" 'yes' }
    $snapshot | ConvertTo-Json -Depth 4 | Set-Content "$result/owner-files.json"
    @{schema=1;gameDir=$game;lease=$lease;rootBefore=@();cloudBefore=@();newPayloadDirs=@($probeDir)} | ConvertTo-Json -Depth 5 | Set-Content "$result/recovery.json"
    $start=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true; $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach($arg in @('-NoProfile','-File',"$copy/Start-ControlsQualification.ps1",'-Recover','-Result',$result,'-GameDir',$game,'-ToolkitRepo',$ToolkitRepo)) { $start.ArgumentList.Add($arg) }
    $start.Environment['USERPROFILE']=$user; $start.Environment['LOCALAPPDATA']=$app
    $process=[Diagnostics.Process]::Start($start)
    $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
    if(!$process.WaitForExit(30000)) { $process.Kill(); throw "Recovery test timed out: $case" }
    $log=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult(); Text "$test/output.txt" $log
    $success=$case -in @('restore','expired-lease')
    $shouldRestore=$success -or $case -in @('unexpected-file','environment-failure')
    Check (($process.ExitCode -eq 0) -eq $success) "Wrong recovery exit ($case): $log"
    Check (([IO.File]::ReadAllText($target) -eq 'original native') -eq $shouldRestore) "Wrong native recovery ($case)"
    Check ((Test-Path "$result/restored.txt") -eq ($success -or $case -eq 'already-restored')) "Wrong success receipt ($case)"
    Check ((Test-Path $leasePath) -ne $success) "Wrong lease release ($case)"
    if($shouldRestore) {
        Check (!(Test-Path $probe) -and !(Test-Path "$localLow/new.log") -and !(Test-Path "$cloud/new.sav")) "Temporary files retained ($case)"
    }
    if($case -eq 'restore') { Check (!(Test-Path $probeDir) -and (Test-Path "$game/environment-restored.txt")) 'Empty probe directory or environment restore failed' }
    if($case -eq 'unexpected-file') { Check ((Get-Content "$probeDir/unknown.txt" -Raw) -eq 'retain') 'Unexpected file lost' }
    if($case -eq 'outside-path') { Check ((Get-Content $snapshot[0].path -Raw) -eq 'do not change') 'Out-of-scope file changed' }
    $process.Dispose()
}
Write-Output "$checks recovery checks passed; disposable evidence $root"
