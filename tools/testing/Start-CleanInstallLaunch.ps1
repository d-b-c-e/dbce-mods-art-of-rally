# Clean-install lane, Phase B: one first-run launch of a disposable game copy, with the owner's per-user state
# saved (backup bytes verified), isolated and restored byte for byte. Attended: F6 setup in the copy needs a person.
# Run only in a granted rig slot (Rig-Lease held by the caller). Prepare the copy with Test-CleanInstallCopy.ps1's
# mod-free recipe and install the candidate into it first.
#
# Steam Cloud: the game's saves are Steam Auto-Cloud. A first run from empty saves while Steam syncs could delete the
# owner's cloud saves, so the run refuses unless the operator states which protection is in place. OwnerWaived: the
# owner does not care about save games and authorized Phase B without cloud protection (2026-10-31, via guna 485552).
#
# Success needs all of: the launched copy seen at its own path, a clean exit before the time limit, no other
# art of rally process at any point, owner state restored and equal, installed mod unchanged. This script stops only
# the process it started; any other art of rally process is a conflict it reports and never kills, and the restore
# waits for that process to close.
#
# -SelfTest runs every scenario on throwaway state with a stand-in executable; it touches no game or owner state.
param(
    [string]$Copy,
    [string]$StateDir,
    [ValidateSet('SteamExited', 'CloudDisabledForArt', 'OwnerWaived')][string]$CloudProtection,
    [string]$Installed = 'D:\Program Files (x86)\Steam\steamapps\common\artofrally',
    [int]$MaxSeconds = 1800,
    [int]$ConflictWaitSeconds = 600,
    # Unattended runs: once the copy is seen at its own path for this long, ask its window to close (what clicking X
    # does; the game rejects injected input). 0 = wait for a person to quit the game.
    [int]$CloseAfterSeconds = 0,
    [switch]$SelfTest,
    # Self-test plumbing only.
    [string]$ThrowawayRoot,
    [string]$GameArgs,
    [ValidateSet('', 'regdelete', 'isolate', 'launch', 'cleanup')][string]$FailAt = ''
)
$ErrorActionPreference = 'Stop'

# ArtUserState.ps1 is dot-sourced inside each function: at script scope its param block would reset this script's
# own parameters.
function Invoke-Launch {
    . (Join-Path $PSScriptRoot 'ArtUserState.ps1')
    $throwaway = [bool]$ThrowawayRoot
    $state = if ($throwaway) { New-ThrowawayState $ThrowawayRoot } else { Get-OwnerState }
    $exe = [IO.Path]::GetFullPath((Join-Path $Copy 'artofrally.exe'))
    $log = [System.Collections.Generic.List[string]]::new()
    function Note([string]$text) { $line = (Get-Date -Format 'HH:mm:ss') + ' ' + $text; $log.Add($line); Write-Host $line }

    # Preflight: nothing owner-side is changed before these pass.
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'An art of rally process is already running.' }
    if (-not (Test-Path -LiteralPath $exe)) { throw "No game copy at $Copy." }
    # The copy and the backup live only under disposable roots, through no junction or symlink, and the backup is
    # never inside a state folder it will restore (or the installed game).
    function Inside([string]$path, [string[]]$roots) {
        $full = [IO.Path]::GetFullPath($path).TrimEnd('\') + '\'
        foreach ($r in $roots) { if ($full.StartsWith([IO.Path]::GetFullPath($r).TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { return $true } }
        $false
    }
    $disposable = if ($throwaway) { @($env:TEMP) } else { @('E:\Source\_work', 'E:\Source\temp') }
    if (-not (Inside $Copy $disposable)) { throw "The copy must be under $($disposable -join ' or ')." }
    if (-not (Inside $StateDir $disposable)) { throw "The state directory must be under $($disposable -join ' or ')." }
    Assert-NoReparse $Copy
    Assert-NoReparse $StateDir
    if (Inside $StateDir @($state.Folders.Values)) { throw 'The state directory is inside a state folder it would restore.' }
    if (-not $throwaway -and ((Inside $Copy @($Installed)) -or (Inside $StateDir @($Installed)) -or (Inside $Installed @($Copy)))) {
        throw 'The copy or state directory overlaps the installed game.'
    }
    if (-not $throwaway) {
        if (-not $CloudProtection) { throw 'State -CloudProtection SteamExited, CloudDisabledForArt or OwnerWaived.' }
        if ($CloudProtection -eq 'SteamExited' -and (Get-Process steam -ErrorAction SilentlyContinue)) { throw 'Steam is running.' }
        # steam_appid.txt makes SteamAPI_RestartAppIfNecessary return false, so Steam does not start the installed
        # game instead; the process watch below proves it. Written into the copy only.
        Set-Content -LiteralPath (Join-Path $Copy 'steam_appid.txt') -Value '550320' -NoNewline
        $installedMod = Join-Path $Installed 'Mods\ArtOfSimRally\ArtOfSimRally.Mod.dll'
        $installedHash = (Get-FileHash -LiteralPath $installedMod).Hash
    }
    Save-State $state $StateDir
    if ((Compare-State $state $StateDir).Count) { throw 'Saved state does not match the live state; nothing was changed.' }
    Note "state saved and verified: $StateDir"

    $result = [ordered]@{ copy = $exe; cloudProtection = $CloudProtection; seenCopy = $false; exited = $false; exitCode = $null;
        timedOut = $false; conflicts = @(); restorePending = $false; ownerStateRestored = $false; installedModUnchanged = $true }
    $isolated = $false
    $launched = $null
    try {
        $isolated = $true
        if ($FailAt -ne 'regdelete') { & reg.exe delete $state.Key /f *> $null }
        & reg.exe query $state.Key *> $null
        if ($LASTEXITCODE -eq 0) { throw "The registry key is still present after delete: $($state.Key)" }
        if ($FailAt -eq 'isolate') { throw 'injected failure during isolation' }
        foreach ($folder in $state.Folders.Values) {
            Assert-SafeTarget $state $folder
            if (Test-Path -LiteralPath $folder) { Remove-Item -LiteralPath $folder -Recurse -Force }
        }
        Note 'state isolated (first run)'
        $start = @{ FilePath = $exe; WorkingDirectory = $Copy; PassThru = $true }
        if ($GameArgs) { $start.ArgumentList = $GameArgs; $start.WindowStyle = 'Hidden' }
        $launched = Start-Process @start
        $null = $launched.Handle  # keeps the handle so ExitCode is available after exit
        $launchedStart = $launched.StartTime
        Note "launched pid $($launched.Id)"
        if ($FailAt -eq 'launch') { throw 'injected failure after launch' }

        $deadline = (Get-Date).AddSeconds($MaxSeconds); $closeAsked = $false; $seenAt = $null
        function Foreign { @(Get-Process artofrally -ErrorAction SilentlyContinue | Where-Object { $_.Id -ne $launched.Id -or $_.StartTime -ne $launchedStart }) }
        function Record($processes) {
            foreach ($p in $processes) {
                $path = try { $p.Path } catch { $null }
                $result.conflicts += ('pid {0} {1}' -f $p.Id, ($(if ($path) { $path } else { 'path unreadable' })))
                Note "CONFLICT: another art of rally process (pid $($p.Id)); not stopped"
            }
        }
        while ((Get-Date) -lt $deadline) {
            Start-Sleep -Milliseconds 500
            $other = Foreign
            if ($other.Count) { Record $other; break }
            $launched.Refresh()
            if (-not $launched.HasExited) {
                $path = try { $launched.Path } catch { $null }
                if ($path -and [IO.Path]::GetFullPath($path) -ieq $exe) { if (-not $result.seenCopy) { Note "copy running at its own path"; $seenAt = Get-Date }; $result.seenCopy = $true }
                elseif (-not $result.seenCopy) { Note "launched process path not confirmed: [$path]" }
                if ($CloseAfterSeconds -gt 0 -and $result.seenCopy -and -not $closeAsked -and ((Get-Date) - $seenAt).TotalSeconds -ge $CloseAfterSeconds) {
                    # Until the game window exists CloseMainWindow returns false; ask again on the next poll.
                    if ($launched.CloseMainWindow()) { $closeAsked = $true; Note "asked the copy's window to close (-CloseAfterSeconds $CloseAfterSeconds)" }
                }
                continue
            }
            # A Steam relaunch quits the copy first and starts another game process a moment later: keep watching.
            $grace = (Get-Date).AddSeconds(20)
            while ((Get-Date) -lt $grace) {
                Start-Sleep -Milliseconds 500
                $other = Foreign
                if ($other.Count) { Record $other; break }
            }
            $result.exitCode = $launched.ExitCode
            if ($result.exitCode -ne 0) { Note "copy exited with code $($result.exitCode)" }
            $result.exited = $result.conflicts.Count -eq 0 -and $result.exitCode -eq 0
            break
        }
        if (-not $result.exited -and $result.conflicts.Count -eq 0 -and $null -eq $result.exitCode) { $result.timedOut = $true; Note 'time limit reached' }
        if (-not $throwaway) {
            $copyLog = Join-Path $state.Folders.locallow 'Player.log'
            if (Test-Path -LiteralPath $copyLog) { Copy-Item -LiteralPath $copyLog -Destination (Join-Path $StateDir 'copy-run-Player.log') }
            $umm = Join-Path $Copy 'artofrally_Data\Managed\UnityModManager\Log.txt'
            if (Test-Path -LiteralPath $umm) { Copy-Item -LiteralPath $umm -Destination (Join-Path $StateDir 'copy-run-umm-Log.txt') }
        }
    }
    catch { Note "ERROR: $($_.Exception.Message)" }
    finally {
        # Owned-process cleanup may fail; the restore below still runs, and it still refuses while any game runs.
        if ($launched) {
            try {
                if ($FailAt -eq 'cleanup') { throw 'injected cleanup failure' }
                $launched.Refresh()
                if (-not $launched.HasExited) { Note "stopping launched pid $($launched.Id)"; Stop-Process -Id $launched.Id -Force; $launched.WaitForExit(10000) | Out-Null }
            }
            catch { Note "could not stop launched pid $($launched.Id): $($_.Exception.Message)" }
        }
        if ($isolated) {
            # Never restore under a running game: wait for any other art of rally process to close.
            $wait = (Get-Date).AddSeconds($ConflictWaitSeconds)
            while ((Get-Process artofrally -ErrorAction SilentlyContinue) -and (Get-Date) -lt $wait) { Start-Sleep -Seconds 1 }
            if (Get-Process artofrally -ErrorAction SilentlyContinue) {
                $result.restorePending = $true
                Note "RESTORE PENDING: close art of rally, then run ArtUserState.ps1 -Restore `"$StateDir`""
            }
            else {
                try {
                    Restore-State $state $StateDir
                    $result.ownerStateRestored = (Compare-State $state $StateDir).Count -eq 0
                }
                catch { Note "RESTORE FAILED: $($_.Exception.Message); backup kept at $StateDir" }
            }
        }
        if (-not $throwaway) { $result.installedModUnchanged = ((Get-FileHash -LiteralPath $installedMod).Hash -eq $installedHash) }
        Note "restored: $($result.ownerStateRestored); installed mod unchanged: $($result.installedModUnchanged)"
        $result.log = $log
        $result | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $StateDir 'phaseB-result.json') -Encoding UTF8
    }
    $ok = $result.seenCopy -and $result.exited -and -not $result.timedOut -and $result.conflicts.Count -eq 0 -and
        $result.ownerStateRestored -and $result.installedModUnchanged
    if ($ok) { 0 } else { 1 }
}

function Invoke-LaunchSelfTest {
    . (Join-Path $PSScriptRoot 'ArtUserState.ps1')
    $base = Join-Path $env:TEMP ('art-launch-selftest-' + [guid]::NewGuid().ToString('n'))
    New-Item -ItemType Directory -Path $base | Out-Null
    # Stand-ins named artofrally.exe: ping.exe waits ~1 s per echo and then exits.
    $copy = Join-Path $base 'copy'; $other = Join-Path $base 'other'
    New-Item -ItemType Directory -Path $copy, $other | Out-Null
    Copy-Item "$env:WINDIR\System32\PING.EXE" (Join-Path $copy 'artofrally.exe')
    Copy-Item "$env:WINDIR\System32\PING.EXE" (Join-Path $other 'artofrally.exe')
    $failed = [System.Collections.Generic.List[string]]::new()
    $outside = Join-Path 'E:\Source\_work' ('art-launch-outside-' + [guid]::NewGuid().ToString('n'))
    function Scenario([string]$name, [int]$expectExit, [hashtable]$extra, [scriptblock]$during, [scriptblock]$check) {
        $root = Join-Path $base $name
        $state = New-ThrowawayState $root
        & reg.exe add $state.Key /v seeded /t REG_SZ /d $name /f *> $null
        New-Item -ItemType Directory -Force $state.Folders.a | Out-Null
        Set-Content (Join-Path $state.Folders.a 'save.txt') $name
        $stateDir = Join-Path $root 'saved'
        $childArgs = @('-NoProfile', '-File', $PSCommandPath, '-Copy', $copy, '-StateDir', $stateDir, '-ThrowawayRoot', $root) + $extra.args
        $job = Start-Job { param($a) & pwsh @a *> $null; $LASTEXITCODE } -ArgumentList (, $childArgs)
        if ($during) { & $during }
        $code = Receive-Job $job -Wait -AutoRemoveJob
        $equal = (Compare-State $state $stateDir).Count -eq 0
        $r = Get-Content (Join-Path $stateDir 'phaseB-result.json') -Raw | ConvertFrom-Json
        $ok = $code -eq $expectExit -and $equal -and (& $check $r)
        Write-Host ("{0} {1} (exit {2}, state equal {3})" -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $name, $code, $equal)
        if (-not $ok) { $failed.Add($name) }
        & reg.exe delete $state.Key /f *> $null
    }
    try {
        Scenario 'clean-exit' 0 @{ args = @('-GameArgs', '-n 3 127.0.0.1') } $null { param($r) $r.seenCopy -and $r.exited }
        # -CloseAfterSeconds on a windowed stand-in (a transparent form that exits 0 when asked to close, as Unity does).
        $form = Join-Path $base 'form.cs'
        Set-Content $form 'class F { static void Main() { var f = new System.Windows.Forms.Form { Opacity = 0, StartPosition = System.Windows.Forms.FormStartPosition.Manual, Location = new System.Drawing.Point(-32000, -32000) }; System.Windows.Forms.Application.Run(f); } }'
        & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /r:System.Windows.Forms.dll /r:System.Drawing.dll "/out:$(Join-Path $copy 'artofrally.exe')" $form | Out-Null
        Scenario 'close-after' 0 @{ args = @('-CloseAfterSeconds', '2', '-MaxSeconds', '30') } $null { param($r) $r.seenCopy -and $r.exited -and ($r.log -match 'asked the copy') }
        Copy-Item "$env:WINDIR\System32\PING.EXE" (Join-Path $copy 'artofrally.exe') -Force
        Scenario 'fail-during-isolation' 1 @{ args = @('-FailAt', 'isolate', '-GameArgs', '-n 2 127.0.0.1') } $null { param($r) $r.ownerStateRestored -and -not $r.seenCopy }
        Scenario 'fail-after-launch' 1 @{ args = @('-FailAt', 'launch', '-GameArgs', '-n 30 127.0.0.1') } $null { param($r) $r.ownerStateRestored -and -not $r.exited }
        Scenario 'time-limit' 1 @{ args = @('-MaxSeconds', '4', '-GameArgs', '-n 60 127.0.0.1') } $null { param($r) $r.timedOut -and $r.ownerStateRestored }
        Scenario 'foreign-process' 1 @{ args = @('-ConflictWaitSeconds', '60', '-GameArgs', '-n 20 127.0.0.1') } {
            Start-Sleep -Seconds 4
            Start-Process (Join-Path $other 'artofrally.exe') -ArgumentList '-n', '12', '127.0.0.1' -WindowStyle Hidden | Out-Null
        } { param($r) $r.conflicts.Count -ge 1 -and $r.ownerStateRestored -and -not $r.restorePending }
        # 192.0.2.1 (TEST-NET) never answers, so ping runs ~2 s and exits 1: a crash after the copy was seen.
        Scenario 'nonzero-exit' 1 @{ args = @('-GameArgs', '-n 3 -w 700 192.0.2.1') } $null { param($r) $r.seenCopy -and -not $r.exited -and $r.exitCode -ne 0 -and $r.ownerStateRestored }
        Scenario 'key-not-deleted' 1 @{ args = @('-FailAt', 'regdelete', '-GameArgs', '-n 2 127.0.0.1') } $null { param($r) -not $r.seenCopy -and $r.ownerStateRestored -and ($r.log -match 'still present') }
        # Cleanup throws while the timed-out copy still runs: the restore waits for it to close, then restores.
        Scenario 'cleanup-throws' 1 @{ args = @('-FailAt', 'cleanup', '-MaxSeconds', '3', '-ConflictWaitSeconds', '60', '-GameArgs', '-n 10 127.0.0.1') } $null {
            param($r) $r.timedOut -and $r.ownerStateRestored -and -not $r.restorePending -and ($r.log -match 'could not stop') }

        # Preflight refusals: nothing is saved or changed.
        function Refusal([string]$name, [string]$copyPath, [string]$stateDir) {
            $root = Join-Path $base $name
            $state = New-ThrowawayState $root
            & reg.exe add $state.Key /v seeded /t REG_SZ /d $name /f *> $null
            New-Item -ItemType Directory -Force $state.Folders.a | Out-Null
            Set-Content (Join-Path $state.Folders.a 'save.txt') $name
            if (-not $stateDir) { $stateDir = Join-Path $root 'saved' }
            & pwsh -NoProfile -File $PSCommandPath -Copy $copyPath -StateDir $stateDir -ThrowawayRoot $root -GameArgs '-n 2 127.0.0.1' *> $null
            $code = $LASTEXITCODE
            & reg.exe query $state.Key /v seeded *> $null
            $untouched = $LASTEXITCODE -eq 0 -and (Get-Content (Join-Path $state.Folders.a 'save.txt')) -eq $name -and
                -not (Test-Path -LiteralPath (Join-Path $stateDir 'manifest.json'))
            $ok = $code -ne 0 -and $untouched
            Write-Host ("{0} {1} (exit {2}, untouched {3})" -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $name, $code, $untouched)
            if (-not $ok) { $failed.Add($name) }
            & reg.exe delete $state.Key /f *> $null
        }
        $alias = Join-Path $base 'copy-alias'
        New-Item -ItemType Junction -Path $alias -Target $copy | Out-Null
        Refusal 'refuse-copy-junction' $alias $null
        Refusal 'refuse-statedir-in-state' $copy (Join-Path $base 'refuse-statedir-in-state\live\a\saved')
        # A real stand-in outside the self-test's disposable root (%TEMP%), so only the bound can refuse it.
        New-Item -ItemType Directory -Path $outside | Out-Null
        Copy-Item "$env:WINDIR\System32\PING.EXE" (Join-Path $outside 'artofrally.exe')
        Refusal 'refuse-copy-outside-roots' $outside $null
    }
    finally {
        Get-Process artofrally -ErrorAction SilentlyContinue | Where-Object { $_.Path -like "$base*" } | Stop-Process -Force
        Start-Sleep -Seconds 1
        $alias = Join-Path $base 'copy-alias'
        if (Test-Path -LiteralPath $alias) { (Get-Item -LiteralPath $alias -Force).Delete() }
        Remove-Item -LiteralPath $base -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $outside -Recurse -Force -ErrorAction SilentlyContinue
    }
    $failed
}

if ($SelfTest) {
    if (Get-Process artofrally -ErrorAction SilentlyContinue) { throw 'Close art of rally before the self-test.' }
    $failed = Invoke-LaunchSelfTest
    if ($failed.Count) { Write-Host "LAUNCH SELFTEST FAIL ($($failed -join ', '))"; exit 1 }
    Write-Host 'LAUNCH SELFTEST PASS'
    exit 0
}
if (-not $Copy -or -not $StateDir) { Write-Host 'Use -Copy and -StateDir (and -CloudProtection), or -SelfTest.'; exit 2 }
exit (Invoke-Launch | Select-Object -Last 1)
