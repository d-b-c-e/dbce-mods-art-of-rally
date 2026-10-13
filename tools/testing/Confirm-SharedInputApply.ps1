#Requires -Version 7.0
# After Wheelkit's production Apply on a copy staged with Prepare-SharedInputCopy -AwaitApply (sana 43671/43761): pins
# exactly what the Apply wrote, from its own LiveApplyRun receipt (ConfigurationQualification apply.json), so
# Invoke-SharedInputCopy's pin check covers the applied profile and settings. Read-only on the game copy; writes only
# staged.json. Never starts a process or opens a device.
# Refuses unless:
#  - the receipt is an applied art-of-rally run on this staged copy;
#  - the Apply ran on the staged settings (its before-hash is the staged one);
#  - the copy's applied files still equal the receipt's after-hashes;
#  - the applied settings select SharedInput and still keep force feedback off where staging required it;
#  - an applied dbce-controls.ini exists.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Result, [Parameter(Mandatory)][string]$ApplyEvidence)
$ErrorActionPreference = 'Stop'
function Full([string]$p) { [IO.Path]::GetFullPath($p).TrimEnd('\') }
function Hash([string]$p) { if (Test-Path -LiteralPath $p -PathType Leaf) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash } else { 'absent' } }
$Result = Full $Result; $ApplyEvidence = Full $ApplyEvidence
$stagedPath = Join-Path $Result 'staged.json'
$staged = Get-Content -LiteralPath $stagedPath -Raw | ConvertFrom-Json
if ($staged.schema -ne 1 -or $staged.kind -ne 'shared-input-ordinary') { throw 'Not a SharedInput staging result.' }
if ($staged.applyPending -ne $true) { throw 'This staging is not awaiting an Apply.' }
if (Test-Path -LiteralPath (Join-Path $Result 'launch.json')) { throw 'This staging has already been launched.' }
if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Art is running; no confirmation.' }
$receiptPath = Join-Path $ApplyEvidence 'apply.json'
$receipt = Get-Content -LiteralPath $receiptPath -Raw | ConvertFrom-Json
if ($receipt.game -ne 'art-of-rally' -or $receipt.status -ne 'applied') { throw "Receipt is not a completed Art Apply (game $($receipt.game), status $($receipt.status))." }
if ((Full $receipt.install) -ne (Full $staged.gameCopy)) { throw 'The Apply ran on another install than this staged copy.' }
$settingsPath = 'Mods/ArtOfSimRally/Settings.xml'; $controlsPath = 'Mods/ArtOfSimRally/dbce-controls.ini'
$byPath = @{}; foreach ($f in $receipt.files) { $byPath[$f.path] = $f }
foreach ($p in @($settingsPath, $controlsPath)) { if (-not $byPath.ContainsKey($p)) { throw "The receipt does not cover $p." } }
if ($byPath[$settingsPath].beforeHash -ne $staged.files.$settingsPath) { throw 'The Apply did not start from the staged settings.' }
if ($byPath[$controlsPath].afterHash -in @($null, '', 'absent')) { throw 'The Apply wrote no dbce-controls.ini.' }
foreach ($f in $receipt.files) {
    $current = Hash (Join-Path $staged.gameCopy $f.path)
    $expected = if ($f.afterHash -and $f.afterHash -ne 'absent') { $f.afterHash.ToUpperInvariant() } else { 'absent' }
    if ($current -ne $expected) { throw "Applied file changed since the Apply: $($f.path)" }
}
[xml]$settings = Get-Content -LiteralPath (Join-Path $staged.gameCopy $settingsPath) -Raw
if ($settings.DocumentElement.SharedInput -ne 'true') { throw 'The applied settings do not select SharedInput.' }
if ($staged.forceFeedbackOff -and $settings.DocumentElement.ForceFeedbackEnabled -ne 'false') { throw 'The applied settings turned force feedback back on.' }
$files = [ordered]@{}; foreach ($p in $staged.files.PSObject.Properties) { $files[$p.Name] = $p.Value }
$files[$settingsPath] = $byPath[$settingsPath].afterHash
$files[$controlsPath] = $byPath[$controlsPath].afterHash
$staged.files = [pscustomobject]$files
$staged.applyPending = $false
$staged | Add-Member -NotePropertyName apply -NotePropertyValue ([ordered]@{ evidence = $ApplyEvidence; receiptSha256 = (Hash $receiptPath)
    settingsBefore = $byPath[$settingsPath].beforeHash; settingsAfter = $byPath[$settingsPath].afterHash; controlsAfter = $byPath[$controlsPath].afterHash
    confirmedUtc = [DateTime]::UtcNow.ToString('o') }) -Force
$staged | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $stagedPath
"Confirmed the production Apply on $($staged.gameCopy): settings $($byPath[$settingsPath].afterHash.Substring(0, 8)), controls $($byPath[$controlsPath].afterHash.Substring(0, 8)). Staging ready for Invoke-SharedInputCopy."
