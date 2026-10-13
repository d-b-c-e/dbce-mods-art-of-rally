# Shared by packaging, the installer, the RC gate and the optional controls setup launcher. Integrity, not a signature.

function Find-ArtOfRally {
    # Steam records its own location in the registry. Reading it is the only
    # reliable way to find a library that is not on C:.
    $steam = $null
    foreach ($key in 'HKCU:\Software\Valve\Steam', 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam') {
        try {
            $v = Get-ItemProperty $key -ErrorAction Stop
            if ($v.SteamPath)   { $steam = $v.SteamPath }
            elseif ($v.InstallPath) { $steam = $v.InstallPath }
            if ($steam) { break }
        } catch { }
    }
    if (-not $steam) { return $null }
    $steam = $steam -replace '/', '\'

    # Every library folder, not just the default one.
    $libraries = @($steam)
    $vdf = Join-Path $steam 'steamapps\libraryfolders.vdf'
    if (Test-Path -LiteralPath $vdf) {
        foreach ($line in Get-Content -LiteralPath $vdf) {
            if ($line -match '"path"\s+"(.+?)"') {
                $libraries += ($Matches[1] -replace '\\\\', '\')
            }
        }
    }

    # Deduplicate case-insensitively: the registry value and libraryfolders.vdf
    # routinely disagree on casing for the same path, which would otherwise scan
    # the same library twice.
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    foreach ($lib in $libraries) {
        if (-not $seen.Add($lib.TrimEnd([char]92))) { continue }   # 92 = backslash
        $candidate = Join-Path $lib 'steamapps\common\artofrally'
        if (Test-Path -LiteralPath (Join-Path $candidate 'artofrally.exe') -PathType Leaf) { return $candidate }
    }
    return $null
}

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
    if ($candidate -and (-not $candidate.controls -or $candidate.controls.assemblySha256 -ne $manifest.files.'ArtOfSimRally/ArtOfSimRally.Mod.dll')) {
        throw 'Shared-input controls contract does not name this package''s mod assembly'
    }
    return $manifest
}
