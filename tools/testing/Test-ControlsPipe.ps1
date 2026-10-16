#Requires -Version 7.0
# Fake named-pipe peer only. No game, native DLL or hardware is opened.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ControlsPipe.ps1')
if(!('ControlsPipeAuthFake' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
public static class ControlsPipeAuthFake {
    // Counts every byte a client sends before it closes: a refused client must send none.
    public static async Task<int> Received(NamedPipeServerStream server) {
        using (server) {
            await server.WaitForConnectionAsync();
            var buffer=new byte[512]; int total=0, read;
            while((read=await server.ReadAsync(buffer,0,buffer.Length))>0) total+=read;
            return total;
        }
    }
    public static async Task<string> Reply(NamedPipeServerStream server,byte[] bytes,int delay,int chunk) {
        using var owned=server;
        await server.WaitForConnectionAsync();
        using var reader=new StreamReader(server,Encoding.ASCII,false,1024,true);
        string command=await reader.ReadLineAsync();
        if(delay>0) await Task.Delay(delay);
        try {
            for(int i=0;i<bytes.Length;i+=chunk) {
                await server.WriteAsync(bytes,i,Math.Min(chunk,bytes.Length-i));
                await server.FlushAsync();
            }
        } catch(IOException) { } // timeout/oversize cases close the client
        return command;
    }
}
'@
}
$checks=0
function Check([bool]$value,[string]$message) { if(!$value){throw $message}; $script:checks++ }
# The fake stands in for the game: served by this process, owned by this user SID as the medium game's pipe is
# (the runner may be elevated; its token owner is then BUILTIN\Administrators, which the pipe must not need).
$identity=[Security.Principal.WindowsIdentity]::GetCurrent()
$elevated=([Security.Principal.WindowsPrincipal]$identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$self=Get-Process -Id $PID
$selfTicks=$self.StartTime.ToUniversalTime().Ticks; $selfImage=$self.MainModule.FileName
function New-FakeServer([string]$Name,[Security.Principal.SecurityIdentifier]$Owner=$identity.User) {
    $security=[IO.Pipes.PipeSecurity]::new()
    $security.AddAccessRule([IO.Pipes.PipeAccessRule]::new($identity.User,[IO.Pipes.PipeAccessRights]::FullControl,[Security.AccessControl.AccessControlType]::Allow))
    $security.SetOwner($Owner)
    return [IO.Pipes.NamedPipeServerStreamAcl]::Create($Name,[IO.Pipes.PipeDirection]::InOut,1,[IO.Pipes.PipeTransmissionMode]::Byte,[IO.Pipes.PipeOptions]::Asynchronous,4096,4096,$security)
}
function Case([byte[]]$reply,[string]$expected,[int]$delay=0,[int]$chunk=512,[int]$timeout=1000) {
    $name='ArtControlsFake.'+[guid]::NewGuid().ToString('N')
    $server=[ControlsPipeAuthFake]::Reply((New-FakeServer $name),$reply,$delay,$chunk)
    $line='CONTROLS STATUS 11111111111111111111111111111111'
    try { $actual=Invoke-ControlsPipeCommand -PipeName $name -Line $line -ServerProcessId $PID -ServerStartUtcTicks $selfTicks -ServerImage $selfImage -TimeoutMs $timeout }
    catch { $actual=$_.Exception.ToString() }
    Check ($actual.Contains($expected)) "Expected '$expected', got '$actual'"
    Check ($server.Wait(3000) -and $server.Result -ceq $line) 'Expected one exact command; no retry.'
}
# A refused server identity gets zero bytes: the check runs after Connect and before the command is written.
function Refused([string]$expected,[int]$serverPid=$PID,[long]$ticks=$selfTicks,[string]$image=$selfImage,[Security.Principal.SecurityIdentifier]$owner=$identity.User) {
    $name='ArtControlsFake.'+[guid]::NewGuid().ToString('N')
    $received=[ControlsPipeAuthFake]::Received((New-FakeServer $name $owner))
    try { Invoke-ControlsPipeCommand -PipeName $name -Line 'CONTROLS STOP 11111111111111111111111111111111' -ServerProcessId $serverPid -ServerStartUtcTicks $ticks -ServerImage $image -TimeoutMs 1000; $actual='accepted' }
    catch { $actual=$_.Exception.Message }
    Check ($actual.Contains($expected) -and $actual.Contains('nothing was sent')) "Expected refusal '$expected', got '$actual'"
    Check ($received.Wait(3000) -and $received.Result -eq 0) "A refused client sent $($received.Result) byte(s)."
}
Refused 'not the admitted game' -serverPid 4
Refused 'not the admitted game process' -ticks ($selfTicks + 1)
Refused 'not the admitted game process' -image 'C:\Games\artofrally.exe'
$ownerCase='not run (needs an elevated runner, the only token that can own a pipe as Administrators)'
if($elevated) { Refused 'not owned by the current user' -owner $identity.Owner; $ownerCase='run' }
Case ([Text.Encoding]::UTF8.GetBytes("OK armed: no force`r`n")) 'armed: no force' -chunk 1
Case ([Text.Encoding]::UTF8.GetBytes("ERROR refused nonce`n")) 'Probe refused: ERROR refused nonce'
Case ([Text.Encoding]::UTF8.GetBytes('OK incomplete')) 'disconnected before a complete reply'
Case ([byte[]]@(79,75,32,255,10)) 'Unable to translate bytes'
Case ([Text.Encoding]::UTF8.GetBytes(('x'*4097)+"`n")) 'Oversize probe reply'
Case ([Text.Encoding]::UTF8.GetBytes("OK too late`n")) 'outcome is unknown' -delay 500 -timeout 150
try { Invoke-ControlsPipeCommand -PipeName 'unused' -Line "CONTROLS STATUS x`nCONTROLS RAW x" -ServerProcessId $PID -ServerStartUtcTicks $selfTicks -ServerImage $selfImage; throw 'accepted multiline' }
catch { Check ($_.Exception.Message -eq 'Invalid single-line controls command.') 'Multiline command was not refused locally.' }
try {
    & (Join-Path $PSScriptRoot 'Send-ControlsCommand.ps1') -Command Status -Nonce ('1'*32) -GameProcessId $PID -ExpectedStartTimeUtc (Get-Process -Id $PID).StartTime.ToUniversalTime()
    throw 'accepted other process'
} catch { Check ($_.Exception.Message -eq 'Game process identity changed; no further commands may be sent.') 'Unrelated process was not refused.' }
"PASS: $checks fake transport and identity checks (client elevated: $elevated; wrong-owner case $ownerCase); no game/device/native calls."
