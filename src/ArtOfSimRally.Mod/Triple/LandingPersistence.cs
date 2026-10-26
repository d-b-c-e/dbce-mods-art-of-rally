using System;
using System.IO;

namespace ArtOfSimRally.Mod.Triple;

// Shared by the native host and offline checks; delegates are the Unity/API boundary.
internal sealed class LandingPersistence
{
    private readonly Func<string> _inspect;
    private readonly string _initialBlock;
    internal LandingPersistence(Func<string> inspect) { _inspect = inspect; _initialBlock = Inspect(); }
    internal string Block => _initialBlock ?? Inspect();
    private string Inspect()
    {
        try { return _inspect(); }
        catch { return "launch classification unavailable"; }
    }
    internal static bool MarkerPresent(string path, Func<string, FileAttributes> attributes)
    {
        if (string.IsNullOrEmpty(path)) throw new IOException("Marker path unavailable");
        try { attributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
        // Access, invalid-path and other I/O errors must reach classification's catch.
    }
    internal bool Save(int? expected, int index, Func<int?> read, Action<int> set,
        Action remove, Action flush, Action<string> log)
    {
        var attempted = false;
        try
        {
            var blocked = Block;
            if (blocked != null) { log("Window landing: start monitor write rejected: " + blocked + "."); return false; }
            if (read() != expected) { log("Window landing: start monitor write rejected: preference changed."); return false; }
            attempted = true;
            set(index);
            flush();
            return true;
        }
        catch (Exception failure)
        {
            log("Window landing: start monitor write failed (" + failure.GetType().Name + "); persistence unconfirmed.");
            if (attempted)
            {
                try
                {
                    if (expected.HasValue) set(expected.Value); else remove();
                    log("Window landing: previous in-memory start monitor restored; disk state unconfirmed.");
                }
                catch (Exception restore)
                {
                    log("Window landing: start monitor restore failed (" + restore.GetType().Name + "); memory/disk state unconfirmed.");
                }
            }
            return false;
        }
    }
}
