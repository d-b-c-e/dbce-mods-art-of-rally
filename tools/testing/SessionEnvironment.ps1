# Launch-scoped game state. Keep this adapter's paths explicit; never restore arbitrary paths from a tape.
# The game's PlayerPrefs key is saved and restored value by value as raw bytes (toolkit RegistryState.ps1, which never
# deletes the key). reg.exe export was observed silently omitting Unity PlayerPrefs values, so reg.exe is not used for
# this key; environment backups holding playerprefs.reg or playerprefs-raw.json (schema 3 and older) are refused.
$registryState = Join-Path $PSScriptRoot '..\..\..\_toolkits\dbce-wheel-mod-toolkit\tools\powershell\RegistryState.ps1'
if (-not (Test-Path -LiteralPath $registryState)) {
    throw "Toolkit file not found: $registryState. Clone dbce-wheel-mod-toolkit to E:\Source\game-modding\_toolkits\dbce-wheel-mod-toolkit."
}
. $registryState
$script:PrefsKey = 'HKCU\Software\Funselektor Labs\art of rally'   # the offline test points this at a scratch key
$script:PrefsDisplayPattern = '^(Screenmanager |UnitySelectMonitor_|SETTINGS_(RESOLUTION_|FULLSCREEN_|VSYNC_|FRAMERATE_CAP_))'
function Get-SessionPrefsPsKey { $script:PrefsKey -replace '^HKCU\\', 'HKCU:\' }

# Single-value access for the display preferences the presentation restore writes; the key comes from $script:PrefsKey.
function Initialize-SessionRegistry {
    if (-not ('ArtSessionRegistry' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class ArtSessionRegistry {
    public static string Path = @"Software\Funselektor Labs\art of rally";
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
    [ArtSessionRegistry]::Path = $script:PrefsKey -replace '^HKCU\\', ''
}
function Get-SessionRegistryValue([string]$Name) { Initialize-SessionRegistry; [ArtSessionRegistry]::Read($Name) }
function Set-SessionRegistryValue($Value) { Initialize-SessionRegistry; [ArtSessionRegistry]::Write($Value.name, $Value.type, $Value.data) }

# The verified registry snapshot of an environment backup (or a tape's environment-at-start): the right format, the
# right key, unchanged since it was saved. Returns its path and its values as { name; type; data (base64) } objects.
function Get-SessionPrefs([string]$Directory, $State) {
    if ($State.schema -ne 4 -or -not $State.prefsHash) { throw 'Old environment backup format (reg export); it is refused, never imported. Save a new one.' }
    $path = Join-Path $Directory 'playerprefs.json'
    if ((Get-FileHash -LiteralPath $path).Hash -ne $State.prefsHash) { throw 'Preference backup changed.' }
    $doc = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable -Depth 64
    if ($doc.schema -ne 'dbce.registry-state@1' -or $doc.key -cne $script:PrefsKey) { throw 'Unexpected preference key.' }
    $values = @()
    if ($doc.tree) {
        foreach ($name in $doc.tree.values.Keys) {
            $null = [Convert]::FromBase64String($doc.tree.values[$name].data)
            if ($name.Contains([char]0)) { throw 'Invalid preference name.' }
            $values += [pscustomobject]@{ name = $name; type = [uint32]$doc.tree.values[$name].type; data = $doc.tree.values[$name].data }
        }
    }
    [pscustomobject]@{ path = $path; values = $values }
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
    $state = @{ schema = 4 }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        $exists = Test-Path -LiteralPath $entry.Value -PathType Leaf
        $hash = $null
        if ($exists) {
            Copy-Item -LiteralPath $entry.Value -Destination (Join-Path $Directory $entry.Key)
            $hash = (Get-FileHash -LiteralPath (Join-Path $Directory $entry.Key)).Hash
        }
        $state[$entry.Key] = @{ exists = $exists; sha256 = $hash }
    }
    # Save-RegistryState reads the file back and compares it with the live key before it returns.
    $prefs = Join-Path $Directory 'playerprefs.json'
    Save-RegistryState -Key $script:PrefsKey -Path $prefs | Write-Host
    $state['prefsHash'] = (Get-FileHash -LiteralPath $prefs).Hash
    $state | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $Directory 'state.json') -Encoding UTF8
}
function Restore-SessionEnvironment([string]$Directory, [switch]$NoReceipt) {
    Assert-GameClosed
    $state = Get-Content -LiteralPath (Join-Path $Directory 'state.json') -Raw | ConvertFrom-Json
    # Validate every backup before touching the live environment.
    $prefs = Get-SessionPrefs $Directory $state
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        $saved = $state.($entry.Key)
        if ($null -eq $saved) { throw 'Incomplete environment backup.' }
        if ($saved.exists -and (Get-FileHash -LiteralPath (Join-Path $Directory $entry.Key)).Hash -ne $saved.sha256) { throw "Backup changed: $($entry.Key)" }
    }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        if ($state.($entry.Key).exists) { Copy-Item -LiteralPath (Join-Path $Directory $entry.Key) -Destination $entry.Value -Force }
        elseif (Test-Path -LiteralPath $entry.Value) { Remove-Item -LiteralPath $entry.Value }
    }
    # Value by value; values the run added are removed, the key is never deleted. Throws unless the key then equals the snapshot.
    Restore-RegistryState -Path $prefs.path | Write-Host
    if (-not (Compare-RegistryState -Path $prefs.path)) { throw 'Preference restore verification failed.' }
    foreach ($entry in (Get-SessionFiles).GetEnumerator()) {
        if ($state.($entry.Key).exists -and (Get-FileHash -LiteralPath $entry.Value).Hash -ne $state.($entry.Key).sha256) { throw "Restore verification failed: $($entry.Key)" }
    }
    if (-not $NoReceipt) { Set-Content -LiteralPath (Join-Path $Directory 'restored.txt') -Value ([DateTime]::UtcNow.ToString('o')) }
}

# Stage/car preferences belong to the tape; presentation belongs to this rig.
# Write back only the display preferences from the verified owner backup.
function Restore-SessionPresentation([string]$Directory) {
    Assert-GameClosed
    $state = Get-Content -LiteralPath (Join-Path $Directory 'state.json') -Raw | ConvertFrom-Json
    $display = @((Get-SessionPrefs $Directory $state).values | Where-Object { $_.name -match $script:PrefsDisplayPattern })
    $triple = (Get-SessionFiles).triplesettings
    if ($state.triplesettings.exists -and (Get-FileHash -LiteralPath (Join-Path $Directory 'triplesettings')).Hash -ne $state.triplesettings.sha256) { throw 'Triple settings backup changed.' }
    $key = Get-SessionPrefsPsKey
    if (-not (Test-Path -LiteralPath $key)) { New-Item -Path $key -Force | Out-Null }
    foreach ($name in (Get-Item -LiteralPath $key).GetValueNames()) {
        if ($name -match $script:PrefsDisplayPattern -and $name -notin $display.name) { Remove-ItemProperty -LiteralPath $key -Name $name }
    }
    foreach ($value in $display) { Set-SessionRegistryValue $value }
    foreach ($value in $display) {
        $actual = Get-SessionRegistryValue $value.name
        if ($actual.type -ne $value.type -or $actual.data -cne $value.data) { throw 'Display preference restore verification failed.' }
    }
    if ($null -ne $state.triplesettings) {
        if ($state.triplesettings.exists) { Copy-Item -LiteralPath (Join-Path $Directory 'triplesettings') -Destination $triple -Force }
        elseif (Test-Path -LiteralPath $triple) { Remove-Item -LiteralPath $triple }
    }
}
