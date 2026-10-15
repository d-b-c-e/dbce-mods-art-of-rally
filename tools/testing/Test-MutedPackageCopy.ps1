#Requires -Version 7.0
# Fake package/copy checks. The pinned native is copied as opaque bytes, never loaded.
param([Parameter(Mandatory)][string]$MutedNative)
$ErrorActionPreference = 'Stop'
$pin = 'DD3013147821364AD8795F76B4500FB479F669E6BD89576F83E725A677273414'
function Hash([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }
if ((Hash $MutedNative) -cne $pin) { throw 'Fixture needs the pinned DD301 native; no DLL is loaded.' }
$root = Join-Path ([IO.Path]::GetTempPath()) ('art-muted-package-' + [guid]::NewGuid().ToString('N'))
$prepare = Join-Path $PSScriptRoot 'Prepare-SharedInputCopy.ps1'
$checks = 0
function Check([bool]$ok, [string]$why) { if (-not $ok) { throw "FAILED: $why" }; $script:checks++ }
function Write-Text([string]$p, [string]$value) { $null = [IO.Directory]::CreateDirectory((Split-Path $p)); [IO.File]::WriteAllText($p, $value) }
function Refused([scriptblock]$action, [string]$message) {
    try { & $action | Out-Null } catch { Check ($_.Exception.Message -like "*$message*") "refusal: $message"; return }
    throw "Expected refusal: $message"
}
try {
    $pkg = Join-Path $root 'package'; $owner = Join-Path $root 'owner'
    Write-Text "$owner/Mods/ArtOfSimRally/Settings.xml" '<Settings><ForceFeedbackEnabled>true</ForceFeedbackEnabled><Strength>50</Strength></Settings>'
    $profile = Join-Path $root 'profile.ini'; Write-Text $profile '[Controls]'
    foreach ($name in @('ArtOfSimRally.Mod.dll','Info.json','dbce_shim.dll','UnityForceFeedback.dll')) { Write-Text "$pkg/ArtOfSimRally/$name" "fake $name" }
    $packagedNative = Hash "$pkg/ArtOfSimRally/UnityForceFeedback.dll"; $shim = Hash "$pkg/ArtOfSimRally/dbce_shim.dll"
    Write-Text "$pkg/ArtOfSimRally/build.json" (@{ identity = 'fake'; sharedInputCandidate = @{ shimSha256 = $shim; nativeSha256 = $packagedNative } } | ConvertTo-Json -Depth 4)
    $files = [ordered]@{}
    foreach ($file in Get-ChildItem -LiteralPath $pkg -File -Recurse) { $files[[IO.Path]::GetRelativePath($pkg,$file.FullName).Replace('\','/')] = Hash $file.FullName }
    Write-Text "$pkg/manifest.json" (@{ schema = 1; release = 'fake'; files = $files } | ConvertTo-Json -Depth 4)
    $zip = Join-Path $root 'package.zip'; Compress-Archive -Path "$pkg/*" -DestinationPath $zip
    $zipHash = Hash $zip; $ownerHash = Hash "$owner/Mods/ArtOfSimRally/Settings.xml"
    $common = @{ SourceGame = $owner; Profile = $profile; PackageZip = $zip; ExpectedPackageSha256 = $zipHash; ExpectedShimSha256 = $shim }
    foreach ($mode in @('original','muted')) {
        $copy = Join-Path $root $mode; $result = Join-Path $root "result-$mode"
        Write-Text "$copy/artofrally.exe" 'fake exe'; $null = [IO.Directory]::CreateDirectory("$copy/artofrally_Data/Plugins/x86_64")
        $extra = @{}; if ($mode -eq 'muted') { $extra.MutedNative = $MutedNative }
        & $prepare @common @extra -GameCopy $copy -Result $result -ForceFeedbackOff | Out-Null
        $s = Get-Content -LiteralPath "$result/staged.json" -Raw | ConvertFrom-Json
        $expected = if ($mode -eq 'muted') { $pin } else { $packagedNative }
        foreach ($relative in @('Mods/ArtOfSimRally/UnityForceFeedback.dll','artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')) {
            Check ((Hash "$copy/$relative") -eq $expected -and $s.files.$relative -eq $expected) "$mode native copy and staged pin: $relative"
        }
        Check ($s.package.nativeSha256 -eq $packagedNative -and (Hash "$result/package/ArtOfSimRally/UnityForceFeedback.dll") -eq $packagedNative) "$mode keeps package receipt and extracted bytes"
        Check ((Hash "$copy/Mods/ArtOfSimRally/ArtOfSimRally.Mod.dll") -eq $files.'ArtOfSimRally/ArtOfSimRally.Mod.dll') "$mode keeps packaged managed payload"
        Check ($s.nativeSha256 -eq $expected -and ([bool]$s.nativeSubstitution -eq ($mode -eq 'muted'))) "$mode records exercised native separately"
        if ($mode -eq 'muted') { Check ($s.nativeSubstitution.packagedSha256 -eq $packagedNative -and $s.nativeSubstitution.exercisedSha256 -eq $pin -and $s.nativeSubstitution.exercisedPaths.Count -eq 2) 'both hashes and paths recorded' }
    }
    Refused { & $prepare @common -GameCopy "$root/unused" -Result "$root/refuse-native" -ForceFeedbackOff -MutedNative "$pkg/ArtOfSimRally/UnityForceFeedback.dll" } 'not the pinned DD301'
    Check (-not (Test-Path -LiteralPath "$root/refuse-native")) 'unknown native refused before staging'
    Refused { & $prepare @common -GameCopy "$root/unused" -Result "$root/refuse-force" -MutedNative $MutedNative } 'requires -PackageZip and -ForceFeedbackOff'
    Refused { & $prepare -GameCopy "$root/unused" -SourceGame $owner -Result "$root/refuse-package" -MutedNative $MutedNative -ForceFeedbackOff } 'requires -PackageZip and -ForceFeedbackOff'
    # A valid mute pin must not let a changed payload evade the original package manifest.
    Write-Text "$pkg/ArtOfSimRally/ArtOfSimRally.Mod.dll" 'tampered managed payload'
    $badZip = Join-Path $root 'tampered.zip'; Compress-Archive -Path "$pkg/*" -DestinationPath $badZip
    $bad = $common.Clone(); $bad.PackageZip = $badZip; $bad.ExpectedPackageSha256 = Hash $badZip
    Refused { & $prepare @bad -GameCopy "$root/original" -Result "$root/refuse-manifest" -MutedNative $MutedNative -ForceFeedbackOff } 'differs from its manifest'
    Check ((Hash "$root/original/Mods/ArtOfSimRally/UnityForceFeedback.dll") -eq $packagedNative) 'manifest refusal occurs before private native substitution'
    Check ((Hash $zip) -eq $zipHash -and (Hash "$owner/Mods/ArtOfSimRally/Settings.xml") -eq $ownerHash) 'ZIP and source settings unchanged'
} finally {
    $resolved = [IO.Path]::GetFullPath($root)
    if ((Split-Path $resolved -Parent) -ne [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') -or (Split-Path $resolved -Leaf) -notlike 'art-muted-package-*') { throw 'Refusing cleanup outside exact temporary fixture root.' }
    if (Test-Path -LiteralPath $resolved) { Remove-Item -LiteralPath $resolved -Recurse -Force }
}
"PASS: $checks muted-package temporary-copy checks; no DLL loaded, game or device started."
