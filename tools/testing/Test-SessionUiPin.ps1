[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$dir = Join-Path $root 'lib/session-ui'
$pin = Get-Content (Join-Path $dir 'provenance.json') -Raw | ConvertFrom-Json
foreach ($file in $pin.files) {
    if ($file.file -notin @('SessionStatusPresentation.cs', 'SessionStatusHud.cs', 'LICENSE')) { throw 'Unknown session UI source' }
    if ((Get-FileHash (Join-Path $dir $file.file)).Hash.ToLowerInvariant() -cne $file.sha256) { throw "Shared session UI differs from pin: $($file.file)" }
}
if (@($pin.files).Count -ne 3) { throw 'Incomplete session UI source pin' }
Write-Output "Shared session UI source matches toolkit $($pin.sourceRevision)"
