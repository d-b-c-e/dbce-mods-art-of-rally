$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../package/StandaloneSetupChecksum.ps1')
$hash = 'f3cf1345402cd31562b8a6112f440419dc79c6d28ddd276b4dbedfea084fc331'
$filename = 'StandaloneModSetup-private-a8583c59feef-win-x64.zip'
$checks = 0
function Check([bool]$Value) { if (!$Value) { throw "Checksum assertion $checks failed" }; $script:checks++ }
Check (Test-StandaloneSetupChecksumLine ($hash + '  ' + $filename) $hash.ToUpperInvariant() $filename)
Check (Test-StandaloneSetupChecksumLine ($hash.ToUpperInvariant() + '  ' + $filename) $hash $filename)
Check (Test-StandaloneSetupChecksumLine ($hash.Substring(0, 20).ToUpperInvariant() + $hash.Substring(20) + '  ' + $filename) $hash $filename)
Check (!(Test-StandaloneSetupChecksumLine ($hash + '  ' + $filename.ToLowerInvariant()) $hash $filename))
Check (!(Test-StandaloneSetupChecksumLine ($hash + '  ' + $filename + '.old') $hash $filename))
Check (!(Test-StandaloneSetupChecksumLine ('0' + $hash.Substring(1) + '  ' + $filename) $hash $filename))
Check (!(Test-StandaloneSetupChecksumLine ($hash + ' ' + $filename) $hash $filename))
Check (!(Test-StandaloneSetupChecksumLine ($hash + '  ' + $filename + "`nextra") $hash $filename))
Check (!(Test-StandaloneSetupChecksumLine ('g' + $hash.Substring(1) + '  ' + $filename) $hash $filename))
Check (!(Test-StandaloneSetupChecksumLine ($hash + '  ' + $filename) 'invalid' $filename))
Write-Output "PASS $checks pure checksum-sidecar checks; no file writes or child processes."
