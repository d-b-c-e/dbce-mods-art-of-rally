namespace CrashFeel;

// One CSV per session, flushed per line so a crash or force-close keeps it.
sealed class SessionLog : IDisposable
{
    readonly StreamWriter _writer;
    public string Path { get; }

    public SessionLog()
    {
        string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ArtOfSimRally", "crash-feel");
        Directory.CreateDirectory(dir);
        Path = System.IO.Path.Combine(dir, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
        _writer = new StreamWriter(Path, false) { AutoFlush = true };
        _writer.WriteLine("local_time,event,candidate,strength_pct,direction,load,detail");
    }

    public void Write(string evt, string candidate = "", string strength = "", string direction = "", string load = "", string detail = "")
        => _writer.WriteLine(string.Join(",", DateTime.Now.ToString("HH:mm:ss.fff"), evt, Quote(candidate), strength, direction, load, Quote(detail)));

    static string Quote(string value) => value.IndexOfAny(new[] { ',', '"', '\n' }) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";

    public void Dispose() => _writer.Dispose();
}
