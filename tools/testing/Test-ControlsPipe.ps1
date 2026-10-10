#Requires -Version 7.0
# Fake named-pipe peer only. No game, native DLL or hardware is opened.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ControlsPipe.ps1')
if(!('ControlsPipeFake' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
public static class ControlsPipeFake {
    public static async Task<string> Reply(string name,byte[] bytes,int delay,int chunk) {
        using var server=new NamedPipeServerStream(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
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
function Case([byte[]]$reply,[string]$expected,[int]$delay=0,[int]$chunk=512,[int]$timeout=1000) {
    $name='ArtControlsFake.'+[guid]::NewGuid().ToString('N')
    $server=[ControlsPipeFake]::Reply($name,$reply,$delay,$chunk)
    $line='CONTROLS STATUS 11111111111111111111111111111111'
    try { $actual=Invoke-ControlsPipeCommand -PipeName $name -Line $line -TimeoutMs $timeout }
    catch { $actual=$_.Exception.ToString() }
    Check ($actual.Contains($expected)) "Expected '$expected', got '$actual'"
    Check ($server.Wait(3000) -and $server.Result -ceq $line) 'Expected one exact command; no retry.'
}
Case ([Text.Encoding]::UTF8.GetBytes("OK armed: no force`r`n")) 'armed: no force' -chunk 1
Case ([Text.Encoding]::UTF8.GetBytes("ERROR refused nonce`n")) 'Probe refused: ERROR refused nonce'
Case ([Text.Encoding]::UTF8.GetBytes('OK incomplete')) 'disconnected before a complete reply'
Case ([byte[]]@(79,75,32,255,10)) 'Unable to translate bytes'
Case ([Text.Encoding]::UTF8.GetBytes(('x'*4097)+"`n")) 'Oversize probe reply'
Case ([Text.Encoding]::UTF8.GetBytes("OK too late`n")) 'outcome is unknown' -delay 500 -timeout 150
try { Invoke-ControlsPipeCommand -PipeName 'unused' -Line "CONTROLS STATUS x`nCONTROLS RAW x"; throw 'accepted multiline' }
catch { Check ($_.Exception.Message -eq 'Invalid single-line controls command.') 'Multiline command was not refused locally.' }
try {
    & (Join-Path $PSScriptRoot 'Send-ControlsCommand.ps1') -Command Status -Nonce ('1'*32) -GameProcessId $PID -ExpectedStartTimeUtc (Get-Process -Id $PID).StartTime.ToUniversalTime()
    throw 'accepted other process'
} catch { Check ($_.Exception.Message -eq 'Game process identity changed; no further commands may be sent.') 'Unrelated process was not refused.' }
"PASS: $checks fake transport and identity checks; no game/device/native calls."
