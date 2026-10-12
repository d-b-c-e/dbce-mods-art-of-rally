# Prepares a COPY-ONLY, menu-only diagnostic payload. Never modifies a game,
# starts a process, sets persistent environment variables, or opens a device.
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Result,
    [Parameter(Mandatory)][string]$SourceGame,
    [Parameter(Mandatory)][string]$GameCopy,
    [Parameter(Mandatory)][string]$Settings,
    [Parameter(Mandatory)][string]$Profile,
    [Parameter(Mandatory)][string]$NativeCandidate,
    [Parameter(Mandatory)][string]$ShimCandidate,
    [ValidateSet('Observe','Apply','Trial','Drive')][string]$Mode = 'Observe',
    [string]$ObserveReceipt,
    # Trial only, diagnostic: the mod may end the title splash once to reach the main menu (romi 29497).
    [switch]$AdvanceTitle,
    # Also copy the payload into the existing disposable copy (copy-only; refuses while Art runs).
    [switch]$StageCopy,
    # Disposable copy only: write this Art transmission mode into the staged Settings.xml (a manual box for gear rows).
    # The owner file is only read; Observe and Drive must use the same value (the settings hash is pinned).
    [ValidateSet('Automatic','Sequential','H-pattern')][string]$TransmissionMode,
    # Drive: stage the dev recorder (its capture driver reaches the stage and tapes the car) into the copy.
    [switch]$Recorder,
    [ValidateRange(1,30)][int]$ExpiresMinutes = 15
)
$ErrorActionPreference = 'Stop'
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$Result = [IO.Path]::GetFullPath($Result)
$SourceGame = [IO.Path]::GetFullPath($SourceGame).TrimEnd('\')
$GameCopy = [IO.Path]::GetFullPath($GameCopy).TrimEnd('\')
function Within([string]$path, [string]$parent) { $path.Equals($parent, [StringComparison]::OrdinalIgnoreCase) -or $path.StartsWith($parent + '\', [StringComparison]::OrdinalIgnoreCase) }
if ((Within $GameCopy $SourceGame) -or (Within $SourceGame $GameCopy) -or (Within $Result $SourceGame) -or (Within $Result $GameCopy)) { throw 'Result and game copy must be outside the owner game and distinct from each other.' }
if (Test-Path -LiteralPath $Result) { throw 'Use a new result folder.' }
if (-not (Test-Path -LiteralPath (Join-Path $SourceGame 'artofrally.exe'))) { throw 'Source game executable missing.' }
# Refuse redirection through junctions/symlinks for destinations.
foreach ($target in @($Result, $GameCopy)) {
    $ancestor = $target
    while ($ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and ((Get-Item -LiteralPath $ancestor -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Destination traverses a link: $ancestor" }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
}
$bin = Join-Path $repo 'src/ArtOfSimRally.Mod/bin/Release'
$files = @('ArtOfSimRally.Mod.dll','Dbce.Wheel.Ffb.dll','Dbce.Wheel.Telemetry.dll','Dbce.TripleScreen.Core.dll','Dbce.TripleScreen.Protocol.dll')
foreach ($file in $files) { if (-not (Test-Path -LiteralPath (Join-Path $bin $file))) { throw "Build candidate first: $file" } }
foreach ($path in @($Settings,$Profile,$NativeCandidate,$ShimCandidate)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Input missing: $path" } }
if ($Mode -ne 'Observe' -and (-not $ObserveReceipt -or -not (Test-Path -LiteralPath $ObserveReceipt -PathType Leaf))) { throw "$Mode probe needs its earlier Observe receipt." }
# Trial (Slice B, latched Core) runs only on the reviewed latched bridge build, art-shim-bridge f38b855.
if ($AdvanceTitle -and $Mode -ne 'Trial') { throw 'Only a Trial may advance the title.' }
if ($Recorder -and $Mode -ne 'Drive') { throw 'Only a Drive stages the recorder.' }
if ($Mode -in @('Trial','Drive') -and (Get-FileHash -LiteralPath $ShimCandidate -Algorithm SHA256).Hash -ne '0D10F913C998C97D7DDF3A8F5F470D010092C3681152059DC88D7E0A7BB0386B') { throw "$Mode needs the reviewed latched bridge dbce_shim.dll (0D10F913)." }
$payload = Join-Path $Result 'payload/ArtOfSimRally'
New-Item -ItemType Directory -Path $payload | Out-Null
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $bin $file) -Destination $payload }
Copy-Item -LiteralPath (Join-Path $repo 'src/ArtOfSimRally.Mod/Info.json') -Destination $payload
$settingsSourceSha256 = (Get-FileHash -LiteralPath $Settings -Algorithm SHA256).Hash
Copy-Item -LiteralPath $Settings -Destination (Join-Path $payload 'Settings.xml')
if ($TransmissionMode) {
    [xml]$staged = Get-Content -LiteralPath (Join-Path $payload 'Settings.xml') -Raw
    $node = $staged.DocumentElement.SelectSingleNode('TransmissionMode')
    if (-not $node) { $node = $staged.CreateElement('TransmissionMode'); $null = $staged.DocumentElement.AppendChild($node) }
    $node.InnerText = $TransmissionMode
    $staged.Save((Join-Path $payload 'Settings.xml'))
}
$recorderFiles = @()
if ($Recorder) {
    $recorderPayload = Join-Path $Result 'payload/ArtOfSimRally.DevRecorder'
    New-Item -ItemType Directory -Path $recorderPayload | Out-Null
    foreach ($source in @((Join-Path $repo 'tools/testing/Recorder/bin/Release/net48/ArtOfSimRally.DevRecorder.dll'), (Join-Path $repo 'tools/testing/Recorder/Info.json'), (Join-Path $repo 'lib/playback/Dbce.Wheel.Playback.dll'))) {
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Build the recorder first: $source" }
        Copy-Item -LiteralPath $source -Destination $recorderPayload
    }
}
Copy-Item -LiteralPath $NativeCandidate -Destination (Join-Path $payload 'UnityForceFeedback.dll')
Copy-Item -LiteralPath $ShimCandidate -Destination (Join-Path $Result 'dbce_shim.dll')
Copy-Item -LiteralPath $Profile -Destination (Join-Path $Result 'controls.ini')
if ($Mode -ne 'Observe') { Copy-Item -LiteralPath $ObserveReceipt -Destination (Join-Path $Result 'observe-receipt.json') }
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
$request = [ordered]@{
    Schema=1; Mode=$Mode; Nonce=[Guid]::NewGuid().ToString('N'); ExpiresUtc=[DateTime]::UtcNow.AddMinutes($ExpiresMinutes).ToString('o')
    GameRoot=$GameCopy; SourceGameRoot=$SourceGame; Result=$Result
    ModSha256=(Hash (Join-Path $payload 'ArtOfSimRally.Mod.dll')); NativeSha256=(Hash (Join-Path $payload 'UnityForceFeedback.dll'))
    ShimSha256=(Hash (Join-Path $Result 'dbce_shim.dll')); SettingsSha256=(Hash (Join-Path $payload 'Settings.xml')); ProfileSha256=(Hash (Join-Path $Result 'controls.ini'))
    ShimPath=(Join-Path $Result 'dbce_shim.dll'); ProfilePath=(Join-Path $Result 'controls.ini')
    ObserveReceipt=$(if ($Mode -ne 'Observe') { Join-Path $Result 'observe-receipt.json' } else { $null })
    AdvanceTitle=[bool]$AdvanceTitle
}
$utf8 = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $Result 'request.json'), ($request | ConvertTo-Json -Depth 5), $utf8)
$inventory = [ordered]@{ SourceGame=$SourceGame; GameCopy=$GameCopy; SettingsSource=[IO.Path]::GetFullPath($Settings); SettingsSourceSha256=(Hash $Settings); TransmissionMode=$TransmissionMode; ProfileSource=[IO.Path]::GetFullPath($Profile); ProfileSourceSha256=(Hash $Profile); SourceCommit=(& git -C $repo rev-parse HEAD); SourceStatus=(@(& git -C $repo status --porcelain)); Files=@() }
if ($inventory.SettingsSourceSha256 -ne $settingsSourceSha256 -or (-not $TransmissionMode -and $inventory.SettingsSourceSha256 -ne $request.SettingsSha256) -or $inventory.ProfileSourceSha256 -ne $request.ProfileSha256) { throw 'Source settings/profile changed during snapshot; prepare a fresh result.' }
foreach ($file in Get-ChildItem -LiteralPath $payload -File) { $inventory.Files += [ordered]@{ Name=$file.Name; Sha256=(Hash $file.FullName) } }
$inventory.RecorderFiles = @()
if ($Recorder) { foreach ($file in Get-ChildItem -LiteralPath $recorderPayload -File) { $inventory.RecorderFiles += [ordered]@{ Name=$file.Name; Sha256=(Hash $file.FullName) } } }
[IO.File]::WriteAllText((Join-Path $Result 'prepared.json'), ($inventory | ConvertTo-Json -Depth 5), $utf8)
$recipe = @"
Prepared only. No game or profile was modified and no process was launched.

Source game: $SourceGame
Disposable game: $GameCopy
Payload: $payload
Request: $(Join-Path $Result 'request.json')
Expires UTC: $($request.ExpiresUtc)

Use docs/research/2026-10-11-shim-muted-probe.md at the serialized rig slot.
Copy the game to the disposable path using robocopy /E /XJ (no /MIR).
Remove or disable optional third-party mods ONLY in that disposable copy.
Copy payload contents into the COPY's Mods/ArtOfSimRally folder.
Copy the candidate UnityForceFeedback.dll into the COPY's artofrally_Data/Plugins/x86_64 folder too.
Write ASCII 550320 to the COPY's steam_appid.txt to prevent Steam restarting into the owner installation.
Keep the source Settings.xml byte-identical. Never run an installer or Wheelkit Apply.
Create no injection marker, injection command, or saved backend setting.
Use Invoke-ShimProbeCopy.ps1 only in the coordinator's explicit rig slot. It sets child-only DBCE_FFB_MUTE=1 before native load.
Remain in menus; no physical movement is required. Inspect probe-receipt.json.
Trial: start on the main menu with the game focused; the mod itself injects one navDown and one navUp
(latched Core, every other action masked), reads the selection back and ends the session.
Exit the copy normally; compare source hashes before and after. Do not publish.
"@
[IO.File]::WriteAllText((Join-Path $Result 'COPY-ONLY-RECIPE.txt'), $recipe, $utf8)
if ($StageCopy) {
    if (-not (Test-Path -LiteralPath (Join-Path $GameCopy 'artofrally.exe'))) { throw 'Disposable copy has no artofrally.exe; robocopy it first.' }
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Art is running; no staging.' }
    $mod = Join-Path $GameCopy 'Mods/ArtOfSimRally'
    foreach ($file in Get-ChildItem -LiteralPath $payload -File) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $mod $file.Name) -Force }
    Copy-Item -LiteralPath (Join-Path $payload 'UnityForceFeedback.dll') -Destination (Join-Path $GameCopy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll') -Force
    [IO.File]::WriteAllText((Join-Path $GameCopy 'steam_appid.txt'), '550320', [Text.Encoding]::ASCII)
    # The recorder is present only for a Drive; any earlier one is removed (copy only).
    $recorderDir = Join-Path $GameCopy 'Mods/ArtOfSimRally.DevRecorder'
    if (Test-Path -LiteralPath $recorderDir) { Get-ChildItem -LiteralPath $recorderDir -File | Remove-Item; Remove-Item -LiteralPath $recorderDir }
    if ($Recorder) {
        New-Item -ItemType Directory -Path $recorderDir | Out-Null
        foreach ($file in Get-ChildItem -LiteralPath $recorderPayload -File) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $recorderDir $file.Name) }
        foreach ($file in Get-ChildItem -LiteralPath $recorderPayload -File) { if ((Hash (Join-Path $recorderDir $file.Name)) -ne (Hash $file.FullName)) { throw "Staged recorder differs: $($file.Name)" } }
    }
    # The probe takes its shim and profile from the result folder; shared-input staging leftovers go.
    foreach ($stray in @('dbce_shim.dll', 'dbce-controls.ini')) { $path = Join-Path $mod $stray; if (Test-Path -LiteralPath $path) { Remove-Item -LiteralPath $path } }
    foreach ($file in Get-ChildItem -LiteralPath $payload -File) { if ((Hash (Join-Path $mod $file.Name)) -ne (Hash $file.FullName)) { throw "Staged copy differs: $($file.Name)" } }
    if ((Hash (Join-Path $GameCopy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')) -ne $request.NativeSha256) { throw 'Staged plugin native differs.' }
    Write-Output "Staged the payload into $GameCopy (copy only; no process started)."
}
Write-Output (Join-Path $Result 'COPY-ONLY-RECIPE.txt')
