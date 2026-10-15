#Requires -Version 7.0
# Copy-only staging for the opt-in SharedInput candidate in an existing disposable game copy.
# Never touches the owner installation, never starts a process, never opens a device.
# -PackageZip (sana 43671/43761): stage an exact release package's payload instead of the build output. Every file is
# checked against the ZIP's own manifest.json, and the shim and native against its build.json sharedInputCandidate; the
# ZIP itself is pinned by -ExpectedPackageSha256. Nothing is built or rebaked.
# -AwaitApply (with -PackageZip): stage for Wheelkit's production Apply on the copy. No dbce-controls.ini is staged (an
# earlier copy-only one is removed) and SharedInput is left as the settings source has it, so the Apply's write is
# exercised. staged.json is marked applyPending; Confirm-SharedInputApply.ps1 pins what the Apply wrote before any run.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$GameCopy,
      [Parameter(Mandatory)][string]$SourceGame,
      [string]$Profile,
      [string]$Shim,
      [string]$Native,
      [Parameter(Mandatory)][string]$Result,
      [string]$PackageZip,
      [string]$ExpectedPackageSha256,
      # Explicit test-copy substitution only: keep the verified package receipt unchanged, but exercise the known
      # load-time-muted DD301 native in both private-copy locations. Never qualifies the package's production native.
      [string]$MutedNative,
      [switch]$AwaitApply,
      # The reviewed private-candidate shim (lib/input-shim/CANDIDATE.json), the same pin the mod and package.ps1 enforce.
      [string]$ExpectedShimSha256 = (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../lib/input-shim/CANDIDATE.json') -Raw | ConvertFrom-Json).shimSha256,
      # The toolkit snapshot candidate WheelFfb with the shared read exports (Observe/Apply pin).
      [string]$ExpectedNativeSha256 = 'DD3013147821364AD8795F76B4500FB479F669E6BD89576F83E725A677273414',
      # Native-script coexistence (Invoke-SharedInputCopy -RouteSource): also stage the dev recorder. Without it any
      # earlier recorder is removed from the copy.
      [switch]$Recorder,
      # Copy-only Settings.xml with ForceFeedbackEnabled=false, so the mod never initialises a force device. Required
      # for a native without the DBCE_FFB_MUTE load-time latch (the native-0.9.1 read-snapshot port 44E46799). The
      # owner file is only read; every force tuning value is left as it is.
      [switch]$ForceFeedbackOff,
      # Copy-only: start from this Settings.xml (for example Wheelkit's Apply output, romi 33299) instead of the owner's.
      # It must already select SharedInput. The owner file is still pinned and checked unchanged.
      [string]$SettingsSource)
$ErrorActionPreference = 'Stop'
function Full([string]$p) { [IO.Path]::GetFullPath($p).TrimEnd('\') }
function Hash([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }
function Within([string]$p, [string]$parent) { $p.Equals($parent, [StringComparison]::OrdinalIgnoreCase) -or $p.StartsWith($parent + '\', [StringComparison]::OrdinalIgnoreCase) }
function Plain([string]$p) {
    $cursor = Full $p
    while ($cursor) {
        if ((Test-Path -LiteralPath $cursor) -and ((Get-Item -LiteralPath $cursor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked path refused: $cursor" }
        $cursor = [IO.Path]::GetDirectoryName($cursor)
    }
}
# The natives with the DBCE_FFB_MUTE load-time latch (toolkit 82c40c4); any other needs -ForceFeedbackOff.
$latchedNatives = @('DD3013147821364AD8795F76B4500FB479F669E6BD89576F83E725A677273414')
$packageMode = [bool]$PackageZip
if ($MutedNative -and (-not $packageMode -or -not $ForceFeedbackOff)) { throw '-MutedNative requires -PackageZip and -ForceFeedbackOff.' }
if ($MutedNative) {
    $MutedNative = Full $MutedNative; Plain $MutedNative
    if ((Hash $MutedNative) -cne $latchedNatives[0]) { throw '-MutedNative is not the pinned DD301 load-time-muted native.' }
}
if ($packageMode) {
    if ($Shim -or $Native) { throw '-PackageZip stages the package''s own shim and native; omit -Shim and -Native.' }
    if (-not $ExpectedPackageSha256) { throw '-PackageZip needs -ExpectedPackageSha256.' }
    if ($AwaitApply -and $Profile) { throw '-AwaitApply stages no profile: Wheelkit''s Apply writes it.' }
    if (-not $AwaitApply -and -not $Profile) { throw 'Give -Profile, or -AwaitApply for a production Apply on the copy.' }
} else {
    if ($AwaitApply) { throw '-AwaitApply is for an exact package (-PackageZip).' }
    if (-not $Profile -or -not $Shim -or -not $Native) { throw 'Build-output staging needs -Profile, -Shim and -Native.' }
}
$repo = Full (Join-Path $PSScriptRoot '../..')
$GameCopy = Full $GameCopy; $SourceGame = Full $SourceGame; $Result = Full $Result
foreach ($p in @($GameCopy, $SourceGame, $Result)) { Plain $p }
if ((Within $GameCopy $SourceGame) -or (Within $SourceGame $GameCopy) -or (Within $Result $GameCopy) -or (Within $Result $SourceGame)) { throw 'Copy, owner and result must be separate.' }
if (Test-Path -LiteralPath $Result) { throw 'Use a new result folder.' }
if (-not (Test-Path -LiteralPath (Join-Path $GameCopy 'artofrally.exe'))) { throw 'Disposable copy has no artofrally.exe; robocopy it first.' }
if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Art is running; no staging.' }
$recorderSources = @((Join-Path $repo 'tools/testing/Recorder/bin/Release/net48/ArtOfSimRally.DevRecorder.dll'), (Join-Path $repo 'tools/testing/Recorder/Info.json'), (Join-Path $repo 'lib/playback/Dbce.Wheel.Playback.dll'))
if ($Recorder) { foreach ($p in $recorderSources) { if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Build the recorder first: $p" } } }
$ownerSettings = Join-Path $SourceGame 'Mods/ArtOfSimRally/Settings.xml'
$mod = Join-Path $GameCopy 'Mods/ArtOfSimRally'
$package = $null
$nativeSubstitution = $null
if ($packageMode) {
    $PackageZip = Full $PackageZip; Plain $PackageZip
    if ((Hash $PackageZip) -ne $ExpectedPackageSha256.ToUpperInvariant()) { throw 'Package ZIP is not the pinned release bytes.' }
    $sources = @() + $(if ($Recorder) { @('tools/testing/Recorder', 'lib/playback') } else { @() })
} else {
    if ((Hash $Shim) -ne $ExpectedShimSha256) { throw 'dbce_shim.dll is not the qualified copied-provider build.' }
    if ((Hash $Native) -ne $ExpectedNativeSha256) { throw 'UnityForceFeedback.dll is not the pinned snapshot candidate native.' }
    $build = Join-Path $repo 'src/ArtOfSimRally.Mod/bin/Release'
    $payload = @('ArtOfSimRally.Mod.dll', 'Dbce.TripleScreen.Core.dll', 'Dbce.TripleScreen.Protocol.dll', 'Dbce.Wheel.Ffb.dll', 'Dbce.Wheel.Telemetry.dll')
    foreach ($n in $payload) { if (-not (Test-Path -LiteralPath (Join-Path $build $n))) { throw "Build output missing: $n (build the solution first)." } }
    $sources = @('src') + $(if ($Recorder) { @('tools/testing/Recorder', 'lib/playback') } else { @() })
}
$commit = (git -C $repo rev-parse HEAD).Trim(); $dirty = if ($sources.Count) { @(git -C $repo status --porcelain -- @sources) } else { @() }
if ($dirty.Count) { throw 'Mod or recorder source has uncommitted changes; stage only a committed build.' }
New-Item -ItemType Directory -Path $Result | Out-Null
if ($packageMode) {
    # The exact package: extracted into the result, every file checked against its own manifest.
    $extract = Join-Path $Result 'package'
    Expand-Archive -LiteralPath $PackageZip -DestinationPath $extract
    $manifest = Get-Content -LiteralPath (Join-Path $extract 'manifest.json') -Raw | ConvertFrom-Json
    if ($manifest.schema -ne 1 -or -not $manifest.files) { throw 'Package manifest is not schema 1.' }
    $listed = @($manifest.files.PSObject.Properties.Name)
    foreach ($entry in $manifest.files.PSObject.Properties) {
        $p = Join-Path $extract $entry.Name; Plain $p
        if (-not (Test-Path -LiteralPath $p -PathType Leaf) -or (Hash $p) -ne $entry.Value) { throw "Package file differs from its manifest: $($entry.Name)" }
    }
    $extra = @(Get-ChildItem -LiteralPath $extract -File -Recurse | ForEach-Object { [IO.Path]::GetRelativePath($extract, $_.FullName).Replace('\', '/') } | Where-Object { $_ -ne 'manifest.json' -and $_ -notin $listed })
    if ($extra.Count) { throw "Package holds files outside its manifest: $($extra -join ', ')" }
    $payload = @($listed | Where-Object { $_.StartsWith('ArtOfSimRally/') } | ForEach-Object { $_.Substring('ArtOfSimRally/'.Length) } | Sort-Object)
    foreach ($required in @('ArtOfSimRally.Mod.dll', 'Info.json', 'build.json', 'dbce_shim.dll', 'UnityForceFeedback.dll')) { if ($required -notin $payload) { throw "Package lacks ArtOfSimRally/$required." } }
    $buildJson = Get-Content -LiteralPath (Join-Path $extract 'ArtOfSimRally/build.json') -Raw | ConvertFrom-Json
    $candidate = $buildJson.sharedInputCandidate
    if (-not $candidate) { throw 'Package build.json declares no sharedInputCandidate.' }
    if ($candidate.shimSha256 -ne $manifest.files.'ArtOfSimRally/dbce_shim.dll' -or $candidate.shimSha256 -ne $ExpectedShimSha256) { throw 'Package shim differs from its build.json or the qualified copied-provider build.' }
    if ($candidate.nativeSha256 -ne $manifest.files.'ArtOfSimRally/UnityForceFeedback.dll') { throw 'Package native differs from its build.json.' }
    $ExpectedNativeSha256 = $candidate.nativeSha256
    if ($ExpectedNativeSha256 -notin $latchedNatives -and -not $ForceFeedbackOff) { throw 'This package''s native has no DBCE_FFB_MUTE latch: stage with -ForceFeedbackOff.' }
    # Only the package's files (and settings or Unity Mod Manager loader caches/backups) may sit in the copy's mod folder.
    if (Test-Path -LiteralPath $mod) {
        $stale = @(Get-ChildItem -LiteralPath $mod -File | Where-Object { $_.Extension -in '.dll', '.json' -and $_.Name -notin $payload -and $_.Name -ne 'desired-layout.json' })
        if ($stale.Count) { throw "Copy mod folder holds files outside the package; restore the copy baseline first: $($stale.Name -join ', ')" }
    } else { New-Item -ItemType Directory -Path $mod | Out-Null }
    foreach ($n in $payload) { Copy-Item -LiteralPath (Join-Path $extract "ArtOfSimRally/$n") -Destination (Join-Path $mod $n) -Force }
    Copy-Item -LiteralPath (Join-Path $extract 'ArtOfSimRally/UnityForceFeedback.dll') -Destination (Join-Path $GameCopy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll') -Force
    $package = [ordered]@{ zip = $PackageZip; sha256 = (Hash $PackageZip); release = $manifest.release; manifestSha256 = (Hash (Join-Path $extract 'manifest.json'))
        identity = $buildJson.identity; shimSha256 = $candidate.shimSha256; nativeSha256 = $candidate.nativeSha256 }
    if ($MutedNative) {
        # Original ZIP, extracted manifest and build.json remain intact. Only the disposable copy's two natives change.
        if ((Hash $MutedNative) -cne $latchedNatives[0]) { throw '-MutedNative changed before substitution.' }
        $targets = @((Join-Path $mod 'UnityForceFeedback.dll'), (Join-Path $GameCopy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll'))
        foreach ($target in $targets) {
            Copy-Item -LiteralPath $MutedNative -Destination $target -Force
            if ((Hash $target) -cne $latchedNatives[0]) { throw 'Muted native copy differs from the DD301 pin.' }
        }
        $nativeSubstitution = [ordered]@{ kind = 'test-copy-load-time-mute'; packagedPath = (Join-Path $extract 'ArtOfSimRally/UnityForceFeedback.dll')
            packagedSha256 = $candidate.nativeSha256; exercisedSource = $MutedNative; exercisedSha256 = $latchedNatives[0]; exercisedPaths = $targets }
        $ExpectedNativeSha256 = $latchedNatives[0]
    }
} else {
    foreach ($n in $payload) { Copy-Item -LiteralPath (Join-Path $build $n) -Destination (Join-Path $mod $n) -Force }
    Copy-Item -LiteralPath (Join-Path $repo 'src/ArtOfSimRally.Mod/Info.json') -Destination (Join-Path $mod 'Info.json') -Force # source, not a build output
    Copy-Item -LiteralPath $Native -Destination (Join-Path $mod 'UnityForceFeedback.dll') -Force
    Copy-Item -LiteralPath $Native -Destination (Join-Path $GameCopy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll') -Force
    Copy-Item -LiteralPath $Shim -Destination (Join-Path $mod 'dbce_shim.dll') -Force
}
$controls = Join-Path $mod 'dbce-controls.ini'
$removedControls = [ordered]@{}
if ($AwaitApply) {
    # The Apply writes the profile; an earlier copy-only one is kept as evidence and removed, with its Wheelkit backup.
    foreach ($p in @($controls, "$controls.wheelkit-backup")) {
        if (Test-Path -LiteralPath $p) {
            $name = [IO.Path]::GetFileName($p)
            Copy-Item -LiteralPath $p -Destination (Join-Path $Result ('removed-' + $name)); $removedControls[$name] = Hash $p
            Remove-Item -LiteralPath $p
        }
    }
} else { Copy-Item -LiteralPath $Profile -Destination $controls -Force }
$recorderDir = Join-Path $GameCopy 'Mods/ArtOfSimRally.DevRecorder'
if (Test-Path -LiteralPath $recorderDir) { Plain $recorderDir; Get-ChildItem -LiteralPath $recorderDir -File | Remove-Item; Remove-Item -LiteralPath $recorderDir }
if ($Recorder) {
    New-Item -ItemType Directory -Path $recorderDir | Out-Null
    foreach ($p in $recorderSources) { Copy-Item -LiteralPath $p -Destination (Join-Path $recorderDir ([IO.Path]::GetFileName($p))) }
}
# The owner's exact settings with only SharedInput turned on (the owner file is read, never written). -AwaitApply leaves
# SharedInput as the source has it: the Apply turns it on.
$settingsFrom = if ($SettingsSource) { Full $SettingsSource } else { $ownerSettings }
[xml]$settings = Get-Content -LiteralPath $settingsFrom -Raw
if ($SettingsSource -and -not $AwaitApply -and $settings.DocumentElement.SharedInput -ne 'true') { throw 'The settings source does not select SharedInput.' }
if ($AwaitApply) {
    if ($settings.DocumentElement.SharedInput -eq 'true') { throw 'The settings source already selects SharedInput; the Apply would not write it.' }
} else {
    $node = $settings.DocumentElement.SelectSingleNode('SharedInput')
    if (-not $node) { $node = $settings.CreateElement('SharedInput'); $null = $settings.DocumentElement.AppendChild($node) }
    $node.InnerText = 'true'
}
if ($ForceFeedbackOff) {
    $ffb = $settings.DocumentElement.SelectSingleNode('ForceFeedbackEnabled')
    if (-not $ffb) { $ffb = $settings.CreateElement('ForceFeedbackEnabled'); $null = $settings.DocumentElement.AppendChild($ffb) }
    $ffb.InnerText = 'false'
}
$settings.Save((Join-Path $mod 'Settings.xml'))
[IO.File]::WriteAllText((Join-Path $GameCopy 'steam_appid.txt'), '550320', [Text.Encoding]::ASCII)
$staged = [ordered]@{
    schema = 1; kind = 'shared-input-ordinary'; utc = [DateTime]::UtcNow.ToString('o'); sourceCommit = $commit; recorder = [bool]$Recorder
    forceFeedbackOff = [bool]$ForceFeedbackOff; nativeSha256 = $ExpectedNativeSha256
    gameCopy = $GameCopy; sourceGame = $SourceGame; ownerSettingsSha256 = Hash $ownerSettings
    settingsSource = $settingsFrom; settingsSourceSha256 = Hash $settingsFrom
    package = $package; nativeSubstitution = $nativeSubstitution; applyPending = [bool]$AwaitApply; removedControls = $removedControls
    files = [ordered]@{}
}
$pinned = if ($packageMode) { $payload } else { $payload + @('Info.json', 'UnityForceFeedback.dll', 'dbce_shim.dll') }
$pinned += @('Settings.xml') + $(if ($AwaitApply) { @() } else { @('dbce-controls.ini') })
foreach ($n in $pinned) { $staged.files["Mods/ArtOfSimRally/$n"] = Hash (Join-Path $mod $n) }
$staged.files['artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll'] = Hash (Join-Path $GameCopy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')
if ($Recorder) { foreach ($p in $recorderSources) { $n = [IO.Path]::GetFileName($p); $staged.files["Mods/ArtOfSimRally.DevRecorder/$n"] = Hash (Join-Path $recorderDir $n) } }
$staged | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Result 'staged.json')
Write-Output ("Staged SharedInput candidate " + $(if ($packageMode) { "package $($package.release) ($($package.identity))" } else { $commit }) +
    $(if ($Recorder) { ' with the dev recorder' }) + $(if ($AwaitApply) { ', awaiting the production Apply' }) + " into $GameCopy (owner untouched). No process started.")
