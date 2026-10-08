param([switch]$Model)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
if ($Model) {
    & 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --python (Join-Path $PSScriptRoot 'model.py')
    if ($LASTEXITCODE -ne 0) { throw 'Blender export failed' }
}
& python (Join-Path $PSScriptRoot 'validate.py')
if ($LASTEXITCODE -ne 0) { throw 'Asset validation failed' }
& dotnet build (Join-Path $root 'src\TurtleVan.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Mod build failed' }
& dotnet build (Join-Path $root 'tests\AssetLoader.Tests.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Loader test build failed' }
& (Join-Path $root 'tests\bin\Release\net48\AssetLoader.Tests.exe') (Join-Path $root 'assets\turtle-van.json') (Join-Path $root 'previews\validation.json') (Join-Path $root 'vehicle.json') (Join-Path $root 'src\bin\Release\TurtleVan.Experimental.dll') 'D:\Program Files (x86)\Steam\steamapps\common\artofrally\artofrally_Data\Managed'
if ($LASTEXITCODE -ne 0) { throw 'Production loader test failed' }
$dest = Join-Path $root 'dist\ArtOfSimRally.TurtleVan'
New-Item -ItemType Directory -Force $dest | Out-Null
foreach ($p in @('Info.json','vehicle.json','src\bin\Release\TurtleVan.Experimental.dll','assets\turtle-van.json','assets\palette.png')) {
    Copy-Item -LiteralPath (Join-Path $root $p) -Destination $dest -Force
}
$manifest = @(Get-ChildItem -LiteralPath $dest -File | Where-Object Name -ne 'manifest.json' | ForEach-Object {
    [ordered]@{file=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dest 'manifest.json') -Encoding utf8
Write-Output "Built and validated: $dest"
