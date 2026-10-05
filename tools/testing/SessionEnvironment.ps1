# Launch-scoped game state. Keep this adapter's paths explicit; never restore arbitrary paths from a tape.
# reg.exe export was observed silently omitting Unity PlayerPrefs values.
# Preserve exact registry bytes; the native helper can access only this game's key.
function Initialize-SessionRegistry {
    if ('ArtSessionRegistry' -as [type]) { return }
    Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class ArtSessionRegistry {
    const string Path = @"Software\Funselektor Labs\art of rally";
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegOpenKeyEx(IntPtr key,string path,int options,int access,out IntPtr result);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegQueryValueEx(IntPtr key,string name,IntPtr reserved,out uint type,byte[] data,ref uint size);
    [DllImport("advapi32.dll", CharSet=CharSet.Unicode)] static extern int RegSetValueEx(IntPtr key,string name,int reserved,uint type,byte[] data,int size);
    [DllImport("advapi32.dll")] static extern int RegCloseKey(IntPtr key);
    public sealed class Value { public string name; public uint type; public string data; }
    static IntPtr Open(int access) { IntPtr key; int error=RegOpenKeyEx(new IntPtr(unchecked((int)0x80000001)),Path,0,access,out key); if(error!=0)throw new Win32Exception(error);return key; }
    public static Value Read(string name) {
        IntPtr key=Open(1);
        try { uint size=0,type;int error=RegQueryValueEx(key,name,IntPtr.Zero,out type,null,ref size);if(error!=0)throw new Win32Exception(error);
            if(size>16777216)throw new InvalidOperationException("Oversize game preference");
            byte[] bytes=new byte[size];error=RegQueryValueEx(key,name,IntPtr.Zero,out type,bytes,ref size);if(error!=0)throw new Win32Exception(error);
            return new Value { name=name,type=type,data=Convert.ToBase64String(bytes,0,(int)size) };
        } finally { RegCloseKey(key); }
    }
    public static void Write(string name,uint type,string data) {
        if(name==null || name.IndexOf('\0')>=0)throw new ArgumentException("Invalid preference name");
        byte[] bytes=Convert.FromBase64String(data);if(bytes.Length>16777216)throw new InvalidOperationException("Oversize game preference");
        IntPtr key=Open(2);try { int error=RegSetValueEx(key,name,0,type,bytes,bytes.Length);if(error!=0)throw new Win32Exception(error); } finally { RegCloseKey(key); }
    }
}
'@
}
function Get-SessionRegistryValue([string]$Name) { Initialize-SessionRegistry; [ArtSessionRegistry]::Read($Name) }
function Set-SessionRegistryValue($Value) { Initialize-SessionRegistry; [ArtSessionRegistry]::Write($Value.name, $Value.type, $Value.data) }
function Get-SessionRegistryBackup([string]$Directory, $State) {
    if (-not $State.prefsRawHash) { return @() }
    $path = Join-Path $Directory 'playerprefs-raw.json'
    if ((Get-FileHash -LiteralPath $path).Hash -ne $State.prefsRawHash) { throw 'Raw preference backup changed.' }
    $values = @(Get-Content -LiteralPath $path -Raw | ConvertFrom-Json)
    if (@(Compare-Object @($State.prefsNames | Sort-Object) @($values.name | Sort-Object)).Count -ne 0) { throw 'Incomplete raw preference backup.' }
    foreach ($value in $values) { $null = [Convert]::FromBase64String($value.data); if ($value.name.Contains([char]0)) { throw 'Invalid preference name.' } }
    return $values
}
function Get-SessionFiles {
    @{
        customrally = $customRally
        modsettings = Join-Path $GameDir 'Mods/ArtOfSimRally/Settings.xml'
        triplesettings = Join-Path $GameDir 'Mods/ArtOfSimRally/TripleScreen.xml'
    }
}
function Save-SessionEnvironment([string]$Directory) {
    New-Item -ItemType Directory -Force -Path $Directory | Out-Null
    $state = @{ schema = 3 }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        $exists = Test-Path -LiteralPath $entry.Value -PathType Leaf
        $hash = $null
        if ($exists) {
            Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $Directory $entry.Key)
            $hash = (Get-FileHash -LiteralPath (Join-Path $Directory $entry.Key)).Hash
        }
        $state[$entry.Key] = @{ exists = $exists; sha256 = $hash }
    }
    & reg export 'HKCU\Software\Funselektor Labs\art of rally' (Join-Path $Directory 'playerprefs.reg') /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not snapshot game preferences.' }
    $state['prefsHash'] = (Get-FileHash -LiteralPath (Join-Path $Directory 'playerprefs.reg')).Hash
    $state['prefsNames'] = @((Get-Item -LiteralPath 'HKCU:\Software\Funselektor Labs\art of rally').GetValueNames())
    $raw = @($state.prefsNames | ForEach-Object { Get-SessionRegistryValue $_ })
    $rawPath = Join-Path $Directory 'playerprefs-raw.json'
    ConvertTo-Json -InputObject $raw -Depth 4 | Set-Content -LiteralPath $rawPath -Encoding UTF8
    $state['prefsRawHash'] = (Get-FileHash -LiteralPath $rawPath).Hash
    $state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Directory 'state.json') -Encoding UTF8
}
function Restore-SessionEnvironment([string]$Directory, [switch]$NoReceipt) {
    Assert-GameClosed
    $state = Get-Content -LiteralPath (Join-Path $Directory 'state.json') -Raw | ConvertFrom-Json
    $raw = @(Get-SessionRegistryBackup $Directory $state)
    if ($state.schema -eq 3 -and -not $state.prefsRawHash) { throw 'Missing raw preference backup.' }
    # Validate every backup before touching the live environment.
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        $saved = $state.($entry.Key)
        # Older sessions predate triple-setting snapshots. Leave that file alone.
        if ($null -eq $saved -and $entry.Key -eq 'triplesettings' -and $state.schema -notin @(2,3)) { continue }
        if ($null -eq $saved) { throw 'Incomplete environment backup.' }
        if ($saved.exists -and (Get-FileHash -LiteralPath (Join-Path $Directory $entry.Key)).Hash -ne $saved.sha256) { throw "Backup changed: $($entry.Key)" }
    }
    $prefs = Join-Path $Directory 'playerprefs.reg'
    if ((Get-FileHash -LiteralPath $prefs).Hash -ne $state.prefsHash) { throw 'Preference backup changed.' }
    $sections = @(Get-Content -LiteralPath $prefs | Where-Object { $_ -match '^\[' })
    if ($sections.Count -ne 1 -or $sections[0] -cne '[HKEY_CURRENT_USER\Software\Funselektor Labs\art of rally]') { throw 'Unexpected preference key.' }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        if ($null -eq $state.($entry.Key)) { continue }
        if ($state.($entry.Key).exists) { Copy-Item -LiteralPath (Join-Path $Directory $entry.Key) -Destination $entry.Value -Force }
        elseif (Test-Path -LiteralPath $entry.Value) { Remove-Item -LiteralPath $entry.Value }
    }
    $key = Get-Item -LiteralPath 'HKCU:\Software\Funselektor Labs\art of rally'
    foreach ($name in $key.GetValueNames()) {
        if ($name -notin $state.prefsNames) { Remove-ItemProperty -LiteralPath 'HKCU:\Software\Funselektor Labs\art of rally' -Name $name }
    }
    if ($state.prefsRawHash) {
        foreach ($value in $raw) { Set-SessionRegistryValue $value }
        foreach ($value in $raw) {
            $actual = Get-SessionRegistryValue $value.name
            if ($actual.type -ne $value.type -or $actual.data -cne $value.data) { throw 'Preference restore verification failed.' }
        }
    } else {
        & reg import $prefs | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not restore game preferences; backup retained.' }
    }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        if ($state.($entry.Key).exists -and (Get-FileHash -LiteralPath $entry.Value).Hash -ne $state.($entry.Key).sha256) { throw "Restore verification failed: $($entry.Key)" }
    }
    if (-not $NoReceipt) { Set-Content -LiteralPath (Join-Path $Directory 'restored.txt') -Value ([DateTime]::UtcNow.ToString('o')) }
}

# Stage/car preferences belong to the tape; presentation belongs to this rig.
# Import only explicit DWORD display preferences from the verified owner backup.
function Restore-SessionPresentation([string]$Directory) {
    Assert-GameClosed
    $state = Get-Content -LiteralPath (Join-Path $Directory 'state.json') -Raw | ConvertFrom-Json
    $raw = @(Get-SessionRegistryBackup $Directory $state)
    if ($state.schema -eq 3 -and -not $state.prefsRawHash) { throw 'Missing raw preference backup.' }
    $prefs = Join-Path $Directory 'playerprefs.reg'
    if ((Get-FileHash -LiteralPath $prefs).Hash -ne $state.prefsHash) { throw 'Preference backup changed.' }
    $lines = @(Get-Content -LiteralPath $prefs)
    $sections = @($lines | Where-Object { $_ -match '^\[' })
    if ($sections.Count -ne 1 -or $sections[0] -cne '[HKEY_CURRENT_USER\Software\Funselektor Labs\art of rally]') { throw 'Unexpected preference key.' }
    $pattern = '^(Screenmanager |UnitySelectMonitor_|SETTINGS_(RESOLUTION_|FULLSCREEN_|VSYNC_|FRAMERATE_CAP_))'
    $display = @($lines | Where-Object { $_ -match '^"([^"\\]+)"=dword:[0-9a-fA-F]{8}$' -and $Matches[1] -match $pattern })
    $names = @($display | ForEach-Object { ($_ -split '"')[1] })
    if ($state.prefsRawHash) { $names = @($raw | Where-Object { $_.name -match $pattern } | ForEach-Object { $_.name }) }
    $triple = (Get-SessionFiles).triplesettings
    if ($state.triplesettings.exists -and (Get-FileHash -LiteralPath (Join-Path $Directory 'triplesettings')).Hash -ne $state.triplesettings.sha256) { throw 'Triple settings backup changed.' }
    $key = 'HKCU:\Software\Funselektor Labs\art of rally'
    foreach ($name in (Get-Item -LiteralPath $key).GetValueNames()) {
        if ($name -match $pattern -and $name -notin $names) { Remove-ItemProperty -LiteralPath $key -Name $name }
    }
    if ($state.prefsRawHash) {
        foreach ($value in $raw | Where-Object { $_.name -match $pattern }) { Set-SessionRegistryValue $value }
    } else {
        $presentation = Join-Path $Directory 'presentation.reg'
        @('Windows Registry Editor Version 5.00','',$sections[0]) + $display | Set-Content -LiteralPath $presentation -Encoding Unicode
        & reg import $presentation | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not restore display preferences.' }
    }
    if ($null -ne $state.triplesettings) {
        if ($state.triplesettings.exists) { Copy-Item -LiteralPath (Join-Path $Directory 'triplesettings') -Destination $triple -Force }
        elseif (Test-Path -LiteralPath $triple) { Remove-Item -LiteralPath $triple }
    }
}
