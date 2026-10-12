#Requires -Version 7.0
# Developer check of the Medium broker: no game, no Steam, no rig. Briefly opens a console through
# the user shell. Proves the child runs at Medium with only the broker's cmd-local variables.
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SteamLaunchContext.ps1')
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('art-steam-context-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture | Out-Null
$script:checks = 0
function Check([bool]$Condition, [string]$Message) { $script:checks++; if (-not $Condition) { throw $Message } }
try {
    $out = Join-Path $fixture 'child.json'; $helper = Join-Path $PSScriptRoot 'SteamLaunchContext.ps1'
    $probe = "`$i = & { . '$helper'; Get-ProcessIntegrity `$PID }; [pscustomobject]@{ integrity = `$i.integrity; elevated = `$i.elevated; mute = `$env:DBCE_FFB_MUTE; marker = `$env:DBCE_BROKER_TEST; cwd = (Get-Location).Path } | ConvertTo-Json | Set-Content -LiteralPath '$out'"
    "@echo off`r`npwsh -NoProfile -Command `"$probe`"`r`n" | Set-Content -LiteralPath (Join-Path $fixture 'target.cmd') -Encoding ascii -NoNewline
    $broker = Join-Path $fixture 'broker.cmd'
    $hash = New-ChildEnvironmentBroker -Path $broker -Executable (Join-Path $fixture 'target.cmd') -WorkingDirectory $fixture `
        -Environment ([ordered]@{ DBCE_FFB_MUTE = '1'; DBCE_BROKER_TEST = 'child-only' })
    Check ($hash -eq (Get-FileHash -LiteralPath $broker).Hash) 'Broker hash is not the written file.'
    foreach ($bad in @('a%PATH%b', 'a&b', 'a"b', 'a^b', 'a!b', 'a|b', "a`r`nb")) {
        $refused = $false
        try { $null = New-ChildEnvironmentBroker -Path (Join-Path $fixture 'bad.cmd') -Executable (Join-Path $fixture 'target.cmd') -WorkingDirectory $fixture -Environment @{ X = $bad } }
        catch { $refused = $true }
        Check $refused "Broker accepted a cmd-reinterpreted value: $bad"
    }
    Start-MediumBroker $broker
    $deadline = (Get-Date).AddSeconds(30)
    while (-not (Test-Path -LiteralPath $out) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 250 }
    Start-Sleep -Milliseconds 500
    Check (Test-Path -LiteralPath $out) 'Broker child produced no output.'
    $child = Get-Content -LiteralPath $out -Raw | ConvertFrom-Json
    Check ($child.integrity -eq '0x2000' -and -not $child.elevated) "Broker child is not at Medium: $($child.integrity)"
    Check ($child.mute -eq '1' -and $child.marker -eq 'child-only') 'Broker child lacks its cmd-local variables.'
    Check ($child.cwd.TrimEnd('\') -eq $fixture.TrimEnd('\')) 'Broker child working directory differs.'
    Check (-not $env:DBCE_BROKER_TEST -and -not [Environment]::GetEnvironmentVariable('DBCE_BROKER_TEST', 'User') -and
        -not [Environment]::GetEnvironmentVariable('DBCE_BROKER_TEST', 'Machine')) 'Broker variable leaked beyond the child.'
    $context = Get-SteamLaunchContext
    Check ($context.decision -in @('direct', 'broker', 'refuse') -and $context.launcher.integrity -match '^0x[0-9A-F]+$') 'Steam context is malformed.'
    Check (@($context.steam).Count -ne 0 -or $context.decision -eq 'refuse') 'Absent Steam must refuse.'
    "Steam launch context: $script:checks checks passed (launcher $($context.launcher.integrity), decision $($context.decision))."
}
finally { Remove-Item -LiteralPath $fixture -Recurse -Force -ErrorAction SilentlyContinue }
