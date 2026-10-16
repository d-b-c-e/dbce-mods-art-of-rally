#Requires -Version 7.0
# Actual transport replies and Status/Stop caller predicates; no pipe/native/device/process/game.
$ErrorActionPreference='Stop'
function Parse([string]$Name){
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $Name),[ref]$tokens,[ref]$errors)
    if($errors.Count){throw ('Parse failed: '+$Name)}
    return $ast
}
$transport=Parse 'ControlsPipe.ps1'
$guard=$transport.Find({param($n) $n -is [Management.Automation.Language.IfStatementAst] -and $n.Extent.Text.StartsWith('if(!$text.StartsWith(')},$true)
$strip=$transport.Find({param($n) $n -is [Management.Automation.Language.ReturnStatementAst] -and $n.Extent.Text -ceq 'return $text.Substring(3)'},$true)
$caller=Parse 'Invoke-SharedInputCopy.ps1'
$admit=$caller.Find({param($n) $n -is [Management.Automation.Language.IfStatementAst] -and $n.Extent.Text.StartsWith('if($reply -notmatch ') -and $n.Extent.Text.Contains("throw 'Controls probe status refused; no action tail.'")},$true)
$stop=$caller.Find({param($n) $n -is [Management.Automation.Language.IfStatementAst] -and $n.Extent.Text.StartsWith('if($reply -notmatch ') -and $n.Extent.Text.Contains("throw 'Controls Stop refused.'")},$true)
if(!$guard -or !$strip -or !$admit -or !$stop){throw 'Actual transport/caller contract was not located'}
$decode=[scriptblock]::Create($guard.Extent.Text+"`n"+$strip.Extent.Text)
$gates=@{Status=[scriptblock]::Create($admit.Extent.Text);Stop=[scriptblock]::Create($stop.Extent.Text)}
$recorder=Get-Content (Join-Path $PSScriptRoot 'Recorder/ControlsProbe.cs') -Raw
$stopWire=[regex]::Match($recorder,'return "(OK controls test stopped;[^"\r\n]*)"').Groups[1].Value
if(!$stopWire){throw 'Actual recorder Stop wire reply was not located'}
$checks=0
foreach($case in @(
    @{Kind='Status';Wire='OK native=armed: no force; no sample running; 0 accepted, 0 reads carried an injected sample commands=0';Pass=$true},
    @{Kind='Status';Wire='ERROR controls probe not armed for this run';Pass=$false},
    @{Kind='Status';Wire=$stopWire;Pass=$false},
    @{Kind='Status';Wire='OK OK native=wrong extra prefix';Pass=$false},
    @{Kind='Stop';Wire=$stopWire;Pass=$true},
    @{Kind='Stop';Wire='ERROR controls probe not armed for this run';Pass=$false},
    @{Kind='Stop';Wire='OK native=armed: no force';Pass=$false},
    @{Kind='Stop';Wire=('OK '+$stopWire);Pass=$false}
)){
    $accepted=$false
    try{$text=$case.Wire;$reply=& $decode;& $gates[$case.Kind];$accepted=$true}catch{}
    if($accepted -ne $case.Pass){throw ('Unexpected actual '+$case.Kind+' transport/caller verdict: '+$case.Wire)}
    $checks++
}
"PASS: $checks actual Status/Stop reply-contract cases; no pipe/game/native/process calls."
