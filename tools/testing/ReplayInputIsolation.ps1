# A replay never reads input or force devices (sana 36236): the tape supplies every input, so for the replay
# process the mod's shared profile, its direct wheel and shifter readers, its device shortcuts (settings, stop
# force, camera) and force feedback are all off. Without these, turning SharedInput off would only reopen the
# mod's own direct wheel path. Only the game's applied Settings.xml changes: the tape's recorded copy is never
# edited, and the owner's own settings are restored after replay as before. Mod-internal shortcuts are not
# replayed and are inert during replay.
$ReplayInputFlags = [ordered]@{ SharedInput = 'false'; WheelInputEnabled = 'false'; ShifterEnabled = 'false'; ForceFeedbackEnabled = 'false' }
$ReplayInputShortcuts = @('SettingsButtonBinding', 'StopFfbButtonBinding')

function Set-ReplayInputIsolation([string]$SettingsPath, [string]$Evidence) {
    $record = [ordered]@{ schema = 1; settings = $SettingsPath }
    $evidenceFile = Join-Path $Evidence 'replay-input.json'
    if (-not (Test-Path -LiteralPath $SettingsPath)) {
        # No mod settings: every flag takes the mod's default. ForceFeedbackEnabled defaults on, so write one.
        $record.before = 'absent'
        Set-Content -LiteralPath $SettingsPath -Value "<?xml version=`"1.0`" encoding=`"utf-8`"?>`r`n<Settings />" -Encoding UTF8
    } else { $record.before = (Get-FileHash -LiteralPath $SettingsPath -Algorithm SHA256).Hash }
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $reader = [System.Xml.XmlReader]::Create($SettingsPath, (New-Object System.Xml.XmlReaderSettings -Property @{ DtdProcessing = 'Prohibit'; XmlResolver = $null }))
    try { $xml.Load($reader) } finally { $reader.Dispose() }
    $root = $xml.DocumentElement
    if ($null -eq $root -or $root.LocalName -ne 'Settings') { throw 'Unrecognised Art settings root; replay input isolation not applied.' }
    function Element([string]$Name) {
        $found = @($root.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.LocalName -eq $Name })
        if ($found.Count -gt 1) { throw "Ambiguous Art setting $Name; replay input isolation not applied." }
        if ($found.Count -eq 1) { return $found[0] }
        return $root.AppendChild($xml.CreateElement($Name))
    }
    $recorded = [ordered]@{}
    foreach ($name in $ReplayInputFlags.Keys) {
        $node = Element $name
        $recorded[$name] = if ($node.IsEmpty -and $node.InnerText -eq '' -and -not $node.HasChildNodes) { '(default)' } else { $node.InnerText.Trim() }
        $node.InnerText = $ReplayInputFlags[$name]
    }
    foreach ($name in $ReplayInputShortcuts) {
        $node = Element $name
        $recorded[$name] = if ($node.InnerText.Trim()) { 'bound' } else { 'unbound' }
        $node.InnerText = ''
    }
    $cameras = @($root.ChildNodes | Where-Object { $_.NodeType -eq 'Element' -and $_.LocalName -eq 'CameraButtonBindings' })
    if ($cameras.Count -gt 1) { throw 'Ambiguous Art setting CameraButtonBindings; replay input isolation not applied.' }
    $bound = 0
    if ($cameras.Count -eq 1) { foreach ($item in @($cameras[0].ChildNodes | Where-Object { $_.NodeType -eq 'Element' })) { if ($item.InnerText.Trim()) { $bound++ }; $item.InnerText = '' } }
    $recorded.CameraButtonBindings = "$bound bound"
    $xml.Save($SettingsPath)
    $record.recorded = $recorded
    $record.applied = [ordered]@{} + $ReplayInputFlags
    foreach ($name in $ReplayInputShortcuts) { $record.applied[$name] = 'unbound' }
    $record.applied.CameraButtonBindings = '0 bound'
    $record.after = (Get-FileHash -LiteralPath $SettingsPath -Algorithm SHA256).Hash
    $record | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $evidenceFile -Encoding UTF8
}
