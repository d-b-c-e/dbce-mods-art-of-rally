[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dir = Join-Path $root 'lib/display'
$pin = Get-Content (Join-Path $dir 'provenance.json') -Raw | ConvertFrom-Json
foreach ($file in $pin.files) {
    if ($file.file -notin @('RacingRow.cs', 'WindowsMonitors.cs', 'WindowLanding.cs', 'LICENSE')) { throw 'Unknown racing-row source' }
    if ((Get-FileHash (Join-Path $dir $file.file)).Hash.ToLowerInvariant() -cne $file.sha256) { throw "Shared racing-row source differs from pin: $($file.file)" }
}
if (@($pin.files).Count -ne 4) { throw 'Incomplete racing-row source pin' }
Write-Output "Shared racing-row source matches toolkit $($pin.sourceRevision)"
