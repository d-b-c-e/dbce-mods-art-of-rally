#Requires -Version 7.0
# Copy-only staging for the opt-in SharedInput candidate in an existing disposable game copy.
# Never touches the owner installation, never starts a process, never opens a device.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$GameCopy,
      [Parameter(Mandatory)][string]$SourceGame,
      [Parameter(Mandatory)][string]$Profile,
      [Parameter(Mandatory)][string]$Shim,
      [Parameter(Mandatory)][string]$Native,
      [Parameter(Mandatory)][string]$Result,
      [string]$ExpectedShimSha256 = '7DDC962132819EDAE76EF84CE6509F1936B8C38177A36D498B8ABBCFDCFFBCAF',
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
$repo = Full (Join-Path $PSScriptRoot '../..')
$GameCopy = Full $GameCopy; $SourceGame = Full $SourceGame; $Result = Full $Result
foreach ($p in @($GameCopy, $SourceGame, $Result)) { Plain $p }
if ((Within $GameCopy $SourceGame) -or (Within $SourceGame $GameCopy) -or (Within $Result $GameCopy) -or (Within $Result $SourceGame)) { throw 'Copy, owner and result must be separate.' }
if (Test-Path -LiteralPath $Result) { throw 'Use a new result folder.' }
if (-not (Test-Path -LiteralPath (Join-Path $GameCopy 'artofrally.exe'))) { throw 'Disposable copy has no artofrally.exe; robocopy it first.' }
if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Art is running; no staging.' }
if ((Hash $Shim) -ne $ExpectedShimSha256) { throw 'dbce_shim.dll is not the qualified copied-provider build.' }
if ((Hash $Native) -ne $ExpectedNativeSha256) { throw 'UnityForceFeedback.dll is not the pinned snapshot candidate native.' }
$build = Join-Path $repo 'src/ArtOfSimRally.Mod/bin/Release'
$payload = @('ArtOfSimRally.Mod.dll', 'Dbce.TripleScreen.Core.dll', 'Dbce.TripleScreen.Protocol.dll', 'Dbce.Wheel.Ffb.dll', 'Dbce.Wheel.Telemetry.dll')
foreach ($n in $payload) { if (-not (Test-Path -LiteralPath (Join-Path $build $n))) { throw "Build output missing: $n (build the solution first)." } }
$recorderSources = @((Join-Path $repo 'tools/testing/Recorder/bin/Release/net48/ArtOfSimRally.DevRecorder.dll'), (Join-Path $repo 'tools/testing/Recorder/Info.json'), (Join-Path $repo 'lib/playback/Dbce.Wheel.Playback.dll'))
if ($Recorder) { foreach ($p in $recorderSources) { if (-not (Test-Path -LiteralPath $p -PathType Leaf)) { throw "Build the recorder first: $p" } } }
$sources = @('src') + $(if ($Recorder) { @('tools/testing/Recorder', 'lib/playback') } else { @() })
$commit = (git -C $repo rev-parse HEAD).Trim(); $dirty = @(git -C $repo status --porcelain -- @sources)
if ($dirty.Count) { throw 'Mod or recorder source has uncommitted changes; stage only a committed build.' }
$ownerSettings = Join-Path $SourceGame 'Mods/ArtOfSimRally/Settings.xml'
$mod = Join-Path $GameCopy 'Mods/ArtOfSimRally'
New-Item -ItemType Directory -Path $Result | Out-Null
foreach ($n in $payload) { Copy-Item -LiteralPath (Join-Path $build $n) -Destination (Join-Path $mod $n) -Force }
Copy-Item -LiteralPath (Join-Path $repo 'src/ArtOfSimRally.Mod/Info.json') -Destination (Join-Path $mod 'Info.json') -Force # source, not a build output
Copy-Item -LiteralPath $Native -Destination (Join-Path $mod 'UnityForceFeedback.dll') -Force
Copy-Item -LiteralPath $Native -Destination (Join-Path $GameCopy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll') -Force
Copy-Item -LiteralPath $Shim -Destination (Join-Path $mod 'dbce_shim.dll') -Force
Copy-Item -LiteralPath $Profile -Destination (Join-Path $mod 'dbce-controls.ini') -Force
$recorderDir = Join-Path $GameCopy 'Mods/ArtOfSimRally.DevRecorder'
if (Test-Path -LiteralPath $recorderDir) { Plain $recorderDir; Get-ChildItem -LiteralPath $recorderDir -File | Remove-Item; Remove-Item -LiteralPath $recorderDir }
if ($Recorder) {
    New-Item -ItemType Directory -Path $recorderDir | Out-Null
    foreach ($p in $recorderSources) { Copy-Item -LiteralPath $p -Destination (Join-Path $recorderDir ([IO.Path]::GetFileName($p))) }
}
# The owner's exact settings with only SharedInput turned on (the owner file is read, never written).
$settingsFrom = if ($SettingsSource) { Full $SettingsSource } else { $ownerSettings }
[xml]$settings = Get-Content -LiteralPath $settingsFrom -Raw
if ($SettingsSource -and $settings.DocumentElement.SharedInput -ne 'true') { throw 'The settings source does not select SharedInput.' }
$node = $settings.DocumentElement.SelectSingleNode('SharedInput')
if (-not $node) { $node = $settings.CreateElement('SharedInput'); $null = $settings.DocumentElement.AppendChild($node) }
$node.InnerText = 'true'
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
    files = [ordered]@{}
}
foreach ($n in $payload + @('Info.json', 'UnityForceFeedback.dll', 'dbce_shim.dll', 'dbce-controls.ini', 'Settings.xml')) { $staged.files["Mods/ArtOfSimRally/$n"] = Hash (Join-Path $mod $n) }
$staged.files['artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll'] = Hash (Join-Path $GameCopy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')
if ($Recorder) { foreach ($p in $recorderSources) { $n = [IO.Path]::GetFileName($p); $staged.files["Mods/ArtOfSimRally.DevRecorder/$n"] = Hash (Join-Path $recorderDir $n) } }
$staged | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Result 'staged.json')
Write-Output "Staged SharedInput candidate $commit$(if ($Recorder) { ' with the dev recorder' }) into $GameCopy (owner untouched). No process started."
