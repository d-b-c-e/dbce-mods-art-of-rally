#Requires -Version 7.0
# Developer-only, coordinator-serialized smoke of the opt-in SharedInput candidate in a disposable copy
# staged by Prepare-SharedInputCopy.ps1 (its staged.json is the sole configuration). Muted from the
# native load-time latch (DBCE_FFB_MUTE=1): force feedback together with the shared profile stays an
# attended owner test. Never installs into the owner game, sends input or force, or answers Steam
# prompts. Observes the copy's UMM log for the shared phases, closes the exact child, then restores
# shared state.
# -RouteSource (romi 31616, staged with Prepare-SharedInputCopy -Recorder): the dev recorder's capture driver replays
# the route's taped menus to the stage under the route's start state, then scripts the Rewired values Art reads
# (carInput=native) while the ordinary session owns the wheel at rest; its native-verdict.txt judges whether the mod
# returned Art's own result unchanged. Scripted Rewired native-path evidence, not physical keyboard or wheel/FFB
# qualification.
# -SharedCar (with -RouteSource; sana 43671/43761): the route's taped menus reach the stage, then the car is left to the
# mod's ordinary shared input (carInput=shared), with the route held at the title until the shared session has started
# (holdForShared=1). Whatever the shared profile's devices deliver drives the car; this runner sends none. The capture
# ends captureSeconds after UNDERWAY; shared-car.json summarises the tapes. Menus stay route-driven (not qualified here).
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Result,
      [switch]$PreflightOnly,
      [string]$RouteSource,
      [switch]$SharedCar,
      # Diagnostic (with -SharedCar; sana 45712): the recorder observes the identity mapper's inputs and probes DualShock 4 rows.
      [switch]$IdentityProbe,
      # Diagnostic (with -SharedCar; sana 44749/47008): the recorder writes Rewired's joystick rows (rewired-inventory.tsv).
      # Off unless asked: an ordinary recording no longer dumps them.
      [switch]$RewiredInventory,
      # A cold replay of a recording made here (sana 36236): the tape's start state, then no input or force device.
      [string]$ReplayTape,
      # A capture-driver recording made here has no environment-at-start of its own: its start state was this route's
      # (Set-RouteEnvironment). Replay it from the same route.
      [string]$ReplayRoute,
      # F6 visual and shared-binding messaging capture (docs/F6-UI-CAPTURE-RECIPE.md, sana 124479): the recorder's mode=ui
      # script opens Wheel settings at the title, takes game-only shots of every page and the host's binding states, and
      # puts everything back. Explicit host calls only: no moved control, no OS input, no device API, outputs muted.
      [switch]$UiCapture,
      # With -UiCapture: full (every page, both text modes, the narrow pass and the binding steps) or followup (normal FFB,
      # 640 Advanced Cameras at the larger scale and the binding steps; the bounded re-check after a fix, sana 126101).
      [ValidateSet('full','followup')][string]$UiSet='full',
      # With -UiCapture (sana 126190/126330; owner direction 2026-10-14 in the hub AGENTS.md: advance notice first): one
      # bare SetForegroundWindow on the exact child's own visible Unity window, because Art's force connect and its binding
      # editor both need a focused game. No keys, Alt taps or SendInput, one call and no retry; if Windows refuses, that
      # is recorded in activation.json, not worked around.
      [switch]$Activate,
      [ValidateRange(30,300)][int]$ObserveSeconds=120,
      [ValidateRange(0,60)][int]$SettleSeconds=10,
      [ValidateRange(10,60)][int]$CaptureSeconds=12,
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
# The copy's UMM log, read while the game may still write it. A level tag may precede the text: the mod logs its
# refusal as "[ArtOfSimRally] [Warning] Shared input not used: ..." (run-01 and diag-01 timed out without seeing it).
# Before launch: the previous launch's log leaves the copy (kept as evidence), so Read-Shared can only see this launch's
# lines. Its prior bytes were snapshotted first and come back with the restoration.
function Set-UmmLogAside([string]$Log, [string]$Keep) {
    if(Test-Path -LiteralPath $Keep) { throw 'The pre-launch UMM log evidence already exists.' }
    if(Test-Path -LiteralPath $Log) { Move-Item -LiteralPath $Log -Destination $Keep }
}
function Read-Shared([string]$p) {
    if(!(Test-Path -LiteralPath $p -PathType Leaf)) { return @() }
    $stream=[IO.File]::Open($p,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
    try { $reader=[IO.StreamReader]::new($stream); @($reader.ReadToEnd() -split "`r?`n" | Where-Object { $_ -match '\[ArtOfSimRally\] (\[\w+\] )?(Shared input|Wheel input|Loaded)' }) }
    finally { $stream.Dispose() }
}
# The recorder request for a route recording: native scripts Art's Rewired values at the stage; shared leaves the car to
# the shared profile and holds the route at the title until the shared session has started.
function New-CaptureRequest([string]$Tape, [string]$Route, [bool]$Shared, [int]$Seconds, [DateTime]$ExpiresUtc, [bool]$Probe = $false, [bool]$Inventory = $false) {
    @("mode=record","tape=$Tape","captureDriver=$Route","carInput=$(if($Shared) { 'shared' } else { 'native' })") +
        $(if($Shared) { @('holdForShared=1') } else { @() }) + $(if($Probe) { @('identityProbe=1') } else { @() }) + $(if($Inventory) { @('rewiredInventory=1') } else { @() }) +
        @("captureSeconds=$Seconds","muteOutputs=1","expiresUtc=$($ExpiresUtc.ToString('o'))")
}
# A shared-car capture's tapes, summarised (the per-action delivery verdict belongs to the caller's device script).
function Get-SharedCarSummary([string]$Capture) {
    function Rows([string]$name) { $p=Join-Path $Capture $name; if(Test-Path -LiteralPath $p -PathType Leaf) { [Math]::Max(0, @(Get-Content -LiteralPath $p).Count - 1) } else { $null } }
    $events=Join-Path $Capture 'events.log'; $end=Join-Path $Capture 'end.txt'
    [ordered]@{ sharedTapeRows=(Rows 'shared.tape'); carTapeRows=(Rows 'car.tape'); shiftRows=(Rows 'shifts.tape')
        underway=((Test-Path -LiteralPath $events -PathType Leaf) -and [bool](Select-String -LiteralPath $events -Pattern '\|UNDERWAY' -Quiet))
        end=$(if(Test-Path -LiteralPath $end -PathType Leaf) { (Get-Content -LiteralPath $end -Raw).Trim() } else { $null })
        incomplete=(Test-Path -LiteralPath (Join-Path $Capture 'incomplete.txt')) }
}
# -Activate: the exact child's own visible top-level UnityWndClass window, one bare SetForegroundWindow, and the foreground
# window's owner a second later. Window titles are never read (the foreground before may be the owner's own window).
function Invoke-ChildActivation([Diagnostics.Process]$Child, [string]$Path) {
    if(!('DbceChildWindow' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class DbceChildWindow {
    private delegate bool EnumProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    public static IntPtr Find(uint pid) {
        IntPtr found = IntPtr.Zero;
        EnumProc visit = (window, data) => {
            uint owner; GetWindowThreadProcessId(window, out owner);
            if (owner != pid || !IsWindowVisible(window)) return true;
            var name = new StringBuilder(64); GetClassName(window, name, name.Capacity);
            if (name.ToString() != "UnityWndClass") return true;
            found = window; return false;
        };
        EnumWindows(visit, IntPtr.Zero);
        GC.KeepAlive(visit);
        return found;
    }
    public static uint Owner(IntPtr window) { uint pid = 0; if (window != IntPtr.Zero) GetWindowThreadProcessId(window, out pid); return pid; }
}
'@
    }
    $deadline=[DateTime]::UtcNow.AddSeconds(30); $window=[IntPtr]::Zero
    while($window -eq [IntPtr]::Zero -and !$Child.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        $window=[DbceChildWindow]::Find([uint32]$Child.Id)
        if($window -eq [IntPtr]::Zero) { $null=$Child.WaitForExit(250) }
    }
    $record=[ordered]@{ utc=[DateTime]::UtcNow.ToString('o'); pid=$Child.Id; window=[int64]$window
        method='SetForegroundWindow on the child''s UnityWndClass window; no keys, Alt taps or SendInput; one call, no retry'
        foregroundBeforeOwnerPid=[int64][DbceChildWindow]::Owner([DbceChildWindow]::GetForegroundWindow())
        called=$false; returned=$null; foregroundAfterIsChild=$null; foregroundAfterOwnerPid=$null; reason=$null }
    if($window -ne [IntPtr]::Zero) {
        $record.called=$true; $record.returned=[DbceChildWindow]::SetForegroundWindow($window)
        Start-Sleep -Seconds 1
        $after=[DbceChildWindow]::GetForegroundWindow()
        $record.foregroundAfterIsChild=($after -eq $window); $record.foregroundAfterOwnerPid=[int64][DbceChildWindow]::Owner($after)
    } else { $record.reason='the child showed no visible UnityWndClass window within 30 s' }
    $record | ConvertTo-Json | Set-Content -LiteralPath $Path
    [pscustomobject]$record
}
$Result=Full $Result; Plain $Result
$native=[bool]$RouteSource
$replay=[bool]$ReplayTape
$ui=[bool]$UiCapture
if($ui -and ($native -or $replay -or $SharedCar)) { throw '-UiCapture runs alone: not with -RouteSource, -SharedCar or -ReplayTape.' }
if($ui -and !$PSBoundParameters.ContainsKey('ObserveSeconds')) { $ObserveSeconds=240 }
if(($Activate -or $PSBoundParameters.ContainsKey('UiSet')) -and !$ui) { throw '-Activate and -UiSet belong to -UiCapture.' }
if($IdentityProbe -and !$SharedCar) { throw '-IdentityProbe is a -SharedCar diagnostic.' }
if($RewiredInventory -and !$SharedCar) { throw '-RewiredInventory is a -SharedCar diagnostic.' }
if($SharedCar -and !$native) { throw '-SharedCar records a route''s stage with the car left to shared input; give -RouteSource.' }
if($native -and $replay) { throw 'Choose -RouteSource (record) or -ReplayTape (replay), not both.' }
if($ReplayRoute -and !$replay) { throw '-ReplayRoute only gives a replayed tape its start state.' }
if($native) {
    $RouteSource=Full $RouteSource; Plain $RouteSource
    . (Join-Path $PSScriptRoot 'RouteEnvironment.ps1')
    Assert-RouteSource $RouteSource
    if(!$PSBoundParameters.ContainsKey('ObserveSeconds')) { $ObserveSeconds=300 }
    if($SharedCar -and !$PSBoundParameters.ContainsKey('CaptureSeconds')) { $CaptureSeconds=30 }
}
if($replay) {
    $ReplayTape=Full $ReplayTape; Plain $ReplayTape
    foreach($n in @('session.txt','end.txt','complete.tsv') + $(if($ReplayRoute) { @() } else { @('environment-at-start/state.json') })) {
        if(!(Test-Path -LiteralPath (Join-Path $ReplayTape $n) -PathType Leaf)) { throw "Replay tape incomplete: $n" }
    }
    if(Test-Path -LiteralPath (Join-Path $ReplayTape 'incomplete.txt')) { throw 'Replay tape is marked incomplete.' }
    if($ReplayRoute) {
        if(Test-Path -LiteralPath (Join-Path $ReplayTape 'environment-at-start')) { throw 'This tape has its own start state; omit -ReplayRoute.' }
        $ReplayRoute=Full $ReplayRoute; Plain $ReplayRoute
        . (Join-Path $PSScriptRoot 'RouteEnvironment.ps1')
        Assert-RouteSource $ReplayRoute
    }
    . (Join-Path $PSScriptRoot 'ReplayInputIsolation.ps1')
    if(!$PSBoundParameters.ContainsKey('ObserveSeconds')) { $ObserveSeconds=300 }
}
$staged=Get-Content -LiteralPath (Join-Path $Result 'staged.json') -Raw | ConvertFrom-Json
if($staged.schema -ne 1 -or $staged.kind -ne 'shared-input-ordinary') { throw 'Not a SharedInput staging result.' }
if($staged.applyPending -eq $true) { throw 'This staging awaits its production Apply; run Confirm-SharedInputApply.ps1 after the Apply first.' }
$GameDir=Full $staged.gameCopy; $owner=Full $staged.sourceGame
foreach($p in @($GameDir,$owner)) { Plain $p }
if((Within $GameDir $owner) -or (Within $owner $GameDir) -or (Within $Result $GameDir) -or (Within $Result $owner) -or ($native -and (Within $RouteSource $GameDir)) -or
    ($replay -and ((Within $ReplayTape $GameDir) -or (Within $ReplayTape $Result))) -or ($ReplayRoute -and (Within $ReplayRoute $GameDir))) { throw 'Invalid disposable scope.' }
$recorded=$native -or $replay -or $ui
if($recorded -ne [bool]$staged.recorder) { throw $(if($recorded) { 'A native-script run or replay needs Prepare-SharedInputCopy -Recorder.' } else { 'Staged with the recorder; the ordinary smoke runs without it.' }) }
if(Test-Path -LiteralPath "$Result/launch.json") { throw 'This staging has already been launched; stage a fresh result.' }
$mod=Join-Path $GameDir 'Mods/ArtOfSimRally'
$umLog=Join-Path $GameDir 'artofrally_Data/Managed/UnityModManager/Log.txt'
function Assert-Pins {
    foreach($entry in $staged.files.PSObject.Properties) {
        $p=Join-Path $GameDir $entry.Name; Plain $p
        if((Hash $p) -ne $entry.Value) { throw "Staged pin differs: $($entry.Name)" }
    }
    if((Hash (Join-Path $owner 'Mods/ArtOfSimRally/Settings.xml')) -ne $staged.ownerSettingsSha256) { throw 'Owner settings changed since staging.' }
    if((Get-Content -LiteralPath (Join-Path $GameDir 'steam_appid.txt') -Raw).Trim() -cne '550320') { throw 'Copy-only Steam app id missing; direct launch could redirect through Steam.' }
}
Assert-Pins
[xml]$copySettings=Get-Content -LiteralPath (Join-Path $mod 'Settings.xml') -Raw
if($copySettings.DocumentElement.SharedInput -ne 'true') { throw 'Staged settings do not turn SharedInput on.' }
# DBCE_FFB_MUTE only mutes a native with the load-time latch (toolkit 82c40c4, DD301314). Any other native is run
# with force feedback off in the copy, so the mod never initialises a force device (checked again after the run).
$latchedNatives=@('DD3013147821364AD8795F76B4500FB479F669E6BD89576F83E725A677273414')
$forceOff=$staged.files.'Mods/ArtOfSimRally/UnityForceFeedback.dll' -notin $latchedNatives
# A copy staged with -ForceFeedbackOff is checked the same way even with the latched native (sana 131300): its
# Settings.xml must say false, and the launch's own UMM log must show ffb=False with no force attempt.
if($staged.forceFeedbackOff -eq $true) {
    if($copySettings.DocumentElement.ForceFeedbackEnabled -ne 'false') { throw 'Staged with -ForceFeedbackOff, but the copy''s Settings.xml does not turn force feedback off.' }
    $forceOff=$true
}
if($forceOff -and ($staged.forceFeedbackOff -ne $true -or $copySettings.DocumentElement.ForceFeedbackEnabled -ne 'false')) {
    throw 'This native has no DBCE_FFB_MUTE latch: stage with Prepare-SharedInputCopy -ForceFeedbackOff.'
}
$allowed=@('ArtOfSimRally') + $(if($recorded) { @('ArtOfSimRally.DevRecorder') } else { @() })
$otherMods=@(Get-ChildItem -LiteralPath (Join-Path $GameDir 'Mods') -Directory | Where-Object Name -NotIn $allowed)
if($otherMods.Count) { throw 'Disposable copy contains optional mod folders; stage only ArtOfSimRally (and the recorder for a native-script run).' }
[xml]$umm=Get-Content -LiteralPath (Join-Path $GameDir 'artofrally_Data/Managed/UnityModManager/Params.xml') -Raw
if(@($umm.Param.ModParams.Mod | Where-Object { $_.Id -eq 'ArtOfSimRally' -and $_.Enabled -eq 'true' }).Count -ne 1) { throw 'Candidate UMM mod is not enabled.' }
if($recorded -and @($umm.Param.ModParams.Mod | Where-Object { $_.Id -eq 'ArtOfSimRally.DevRecorder' -and $_.Enabled -eq 'false' }).Count) { throw 'The recorder is disabled in the copy UMM parameters.' }
# Ordinary mode only: no probe request, developer marker or injection switch may be present (a native-script
# run writes its own recorder request once it holds the lease; restoration removes it).
if($env:DBCE_ART_SHIM_REQUEST) { throw 'DBCE_ART_SHIM_REQUEST is set; the ordinary smoke never runs the probe.' }
$sessionRequest=Join-Path $env:LOCALAPPDATA 'ArtOfSimRally/session-request.txt'
foreach($p in @('ArtOfSimRally/controls-request.txt','ArtOfSimRally/session-request.txt','dbce/art-of-rally/inject.on')) {
    if(Test-Path -LiteralPath (Join-Path $env:LOCALAPPDATA $p)) { throw "Existing developer marker refused: $p" }
}
Assert-GameClosed
. (Join-Path $PSScriptRoot 'SteamLaunchContext.ps1')
# SteamAPI_Init needs a running Steam client at the game's access level; refuse before any lease or snapshot.
$context=Get-SteamLaunchContext
$context | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$Result/preflight-steam-context.json"
if($context.decision -eq 'refuse') { throw "Steam launch context refused: $($context.reason)" }
if($PreflightOnly) { Write-Output "Staged pins, settings, app id, UMM, $(if($native) { 'recorder, route, ' } elseif($replay) { 'recorder, replay tape, ' })marker and Steam context ($($context.decision)) checks passed. No launch or shared-state mutation."; return }
$ownerInput=Join-Path $HubRepo 'tools/Owner-Input.ps1'
. (Join-Path $ToolkitRepo 'tools/playback/Stage-OwnerIdle.ps1')
. (Join-Path $PSScriptRoot 'OwnerActivity.ps1')
$ownerRecord=Join-Path $Result 'owner-activity.json'
if(!(Test-OwnerGate $ownerInput 'launch' 300 $ownerRecord)) { throw 'Owner input is recent; no launch or shared-state mutation.' }
$artLocal=Join-Path $env:LOCALAPPDATA 'ArtOfSimRally'
$localLow=Join-Path $env:USERPROFILE 'AppData/LocalLow/Funselektor Labs/Art of Rally'
$customRally=Join-Path $localLow 'customrally' # read by SessionEnvironment.ps1's Get-SessionFiles
. (Join-Path $PSScriptRoot 'SessionEnvironment.ps1')
. (Join-Path $ToolkitRepo 'tools/playback/Stage-RigLease.ps1')
$snapshots=[Collections.Generic.List[object]]::new(); $ownerPins=[Collections.Generic.List[object]]::new()
$lease=$null; $game=$null; $ready=$false; $environmentSaved=$false; $launchAttempted=$false; $routeApplied=$false; $localBefore=@(); $artBefore=@()
$capture=Join-Path $Result 'capture'
$replayOut=Join-Path $Result 'replay'; $replayResult=Join-Path $replayOut 'result.txt'; $ffbBefore=$null
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
$lease=Enter-StageRigLease -Path (Join-Path $env:LOCALAPPDATA 'dbce/test-slot.txt') -Owner art-shared-input -Purpose $(if($ui) { 'Disposable Art F6 visual and shared-binding messaging capture through the dev recorder (host calls only, no moved control); muted, no force or OS input; restore shared state' } elseif($replay) { 'Disposable Art cold replay of a SharedInput recording with every input and force device off; no OS input; restore shared state' } elseif($SharedCar) { 'Disposable Art SharedInput ordinary shared-car recording at a recorder-reached stage (the shared profile''s devices drive the car); muted, no force or OS input; restore shared state' } elseif($native) { 'Disposable Art SharedInput ordinary native-script coexistence at a recorder-reached stage; muted, no force or OS input; restore shared state' } else { 'Disposable Art SharedInput ordinary startup smoke; muted, no force or input; restore shared state' })
try {
    Assert-GameClosed
    New-Item -ItemType Directory -Path "$Result/shared-files","$Result/after" | Out-Null
    foreach($p in @((Files (Join-Path $owner 'Mods') -Recurse)) + @(
        (Join-Path $owner 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll'),
        (Join-Path $owner 'artofrally_Data/Managed/UnityModManager/Params.xml'),
        (Join-Path $owner 'steam_appid.txt'))) { Plain $p; $ownerPins.Add([pscustomobject]@{path=$p;sha256=(Hash $p)}) }
    $ownerPins | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/owner-pins.json"
    Save-SessionEnvironment "$Result/shared-environment"; $environmentSaved=$true
    # Art's local folder (logs, frame health, the shared-input run folders) and LocalLow (Unity, saves).
    $artBefore=@(Files $artLocal -Recurse | Where-Object { $_ -notmatch '\\(dev-captures|crash-feel)\\' })
    foreach($p in $artBefore) { Save-File $p }
    foreach($p in @('DBCE/TripleScreen/games/art-of-rally/status.json','DBCE/TripleScreen/games/art-of-rally/render-diagnostics.json')) { Save-File (Join-Path $env:LOCALAPPDATA $p) }
    $localBefore=@(Files $localLow -Recurse)
    foreach($p in $localBefore) { Save-File $p }
    # The copy's own UMM log: snapshotted like every shared file, so restoration returns its prior bytes (or removes this
    # launch's log if there was none); it is then set aside before launch (Set-UmmLogAside).
    Save-File $umLog
    # A UI capture's Save bindings step writes the copy's shared profile, and the panel's page and text-mode steps touch
    # its Settings.xml: snapshot both like shared files, so restoration keeps the post-run bytes under after/ as evidence
    # and puts the staged bytes back (kiki 125183).
    if($ui) { Save-File (Join-Path $mod 'dbce-controls.ini'); Save-File (Join-Path $mod 'Settings.xml') }
    $snapshots | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/shared-files.json"
    @{schema=1;gameDir=$GameDir;owner=$owner;lease=$lease;localBefore=$localBefore;artBefore=$artBefore} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$Result/recovery.json"
    $ready=$true
    Assert-StageRigLease -Path $lease.path -Token $lease.token
    if(!(Test-OwnerGate $ownerInput 'prepared' 300 $ownerRecord)) { throw 'Owner input resumed during preparation; no launch.' }
    Assert-Pins # close the preparation-to-launch gap while holding the lease
    $context=Get-SteamLaunchContext # Steam may have stopped or changed since preflight
    if($context.decision -eq 'refuse') { $context | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath "$Result/launch-steam-context.json"; throw "Steam launch context refused: $($context.reason)" }
    if($replay) {
        # The tape's recorded start state, this rig's own display settings, then no input or force device: the
        # tape supplies every input (replay-input.json). Restoration below returns the copy and shared state.
        $routeApplied=$true
        if($ReplayRoute) { Set-RouteEnvironment $ReplayRoute "$Result/shared-environment" } # the recording's own start state
        else {
            Restore-SessionEnvironment (Join-Path $ReplayTape 'environment-at-start') -NoReceipt
            Restore-SessionPresentation "$Result/shared-environment"
        }
        Set-ReplayInputIsolation (Join-Path $mod 'Settings.xml') $Result
        $ffbLog=Join-Path $artLocal 'ffb.log'; $ffbBefore=if(Test-Path -LiteralPath $ffbLog) { [IO.File]::ReadAllText($ffbLog) } else { '' }
        @("mode=replay","playback=trajectory","tape=$ReplayTape","out=$replayOut","poseThreshold=0",
          "expiresUtc=$([DateTime]::UtcNow.AddMinutes(10).ToString('o'))") | Set-Content -LiteralPath $sessionRequest -Encoding ascii
        Copy-Item -LiteralPath $sessionRequest -Destination (Join-Path $Result 'session-request.txt')
    }
    if($native) {
        $routeApplied=$true; Set-RouteEnvironment $RouteSource "$Result/shared-environment"
        # The recorder's request (a shared marker, absent before; restoration removes it and its .taken rename).
        New-CaptureRequest $capture $RouteSource ([bool]$SharedCar) $CaptureSeconds ([DateTime]::UtcNow.AddMinutes(10)) ([bool]$IdentityProbe) ([bool]$RewiredInventory) | Set-Content -LiteralPath $sessionRequest -Encoding ascii
        Copy-Item -LiteralPath $sessionRequest -Destination (Join-Path $Result 'session-request.txt')
    }
    if($ui) {
        # The recorder's mode=ui request (a shared marker, absent before; restoration removes it). The copy's mod folder is
        # hashed before and after: a Save bindings step writes this disposable copy's profile only.
        $uiDir=Join-Path $Result 'ui'
        @(Get-ChildItem -LiteralPath $mod -File | ForEach-Object { [pscustomobject]@{name=$_.Name;sha256=(Hash $_.FullName)} }) | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$Result/ui-copy-mod-before.json"
        @("mode=ui","tape=$uiDir","uiSet=$UiSet","muteOutputs=1","expiresUtc=$([DateTime]::UtcNow.AddMinutes(10).ToString('o'))") | Set-Content -LiteralPath $sessionRequest -Encoding ascii
        Copy-Item -LiteralPath $sessionRequest -Destination (Join-Path $Result 'session-request.txt')
    }
    # The copy's previous UMM log goes aside as evidence before launch, so every line observed belongs to this launch
    # (diag-02 read the previous launch's refusal line before Unity rewrote the log, and closed Art during startup).
    Set-UmmLogAside $umLog (Join-Path $Result 'umm-log-before-launch.txt')
    $exe=Join-Path $GameDir 'artofrally.exe'
    $childEnvironment=[ordered]@{
        SteamAppId='550320'; SteamGameId='550320'
        DBCE_FFB_MUTE='1' # native load-time latch: no force output for the whole process
        DBCE_FFB_LOG_PATH=(Join-Path $Result 'native.log')
    }
    $brokerSha256=$null
    if($context.decision -eq 'direct') {
        $start=[Diagnostics.ProcessStartInfo]::new()
        $start.FileName=$exe; $start.WorkingDirectory=$GameDir; $start.UseShellExecute=$false
        foreach($name in $childEnvironment.Keys) { $start.EnvironmentVariables[$name]=$childEnvironment[$name] }
        $launchUtc=[DateTime]::UtcNow; $launchAttempted=$true
        $game=[Diagnostics.Process]::Start($start)
    } else {
        # Steam runs at the user's Medium level: start the copy through the user shell with cmd-local variables only.
        $broker=Join-Path $Result 'launch-broker.cmd'
        $brokerSha256=New-ChildEnvironmentBroker -Path $broker -Executable $exe -WorkingDirectory $GameDir -Environment $childEnvironment
        $launchUtc=[DateTime]::UtcNow; $launchAttempted=$true
        Start-MediumBroker $broker
        # Adopt only the single new exact copy process; never an older or different Art process.
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
    @{pid=$game.Id;startUtc=$processStart.ToString('o');executable=$imagePath;stagedSha256=(Hash "$Result/staged.json");sourceCommit=$staged.sourceCommit;lease=$lease;routeSource=$RouteSource
        launchMode=$context.decision;brokerSha256=$brokerSha256;steam=$context.steam[0];launcher=$context.launcher;game=$gameIntegrity;integrityMatchesSteam=$matchesSteam;muted=$true} |
        ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/launch.json"
    if(!$matchesSteam) { throw 'Launched copy integrity differs from Steam; SteamAPI_Init cannot succeed. Closing the exact child without observation.' }
    Write-Output "Disposable Art started (muted SharedInput $(if($replay) { 'cold replay' } elseif($SharedCar) { 'shared-car recording' } elseif($native) { 'native-script coexistence' } elseif($ui) { "UI capture ($UiSet)" } else { 'smoke' })): pid=$($game.Id), path=$imagePath. No OS input sent."
    $activation=$null
    if($Activate) {
        $activation=Invoke-ChildActivation $game "$Result/activation.json"
        Write-Output "Activation: $(if(!$activation.called) { 'not called: ' + $activation.reason } else { 'SetForegroundWindow returned ' + $activation.returned + '; child in front 1 s later: ' + $activation.foregroundAfterIsChild })"
    }
    # Ends on a terminal shared line; after "first frame accepted" it keeps watching SettleSeconds for a stop.
    # A native-script run instead ends when the recorder has closed its capture.
    $deadline=[DateTime]::UtcNow.AddSeconds($ObserveSeconds); $outcome='timeout'; $settleUntil=$null
    while(!$game.HasExited -and [DateTime]::UtcNow -lt $deadline) {
        Assert-StageRigLease -Path $lease.path -Token $lease.token
        if(@(Get-Process artofrally -ErrorAction SilentlyContinue | Where-Object Id -NE $game.Id).Count) { throw 'Another Art process appeared; do not answer Steam session dialogs.' }
        if(!(Test-OwnerGate $ownerInput 'observation' 5 $ownerRecord)) { 'Owner input resumed; ending observation.' | Set-Content -LiteralPath "$Result/interrupted.txt"; $outcome='interrupted'; break }
        $lines=Read-Shared $umLog
        if($replay) { if(Test-Path -LiteralPath $replayResult) { $outcome='replayed'; break } }
        # The capture ends on the recorder's end.txt, written only after its last shot file is complete; a shared-input
        # refusal does not end it (the binding shots then record the host's actual state).
        elseif($ui) {
            $uiEnd=Join-Path $uiDir 'end.txt'
            if(Test-Path -LiteralPath $uiEnd) {
                $endText=(Get-Content -LiteralPath $uiEnd -Raw).Trim()
                # Only an end line that starts with complete counts as a capture; incomplete and failed stay distinct, and a
                # capture with unmet step expectations says so (run-cd67994 read ui-captured with all six binding steps unmet).
                $complete=$endText.StartsWith('complete',[StringComparison]::Ordinal) -and $endText -match 'restore ok' -and !(Test-Path -LiteralPath (Join-Path $uiDir 'incomplete.txt'))
                $unmet=if($endText -match 'unmet (\d+)') { [int]$Matches[1] } else { 0 }
                $outcome=if($complete -and $unmet -eq 0) { 'ui-captured' } elseif($complete) { 'ui-captured-unmet' } elseif($endText.StartsWith('incomplete',[StringComparison]::Ordinal)) { 'ui-incomplete' } else { 'ui-failed' }
                break
            }
        }
        elseif($lines -match 'Shared input (not used|stopped)') { $outcome='refused-or-stopped'; break }
        elseif($native) { if(Test-Path -LiteralPath (Join-Path $capture 'end.txt')) { $outcome='captured'; break } }
        else {
            if(!$settleUntil -and ($lines -match 'Shared input: first frame accepted')) { $settleUntil=[DateTime]::UtcNow.AddSeconds($SettleSeconds) }
            if($settleUntil -and [DateTime]::UtcNow -ge $settleUntil) { $outcome='attached'; break }
        }
        $null=$game.WaitForExit(500); $game.Refresh()
    }
    if($game.HasExited -and $outcome -eq 'timeout') { $outcome='exited' }
    $lines=Read-Shared $umLog; $lines | Set-Content -LiteralPath "$Result/shared-lines.txt"
    $verdictPath=Join-Path $capture 'native-verdict.txt'
    $nativeVerdict=if(Test-Path -LiteralPath $verdictPath -PathType Leaf) { (Get-Content -LiteralPath $verdictPath -Raw).Trim() } else { $null }
    $replayLines=if($replay -and (Test-Path -LiteralPath $replayResult -PathType Leaf)) { @(Get-Content -LiteralPath $replayResult) } else { @() }
    $sharedCarSummary=if($SharedCar) { Get-SharedCarSummary $capture } else { $null }
    $uiSummary=if($ui) { [ordered]@{ end=$(if(Test-Path -LiteralPath (Join-Path $uiDir 'end.txt')) { (Get-Content -LiteralPath (Join-Path $uiDir 'end.txt') -Raw).Trim() } else { $null })
        shots=@(Get-ChildItem -LiteralPath (Join-Path $uiDir 'shots') -Filter *.png -ErrorAction SilentlyContinue | ForEach-Object { [pscustomobject]@{name=$_.Name;bytes=$_.Length;sha256=(Hash $_.FullName)} })
        incomplete=(Test-Path -LiteralPath (Join-Path $uiDir 'incomplete.txt')) } } else { $null }
    if($SharedCar) { $sharedCarSummary | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$Result/shared-car.json" }
    # A UI capture staged with force feedback off is binding-host evidence with FFB disabled, never an attended
    # foreground or force result (sana 131300).
    $evidence=if($ui -and $staged.forceFeedbackOff -eq $true) { 'FFB-off binding-host evidence (no attended foreground or force claim)' } else { $null }
    @{outcome=$outcome;evidence=$evidence;forceFeedbackOff=($staged.forceFeedbackOff -eq $true);exited=$game.HasExited;windowTitle=$game.MainWindowTitle;observedUtc=[DateTime]::UtcNow.ToString('o');sharedLines=$lines;nativeVerdict=$nativeVerdict;sharedCar=$sharedCarSummary;ui=$uiSummary;activation=$activation;replayResult=$replayLines} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath "$Result/observation.json"
    if($evidence) { Write-Output "Evidence label: $evidence" }
    Write-Output "Observation: $outcome$(if($SharedCar) { '; shared car: ' + ($sharedCarSummary | ConvertTo-Json -Compress) } elseif($native) { '; ' + $(if($nativeVerdict) { $nativeVerdict -replace "`n", '; ' } else { 'no native verdict (inspect capture and logs)' }) } elseif($replay) { '; ' + $(if($replayLines.Count) { $replayLines[0] } else { 'no replay result (inspect replay and logs)' }) })"
} finally {
    if($game -and !$game.HasExited) {
        $game.Refresh()
        # Close only this exact child identity. Never command a Steam replacement.
        if($game.StartTime.ToUniversalTime() -ne $processStart -or ![string]::Equals($game.MainModule.FileName,(Join-Path $GameDir 'artofrally.exe'),[StringComparison]::OrdinalIgnoreCase)) { throw 'Child identity changed; backup and lease retained.' }
        $null=$game.CloseMainWindow(); $null=$game.WaitForExit(20000)
    }
    Assert-GameClosed
    if(Test-Path -LiteralPath $umLog) { Copy-Item -LiteralPath $umLog -Destination (Join-Path $Result 'umm-log.txt') }
    if($ui -and $launchAttempted) { @(Get-ChildItem -LiteralPath $mod -File | ForEach-Object { [pscustomobject]@{name=$_.Name;sha256=(Hash $_.FullName)} }) | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$Result/ui-copy-mod-after.json" }
    $errors=[Collections.Generic.List[string]]::new()
    $forceViolation=$null
    if($forceOff -and $launchAttempted) {
        # The launch's own UMM log: the mod loaded with ffb=False and never tried to initialise a force device.
        $ummLines=if(Test-Path -LiteralPath (Join-Path $Result 'umm-log.txt')) { @(Get-Content -LiteralPath (Join-Path $Result 'umm-log.txt')) } else { @() }
        $loadedOff=@($ummLines -match '\[ArtOfSimRally\] Loaded - .*ffb=False').Count -gt 0
        $attempts=@($ummLines -match 'Force feedback (device initialised|unavailable)')
        @{loadedWithFfbOff=$loadedOff;forceAttempts=$attempts} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$Result/force-check.json"
        if(!$loadedOff -or $attempts.Count) { $forceViolation='Force feedback was not verifiably off in this launch; see force-check.json.' }
    }
    if($replay -and $launchAttempted -and $null -ne $ffbBefore) {
        # Replay reads no input or force device (sana 36236): this launch's native log opens none. The log may
        # have rotated at startup, in which case all of it belongs to this launch.
        $ffbLog=Join-Path $artLocal 'ffb.log'
        $ffbAfter=if(Test-Path -LiteralPath $ffbLog) { [IO.File]::ReadAllText($ffbLog) } else { '' }
        $rotated=!$ffbAfter.StartsWith($ffbBefore,[StringComparison]::Ordinal)
        $launchLines=@(($(if($rotated) { $ffbAfter } else { $ffbAfter.Substring($ffbBefore.Length) })) -split "`r?`n" | Where-Object { $_ })
        $opens=@($launchLines | Where-Object { $_ -match 'OpenReadDevice|OpenAuxDevice|InitDirectInput|OpenCandidate' })
        @{rotated=$rotated;launchLines=$launchLines.Count;deviceOpens=$opens} | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath "$Result/device-check.json"
        if($opens.Count) { $forceViolation='Replay opened an input or force device; see device-check.json.' }
    }
    function Attempt([scriptblock]$work) { try { & $work } catch { $errors.Add($_.Exception.Message); Write-Warning $_.Exception.Message } }
    if($ready -and $recorded) {
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
        # New files (Core's shared-input run folders, Unity output) are kept as evidence, then removed.
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
        Attempt { $runs=Join-Path $artLocal 'shared-input'; if((Test-Path -LiteralPath $runs) -and !@(Get-ChildItem -LiteralPath $runs -Recurse -File).Count -and !($artBefore | Where-Object { Within $_ $runs })) { Remove-Item -LiteralPath $runs -Recurse } }
    }
    if($environmentSaved -and ($launchAttempted -or $routeApplied)) { Attempt { Restore-SessionEnvironment "$Result/shared-environment" } }
    foreach($pin in $ownerPins) { Attempt { if((Hash $pin.path) -ne $pin.sha256) { throw "Owner file differs; retained for review, never overwritten: $($pin.path)" } } }
    if($errors.Count) { $errors | Set-Content -LiteralPath "$Result/restore-errors.txt"; throw "Restoration incomplete; backup and lease retained: $Result" }
    [DateTime]::UtcNow.ToString('o') | Set-Content -LiteralPath "$Result/restored.txt"
    if($lease) { $null=Exit-StageRigLease -Lease $lease }
    if($forceViolation) { throw $forceViolation }
}
