# Current-user developer pipe transport. No game launch or native/device access.
if(!('ArtControlsPipe.Native' -as [type])) {
    Add-Type -Namespace ArtControlsPipe -Name Native -MemberDefinition '[DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetNamedPipeServerProcessId(IntPtr pipe, out uint serverProcessId);'
}
# The pipe answers only for the admitted game, checked after Connect and before the command is written. That
# replaces PipeOptions.CurrentUserOnly, which compares the pipe owner with the client token's owner. For an elevated
# runner that is BUILTIN\Administrators, never the medium game's user (rc26-targeted-uat-01 refused it so).
function Assert-ControlsPipeServer([IO.Pipes.NamedPipeClientStream]$Pipe,[int]$ProcessId,[long]$StartUtcTicks,[string]$Image) {
    $user=[Security.Principal.WindowsIdentity]::GetCurrent().User
    $owner=[IO.Pipes.PipesAclExtensions]::GetAccessControl($Pipe).GetOwner([Security.Principal.SecurityIdentifier])
    if($null -eq $owner -or $owner -ne $user) { throw 'Probe pipe is not owned by the current user; nothing was sent.' }
    [uint32]$server=0
    if(![ArtControlsPipe.Native]::GetNamedPipeServerProcessId($Pipe.SafePipeHandle.DangerousGetHandle(),[ref]$server)) { throw 'Probe pipe server process is unknown; nothing was sent.' }
    if($server -ne $ProcessId) { throw "Probe pipe is served by process $server, not the admitted game; nothing was sent." }
    $process=Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    try {
        if(!$process -or $process.HasExited -or $process.StartTime.ToUniversalTime().Ticks -ne $StartUtcTicks -or
           ![string]::Equals($process.MainModule.FileName,$Image,[StringComparison]::OrdinalIgnoreCase)) {
            throw 'Probe pipe server is not the admitted game process; nothing was sent.'
        }
    } finally { if($process) { $process.Dispose() } }
}
function Invoke-ControlsPipeCommand {
    param([Parameter(Mandatory)][string]$PipeName,
          [Parameter(Mandatory)][string]$Line,
          [Parameter(Mandatory)][ValidateRange(1,2147483647)][int]$ServerProcessId,
          [Parameter(Mandatory)][long]$ServerStartUtcTicks,
          [Parameter(Mandatory)][string]$ServerImage,
          [ValidateRange(100,10000)][int]$TimeoutMs=7000)
    if($Line.Length -gt 1024 -or $Line.Contains("`r") -or $Line.Contains("`n")) { throw 'Invalid single-line controls command.' }
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.', $PipeName, [IO.Pipes.PipeDirection]::InOut, [IO.Pipes.PipeOptions]::Asynchronous)
    $clock=[Diagnostics.Stopwatch]::StartNew()
    function Remaining {
        $left=$TimeoutMs-[int]$clock.ElapsedMilliseconds
        if($left -le 0) { throw 'Probe reply timed out; outcome is unknown. Do not automatically retry raw input.' }
        return $left
    }
    try {
        $pipe.Connect((Remaining))
        Assert-ControlsPipeServer $pipe $ServerProcessId $ServerStartUtcTicks $ServerImage
        $bytes=[Text.Encoding]::ASCII.GetBytes($Line+"`n")
        $write=$pipe.WriteAsync($bytes,0,$bytes.Length)
        if(!$write.Wait((Remaining))) { throw 'Probe write timed out; outcome is unknown. Do not automatically retry raw input.' }
        $buffer=[byte[]]::new(512)
        $reply=[Collections.Generic.List[byte]]::new()
        while($true) {
            $read=$pipe.ReadAsync($buffer,0,$buffer.Length)
            if(!$read.Wait((Remaining))) { throw 'Probe reply timed out; outcome is unknown. Do not automatically retry raw input.' }
            if($read.Result -eq 0) { throw 'Probe disconnected before a complete reply; outcome is unknown.' }
            for($i=0;$i -lt $read.Result;$i++) {
                if($buffer[$i] -eq 10) {
                    $text=[Text.UTF8Encoding]::new($false,$true).GetString($reply.ToArray()).TrimEnd([char]13)
                    if(!$text.StartsWith('OK ',[StringComparison]::Ordinal)) { throw "Probe refused: $text" }
                    return $text.Substring(3)
                }
                if($reply.Count -ge 4096) { throw 'Oversize probe reply; outcome is unknown.' }
                $reply.Add($buffer[$i])
            }
        }
    } finally { $pipe.Dispose() }
}
