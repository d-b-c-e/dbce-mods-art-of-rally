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
$dest = Join-Path $root 'dist\ArtOfSimRally.TurtleVan'
New-Item -ItemType Directory -Force $dest | Out-Null
foreach ($p in @('Info.json','src\bin\Release\TurtleVan.Experimental.dll','assets\turtle-van.json','assets\palette.png')) {
    Copy-Item -LiteralPath (Join-Path $root $p) -Destination $dest -Force
}
$manifest = @(Get-ChildItem -LiteralPath $dest -File | Where-Object Name -ne 'manifest.json' | ForEach-Object {
    [ordered]@{file=$_.Name; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
})
$manifest | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $dest 'manifest.json') -Encoding utf8
Write-Output "Built and validated: $dest"
