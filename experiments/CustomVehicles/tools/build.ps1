param(
    [string]$GameDir = 'D:\Program Files (x86)\Steam\steamapps\common\artofrally',
    [string]$Blender = 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe',
    [switch]$RegenerateModels
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$managed = Join-Path $GameDir 'artofrally_Data\Managed'
Push-Location $root
try {
    New-Item -ItemType Directory -Force artifacts | Out-Null
    if ($RegenerateModels) {
        foreach ($script in @('creator\make_example.py','creator\make_turtle_package.py')) {
            & $Blender --background --python (Join-Path $root $script)
            if ($LASTEXITCODE -ne 0) { throw "Model generation failed: $script" }
        }
    }
    foreach ($project in @('src\CustomVehicles.csproj','tests\CustomVehicles.Tests.csproj','tools\VehicleTool\VehicleTool.csproj')) {
        & dotnet build (Join-Path $root $project) -c Release --nologo "-p:GameDir=$GameDir"
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
    $plugin = Join-Path $root 'src\bin\Release\ArtOfSimRally.CustomVehicles.Experimental.dll'
    & (Join-Path $root 'tests\bin\Release\net48\CustomVehicles.Tests.exe') (Join-Path $root 'examples') $plugin $managed
    if ($LASTEXITCODE -ne 0) { throw 'Managed/runtime-contract gate failed.' }
    & python (Join-Path $root 'tests\test_tools.py')
    if ($LASTEXITCODE -ne 0) { throw 'Creator packaging/tool tests failed.' }
    if (Test-Path -LiteralPath $Blender) {
        & $Blender --background (Join-Path $root 'examples\trail-scout\trail-scout.blend') --python-exit-code 1 --python (Join-Path $root 'tests\test_blender_export.py')
        if ($LASTEXITCODE -ne 0) { throw 'Blender exporter checks failed.' }
    }
    else { Write-Warning 'Blender unavailable: exporter execution not checked in this build.' }
    $info = Get-Content -LiteralPath (Join-Path $root 'Info.json') -Raw | ConvertFrom-Json
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $name = "CustomVehicles-$($info.Version)-offline-$stamp"
    $stageRoot = Join-Path $root "dist\$name"
    $destination = Join-Path $stageRoot $info.Id
    New-Item -ItemType Directory -Path $destination | Out-Null
    Copy-Item -LiteralPath $plugin -Destination $destination
    foreach ($file in @('Info.json','README.md','CREATOR.md','QUALIFICATION.md')) {
        Copy-Item -LiteralPath (Join-Path $root $file) -Destination $destination
    }
    $cli = Join-Path $root 'tools\VehicleTool\bin\Release\net48\VehicleTool.exe'
    foreach ($folder in Get-ChildItem -LiteralPath (Join-Path $root 'examples') -Directory) {
        $manifest = Join-Path $folder.FullName 'vehicle.json'
        & $cli validate $manifest
        if ($LASTEXITCODE -ne 0) { throw "Invalid package: $manifest" }
        $vehicle = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
        $target = Join-Path $destination "Vehicles\$($vehicle.id)"
        New-Item -ItemType Directory -Path $target | Out-Null
        foreach ($file in @('vehicle.json',$vehicle.model,$vehicle.texture,$vehicle.preview) | Where-Object { $_ } | Select-Object -Unique) {
            Copy-Item -LiteralPath (Join-Path $folder.FullName $file) -Destination $target
        }
    }
    $files = @(Get-ChildItem -LiteralPath $destination -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{path=$_.FullName.Substring($destination.Length+1).Replace('\','/'); bytes=$_.Length; sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}
    })
    $receipt = [ordered]@{
        utc=(Get-Date).ToUniversalTime().ToString('O'); version=$info.Version
        sourceHead=(& git rev-parse HEAD); workingTree=(@(& git status --short -- .) -join "`n")
        gameAssemblySha256=(Get-FileHash -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll')).Hash
        runtimeQualified=$false; installed=$false; gameLaunched=$false; files=$files
    }
    $receiptPath = Join-Path $root "artifacts\$name-receipt.json"
    $receipt | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $receiptPath -Encoding utf8
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = Join-Path $root "artifacts\$name.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($stageRoot,$zip,[IO.Compression.CompressionLevel]::Optimal,$false)
    $hash=(Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($zip))" | Set-Content -LiteralPath "$zip.sha256" -Encoding ascii
    # Separate authoring kit; redistribute the NuGet dependency, never the
    # game-provided copy used by the mod and its runtime compatibility tests.
    $sdk = Join-Path $root "dist\$name-CreatorSDK"
    New-Item -ItemType Directory -Path (Join-Path $sdk 'tools'),(Join-Path $sdk 'creator') | Out-Null
    Copy-Item -LiteralPath (Join-Path $root 'SDK-README.md') -Destination (Join-Path $sdk 'README.md')
    foreach ($file in @('CREATOR.md','QUALIFICATION.md')) { Copy-Item -LiteralPath (Join-Path $root $file) -Destination $sdk }
    foreach ($file in @('export_vehicle.py','make_example.py','render_package.py')) { Copy-Item -LiteralPath (Join-Path $root "creator\$file") -Destination (Join-Path $sdk 'creator') }
    foreach ($file in @('VehicleTool.exe','VehicleTool.exe.config','Newtonsoft.Json.dll','Newtonsoft.Json.LICENSE.md')) {
        Copy-Item -LiteralPath (Join-Path (Split-Path $cli -Parent) $file) -Destination (Join-Path $sdk 'tools')
    }
    Copy-Item -LiteralPath (Join-Path $destination 'Vehicles') -Destination (Join-Path $sdk 'examples') -Recurse
    # Keep example folder names consistent with the creator documentation.
    Rename-Item -LiteralPath (Join-Path $sdk 'examples\dbce.trail-scout') -NewName 'trail-scout'
    Rename-Item -LiteralPath (Join-Path $sdk 'examples\dbce.turtle-van') -NewName 'turtle-van'
    Copy-Item -LiteralPath (Join-Path $root 'examples\trail-scout\trail-scout.blend') -Destination (Join-Path $sdk 'examples\trail-scout')
    $turtleMaster=Join-Path $root 'artifacts\turtle-van-creator.blend'
    if (Test-Path -LiteralPath $turtleMaster) { Copy-Item -LiteralPath $turtleMaster -Destination (Join-Path $sdk 'examples\turtle-van\turtle-van.blend') }
    else { Write-Warning 'Turtle editable master not generated; run -RegenerateModels to include it.' }
    $sdkFiles=@(Get-ChildItem -LiteralPath $sdk -Recurse -File | Sort-Object FullName | ForEach-Object {
        [ordered]@{path=$_.FullName.Substring($sdk.Length+1).Replace('\','/'); sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
    })
    $sdkFiles | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $root "artifacts\$name-CreatorSDK-receipt.json") -Encoding utf8
    $sdkZip=Join-Path $root "artifacts\$name-CreatorSDK.zip"
    [IO.Compression.ZipFile]::CreateFromDirectory($sdk,$sdkZip,[IO.Compression.CompressionLevel]::Optimal,$false)
    "$((Get-FileHash -LiteralPath $sdkZip).Hash)  $([IO.Path]::GetFileName($sdkZip))" | Set-Content -LiteralPath "$sdkZip.sha256" -Encoding ascii
    & (Join-Path $sdk 'tools\VehicleTool.exe') validate (Join-Path $sdk 'examples\trail-scout\vehicle.json')
    if ($LASTEXITCODE -ne 0) { throw 'Isolated creator-kit validation failed.' }
    Write-Output "Offline candidate: $zip"
    Write-Output "Creator kit: $sdkZip"
    Write-Output "Receipt: $receiptPath"
    Write-Output 'Not installed. Physical writes remain locked. Attended runtime qualification pending.'
}
finally { Pop-Location }
