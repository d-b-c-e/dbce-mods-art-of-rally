#Requires -Version 7.0
# Focused checks over the two real extracted Art packages. Reads metadata only; never loads Mod/native code or starts a game.
param([Parameter(Mandatory)][string]$NormalPackageDirectory,
      [Parameter(Mandatory)][string]$ResearchPackageDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $root 'tools/installer/verify.ps1')
. (Join-Path $root 'tools/package/ShimProbeFlavour.ps1')
$checks = 0
function Check([bool]$Pass, [string]$Why) { if (!$Pass) { throw $Why }; $script:checks++ }
$builds = @()
foreach ($case in @(@{ directory = $NormalPackageDirectory; probe = $false }, @{ directory = $ResearchPackageDirectory; probe = $true })) {
    $manifest = Assert-Payload $case.directory
    $build = Get-Content -LiteralPath (Join-Path $case.directory 'ArtOfSimRally/build.json') -Raw | ConvertFrom-Json
    $assembly = Join-Path $case.directory 'ArtOfSimRally/ArtOfSimRally.Mod.dll'
    Check ([bool]$build.sharedInputCandidate) 'Package must declare the shared-input candidate'
    Check ($build.artShimProbe -is [bool] -and $build.artShimProbe -eq $case.probe) 'Candidate receipt has the wrong probe flavor'
    $null = Assert-ShimProbeFlavour $assembly $case.probe; $checks++
    Check ([Diagnostics.FileVersionInfo]::GetVersionInfo($assembly).ProductVersion -ceq $build.identity) 'Actual assembly identity differs from its receipt'
    Check ($build.sourceRevision -ceq (& git -C $root rev-parse HEAD).Trim() -and $build.sourceState -ceq 'clean') 'Package must name this clean frozen source'
    $builds += $build
}
Check ($builds[0].release -ceq $builds[1].release) 'Use the same private RC for the two distinct output flavors'
Check ($builds[0].sharedInputCandidate.shimSha256 -ceq $builds[1].sharedInputCandidate.shimSha256 -and
       $builds[0].sharedInputCandidate.nativeSha256 -ceq $builds[1].sharedInputCandidate.nativeSha256) 'Separating probe flavor must not change shared native payload pins'
Check ($builds[0].sharedInputCandidate.controls.assemblySha256 -cne $builds[1].sharedInputCandidate.controls.assemblySha256) 'Normal and research assemblies must be distinct, with their own controls pins'
$dist = Join-Path $root 'dist'
$before = @(Get-ChildItem -LiteralPath $dist -Name -ErrorAction SilentlyContinue | Sort-Object)
$refused = $false
try {
    & (Join-Path $root 'tools/package/package.ps1') -Version $builds[0].release -ResearchShimProbe
} catch {
    if ($_.Exception.Message -cne '-ResearchShimProbe requires -SharedInputCandidate') { throw }
    $refused = $true
}
Check $refused 'Probe without shared-input selection was not refused'
$after = @(Get-ChildItem -LiteralPath $dist -Name -ErrorAction SilentlyContinue | Sort-Object)
Check ($null -eq (Compare-Object $before $after)) 'Invalid combination created a package artifact'
Check (Test-Path -LiteralPath (Join-Path $dist ('ArtOfSimRally-' + $builds[0].release + '-shared-input.zip'))) 'Normal shared archive name missing'
Check (Test-Path -LiteralPath (Join-Path $dist ('ArtOfSimRally-' + $builds[1].release + '-shared-input-probe.zip'))) 'Research archive must use a distinct suffix'
[ordered]@{ state = 'pass'; checks = $checks; source = $builds[0].sourceRevision; normal = $NormalPackageDirectory;
    research = $ResearchPackageDirectory; gameLaunched = $false; nativeLoaded = $false } | ConvertTo-Json
