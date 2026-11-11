# A replay never sends force to the wheel (sana 36236): the tape supplies every input, so force feedback is off for the
# replay process. Only the game's applied Settings.xml changes: the tape's recorded copy is never edited, and the
# owner's own settings are restored after replay as before. Since 0.4.6 the mod reads controls only through the shared
# profile (Wheelkit's store, or dbce-controls.ini beside the mod), which Settings.xml cannot switch off. The replay car
# still gets only taped input (the recorder's GetInput postfix runs last); live wheel buttons could reach menus.
$ReplayInputFlags = [ordered]@{ ForceFeedbackEnabled = 'false' }

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
    $xml.Save($SettingsPath)
    $record.recorded = $recorded
    $record.applied = [ordered]@{} + $ReplayInputFlags
    $record.after = (Get-FileHash -LiteralPath $SettingsPath -Algorithm SHA256).Hash
    $record | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $evidenceFile -Encoding UTF8
}
