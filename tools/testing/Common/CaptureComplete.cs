using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace ArtOfSimRally.Testing
{
    /// <summary>Captures end with complete.txt, written last after every stream has closed (the toolkit's
    /// TrajectoryRecorder convention). It lists the files; content hashes live in manifest.xml.</summary>
    internal static class CaptureComplete
    {
        public const string FileName = "complete.txt";

        public static void Write(string directory, IEnumerable<string> files)
        {
            var names = files.ToArray();
            foreach (var name in names)
                if (!File.Exists(Path.Combine(directory, name))) throw new InvalidDataException("Missing capture file: " + name);
            string temp = Path.Combine(directory, FileName + ".tmp");
            File.WriteAllText(temp, "files=" + string.Join(",", names) + "\n", new UTF8Encoding(false));
            File.Move(temp, Path.Combine(directory, FileName));
        }

        public static void Require(string directory, params string[] files)
        {
            string marker = Path.Combine(directory, FileName);
            if (!File.Exists(marker)) throw new InvalidDataException("Capture is incomplete: no " + FileName);
            var listed = new HashSet<string>(File.ReadAllLines(marker).Where(l => l.StartsWith("files="))
                .SelectMany(l => l.Substring(6).Split(',')), System.StringComparer.OrdinalIgnoreCase);
            foreach (var name in files)
                if (!listed.Contains(name) || !File.Exists(Path.Combine(directory, name))) throw new InvalidDataException("Capture is missing " + name);
        }
    }
}
