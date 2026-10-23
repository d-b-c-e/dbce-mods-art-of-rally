<# Private companion-only packaging from an existing verified shared-input RC. Never builds or changes the mod. #>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$PackageDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedManifestSha256,
    [Parameter(Mandatory)][string]$StandaloneSetupZip,
    [Parameter(Mandatory)][string]$CompanionStage,
    [Parameter(Mandatory)][string]$CompanionZip
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $root 'tools/installer/verify.ps1')
if ((Get-FileHash -LiteralPath (Join-Path $PackageDirectory 'manifest.json') -Algorithm SHA256).Hash -ne $ExpectedManifestSha256) { throw 'Frozen mod manifest differs from its expected hash' }
$manifest = Assert-Payload $PackageDirectory
$build = Get-Content -LiteralPath (Join-Path $PackageDirectory 'ArtOfSimRally/build.json') -Raw | ConvertFrom-Json
if (-not $build.sharedInputCandidate -or $manifest.release -notmatch '-rc\.') { throw 'Controls setup companion requires a private shared-input RC' }
$Version = $manifest.release
$identity = $build.identity
foreach ($path in @($CompanionStage, $CompanionZip, "$CompanionZip.sha256")) {
    if (Test-Path -LiteralPath $path) { throw 'Controls setup companion already exists. Keep its evidence; choose fresh output paths.' }
}
$inputs = @([IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\') + '\', [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($StandaloneSetupZip)).TrimEnd('\') + '\')
$CompanionStage = [IO.Path]::GetFullPath($CompanionStage).TrimEnd('\')
$CompanionZip = [IO.Path]::GetFullPath($CompanionZip)
foreach ($target in @($CompanionStage, $CompanionZip)) {
    foreach ($inputPath in $inputs) {
        if (($target + '\').StartsWith($inputPath, [StringComparison]::OrdinalIgnoreCase)) { throw 'Companion output must be separate from its inputs' }
    }
}
if ($CompanionZip.StartsWith($CompanionStage + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Companion ZIP must be outside its stage' }
$setupPin = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'standalone-setup.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'StandaloneSetupChecksum.ps1')
$StandaloneSetupZip = [IO.Path]::GetFullPath($StandaloneSetupZip)
if ([IO.Path]::GetFileName($StandaloneSetupZip) -cne $setupPin.zip -or (Get-FileHash -LiteralPath $StandaloneSetupZip -Algorithm SHA256).Hash -ne $setupPin.zipSha256 -or
    !(Test-StandaloneSetupChecksumLine (Get-Content -LiteralPath "$StandaloneSetupZip.sha256" -Raw).TrimEnd("`r", "`n") $setupPin.zipSha256 $setupPin.zip)) {
    throw 'Controls setup helper ZIP or its sidecar differs from standalone-setup.json'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($StandaloneSetupZip)
try {
    if ($archive.Entries.Count -ne $setupPin.files + 1) { throw 'Helper ZIP entry count differs from its pin' }
} finally { $archive.Dispose() }
$setupRoot = Join-Path $companionStage 'ModSetup'
Expand-Archive -LiteralPath $StandaloneSetupZip -DestinationPath $setupRoot
if ((Get-FileHash -LiteralPath (Join-Path $setupRoot 'bundle-manifest.json') -Algorithm SHA256).Hash -ne $setupPin.bundleManifestSha256) { throw 'Helper bundle manifest differs from its pin' }
$bundle = Get-Content -LiteralPath (Join-Path $setupRoot 'bundle-manifest.json') -Raw | ConvertFrom-Json
if ($bundle.kind -ne 'private-standalone-helper' -or $bundle.sourceCommit -ne $setupPin.sourceCommit) { throw 'Helper source differs from its pin' }
if (@($bundle.files).Count -ne $setupPin.files -or @(Get-ChildItem -LiteralPath $setupRoot -File -Recurse).Count -ne $setupPin.files + 1) { throw 'Extracted helper file count differs from its pin' }
foreach ($file in $bundle.files) {
    if ((Get-FileHash -LiteralPath (Join-Path $setupRoot $file.path) -Algorithm SHA256).Hash -ne $file.sha256) { throw "Extracted helper differs from its bundle manifest: $($file.path)" }
}
$controlsSetup = Join-Path $root 'tools\installer\controls-setup'
foreach ($name in @('Set Up Controls.bat', 'Setup-Controls.ps1', 'README.txt')) { Copy-Item -LiteralPath (Join-Path $controlsSetup $name) -Destination $companionStage }
Copy-Item -LiteralPath (Join-Path $root 'tools\installer\verify.ps1') -Destination $companionStage   # Find-ArtOfRally, shared with the installer
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $companionStage
$companionBase = [IO.Path]::GetFullPath($companionStage).TrimEnd('\')
$companionFiles = [ordered]@{}
foreach ($file in Get-ChildItem -LiteralPath $companionBase -File -Recurse | Sort-Object FullName) {
    $companionFiles[$file.FullName.Substring($companionBase.Length + 1).Replace('\', '/')] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
}
[ordered]@{ schema=1; kind='art-controls-setup-companion'; release=$Version; identity=$identity
    standaloneSetup=[ordered]@{ zip=$setupPin.zip; zipSha256=$setupPin.zipSha256; sourceCommit=$setupPin.sourceCommit; bundleManifestSha256=$setupPin.bundleManifestSha256; files=$setupPin.files }
    files=$companionFiles } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $companionStage 'companion-manifest.json') -Encoding UTF8
Compress-Archive -Path (Join-Path $companionStage '*') -DestinationPath $companionZip
(Get-FileHash -LiteralPath $companionZip -Algorithm SHA256).Hash | Set-Content -LiteralPath "$companionZip.sha256" -Encoding ASCII
Write-Host "Packaged $companionZip" -ForegroundColor Green
