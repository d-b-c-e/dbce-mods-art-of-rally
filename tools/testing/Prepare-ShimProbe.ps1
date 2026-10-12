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
    [ValidateSet('Observe','Apply','Trial')][string]$Mode = 'Observe',
    [string]$ObserveReceipt,
    # Trial only, diagnostic: the mod may end the title splash once to reach the main menu (romi 29497).
    [switch]$AdvanceTitle,
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
if ($Mode -eq 'Trial' -and (Get-FileHash -LiteralPath $ShimCandidate -Algorithm SHA256).Hash -ne '0D10F913C998C97D7DDF3A8F5F470D010092C3681152059DC88D7E0A7BB0386B') { throw 'Trial needs the reviewed latched bridge dbce_shim.dll (0D10F913).' }
$payload = Join-Path $Result 'payload/ArtOfSimRally'
New-Item -ItemType Directory -Path $payload | Out-Null
foreach ($file in $files) { Copy-Item -LiteralPath (Join-Path $bin $file) -Destination $payload }
Copy-Item -LiteralPath (Join-Path $repo 'src/ArtOfSimRally.Mod/Info.json') -Destination $payload
Copy-Item -LiteralPath $Settings -Destination (Join-Path $payload 'Settings.xml')
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
$inventory = [ordered]@{ SourceGame=$SourceGame; GameCopy=$GameCopy; SettingsSource=[IO.Path]::GetFullPath($Settings); SettingsSourceSha256=(Hash $Settings); ProfileSource=[IO.Path]::GetFullPath($Profile); ProfileSourceSha256=(Hash $Profile); SourceCommit=(& git -C $repo rev-parse HEAD); SourceStatus=(@(& git -C $repo status --porcelain)); Files=@() }
if ($inventory.SettingsSourceSha256 -ne $request.SettingsSha256 -or $inventory.ProfileSourceSha256 -ne $request.ProfileSha256) { throw 'Source settings/profile changed during snapshot; prepare a fresh result.' }
foreach ($file in Get-ChildItem -LiteralPath $payload -File) { $inventory.Files += [ordered]@{ Name=$file.Name; Sha256=(Hash $file.FullName) } }
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
Write-Output (Join-Path $Result 'COPY-ONLY-RECIPE.txt')
