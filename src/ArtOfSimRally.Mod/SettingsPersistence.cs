#nullable enable
using System;
using System.IO;
using System.Security;
using System.Text;
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
            Replace(path, stream => {
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, true))
                    new XmlSerializer(settings.GetType()).Serialize(writer, settings);
            });

        // Validate the whole XML, then patch only the owned scalar's original text span. Never reserialize unowned XML.
        public static void WriteElement(string path, string name, string? value)
        {
            XmlConvert.VerifyNCName(name);
            value = XmlConvert.VerifyXmlChars(value ?? "");
            string escaped = SecurityElement.Escape(value).Replace("\r", "&#xD;");
            var utf8 = new UTF8Encoding(false, true);
            byte[] before = File.Exists(path) ? File.ReadAllBytes(path) : utf8.GetBytes("<Settings/>");
            int bom = before.Length >= 3 && before[0] == 0xef && before[1] == 0xbb && before[2] == 0xbf ? 3 : 0;
            string text = utf8.GetString(before, bom, before.Length - bom);
            if (text.IndexOf('\0') >= 0) throw new InvalidDataException("Unsupported settings encoding");
            ElementSpan? root = null, target = null;
            bool inside = false;
            var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using (var input = new MemoryStream(before, false))
            using (var reader = XmlReader.Create(input, options))
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element)
                    {
                        if (reader.Depth == 0)
                        {
                            if (reader.Name != "Settings" || reader.NamespaceURI.Length != 0) throw new InvalidDataException("No unqualified <Settings> root");
                            root = Span(reader, text);
                        }
                        else if (reader.Depth == 1 && reader.Name == name)
                        {
                            if (target != null || reader.NamespaceURI.Length != 0) throw new InvalidDataException("Ambiguous settings element " + name);
                            target = Span(reader, text); inside = !target.Empty;
                        }
                        else if (inside) throw new InvalidDataException("Structured settings element " + name);
                    }
                    else if (reader.NodeType == XmlNodeType.EndElement)
                    {
                        if (reader.Depth == 0)
                        {
                            if (root == null) throw new InvalidDataException("Unmapped settings root");
                            root.BodyEnd = TagStart(reader, text);
                        }
                        else if (inside && reader.Depth == 1)
                        {
                            if (target == null) throw new InvalidDataException("Unmapped settings element");
                            target.BodyEnd = TagStart(reader, text); inside = false;
                        }
                    }
                    else if (inside && reader.NodeType != XmlNodeType.Text && reader.NodeType != XmlNodeType.Whitespace && reader.NodeType != XmlNodeType.SignificantWhitespace)
                        throw new InvalidDataException("Structured settings element " + name);
                }
            }
            if (root == null) throw new InvalidDataException("No <Settings> root");
            string element = "<" + name + ">" + escaped + "</" + name + ">", updated;
            if (target != null && !target.Empty) updated = text.Substring(0, target.TagEnd + 1) + escaped + text.Substring(target.BodyEnd);
            else if (target != null) updated = text.Substring(0, target.TagEnd - 1) + ">" + escaped + "</" + name + ">" + text.Substring(target.TagEnd + 1);
            else if (root.Empty) updated = text.Substring(0, root.TagEnd - 1) + ">" + element + "</Settings>" + text.Substring(root.TagEnd + 1);
            else updated = text.Insert(root.BodyEnd, element);
            byte[] body = utf8.GetBytes(updated), after = new byte[bom + body.Length];
            Array.Copy(before, after, bom); Array.Copy(body, 0, after, bom, body.Length);
            Replace(path, stream => stream.Write(after, 0, after.Length));
        }

        private sealed class ElementSpan { public int TagEnd, BodyEnd; public bool Empty; }
        private static ElementSpan Span(XmlReader reader, string text)
        {
            int start = TagStart(reader, text); char quote = '\0';
            for (int i = start + 1; i < text.Length; i++)
            {
                char c = text[i];
                if (quote != '\0') { if (c == quote) quote = '\0'; }
                else if (c == '\'' || c == '"') quote = c;
                else if (c == '>') return new ElementSpan { TagEnd = i, Empty = reader.IsEmptyElement };
            }
            throw new InvalidDataException("Unmapped XML tag");
        }
        private static int TagStart(XmlReader reader, string text)
        {
            var location = (IXmlLineInfo)reader; int offset = 0;
            for (int line = 1; line < location.LineNumber; line++)
            {
                int end = text.IndexOfAny(new[] { '\r', '\n' }, offset);
                if (end < 0) throw new InvalidDataException("Unmapped XML line");
                offset = end + 1;
                if (text[end] == '\r' && offset < text.Length && text[offset] == '\n') offset++;
            }
            int position = offset + location.LinePosition - 1;
            if (position < 0 || position >= text.Length) throw new InvalidDataException("Unmapped XML column");
            int start = text.LastIndexOf('<', position);
            if (start < 0) throw new InvalidDataException("Unmapped XML element");
            return start;
        }

        private static void Replace(string path, Action<Stream> write)
        {
            path = Path.GetFullPath(path);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            bool owned = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { owned = true; write(stream); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                owned = false;
            }
            finally
            {
                try { if (owned) File.Delete(temporary); }
                catch { /* Preserve the original write exception for the caller. */ }
            }
        }
    }
}
