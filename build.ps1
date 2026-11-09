# Builds the solution and runs the offline test harnesses. No arguments needed: `pwsh build.ps1`.
# lib\umm must hold UnityModManager.dll and 0Harmony.dll from the game (see AGENTS.md, Build).
# The release candidate runner, tools/testing/Test-Rc.ps1, adds the package, the installer checks and the Unity Mono runs.
param([string]$GameDir = 'D:\Program Files (x86)\Steam\steamapps\common\artofrally')
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$failed = [Collections.Generic.List[string]]::new()
function Step([string]$Name, [scriptblock]$Run) {
    $out = & $Run 2>&1
    if ($LASTEXITCODE) { $failed.Add($Name); "FAIL $Name"; $out | Select-Object -Last 15 } else { "ok   $Name" }
}
$game = "-p:GameDir=$GameDir"

Step 'ArtOfSimRally.sln' { dotnet build ArtOfSimRally.sln -c Release -warnaserror $game --nologo -v q }
foreach ($t in 'ForceLifecycle', 'GameBindings', 'GameState', 'Landing', 'Lifecycle', 'Regression', 'RuntimeCompatibility',
               'SessionTape', 'SettingsUi', 'SharedBindingPresentation', 'Signals', 'Support', 'TripleScreen', 'UiHost', 'WheelInput', 'CameraTuning') {
    Step $t { dotnet run --project "tests/$t/$t.csproj" -c Release -v q $game }
}
# These build for both net8.0 and the game's Mono; the net8.0 run is the offline one.
foreach ($t in 'PromptGlyphs', 'SharedBindings', 'ShimInput') {
    Step $t { dotnet run --project "tests/$t/$t.csproj" -f net8.0 -c Release -v q $game }
}
# These load the built mod and the game's assemblies, so they take the repo root and the game folder.
foreach ($t in 'Telemetry', 'ProbeHooks') {
    Step $t {
        dotnet build "tests/$t/$t.csproj" -c Release --nologo -warnaserror -v q $game
        if (-not $LASTEXITCODE) { & "tests/$t/bin/Release/net48/$t.exe" $PSScriptRoot ($GameDir -replace '\\', '/') }
    }
}

# The recorder harness writes synthetic sessions into a new folder, removed afterwards.
Step 'Recorder' {
    $work = Join-Path $PSScriptRoot "artifacts\build-check\recorder-$([guid]::NewGuid().ToString('N').Substring(0, 8))"
    dotnet build tools/testing/Recorder/Recorder.csproj -c Release --nologo -warnaserror -v q $game
    if (-not $LASTEXITCODE) { dotnet run --project tests/Recorder/Recorder.csproj -c Release -v q $game -- $work src/ArtOfSimRally.Mod/bin/Release/ArtOfSimRally.Mod.dll ($GameDir -replace '\\', '/') }
    $code = $LASTEXITCODE
    if ([IO.Directory]::Exists($work)) { [IO.Directory]::Delete($work, $true) }
    $global:LASTEXITCODE = $code
}
if ($failed.Count) { throw "$($failed.Count) step(s) failed: $($failed -join ', ')" }
'build and offline tests passed'
