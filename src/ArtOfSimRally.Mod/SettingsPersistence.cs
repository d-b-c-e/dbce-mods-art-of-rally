using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace ArtOfSimRally.Mod
{
    internal static class SettingsPersistence
    {
        // UMM's ModSettings.Save catches and logs errors internally. Use its
        // exact XML serializer format ourselves so failure can reach SaveSettings
        // and keep the deferred retry pending. A failed replacement leaves the
        // previous Settings.xml intact instead of truncating it first.
        public static void Write(object settings, string path) =>
            Replace(path, writer => new XmlSerializer(settings.GetType()).Serialize(writer, settings));

        // Sets one top-level element and keeps the rest of the file as parsed (unknown elements, attributes, comments,
        // absent fields stay absent). Malformed XML or a foreign root throws before anything is written.
        public static void WriteElement(string path, string name, string value)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            if (File.Exists(path)) document.Load(path);
            else document.AppendChild(document.CreateElement("Settings"));
            var root = document.DocumentElement;
            if (root == null || root.Name != "Settings") throw new InvalidDataException(path + " has no <Settings> root.");
            var element = root[name];
            if (element == null)
            {
                element = document.CreateElement(name);
                root.AppendChild(element);
            }
            element.InnerText = value;
            Replace(path, writer => document.Save(writer));
        }

        private static void Replace(string path, Action<StreamWriter> write)
        {
            path = Path.GetFullPath(path);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream))
                    write(writer);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch { /* Preserve the original write exception for the caller. */ }
            }
        }
    }
}
