# Steam client launch context for the disposable copied-game probe. Read-only except the broker
# file it is asked to write. SteamAPI_Init fails when no Steam client is running (the copy's
# steam_appid.txt suppresses SteamAPI_RestartAppIfNecessary) or when the game runs at a different
# access level from Steam (Valve's SteamAPI_Init contract). A direct child of an elevated runner
# inherits High integrity; the owner's Steam normally runs at Medium. Never starts Steam.
if (-not ('DbceProcessIntegrity' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
public static class DbceProcessIntegrity
{
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int cls, IntPtr info, int length, out int returned);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    // { mandatory label RID (0x2000 Medium, 0x3000 High), TokenElevation }
    public static int[] Read(int pid)
    {
        IntPtr process = OpenProcess(0x1000, false, pid); // PROCESS_QUERY_LIMITED_INFORMATION
        if (process == IntPtr.Zero) throw new Win32Exception();
        IntPtr token = IntPtr.Zero, label = IntPtr.Zero, elevation = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(process, 0x0008, out token)) throw new Win32Exception(); // TOKEN_QUERY
            int length; GetTokenInformation(token, 25, IntPtr.Zero, 0, out length); // TokenIntegrityLevel
            label = Marshal.AllocHGlobal(length);
            if (!GetTokenInformation(token, 25, label, length, out length)) throw new Win32Exception();
            IntPtr sid = Marshal.ReadIntPtr(label);
            int rid = Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(Marshal.ReadByte(GetSidSubAuthorityCount(sid)) - 1)));
            elevation = Marshal.AllocHGlobal(4);
            if (!GetTokenInformation(token, 20, elevation, 4, out length)) throw new Win32Exception(); // TokenElevation
            return new[] { rid, Marshal.ReadInt32(elevation) };
        }
        finally
        {
            if (label != IntPtr.Zero) Marshal.FreeHGlobal(label);
            if (elevation != IntPtr.Zero) Marshal.FreeHGlobal(elevation);
            if (token != IntPtr.Zero) CloseHandle(token);
            CloseHandle(process);
        }
    }
}
'@
}
function Get-ProcessIntegrity([int]$ProcessId) {
    $value = [DbceProcessIntegrity]::Read($ProcessId)
    [pscustomobject]@{ pid = $ProcessId; integrity = ('0x{0:X}' -f $value[0]); elevated = [bool]$value[1] }
}
# Decision: 'direct' when Steam runs at the launcher's level; 'broker' when Steam is Medium and the
# launcher High (launch through the user's shell); otherwise 'refuse' with the observed reason.
function Get-SteamLaunchContext {
    $launcher = Get-ProcessIntegrity $PID
    $steam = @(foreach ($p in @(Get-Process -Name steam -ErrorAction SilentlyContinue)) {
        try { $i = Get-ProcessIntegrity $p.Id; [pscustomobject]@{ pid = $p.Id; startUtc = $p.StartTime.ToUniversalTime().ToString('o'); integrity = $i.integrity; elevated = $i.elevated } }
        catch { [pscustomobject]@{ pid = $p.Id; startUtc = $null; integrity = 'unreadable'; elevated = $null } }
    })
    $decision = 'refuse'
    $reason = if ($steam.Count -eq 0) { 'Steam client is not running; the copy cannot start it, so SteamAPI_Init would fail. Do not start Steam elevated to fit the copy.' }
        elseif ($steam.Count -gt 1) { 'More than one steam process; launch context is ambiguous.' }
        elseif ($steam[0].integrity -eq 'unreadable') { 'Steam token integrity is unreadable.' }
        elseif ($steam[0].integrity -eq $launcher.integrity) { $decision = 'direct'; 'Steam runs at the launcher integrity level.' }
        elseif ($steam[0].integrity -eq '0x2000' -and $launcher.integrity -eq '0x3000') { $decision = 'broker'; 'Steam runs at Medium; launch the copy through the user shell at Medium.' }
        else { "Steam integrity $($steam[0].integrity) cannot be matched from launcher $($launcher.integrity)." }
    [pscustomobject]@{ schema = 1; utc = [DateTime]::UtcNow.ToString('o'); launcher = $launcher; steam = $steam; decision = $decision; reason = $reason }
}
# Writes a cmd file whose SET values live only in that cmd process and the game it starts; no
# persistent or parent environment changes. Values are restricted so cmd parsing cannot alter them.
function New-ChildEnvironmentBroker([string]$Path, [string]$Executable, [string]$WorkingDirectory, [System.Collections.IDictionary]$Environment) {
    $safe = '^[A-Za-z0-9 _.,:\\()+=@#$~{}\[\]-]+$'
    foreach ($value in @($Path, $Executable, $WorkingDirectory) + @($Environment.Values)) {
        if ([string]$value -cnotmatch $safe) { throw "Broker value contains characters cmd could reinterpret: $value" }
    }
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('@echo off'); $lines.Add('setlocal DisableDelayedExpansion')
    foreach ($name in $Environment.Keys) {
        if ([string]$name -cnotmatch '^[A-Za-z_][A-Za-z0-9_]*$') { throw "Unsupported broker variable name: $name" }
        $lines.Add('set "' + $name + '=' + $Environment[$name] + '"')
    }
    $lines.Add('cd /d "' + $WorkingDirectory + '"')
    $lines.Add('start "" /D "' + $WorkingDirectory + '" "' + $Executable + '"')
    [IO.File]::WriteAllText($Path, ($lines -join "`r`n") + "`r`n", [Text.Encoding]::ASCII)
    (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}
# Explorer opens the file in the already-running user shell, so cmd and its child run at the
# user's normal (Medium) integrity even when this runner is elevated.
function Start-MediumBroker([string]$Path) {
    Start-Process -FilePath (Join-Path $env:SystemRoot 'explorer.exe') -ArgumentList ('"' + $Path + '"')
}
