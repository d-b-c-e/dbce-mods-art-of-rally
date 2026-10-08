param([string]$GameDir = 'D:\Program Files (x86)\Steam\steamapps\common\artofrally')
$ErrorActionPreference = 'Stop'
if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Close art of rally before installing this prototype.' }
$root = Split-Path $PSScriptRoot -Parent
$package = Join-Path $root 'dist\ArtOfSimRally.TurtleVan'
$manifest = Get-Content -LiteralPath (Join-Path $package 'manifest.json') -Raw | ConvertFrom-Json
foreach ($row in $manifest) {
    if ((Get-FileHash -LiteralPath (Join-Path $package $row.file)).Hash -ne $row.sha256) { throw "Package mismatch: $($row.file)" }
}
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'artofrally.exe'))) { throw 'Not an art of rally installation.' }
if (!(Test-Path -LiteralPath (Join-Path $GameDir 'artofrally_Data\Managed\UnityModManager\UnityModManager.dll'))) { throw 'Unity Mod Manager is required.' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$receiptDir = Join-Path $root "artifacts\install-$stamp"
New-Item -ItemType Directory -Force $receiptDir | Out-Null
# Snapshot and back up existing owner settings/payloads, but never replace them.
$protected = @(Get-ChildItem -LiteralPath (Join-Path $GameDir 'Mods\ArtOfSimRally') -File -Recurse | ForEach-Object {
    [ordered]@{path=$_.FullName;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
})
$ownerBackup = Join-Path $receiptDir 'ArtOfSimRally-backup'
Copy-Item -LiteralPath (Join-Path $GameDir 'Mods\ArtOfSimRally') -Destination $ownerBackup -Recurse
$target = Join-Path $GameDir 'Mods\ArtOfSimRally.TurtleVan'
if (Test-Path -LiteralPath $target) { Copy-Item -LiteralPath $target -Destination (Join-Path $receiptDir 'previous-prototype') -Recurse }
if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Game started during preflight; no deployment performed.' }
New-Item -ItemType Directory -Force $target | Out-Null
foreach ($row in $manifest) { Copy-Item -LiteralPath (Join-Path $package $row.file) -Destination $target -Force }
foreach ($row in $manifest) {
    if ((Get-FileHash -LiteralPath (Join-Path $target $row.file)).Hash -ne $row.sha256) { throw "Installed mismatch: $($row.file)" }
}
foreach ($row in $protected) {
    if ((Get-FileHash -LiteralPath $row.path).Hash -ne $row.sha256) { throw "Owner file changed: $($row.path)" }
}
[ordered]@{time=(Get-Date).ToUniversalTime().ToString('o');target=$target;version='0.1.0';
    enabledByDefault=$false;gameLaunched=$false;runtimeVerified=$false;payload=$manifest;protectedFiles=$protected
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $receiptDir 'receipt.json') -Encoding utf8
Write-Output "Installed prototype; van is OFF until enabled in UMM. Receipt: $receiptDir\receipt.json"
