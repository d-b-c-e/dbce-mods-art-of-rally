#Requires -Version 7.0
# Offline checks for the exact-package staging, the production-Apply confirmation and the shared-car request (sana
# 43761): Prepare-SharedInputCopy -PackageZip/-AwaitApply, Confirm-SharedInputApply, and Invoke-SharedInputCopy's
# -SharedCar request and refusals. Fake packages, copies and receipts in a temporary folder only: no game, Steam,
# process launch, device or owner file.
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$prepare = Join-Path $here 'Prepare-SharedInputCopy.ps1'
$confirm = Join-Path $here 'Confirm-SharedInputApply.ps1'
$invoke = Join-Path $here 'Invoke-SharedInputCopy.ps1'
$root = Join-Path ([IO.Path]::GetTempPath()) ('art-shared-copy-test-' + [guid]::NewGuid().ToString('N'))
$checks = 0
function Check([bool]$ok, [string]$why) { if (!$ok) { throw "FAILED: $why" }; $script:checks++ }
function Refused([scriptblock]$block, [string]$like) {
    try { & $block *> $null } catch { if ($_.Exception.Message -notlike "*$like*") { throw "Wrong refusal: $($_.Exception.Message) (wanted $like)" }; $script:checks++; return }
    throw "Not refused: $like"
}
function Hash([string]$p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash }
function Write-Bytes([string]$p, [string]$text) { New-Item -ItemType Directory -Path (Split-Path $p) -Force | Out-Null; [IO.File]::WriteAllText($p, $text) }

# A package: its files, a manifest of their hashes, and a build.json naming the shim and native.
function New-Package([string]$name, [scriptblock]$tamper = $null, [string]$nativeText = 'native-unlatched') {
    $dir = Join-Path $root "pkg-$name"
    foreach ($f in @('ArtOfSimRally/ArtOfSimRally.Mod.dll', 'ArtOfSimRally/Info.json', 'ArtOfSimRally/Dbce.TripleScreen.Core.dll', 'LICENSE', 'install.ps1')) { Write-Bytes (Join-Path $dir $f) "content of $f" }
    Write-Bytes (Join-Path $dir 'ArtOfSimRally/dbce_shim.dll') 'shim-bytes'
    Write-Bytes (Join-Path $dir 'ArtOfSimRally/UnityForceFeedback.dll') $nativeText
    $build = [ordered]@{ schema = 1; release = '0.4.2-rc.99'; identity = '0.4.2-rc.99+fake.clean'
        sharedInputCandidate = [ordered]@{ shimSha256 = (Hash (Join-Path $dir 'ArtOfSimRally/dbce_shim.dll')); nativeSha256 = (Hash (Join-Path $dir 'ArtOfSimRally/UnityForceFeedback.dll')) } }
    Write-Bytes (Join-Path $dir 'ArtOfSimRally/build.json') ($build | ConvertTo-Json -Depth 4)
    $files = [ordered]@{}
    foreach ($f in Get-ChildItem -LiteralPath $dir -File -Recurse) { $files[[IO.Path]::GetRelativePath($dir, $f.FullName).Replace('\', '/')] = Hash $f.FullName }
    Write-Bytes (Join-Path $dir 'manifest.json') ([ordered]@{ schema = 1; release = '0.4.2-rc.99'; files = $files } | ConvertTo-Json -Depth 4)
    if ($tamper) { & $tamper $dir }
    $zip = Join-Path $root "$name.zip"
    Compress-Archive -Path (Join-Path $dir '*') -DestinationPath $zip
    [pscustomobject]@{ zip = $zip; sha = (Hash $zip); shim = (Hash (Join-Path $dir 'ArtOfSimRally/dbce_shim.dll')); dir = $dir }
}
$ownerSettings = '<?xml version="1.0" encoding="utf-8"?><Settings><WheelInputEnabled>true</WheelInputEnabled><ForceFeedbackEnabled>true</ForceFeedbackEnabled><Strength>50</Strength></Settings>'
function New-Copy([string]$name) {
    $copy = Join-Path $root "copy-$name"
    Write-Bytes (Join-Path $copy 'artofrally.exe') 'exe'
    New-Item -ItemType Directory -Path (Join-Path $copy 'artofrally_Data/Plugins/x86_64') -Force | Out-Null
    Write-Bytes (Join-Path $copy 'Mods/ArtOfSimRally/dbce-controls.ini') "[Controls]`nSchema=ini-controls-1`n"
    Write-Bytes (Join-Path $copy 'Mods/ArtOfSimRally/Settings.xml') $ownerSettings
    Write-Bytes (Join-Path $copy 'Mods/ArtOfSimRally/ArtOfSimRally.Mod.dll.37013.cache') 'loader cache'
    $copy
}
New-Item -ItemType Directory -Path $root | Out-Null
try {
    $owner = Join-Path $root 'owner'; Write-Bytes (Join-Path $owner 'Mods/ArtOfSimRally/Settings.xml') $ownerSettings
    $pkg = New-Package 'good'
    $copy = New-Copy 'a'; $result = Join-Path $root 'staged-a'
    function Args([string]$gameCopy) { @{ GameCopy = $gameCopy; SourceGame = $owner; PackageZip = $pkg.zip; ExpectedPackageSha256 = $pkg.sha; ExpectedShimSha256 = $pkg.shim } }
    $common = Args $copy

    # Exact package awaiting the production Apply.
    & $prepare @common -Result $result -AwaitApply -ForceFeedbackOff | Out-Null
    $staged = Get-Content (Join-Path $result 'staged.json') -Raw | ConvertFrom-Json
    $mod = Join-Path $copy 'Mods/ArtOfSimRally'
    Check ($staged.applyPending -eq $true -and $staged.package.release -eq '0.4.2-rc.99' -and $staged.package.sha256 -eq $pkg.sha -and $staged.forceFeedbackOff) 'package staging recorded'
    Check (!(Test-Path (Join-Path $mod 'dbce-controls.ini')) -and (Test-Path (Join-Path $result 'removed-dbce-controls.ini')) -and $staged.removedControls.'dbce-controls.ini') 'an earlier copy-only profile is kept as evidence and removed'
    Check ($null -eq $staged.files.'Mods/ArtOfSimRally/dbce-controls.ini') 'no profile pinned before the Apply'
    foreach ($n in 'ArtOfSimRally.Mod.dll', 'Info.json', 'build.json', 'dbce_shim.dll', 'UnityForceFeedback.dll', 'Dbce.TripleScreen.Core.dll') {
        Check ((Hash (Join-Path $mod $n)) -eq (Hash (Join-Path $pkg.dir "ArtOfSimRally/$n")) -and $staged.files."Mods/ArtOfSimRally/$n" -eq (Hash (Join-Path $mod $n))) "package byte staged and pinned: $n"
    }
    Check ((Hash (Join-Path $copy 'artofrally_Data/Plugins/x86_64/UnityForceFeedback.dll')) -eq (Hash (Join-Path $pkg.dir 'ArtOfSimRally/UnityForceFeedback.dll'))) 'package native staged in Plugins'
    [xml]$s = Get-Content (Join-Path $mod 'Settings.xml') -Raw
    Check ($s.DocumentElement.ForceFeedbackEnabled -eq 'false' -and $null -eq $s.DocumentElement.SelectSingleNode('SharedInput') -and $s.DocumentElement.Strength -eq '50') 'settings: force off, SharedInput left to the Apply, tuning kept'
    Check ((Hash (Join-Path $owner 'Mods/ArtOfSimRally/Settings.xml')) -eq $staged.ownerSettingsSha256) 'owner settings untouched'

    # Staging refusals.
    Refused { & $prepare @common -Result (Join-Path $root 'r1') -AwaitApply } 'no DBCE_FFB_MUTE latch'
    $bad = $common.Clone(); $bad.ExpectedPackageSha256 = ('0' * 64)
    Refused { & $prepare @bad -Result (Join-Path $root 'r2') -AwaitApply -ForceFeedbackOff } 'not the pinned release bytes'
    $tampered = New-Package 'tampered' { param($d) Write-Bytes (Join-Path $d 'ArtOfSimRally/ArtOfSimRally.Mod.dll') 'swapped' }
    Refused { & $prepare -GameCopy (New-Copy 'b') -SourceGame $owner -PackageZip $tampered.zip -ExpectedPackageSha256 $tampered.sha -ExpectedShimSha256 $tampered.shim -Result (Join-Path $root 'r3') -AwaitApply -ForceFeedbackOff } 'differs from its manifest'
    $extra = New-Package 'extra' { param($d) Write-Bytes (Join-Path $d 'ArtOfSimRally/Extra.dll') 'unlisted' }
    Refused { & $prepare -GameCopy (New-Copy 'c') -SourceGame $owner -PackageZip $extra.zip -ExpectedPackageSha256 $extra.sha -ExpectedShimSha256 $extra.shim -Result (Join-Path $root 'r4') -AwaitApply -ForceFeedbackOff } 'outside its manifest'
    Refused { & $prepare -GameCopy (New-Copy 'd') -SourceGame $owner -PackageZip $pkg.zip -ExpectedPackageSha256 $pkg.sha -Result (Join-Path $root 'r5') -AwaitApply -ForceFeedbackOff } 'qualified copied-provider build'
    Refused { & $prepare @common -Result (Join-Path $root 'r6') -AwaitApply -ForceFeedbackOff -Profile $pkg.zip } 'stages no profile'
    Refused { & $prepare @common -Result (Join-Path $root 'r7') -Shim $pkg.zip -AwaitApply -ForceFeedbackOff } 'omit -Shim and -Native'
    Refused { & $prepare -GameCopy (New-Copy 'e') -SourceGame $owner -Result (Join-Path $root 'r8') -AwaitApply -Profile $pkg.zip -Shim $pkg.zip -Native $pkg.zip } 'exact package'
    $sharedSource = Join-Path $root 'shared-settings.xml'; Write-Bytes $sharedSource '<Settings><SharedInput>true</SharedInput></Settings>'
    $fArgs = Args (New-Copy 'f'); Refused { & $prepare @fArgs -Result (Join-Path $root 'r9') -AwaitApply -ForceFeedbackOff -SettingsSource $sharedSource } 'would not write it'
    $stale = New-Copy 'g'; Write-Bytes (Join-Path $stale 'Mods/ArtOfSimRally/Old.Helper.dll') 'stale'
    $gArgs = Args $stale; Refused { & $prepare @gArgs -Result (Join-Path $root 'r10') -AwaitApply -ForceFeedbackOff } 'outside the package'

    # The runner refuses a staging that still awaits its Apply, before any Steam check or launch.
    Refused { & $invoke -Result $result -PreflightOnly } 'awaits its production Apply'

    # A production Apply's receipt (LiveApplyRun apply.json shape), then the confirmation.
    $settingsPath = 'Mods/ArtOfSimRally/Settings.xml'; $controlsPath = 'Mods/ArtOfSimRally/dbce-controls.ini'
    function Apply-Fake([string]$copyDir, [string]$stagedSettings, [string]$evidence, [string]$settingsXml, [string]$status = 'applied', [string]$install = $copyDir) {
        Write-Bytes (Join-Path $copyDir $settingsPath) $settingsXml
        Write-Bytes (Join-Path $copyDir $controlsPath) "[Controls]`nSchema=ini-controls-1`nProfile=Art DS4`n"
        $receipt = [ordered]@{ Schema = 2; Game = 'art-of-rally'; Install = $install; Status = $status; Files = @(
            [ordered]@{ path = $settingsPath; beforeHash = $stagedSettings; afterHash = (Hash (Join-Path $copyDir $settingsPath)) },
            [ordered]@{ path = 'Mods/ArtOfSimRally/TripleScreen.xml'; beforeHash = 'absent'; afterHash = 'absent' },
            [ordered]@{ path = $controlsPath; beforeHash = 'absent'; afterHash = (Hash (Join-Path $copyDir $controlsPath)) }) }
        Write-Bytes (Join-Path $evidence 'apply.json') ($receipt | ConvertTo-Json -Depth 5)
    }
    $appliedXml = '<?xml version="1.0" encoding="utf-8"?><Settings><WheelInputEnabled>true</WheelInputEnabled><ForceFeedbackEnabled>false</ForceFeedbackEnabled><Strength>50</Strength><SharedInput>true</SharedInput><TransmissionMode>Sequential</TransmissionMode></Settings>'
    $before = $staged.files.$settingsPath
    $evidence = Join-Path $root 'cq-a'
    Apply-Fake $copy $before $evidence $appliedXml
    & $confirm -Result $result -ApplyEvidence $evidence | Out-Null
    $confirmed = Get-Content (Join-Path $result 'staged.json') -Raw | ConvertFrom-Json
    Check ($confirmed.applyPending -eq $false -and $confirmed.files.$settingsPath -eq (Hash (Join-Path $copy $settingsPath)) -and $confirmed.files.$controlsPath -eq (Hash (Join-Path $copy $controlsPath)) -and $confirmed.apply.receiptSha256) 'the Apply''s writes are pinned'
    Check ($confirmed.files.'Mods/ArtOfSimRally/dbce_shim.dll' -eq $staged.files.'Mods/ArtOfSimRally/dbce_shim.dll') 'package pins kept'
    Refused { & $confirm -Result $result -ApplyEvidence $evidence } 'not awaiting an Apply'

    # Confirmation refusals, each on a fresh staging.
    function Fresh([string]$name) { $c = New-Copy $name; $r = Join-Path $root "staged-$name"; $a = Args $c; & $prepare @a -Result $r -AwaitApply -ForceFeedbackOff | Out-Null
        [pscustomobject]@{ copy = $c; result = $r; settings = (Get-Content (Join-Path $r 'staged.json') -Raw | ConvertFrom-Json).files.$settingsPath } }
    $x = Fresh 'h'; Apply-Fake $x.copy $x.settings (Join-Path $root 'cq-h') $appliedXml -install (Join-Path $root 'elsewhere')
    Refused { & $confirm -Result $x.result -ApplyEvidence (Join-Path $root 'cq-h') } 'another install'
    $x = Fresh 'i'; Apply-Fake $x.copy $x.settings (Join-Path $root 'cq-i') $appliedXml -status 'prepared'
    Refused { & $confirm -Result $x.result -ApplyEvidence (Join-Path $root 'cq-i') } 'not a completed Art Apply'
    $x = Fresh 'j'; Apply-Fake $x.copy ('F' * 64) (Join-Path $root 'cq-j') $appliedXml
    Refused { & $confirm -Result $x.result -ApplyEvidence (Join-Path $root 'cq-j') } 'staged settings'
    $x = Fresh 'k'; Apply-Fake $x.copy $x.settings (Join-Path $root 'cq-k') $appliedXml; Write-Bytes (Join-Path $x.copy $controlsPath) 'edited after'
    Refused { & $confirm -Result $x.result -ApplyEvidence (Join-Path $root 'cq-k') } 'changed since the Apply'
    $x = Fresh 'l'; Apply-Fake $x.copy $x.settings (Join-Path $root 'cq-l') $appliedXml.Replace('<ForceFeedbackEnabled>false', '<ForceFeedbackEnabled>true')
    Refused { & $confirm -Result $x.result -ApplyEvidence (Join-Path $root 'cq-l') } 'force feedback back on'
    $x = Fresh 'm'; Apply-Fake $x.copy $x.settings (Join-Path $root 'cq-m') $appliedXml.Replace('<SharedInput>true', '<SharedInput>false')
    Refused { & $confirm -Result $x.result -ApplyEvidence (Join-Path $root 'cq-m') } 'do not select SharedInput'

    # The runner's shared-car request and its summary (functions read from the script, run without any launch).
    $ast = [Management.Automation.Language.Parser]::ParseFile($invoke, [ref]$null, [ref]$null)
    foreach ($name in 'New-CaptureRequest', 'Get-SharedCarSummary') {
        $def = @($ast.FindAll({ param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -eq $name }, $true))
        Check ($def.Count -eq 1) "one $name in the runner"
        . ([scriptblock]::Create($def[0].Extent.Text))
    }
    $until = [DateTime]::new(2026, 10, 12, 20, 0, 0, [DateTimeKind]::Utc)
    $shared = New-CaptureRequest 'C:\t' 'C:\route' $true 30 $until
    $nativeRequest = New-CaptureRequest 'C:\t' 'C:\route' $false 12 $until
    Check (($shared -join '|') -ceq 'mode=record|tape=C:\t|captureDriver=C:\route|carInput=shared|holdForShared=1|captureSeconds=30|muteOutputs=1|expiresUtc=2026-10-12T20:00:00.0000000Z') 'shared-car request'
    Check (($nativeRequest -join '|') -ceq 'mode=record|tape=C:\t|captureDriver=C:\route|carInput=native|captureSeconds=12|muteOutputs=1|expiresUtc=2026-10-12T20:00:00.0000000Z') 'native request unchanged'
    $cap = Join-Path $root 'capture'
    Write-Bytes (Join-Path $cap 'shared.tape') "h`n1`n2`n3"; Write-Bytes (Join-Path $cap 'car.tape') "h`n1`n2"; Write-Bytes (Join-Path $cap 'shifts.tape') "h`n1"
    Write-Bytes (Join-Path $cap 'events.log') "segment 3 Japan6|UNDERWAY`n"; Write-Bytes (Join-Path $cap 'end.txt') "captured`n"
    $sum = Get-SharedCarSummary $cap
    Check ($sum.sharedTapeRows -eq 3 -and $sum.carTapeRows -eq 2 -and $sum.shiftRows -eq 1 -and $sum.underway -and $sum.end -eq 'captured' -and !$sum.incomplete) 'shared-car summary'
    Check ((Get-SharedCarSummary (Join-Path $root 'no-capture')).underway -eq $false) 'an empty capture is not underway'
    Refused { & $invoke -Result $result -SharedCar -PreflightOnly } 'give -RouteSource'
} finally { Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue }
"PASS: $checks shared-copy staging, Apply confirmation and shared-car request checks (fake packages and copies only)."
