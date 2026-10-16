#Requires -Version 7.0
# Actual transport reply and caller predicate; no pipe/native/device/process/game.
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
if(!$guard -or !$strip -or !$admit){throw 'Actual transport/caller contract was not located'}
$decode=[scriptblock]::Create($guard.Extent.Text+"`n"+$strip.Extent.Text)
$gate=[scriptblock]::Create($admit.Extent.Text)
$checks=0
foreach($case in @(
    @{Wire='OK native=armed: no force; no sample running; 0 accepted, 0 reads carried an injected sample commands=0';Pass=$true},
    @{Wire='ERROR controls probe not armed for this run';Pass=$false},
    @{Wire='OK stopped';Pass=$false},
    @{Wire='OK OK native=wrong extra prefix';Pass=$false}
)){
    $accepted=$false
    try{$text=$case.Wire;$reply=& $decode;& $gate;$accepted=$true}catch{}
    if($accepted -ne $case.Pass){throw ('Unexpected actual transport/caller verdict: '+$case.Wire)}
    $checks++
}
"PASS: $checks actual reply-contract cases; no pipe/game/native/process calls."
