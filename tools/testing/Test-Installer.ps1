<# Tests an extracted package in isolated fake game folders using the same
   Windows PowerShell/batch entry points players run. No game or hardware is used.
   Keeps logs and fixtures in results/installer-* for inspection. #>
[CmdletBinding()]
param([Parameter(Mandatory)][string]$PackageDirectory,
      # Optional: a second extracted package installed over the first at the end, e.g. the
      # baseline over a shared-input candidate. Its payload must replace the first exactly.
      [string]$ThenPackageDirectory)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$package = [IO.Path]::GetFullPath($PackageDirectory)
. (Join-Path $root 'tools/installer/verify.ps1')
$manifest = Assert-Payload $package
# The mod-folder payload this package lists (a shared-input candidate adds dbce_shim.dll).
$modPayload = @($manifest.files.PSObject.Properties.Name | Where-Object { $_.StartsWith('ArtOfSimRally/') })
$run = Join-Path $root ('results/installer-' + [Guid]::NewGuid().ToString('N'))
$game = Join-Path $run 'game [USB] with spaces'
$mod = Join-Path $game 'Mods/ArtOfSimRally'
$umm = Join-Path $game 'artofrally_Data/Managed/UnityModManager'
$native = Join-Path $game 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll'
$ps = Join-Path $env:SystemRoot 'System32/WindowsPowerShell/v1.0/powershell.exe'
$checks = 0
New-Item -ItemType Directory -Path $game -Force | Out-Null
Set-Content -LiteralPath (Join-Path $game 'artofrally.exe') -Value 'fixture; never executed'

function Assert([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function Invoke-Installer([string]$Name, [string]$Action = 'Install', [string]$From = $package, [bool]$Fails = $false) {
    # No destructive shell operations: the batch launcher calls its own installer.
    # Quoted paths exercise %* forwarding; redirected stdin makes pause nonblocking.
    $launcher = Join-Path $From ($Action + '.bat')
    $line = '""{0}" -GameDir "{1}" <nul"' -f $launcher, $game
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = $env:ComSpec
    $start.Arguments = '/d /s /c ' + $line
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    # Deliberately inherit PSModulePath: the batch launchers must work when
    # called from PowerShell 7 as well as Explorer/Windows PowerShell.
    $process = [Diagnostics.Process]::Start($start)
    $stdout = $process.StandardOutput.ReadToEndAsync()
    $stderr = $process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) {
        # Only this isolated test helper is terminated; never the real game.
        $process.Kill()
        throw "$Name timed out; see $run"
    }
    $output = @($stdout.Result, $stderr.Result)
    $code = $process.ExitCode
    $process.Dispose()
    $output | Out-File -LiteralPath (Join-Path $run ($Name + '.log')) -Encoding utf8
    Assert $(if ($Fails) { $code -ne 0 } else { $code -eq 0 }) "$Name returned $code; see $run"
    return ($output -join "`n")
}

try {
    # Another loader's winhttp.dll is never replaced: refuse through the actual Install.bat, before any payload.
    $loader = Join-Path $game 'winhttp.dll'
    Set-Content -LiteralPath $loader -Value 'another loader; never loaded'
    $log = Invoke-Installer 'foreign-loader' -Fails $true
    Assert ($log -match 'Another mod loader is already in the game folder') 'Foreign-loader refusal absent'
    Assert (-not (Test-Path -LiteralPath $mod) -and -not (Test-Path -LiteralPath $umm)) 'Foreign-loader refusal wrote files'
    Assert ((Get-Content -LiteralPath $loader -Raw).Trim() -eq 'another loader; never loaded') 'Foreign loader changed'
    Remove-Item -LiteralPath $loader

    # No Unity Mod Manager: the bundled copy goes in with the mod (STD-040), recorded for Uninstall.
    $log = Invoke-Installer 'fresh-install'
    Assert ($log.Contains("All installed files match release $($manifest.release)")) 'Installed release was not reported'
    Assert ($log -match 'Unity Mod Manager installed') 'Bundled loader install not reported'
    foreach ($name in $UmmGameFiles + $UmmManagerFiles) {
        $file = if ($UmmGameFiles -contains $name) { Join-Path $game $name } else { Join-Path $umm $name }
        Assert ((Get-FileHash -LiteralPath $file).Hash -eq $manifest.files."UnityModManager/$name") "Bundled loader mismatch: $name"
    }
    Assert (Test-Path -LiteralPath (Join-Path $umm 'installed-by-art-of-sim-rally.json')) 'Bundled loader record absent'
    foreach ($relative in $modPayload) {
        $file = Join-Path $game ('Mods/' + $relative)
        Assert ((Get-FileHash -LiteralPath $file).Hash -eq $manifest.files.$relative) "Payload mismatch: $relative"
    }
    Assert ((Get-FileHash -LiteralPath $native).Hash -eq $manifest.files.'ArtOfSimRally/UnityForceFeedback.dll') 'Native plugin copy mismatch'

    # dbce_shim.dll is ours only while the installed build.json pins its bytes (romi 32767).
    $shimFile = Join-Path $mod 'dbce_shim.dll'
    $packageShim = $manifest.files.'ArtOfSimRally/dbce_shim.dll'
    if ($packageShim) {
        # A changed shim is not ours: a candidate install refuses before writing anything.
        Add-Content -LiteralPath $shimFile -Value 'changed by hand'
        $changed = (Get-FileHash -LiteralPath $shimFile).Hash
        $modHash = (Get-FileHash -LiteralPath (Join-Path $mod 'ArtOfSimRally.Mod.dll')).Hash
        Remove-Item -LiteralPath $native
        $log = Invoke-Installer 'changed-shim' -Fails $true
        Assert ($log -match 'was not installed by this mod, or has changed') 'Changed-shim refusal absent'
        Assert ((Get-FileHash -LiteralPath $shimFile).Hash -eq $changed) 'Candidate install replaced a changed shim'
        Assert ((Get-FileHash -LiteralPath (Join-Path $mod 'ArtOfSimRally.Mod.dll')).Hash -eq $modHash -and -not (Test-Path -LiteralPath $native)) 'Refused install wrote files'
        Copy-Item -LiteralPath (Join-Path $package 'ArtOfSimRally/dbce_shim.dll') -Destination $shimFile -Force
        $null = Invoke-Installer 'owned-shim-reinstall'
        Assert ((Get-FileHash -LiteralPath $shimFile).Hash -eq $packageShim) 'Owned shim not reinstalled'
    } else {
        # A shim placed by hand is never replaced or removed by a release without one.
        Set-Content -LiteralPath $shimFile -Value 'unknown shim; never loaded'
        $unknownShim = (Get-FileHash -LiteralPath $shimFile).Hash
    }

    $settings = Join-Path $mod 'Settings.xml'
    Set-Content -LiteralPath $settings -Value '<Settings><LandingEffectsEnabled>false</LandingEffectsEnabled><Strength>15</Strength></Settings>'
    $settingsHash = (Get-FileHash -LiteralPath $settings).Hash
    Set-Content -LiteralPath (Join-Path $mod 'user-notes.txt') -Value 'keep'
    $other = Join-Path $game 'Mods/OtherMod'
    New-Item -ItemType Directory -Path $other | Out-Null
    Set-Content -LiteralPath (Join-Path $other 'Info.json') -Value 'keep other mod'
    $log = Invoke-Installer 'upgrade'
    Assert ((Get-FileHash -LiteralPath $settings).Hash -eq $settingsHash) 'Upgrade changed saved preferences'
    if (-not $packageShim) {
        Assert ((Get-FileHash -LiteralPath $shimFile).Hash -eq $unknownShim -and $log -match 'Kept dbce_shim.dll') 'Upgrade changed or removed an unknown shim'
    }

    # Real file lock causes a failed upgrade, and must survive the batch pause
    # as a nonzero exit. Successful retry restores the complete payload.
    $locked = [IO.File]::Open((Join-Path $mod 'ArtOfSimRally.Mod.dll'), 'Open', 'Read', 'None')
    try { $log = Invoke-Installer 'locked-file' -Fails $true } finally { $locked.Dispose() }
    Assert ($log -match 'successful retry verifies the complete install') 'Locked-file retry guidance absent'
    Assert ((Get-FileHash -LiteralPath $settings).Hash -eq $settingsHash) 'Failed upgrade changed settings'
    $null = Invoke-Installer 'retry-after-lock'

    # Uninstall must not depend on UMM remaining installed, and keeps a bundled loader another mod uses.
    Remove-Item -LiteralPath (Join-Path $umm 'UnityModManager.dll')
    $log = Invoke-Installer 'uninstall-without-umm' -Action 'Uninstall'
    Assert ($log -match 'Kept Unity Mod Manager: other mods use it' -and (Test-Path -LiteralPath (Join-Path $game 'winhttp.dll'))) 'Uninstall removed a loader another mod uses'
    Assert ((Get-FileHash -LiteralPath $settings).Hash -eq $settingsHash) 'Uninstall changed settings'
    Assert (Test-Path -LiteralPath (Join-Path $mod 'user-notes.txt')) 'Uninstall removed user notes'
    Assert (Test-Path -LiteralPath (Join-Path $other 'Info.json')) 'Uninstall removed another mod'
    foreach ($relative in $modPayload) {
        Assert (-not (Test-Path -LiteralPath (Join-Path $game ('Mods/' + $relative)))) "Uninstall retained $relative"
    }
    if ($packageShim) { Assert (-not (Test-Path -LiteralPath $shimFile)) 'Uninstall retained the owned shim' }
    else {
        Assert ((Get-FileHash -LiteralPath $shimFile).Hash -eq $unknownShim -and $log -match 'Kept dbce_shim.dll') 'Uninstall changed or removed an unknown shim'
        Remove-Item -LiteralPath $shimFile
    }
    Assert (-not (Test-Path -LiteralPath $native)) 'Uninstall retained native copy'
    $null = Invoke-Installer 'repeat-uninstall' -Action 'Uninstall'

    # Test rejection using copies, never mutate the candidate under evaluation.
    Set-Content -LiteralPath (Join-Path $umm 'UnityModManager.dll') -Value 'fixture; never loaded'
    $bad = Join-Path $run 'damaged download'
    New-Item -ItemType Directory -Path $bad | Out-Null
    Get-ChildItem -LiteralPath $package | Copy-Item -Destination $bad -Recurse
    Add-Content -LiteralPath (Join-Path $bad 'ArtOfSimRally/Info.json') -Value 'damaged'
    $log = Invoke-Installer 'damaged-download' -From $bad -Fails $true
    Assert ($log -match 'incomplete or has changed') 'Damaged-download guidance absent'
    Assert (-not (Test-Path -LiteralPath (Join-Path $mod 'ArtOfSimRally.Mod.dll'))) 'Damaged package wrote a payload'
    Assert ((Get-FileHash -LiteralPath $settings).Hash -eq $settingsHash) 'Damaged package changed settings'
    Remove-Item -LiteralPath (Join-Path $bad 'verify.ps1')
    $log = Invoke-Installer 'missing-verifier' -From $bad -Fails $true
    Assert ($log -match 'incomplete or has changed') 'Missing verifier guidance absent'

    if ($ThenPackageDirectory) {
        # Install the first package again, then the second over it: exactly the second's payload remains.
        $then = [IO.Path]::GetFullPath($ThenPackageDirectory)
        $thenManifest = Assert-Payload $then
        $null = Invoke-Installer 'first-again'
        $log = Invoke-Installer 'then-install' -From $then
        Assert ($log.Contains("All installed files match release $($thenManifest.release)")) 'Second release was not reported'
        $thenPayload = @($thenManifest.files.PSObject.Properties.Name | Where-Object { $_.StartsWith('ArtOfSimRally/') })
        foreach ($relative in $thenPayload) {
            Assert ((Get-FileHash -LiteralPath (Join-Path $game ('Mods/' + $relative))).Hash -eq $thenManifest.files.$relative) "Second payload mismatch: $relative"
        }
        foreach ($relative in $OptionalPayloadFiles | Where-Object { $thenPayload -notcontains $_ }) {
            Assert (-not (Test-Path -LiteralPath (Join-Path $game ('Mods/' + $relative)))) "Second install retained $relative"
        }
        Assert ((Get-FileHash -LiteralPath $native).Hash -eq $thenManifest.files.'ArtOfSimRally/UnityForceFeedback.dll') 'Second native plugin copy mismatch'
        Assert ((Get-FileHash -LiteralPath $settings).Hash -eq $settingsHash) 'Second install changed settings'
        if ($packageShim -and -not $thenManifest.files.'ArtOfSimRally/dbce_shim.dll') {
            # A changed shim survives the second release and its uninstall (romi 32767).
            $null = Invoke-Installer 'first-for-change'
            Add-Content -LiteralPath $shimFile -Value 'changed by hand'
            $changed = (Get-FileHash -LiteralPath $shimFile).Hash
            $log = Invoke-Installer 'then-over-changed-shim' -From $then
            Assert ((Get-FileHash -LiteralPath $shimFile).Hash -eq $changed -and $log -match 'Kept dbce_shim.dll') 'Second release removed a changed shim'
            $log = Invoke-Installer 'uninstall-keeps-changed-shim' -Action 'Uninstall' -From $then
            Assert ((Get-FileHash -LiteralPath $shimFile).Hash -eq $changed -and $log -match 'Kept dbce_shim.dll') 'Uninstall removed a changed shim'
            Remove-Item -LiteralPath $shimFile
        }
    }

    # A clean game: the bundled loader comes out again with the mod when no other mod uses it.
    $game = Join-Path $run 'clean game'; $umm = Join-Path $game 'artofrally_Data/Managed/UnityModManager'
    New-Item -ItemType Directory -Path $game -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $game 'artofrally.exe') -Value 'fixture; never executed'
    $null = Invoke-Installer 'clean-install'
    Set-Content -LiteralPath (Join-Path $umm 'Log.txt') -Value 'written by the manager'
    $log = Invoke-Installer 'clean-uninstall' -Action 'Uninstall'
    Assert ($log -match 'Removed Unity Mod Manager') 'Bundled loader removal not reported'
    Assert (-not (Test-Path -LiteralPath $umm) -and -not (Test-Path -LiteralPath (Join-Path $game 'winhttp.dll')) -and
        -not (Test-Path -LiteralPath (Join-Path $game 'doorstop_config.ini'))) 'Uninstall left the bundled loader'

    # A game whose own Unity Mod Manager is installed: it is used as it is and never recorded or removed.
    $game = Join-Path $run 'own loader'; $umm = Join-Path $game 'artofrally_Data/Managed/UnityModManager'
    New-Item -ItemType Directory -Path $umm -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $game 'artofrally.exe') -Value 'fixture; never executed'
    Set-Content -LiteralPath (Join-Path $umm 'UnityModManager.dll') -Value 'own manager; never loaded'
    Set-Content -LiteralPath (Join-Path $game 'winhttp.dll') -Value 'own loader; never loaded'
    $log = Invoke-Installer 'own-loader-install'
    Assert ($log -match 'Unity Mod Manager present' -and -not (Test-Path -LiteralPath (Join-Path $umm 'installed-by-art-of-sim-rally.json'))) 'Own loader was recorded as bundled'
    Assert ((Get-Content -LiteralPath (Join-Path $game 'winhttp.dll') -Raw).Trim() -eq 'own loader; never loaded') 'Own loader replaced'
    $null = Invoke-Installer 'own-loader-uninstall' -Action 'Uninstall'
    Assert ((Test-Path -LiteralPath (Join-Path $umm 'UnityModManager.dll')) -and (Test-Path -LiteralPath (Join-Path $game 'winhttp.dll'))) 'Uninstall removed an own loader'

    $output = & $ps -NoProfile -ExecutionPolicy Bypass -File (Join-Path $package 'install.ps1') -GameDir (Join-Path $run 'absent game') 2>&1
    $code = $LASTEXITCODE
    $output | Out-File -LiteralPath (Join-Path $run 'invalid-game.log') -Encoding utf8
    Assert ($code -ne 0 -and ($output -join "`n") -match 'Browse local files') 'Invalid game guidance absent'
    [ordered]@{ status='passed'; assertions=$checks; package=$package; release=$manifest.release; evidence=$run; scope='isolated installer only; no game/UMM GUI or hardware executed' } |
        ConvertTo-Json -Compress | Tee-Object -FilePath (Join-Path $run 'report.json')
} catch {
    [ordered]@{ status='failed'; assertions=$checks; error=$_.Exception.Message; evidence=$run } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'failed.json')
    throw
}
