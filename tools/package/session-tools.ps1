<# Optional developer tooling asset. Explicit allowlist; no game/Unity/UMM binaries. #>
[CmdletBinding()]
param([Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+(-rc\.[1-9]\d*)?$')][string]$Version)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$revision = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source' }
if (@(& git -C $root status --porcelain --untracked-files=all).Count -ne 0) { throw 'SessionTools requires clean source' }
$pin = Get-Content (Join-Path $root 'lib/playback/provenance.json') -Raw | ConvertFrom-Json
if ((Get-FileHash (Join-Path $root 'lib/playback/Dbce.Wheel.Playback.dll')).Hash -ne $pin.sha256) { throw 'Playback library differs from pin' }
$stage = Join-Path $root "dist/session-tools-$Version"
$zip = Join-Path $root "dist/ArtOfSimRally-SessionTools-$Version.zip"
if ((Test-Path -LiteralPath $stage) -or (Test-Path -LiteralPath $zip)) { throw 'Artifact exists; choose a new version' }
& dotnet build (Join-Path $root 'tools/testing/Recorder/Recorder.csproj') -c Release --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Recorder build failed' }
& dotnet build (Join-Path $root 'tools/testing/Replay/Replay.csproj') -c Release --nologo -warnaserror
if ($LASTEXITCODE -ne 0) { throw 'Analysis build failed' }
$map = [ordered]@{}
foreach ($path in @('tools/testing/Session.ps1','tools/testing/SessionEnvironment.ps1','tools/testing/Install-Recorder.ps1',
    'tools/testing/Recorder/Info.json','tools/testing/Recorder/bin/Release/net48/ArtOfSimRally.DevRecorder.dll',
    'lib/playback/Dbce.Wheel.Playback.dll','lib/playback/provenance.json','lib/playback/LICENSE','LICENSE','docs/SESSION-REPLAY.md')) { $map[$path] = $path }
foreach ($name in @('Replay.dll','Replay.deps.json','Replay.runtimeconfig.json','Dbce.Wheel.Ffb.dll','Dbce.Wheel.Playback.dll')) {
    $map["analysis/$name"] = "tools/testing/Replay/bin/Release/net8.0/$name"
}
foreach ($entry in $map.GetEnumerator()) {
    $target = Join-Path $stage $entry.Key
    New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
    Copy-Item -LiteralPath (Join-Path $root $entry.Value) -Destination $target
}
$readme = @'
# Art of Sim Rally optional SessionTools

Install the normal ArtOfSimRally mod first. Extract this archive to a writable
folder. Close the game, then open PowerShell in this folder and run:

    ./tools/testing/Install-Recorder.ps1 -SkipBuild
    ./tools/testing/Session.ps1 -Record -Name first-drive
    ./tools/testing/Session.ps1 -Stop
    ./tools/testing/Session.ps1 -Replay -Name first-drive

Read docs/SESSION-REPLAY.md for output muting, recovery, limitations and analysis.
Replay always mutes physical wheel and SimHub output; original signal data remains
available. Recording normally preserves physical output settings; -MuteOutputs
suppresses delivery while retaining the calculation path. F12 takes over playback.

Offline analysis requires .NET 8 runtime:

    dotnet analysis/Replay.dll --replay results/sessions/first-drive

Remove the optional probe while the game is closed:

    ./tools/testing/Install-Recorder.ps1 -Uninstall

No game assemblies, Unity Mod Manager, owner settings or captured sessions are
included. This is development tooling and trajectory playback, not deterministic
physics input replay or a physical force measurement. Keep captured evidence private.
'@
Set-Content -LiteralPath (Join-Path $stage 'README.md') -Value $readme -Encoding UTF8
[ordered]@{ schema=1; release=$Version; sourceRevision=$revision; sourceState='clean'; playback=$pin } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $stage 'build.json') -Encoding UTF8
$null = [Reflection.Assembly]::LoadFrom((Join-Path $root 'lib/playback/Dbce.Wheel.Playback.dll'))
[Dbce.Wheel.Playback.ArtifactSeal]::Complete($stage, [string[]](@($map.Keys) + @('README.md','build.json')))
[Dbce.Wheel.Playback.ArtifactSeal]::Verify($stage, [string[]]@('build.json','tools/testing/Recorder/bin/Release/net48/ArtOfSimRally.DevRecorder.dll'))
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
(Get-FileHash -LiteralPath $zip).Hash | Set-Content -LiteralPath "$zip.sha256" -Encoding ASCII
Write-Output "Packaged $zip"
