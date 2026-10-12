# Shared by packaging, the installer, and the RC gate. Integrity, not a signature.
$PayloadFiles = @(
    'ArtOfSimRally/ArtOfSimRally.Mod.dll', 'ArtOfSimRally/Dbce.Wheel.Telemetry.dll', 'ArtOfSimRally/Dbce.Wheel.Ffb.dll',
    'ArtOfSimRally/UnityForceFeedback.dll', 'ArtOfSimRally/Dbce.TripleScreen.Core.dll', 'ArtOfSimRally/Dbce.TripleScreen.Protocol.dll',
    'ArtOfSimRally/Info.json', 'ArtOfSimRally/build.json',
    'LICENSE', 'README.txt', 'Install.bat', 'Uninstall.bat', 'install.ps1', 'verify.ps1'
)
# Shipped only by a private shared-input candidate (package.ps1 -SharedInputCandidate), whose
# build.json pins it and the native it reads through.
$OptionalPayloadFiles = @('ArtOfSimRally/dbce_shim.dll')

function Assert-Payload([string]$Directory) {
    $base = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $manifest = Get-Content -LiteralPath (Join-Path $base 'manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1) { throw 'Unsupported package manifest' }
    $listed = @($manifest.files.PSObject.Properties.Name)
    $expected = @($PayloadFiles) + @($OptionalPayloadFiles | Where-Object { $listed -contains $_ })
    if (@(Compare-Object ($expected | Sort-Object) ($listed | Sort-Object)).Count) {
        throw 'Manifest payload list differs from the release allowlist'
    }
    $actual = @(Get-ChildItem -LiteralPath $base -File -Recurse | ForEach-Object {
        $_.FullName.Substring($base.Length + 1).Replace('\', '/')
    })
    if (@(Compare-Object (($expected + 'manifest.json') | Sort-Object) ($actual | Sort-Object)).Count) {
        throw 'Package has missing or unexpected files'
    }
    foreach ($relative in $expected) {
        $path = Join-Path $base $relative
        if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked payload: $relative" }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $manifest.files.$relative) {
            throw "Package hash mismatch: $relative"
        }
    }
    $info = Get-Content -LiteralPath (Join-Path $base 'ArtOfSimRally/Info.json') -Raw | ConvertFrom-Json
    $build = Get-Content -LiteralPath (Join-Path $base 'ArtOfSimRally/build.json') -Raw | ConvertFrom-Json
    if ($info.Version -ne $build.modVersion -or $build.release -ne $manifest.release) { throw 'Package version mismatch' }
    $candidate = $build.sharedInputCandidate
    if (($listed -contains 'ArtOfSimRally/dbce_shim.dll') -ne [bool]$candidate) { throw 'Shared-input payload and build record disagree' }
    if ($candidate -and ($manifest.files.'ArtOfSimRally/dbce_shim.dll' -ne $candidate.shimSha256 -or
        $manifest.files.'ArtOfSimRally/UnityForceFeedback.dll' -ne $candidate.nativeSha256)) { throw 'Shared-input candidate pins differ from the payload' }
    return $manifest
}
