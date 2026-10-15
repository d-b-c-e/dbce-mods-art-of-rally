# Pure checksum-sidecar validation. SHA-256 hex is case-insensitive; the
# packaged filename and the two-space sidecar separator remain exact.
function Test-StandaloneSetupChecksumLine {
    param([string]$Line, [string]$ExpectedSha256, [string]$ExpectedFilename)
    if ($ExpectedSha256 -cnotmatch '^[A-Fa-f0-9]{64}$') { return $false }
    $entry = [regex]::Match($Line, '\A([A-Fa-f0-9]{64})  (.+)\z')
    return $entry.Success -and
        [string]::Equals($entry.Groups[1].Value, $ExpectedSha256, [StringComparison]::OrdinalIgnoreCase) -and
        [string]::Equals($entry.Groups[2].Value, $ExpectedFilename, [StringComparison]::Ordinal)
}
