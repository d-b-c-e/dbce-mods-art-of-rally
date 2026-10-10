#Requires -Version 7.0
<# Talks only to a separately armed, hash-pinned developer probe. Native raw
   input stays behind its process-latched force fence. Never retries a command. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Status','Raw','Stop')][string]$Command,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{32}$')][string]$Nonce,
    [Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$GameProcessId,
    [Parameter(Mandatory)][DateTimeOffset]$ExpectedStartTimeUtc,
    [string]$GameDir='D:/Program Files (x86)/Steam/steamapps/common/artofrally',
    [string]$Raw
)
$ErrorActionPreference='Stop'
if($Nonce -eq ('0'*32)) { throw 'Empty run nonce.' }
if($Command -eq 'Raw') {
    if(!$Raw -or !$Raw.StartsWith('inject raw ',[StringComparison]::Ordinal) -or $Raw.Length -gt 512 -or $Raw -match '[^\x20-\x7e]') {
        throw 'Raw command must be inject raw plus printable ASCII, at most 512 bytes.'
    }
} elseif($Raw) { throw '-Raw is only valid with -Command Raw.' }
$expectedExe=[IO.Path]::GetFullPath((Join-Path $GameDir 'artofrally.exe'))
function Assert-Identity {
    $process=Get-Process -Id $GameProcessId -ErrorAction Stop
    try {
        if($process.HasExited -or $process.ProcessName -cne 'artofrally' -or
           $process.StartTime.ToUniversalTime().Ticks -ne $ExpectedStartTimeUtc.UtcTicks -or
           ![string]::Equals($process.MainModule.FileName,$expectedExe,[StringComparison]::OrdinalIgnoreCase)) {
            throw 'Game process identity changed; no further commands may be sent.'
        }
    } finally { $process.Dispose() }
}
Assert-Identity
. (Join-Path $PSScriptRoot 'ControlsPipe.ps1')
$line='CONTROLS '+$Command.ToUpperInvariant()+' '+$Nonce
if($Command -eq 'Raw') { $line+=' '+$Raw }
$reply=Invoke-ControlsPipeCommand -PipeName "ArtOfSimRally.DevRecorder.$GameProcessId" -Line $line
Assert-Identity
$reply
