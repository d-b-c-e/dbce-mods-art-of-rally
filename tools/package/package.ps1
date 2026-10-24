<#
.SYNOPSIS
    Builds a distributable art-of-sim-rally release zip for Unity Mod Manager.

.DESCRIPTION
    Produces dist/ArtOfSimRally-<version>.zip.

    Layout is deliberately dual-purpose: ArtOfSimRally/ sits at the zip root so
    dragging the zip onto Unity Mod Manager installs it correctly, while
    Install.bat next to it gives a double-click install for everyone else. An
    earlier layout nested the mod under Mods/, which made UMM produce
    Mods/Mods/ArtOfSimRally and silently fail to load - reported by a user.

    The native plugin ships inside the mod folder; the installer copies it on to
    artofrally_Data/Plugins/x86_64 as well.
.EXAMPLE
    .\tools\package\package.ps1 -Version 0.1.0
.EXAMPLE
    .\tools\package\package.ps1 -Version 0.1.0-rc.2 -SharedInputCandidate -SharedInputShim D:\private\dbce_shim.dll
    A private owner-test candidate for the opt-in shared Wheelkit profile (romi 32354). It
    also ships the copied-provider dbce_shim.dll and swaps in the native with the read-snapshot
    exports, both pinned in lib/input-shim/CANDIDATE.json. The shim binary is not kept in this
    public repository (romi 32775): pass a private copy, checked against the pin. Without the
    switch nothing changes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Version,
    [switch]$SharedInputCandidate,
    # Research only: compile the optional shim probe. The ordinary shared-input path is present without it.
    [switch]$ResearchShimProbe,
    [string]$SharedInputShim,
    # Private shared-input candidates only: also build the optional controls setup companion ZIP from the pinned helper
    # bundle (standalone-setup.json). The mod ZIP itself does not change.
    [switch]$PrivateStandaloneSetup,
    [string]$StandaloneSetupZip
)

$ErrorActionPreference = 'Stop'
$root  = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ($Version -notmatch '^\d+\.\d+\.\d+(-rc\.[1-9]\d*)?$') { throw 'Use X.Y.Z or X.Y.Z-rc.N' }
$props = [xml](Get-Content -LiteralPath (Join-Path $root 'Version.props') -Raw)
$modVersion = [string]$props.Project.PropertyGroup.ModVersion
if (($Version -split '-')[0] -ne $modVersion) { throw "Version must match Version.props ($modVersion)" }
$info = Get-Content -LiteralPath (Join-Path $root 'src/ArtOfSimRally.Mod/Info.json') -Raw | ConvertFrom-Json
if ($info.Version -ne $modVersion) { throw 'Info.json and Version.props disagree' }
if ($ResearchShimProbe -and -not $SharedInputCandidate) { throw '-ResearchShimProbe requires -SharedInputCandidate' }
# A final shared-input release takes the shim from the official toolkit pin (lib/toolkit/shim, toolkit v0.16.0+);
# release candidates may still pass a private copy of the same pinned build.
$toolkitShim = Join-Path $root 'lib/toolkit/shim/dbce_shim.dll'
if ($SharedInputCandidate -and -not $SharedInputShim -and (Test-Path -LiteralPath $toolkitShim)) { $SharedInputShim = $toolkitShim }
if ($SharedInputCandidate -and -not $SharedInputShim) { throw 'The shared-input candidate needs -SharedInputShim, a private copy of the pinned dbce_shim.dll' }
if ($SharedInputCandidate -and $Version -notmatch '-rc\.' -and ([IO.Path]::GetFullPath($SharedInputShim) -ne [IO.Path]::GetFullPath($toolkitShim) -or $ResearchShimProbe)) {
    throw 'A final shared-input release takes dbce_shim.dll from the official toolkit pin (lib/toolkit/shim) and has no research probe'
}
if ($SharedInputShim -and -not $SharedInputCandidate) { throw '-SharedInputShim is only for -SharedInputCandidate' }
# The helper's art of rally writer needs the candidate's shared-input controls contract; a baseline package gets none.
if ($PrivateStandaloneSetup -and (-not $SharedInputCandidate -or -not $StandaloneSetupZip)) { throw '-PrivateStandaloneSetup needs -SharedInputCandidate and -StandaloneSetupZip (the pinned helper ZIP)' }
if ($StandaloneSetupZip -and -not $PrivateStandaloneSetup) { throw '-StandaloneSetupZip is only for -PrivateStandaloneSetup' }
$flavour = if ($ResearchShimProbe) { '-shared-input-probe' } elseif ($SharedInputCandidate) { '-shared-input' } else { '' }
$dist  = Join-Path $root 'dist'
$stage = Join-Path $dist "stage-$Version$flavour"
$zip = Join-Path $dist "ArtOfSimRally-$Version$flavour.zip"
if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $zip)) {
    throw 'Candidate already exists. Keep its evidence; choose a new RC number.'
}
$companionStage = Join-Path $dist "stage-$Version$flavour-controls-setup"
$companionZip = Join-Path $dist "ArtOfSimRally-$Version$flavour-controls-setup.zip"
if ($PrivateStandaloneSetup -and ((Test-Path -LiteralPath $companionStage) -or (Test-Path -LiteralPath $companionZip))) {
    throw 'Controls setup companion already exists. Keep its evidence; choose a new RC number.'
}
$revision = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source revision' }
$sourceState = if (& git -C $root status --porcelain --untracked-files=all) { 'dirty' } else { 'clean' }
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source state' }
if ($Version -notmatch '-rc\.' -and $sourceState -ne 'clean') { throw 'Final release requires a clean source tree' }
$identity = "$Version+$revision.$sourceState"
. (Join-Path $root 'tools/installer/verify.ps1')

# Validate every pinned artifact before building; do not silently package a
# locally edited vendor binary or omit a missing DLL.
$toolkit = Join-Path $root 'lib/toolkit'
$toolkitPin = (Get-Content -LiteralPath (Join-Path $toolkit 'VERSION') -First 1).Trim()
if ($Version -notmatch '-rc\.' -and $toolkitPin -notmatch '^v\d+\.\d+\.\d+$') {
    throw 'Final release requires an official toolkit release pin; local toolkit builds are for development candidates only'
}
$entries = 0
foreach ($line in Get-Content -LiteralPath (Join-Path $toolkit 'MANIFEST.txt')) {
    if ($line.StartsWith('#') -or [string]::IsNullOrWhiteSpace($line)) { continue }
    if ($line -notmatch '^([A-Fa-f0-9]{64})\s+(.+)$') { throw 'Malformed toolkit manifest' }
    $hash = $Matches[1]; $relative = $Matches[2]
    $vendorPath = [IO.Path]::GetFullPath((Join-Path $toolkit $relative))
    if (-not $vendorPath.StartsWith([IO.Path]::GetFullPath($toolkit) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid toolkit path' }
    if ((Get-FileHash -LiteralPath $vendorPath -Algorithm SHA256).Hash -ne $hash) { throw "Toolkit hash mismatch: $relative" }
    $entries++
}
if ($entries -lt 3) { throw 'Incomplete toolkit manifest' }
$toolkitComponents = Get-Content -LiteralPath (Join-Path $toolkit 'COMPONENT-PROVENANCE.json') -Raw | ConvertFrom-Json
if ($toolkitComponents.schemaVersion -ne 1 -or
    $toolkitComponents.nativeSha256 -cne (Get-FileHash -LiteralPath (Join-Path $toolkit 'native/WheelFfb.dll')).Hash) {
    throw 'Native component provenance does not match the packaged toolkit bytes'
}
$candidate = $null
if ($SharedInputCandidate) {
    $inputShim = Join-Path $root 'lib/input-shim'
    $candidate = Get-Content -LiteralPath (Join-Path $inputShim 'CANDIDATE.json') -Raw | ConvertFrom-Json
    if ($candidate.schema -ne 1) { throw 'Unsupported shared-input candidate record' }
    $SharedInputShim = [IO.Path]::GetFullPath($SharedInputShim)
    foreach ($part in @(@($SharedInputShim, $candidate.shimSha256), @((Join-Path $inputShim 'native/WheelFfb.dll'), $candidate.nativeSha256))) {
        if ((Get-FileHash -LiteralPath $part[0] -Algorithm SHA256).Hash -cne $part[1]) { throw "Shared-input candidate hash mismatch: $($part[0])" }
    }
    # The mod refuses any other shim build, so the package must carry exactly the one it pins.
    $startup = Get-Content -LiteralPath (Join-Path $root 'src/ArtOfSimRally.Mod/SharedInputStartup.cs') -Raw
    if ($startup -notmatch 'ExpectedShimSha256 = "([0-9A-F]{64})"' -or $Matches[1] -cne $candidate.shimSha256) { throw 'dbce_shim.dll is not the build the mod pins' }
    # Shared input reads Art's own wheel handle through these exports; the toolkit pin's native lacks them.
    $nativeText = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes((Join-Path $inputShim 'native/WheelFfb.dll')))
    foreach ($export in @('GetReadDeviceInfoV1', 'ReadDeviceSnapshotV1', 'GetReadDevicePathV1')) {
        if (-not $nativeText.Contains($export + [char]0)) { throw "Shared-input candidate native lacks $export" }
    }
}

Write-Host "Building managed mod..." -ForegroundColor Cyan
# Shared payload delivery and the optional research probe are separate choices; every assembly records its flavour.
$shimProbe = [bool]$ResearchShimProbe
& dotnet build (Join-Path $root 'src\ArtOfSimRally.Mod\ArtOfSimRally.Mod.csproj') -c Release -v q --nologo -warnaserror "-p:ReleaseLabel=$Version" "-p:SourceRevisionId=$revision" "-p:BuildSourceState=$sourceState" "-p:ArtShimProbe=$($shimProbe.ToString().ToLowerInvariant())"
if ($LASTEXITCODE -ne 0) { throw "Managed build failed" }
. (Join-Path $PSScriptRoot 'ShimProbeFlavour.ps1')
$null = Assert-ShimProbeFlavour (Join-Path $root 'src/ArtOfSimRally.Mod/bin/Release/ArtOfSimRally.Mod.dll') $shimProbe
# Inspect the compiled metadata without running game code. A stray recorder
# source file in the production project must fail even if its DLL isn't copied.
& dotnet run --project (Join-Path $root 'tools/testing/Replay/Replay.csproj') -c Release -- --verify-release (Join-Path $root 'src/ArtOfSimRally.Mod/bin/Release/ArtOfSimRally.Mod.dll')
if ($LASTEXITCODE -ne 0) { throw 'Release assembly contains developer recorder code or dependencies.' }

Write-Host "Using verified vendored native plugin..." -ForegroundColor Cyan
# Native FFB layer and telemetry encoder are vendored from dbce-wheel-mod-toolkit (lib\toolkit).
$toolkit = Join-Path $root 'lib\toolkit'
if (-not (Test-Path (Join-Path $toolkit 'native\WheelFfb.dll'))) { throw 'lib\toolkit is missing - run tools\Sync-Toolkit.ps1' }
$modDir    = Join-Path $stage 'ArtOfSimRally'
New-Item -ItemType Directory -Force -Path $modDir | Out-Null

$bin = Join-Path $root 'src\ArtOfSimRally.Mod\bin\Release'
Copy-Item (Join-Path $bin 'ArtOfSimRally.Mod.dll')       $modDir
Copy-Item (Join-Path $bin 'Dbce.Wheel.Telemetry.dll') $modDir
Copy-Item (Join-Path $bin 'Dbce.Wheel.Ffb.dll') $modDir
Copy-Item (Join-Path $bin 'Dbce.TripleScreen.Core.dll') $modDir
Copy-Item (Join-Path $bin 'Dbce.TripleScreen.Protocol.dll') $modDir
Copy-Item (Join-Path $root 'src\ArtOfSimRally.Mod\Info.json') $modDir
$nativeSource = if ($candidate) { Join-Path $root 'lib\input-shim\native\WheelFfb.dll' } else { Join-Path $toolkit 'native\WheelFfb.dll' }
Copy-Item $nativeSource (Join-Path $modDir 'UnityForceFeedback.dll')   # the file name the mod P/Invokes
if ($candidate) { Copy-Item -LiteralPath $SharedInputShim -Destination (Join-Path $modDir 'dbce_shim.dll') }
Copy-Item (Join-Path $root 'LICENSE') $stage
Copy-Item (Join-Path $root 'tools\installer\Install.bat')   $stage
Copy-Item (Join-Path $root 'tools\installer\Uninstall.bat') $stage
Copy-Item (Join-Path $root 'tools\installer\install.ps1')   $stage
Copy-Item (Join-Path $root 'tools\installer\verify.ps1')   $stage
$build = [ordered]@{ schema=1; release=$Version; modVersion=$modVersion; identity=$identity; sourceRevision=$revision; sourceState=$sourceState; toolkitPin=$toolkitPin; builtUtc=[DateTime]::UtcNow.ToString('o') }
  $build['toolkitComponents'] = $toolkitComponents
  # Data-only capability used by Wheelkit; older packages do not receive fields
  # that their runtime ignores merely because the catalog knows newer keys.
  $build['controlsProfileSchema'] = 1
# verify.ps1 accepts dbce_shim.dll only with this record, whose pins must match the payload.
# controls is Wheelkit's contract for this exact assembly (lib/input-shim/CONTROLS.json, romi 33009).
if ($candidate) {
    $build['artShimProbe'] = $shimProbe
    $controls = Get-Content -LiteralPath (Join-Path $root 'lib/input-shim/CONTROLS.json') -Raw | ConvertFrom-Json
    # Schema 3 (sana 162623) needs a Wheelkit helper that admits art-shim-ini-controls-3 (zele a8583c5).
    if ($controls.schema -ne 3 -or $controls.adapter -ne 'art-shim-ini-controls-3') { throw 'Unsupported shared-input controls contract' }
    $controls | Add-Member -NotePropertyName assemblySha256 -NotePropertyValue (Get-FileHash -LiteralPath (Join-Path $modDir 'ArtOfSimRally.Mod.dll') -Algorithm SHA256).Hash
    $build['sharedInputCandidate'] = [ordered]@{ shimSha256=$candidate.shimSha256; shimSource=$candidate.shimSource
        nativeSha256=$candidate.nativeSha256; nativeSource=$candidate.nativeSource; nativeBase=$candidate.nativeBase; controls=$controls }
}
$build | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $modDir 'build.json') -Encoding UTF8
$fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $modDir 'ArtOfSimRally.Mod.dll'))
if ($fileVersion.ProductVersion -ne $identity -or $fileVersion.FileVersion -ne "$modVersion.0") { throw 'Built assembly identity does not match candidate' }

# Keep the player-facing readme editable beside the installer; ship a standalone
# guide with web links, not paths to source-only documents absent from the ZIP.
$readme = (Get-Content -LiteralPath (Join-Path $root 'tools/installer/README.txt') -Raw).Replace('@RELEASE@', $Version)
if ($candidate) { $readme += "`r`n" + (Get-Content -LiteralPath (Join-Path $root 'tools/installer/README-shared-input.txt') -Raw) }
$readme | Set-Content (Join-Path $stage 'README.txt') -Encoding UTF8

$files = [ordered]@{}
$payload = @($PayloadFiles) + $(if ($candidate) { @('ArtOfSimRally/dbce_shim.dll') } else { @() })
foreach ($relative in $payload) { $files[$relative] = (Get-FileHash -LiteralPath (Join-Path $stage $relative) -Algorithm SHA256).Hash }
[ordered]@{ schema=1; release=$Version; files=$files } | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $stage 'manifest.json') -Encoding UTF8
$null = Assert-Payload $stage
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash | Set-Content -LiteralPath "$zip.sha256" -Encoding ASCII

if ($PrivateStandaloneSetup) {
    # Unity Mod Manager unpacks every entry of a dragged mod ZIP into the game's Mods folder, so the optional helper
    # ships as its own companion ZIP, never inside the mod ZIP, and its launcher never copies it into the game.
    & (Join-Path $PSScriptRoot 'controls-setup.ps1') -PackageDirectory $stage `
        -ExpectedManifestSha256 (Get-FileHash -LiteralPath (Join-Path $stage 'manifest.json') -Algorithm SHA256).Hash `
        -StandaloneSetupZip $StandaloneSetupZip -CompanionStage $companionStage -CompanionZip $companionZip
}

Write-Host "Packaged $zip" -ForegroundColor Green
Get-ChildItem $zip | Select-Object Name, Length
