# Current-user developer pipe transport. No game launch or native/device access.
function Invoke-ControlsPipeCommand {
    param([Parameter(Mandatory)][string]$PipeName,
          [Parameter(Mandatory)][string]$Line,
          [ValidateRange(100,10000)][int]$TimeoutMs=7000)
    if($Line.Length -gt 1024 -or $Line.Contains("`r") -or $Line.Contains("`n")) { throw 'Invalid single-line controls command.' }
    $pipe=[IO.Pipes.NamedPipeClientStream]::new('.', $PipeName, [IO.Pipes.PipeDirection]::InOut,
        ([IO.Pipes.PipeOptions]::Asynchronous -bor [IO.Pipes.PipeOptions]::CurrentUserOnly))
    $clock=[Diagnostics.Stopwatch]::StartNew()
    function Remaining {
        $left=$TimeoutMs-[int]$clock.ElapsedMilliseconds
        if($left -le 0) { throw 'Probe reply timed out; outcome is unknown. Do not automatically retry raw input.' }
        return $left
    }
    try {
        $pipe.Connect((Remaining))
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
