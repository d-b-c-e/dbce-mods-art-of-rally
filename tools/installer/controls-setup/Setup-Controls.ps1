#Requires -Version 5.1
<#
.SYNOPSIS
    Optional, private: set up art of sim rally's shared-input wheel controls without Wheelkit.

.DESCRIPTION
    Run from the extracted controls setup download, normally by double-clicking Set Up Controls.bat. It verifies the
    whole download, finds art of rally the same way the mod installer does, checks that the shared-input package of art
    of sim rally is installed and the game is closed, then opens the DBCE mod setup helper for art of rally only.
    For controls the helper writes the controls profile and the shared-input opt-in; on its Screens tab, only the screen
    measurements entered. It never writes force feedback, screen mode or resolution, or art of rally's switches.
    The helper runs from this folder and is never copied into the game folder.

.PARAMETER GameDir
    Skip detection and use this art of rally folder.

.PARAMETER PlanOnly
    Verify and print the exact helper command without starting it.
#>
[CmdletBinding()]
param([string]$GameDir, [switch]$PlanOnly)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here 'verify.ps1')   # Find-ArtOfRally, shared with the mod installer

function Assert-Companion([string]$Directory) {
    $base = [IO.Path]::GetFullPath($Directory).TrimEnd('\', '/')
    $manifest = Get-Content -LiteralPath (Join-Path $base 'companion-manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or $manifest.kind -ne 'art-controls-setup-companion') { throw 'Unsupported controls setup download' }
    $listed = @($manifest.files.PSObject.Properties.Name)
    $actual = @(Get-ChildItem -LiteralPath $base -File -Recurse | ForEach-Object { $_.FullName.Substring($base.Length + 1).Replace('\', '/') } |
        Where-Object { $_ -ne 'companion-manifest.json' })
    if (@(Compare-Object ($listed | Sort-Object) ($actual | Sort-Object)).Count) { throw 'The download has missing or unexpected files' }
    foreach ($relative in $listed) {
        $path = Join-Path $base $relative
        if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked file: $relative" }
        if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $manifest.files.$relative) { throw "Changed file: $relative" }
    }
    # The helper is .NET: its executable alone does not identify the managed code beside it. Every bundle file must be
    # the pinned bundle's own.
    $pin = $manifest.standaloneSetup
    if ($manifest.files.'ModSetup/bundle-manifest.json' -ne $pin.bundleManifestSha256) { throw 'The helper differs from its pin' }
    $bundle = Get-Content -LiteralPath (Join-Path $base 'ModSetup/bundle-manifest.json') -Raw | ConvertFrom-Json
    if ($bundle.kind -ne 'private-standalone-helper' -or $bundle.sourceCommit -ne $pin.sourceCommit -or @($bundle.files).Count -ne $pin.files) { throw 'Unexpected helper identity' }
    foreach ($file in $bundle.files) {
        if ($manifest.files.('ModSetup/' + $file.path) -ne $file.sha256) { throw "Helper file differs from its bundle manifest: $($file.path)" }
    }
    return $manifest
}

try { $null = Assert-Companion $here } catch {
    throw "The controls setup download is incomplete or has changed ($($_.Exception.Message)). Download it again and extract it into a fresh folder."
}
if (-not $GameDir) { $GameDir = Find-ArtOfRally }
if (-not $GameDir -or -not (Test-Path -LiteralPath (Join-Path $GameDir 'artofrally.exe') -PathType Leaf)) {
    throw 'Could not find art of rally. Run: Set Up Controls.bat -GameDir "D:\Games\artofrally" with the folder that contains artofrally.exe.'
}
$GameDir = [IO.Path]::GetFullPath($GameDir).TrimEnd('\', '/')
$build = Join-Path $GameDir 'Mods\ArtOfSimRally\build.json'
if (-not (Test-Path -LiteralPath $build -PathType Leaf)) { throw 'Install art of sim rally first (its Install.bat), then set up controls.' }
# The helper qualifies the installed candidate itself; this only gives a clear message for the baseline package.
if (-not (Get-Content -LiteralPath $build -Raw | ConvertFrom-Json).sharedInputCandidate) {
    throw 'This setup needs the shared-input package of art of sim rally. Install that package first, then set up controls.'
}
if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Close art of rally, then set up controls. No process will be stopped.' }
$helperRoot = Join-Path $here 'ModSetup'
$helper = Join-Path $helperRoot 'DbceModSetup.exe'
$arguments = '--game art-of-rally --game-dir "' + $GameDir + '"'
if ($PlanOnly) { Write-Output ('PLAN ONLY: "' + $helper + '" ' + $arguments); return }
Write-Host 'Opening setup for art of rally. Choose Preview, then Apply. Force feedback, screen mode and resolution are not changed.'
$process = Start-Process -FilePath $helper -ArgumentList $arguments -WorkingDirectory $helperRoot -PassThru -Wait
exit $process.ExitCode
