using System;
using System.IO;

namespace ArtOfSimRally.Mod
{
    internal static class LogFiles
    {
        // The native log only ever appends. Before 0.2.6 it traced every force
        // command, and one T300 user's file had reached 90 MB (2026-10-05).
        // Called before the toolkit is pointed at the file; keeps one previous log.
        internal const long RotateBytes = 8L * 1024 * 1024;
        internal static bool RotateIfLarge(string path, long limit = RotateBytes)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= limit) return false;
                string previous = Path.ChangeExtension(path, ".previous.log");
                if (File.Exists(previous)) File.Delete(previous);
                File.Move(path, previous);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
