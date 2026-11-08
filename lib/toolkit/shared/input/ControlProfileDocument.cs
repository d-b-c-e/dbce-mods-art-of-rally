// Mono-compatible shared profile editing. Binding grammar belongs only to ControlBinding.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

#if DBCE_INPUT_NET35
using BindingMap = System.Collections.Generic.IDictionary<string, Dbce.Wheel.Input.ControlBinding>;
using ProfileEdits = System.Collections.Generic.IDictionary<string, string?>;
using InvalidDataException = System.IO.IOException;
#else
using BindingMap = System.Collections.Generic.IReadOnlyDictionary<string, Dbce.Wheel.Input.ControlBinding>;
using ProfileEdits = System.Collections.Generic.IReadOnlyDictionary<string, string?>;
#endif

namespace Dbce.Wheel.Input
{
    public sealed class ControlProfileDocument
    {
        private readonly string[] lines;
        private readonly string eol;
        private readonly bool finalNewline;
        private readonly bool bom;
        private readonly Dictionary<string, int> positions = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, ControlBinding> bindings = new Dictionary<string, ControlBinding>(StringComparer.Ordinal);
        private readonly int sectionEnd;
        private readonly DeclaredControlExtras? extras;
        public BindingMap Bindings =>
#if DBCE_INPUT_NET35
            new Dictionary<string, ControlBinding>(bindings, StringComparer.Ordinal);
#else
            bindings;
#endif
        public string Name { get; private set; } = "Installed controls";
        public string Revision { get; private set; } = "";
        public string Transmission { get; private set; } = "";

        public ControlProfileDocument(string text)
            : this(text, null) { }
        public ControlProfileDocument(string text, DeclaredControlExtras? extras)
        {
            this.extras = extras;
            if (new UTF8Encoding(false, true).GetByteCount(text) > 1024 * 1024 || text.IndexOf('\0') >= 0)
                throw new InvalidDataException("The shared profile is oversized or contains a null character.");
            string bare = text.Replace("\r\n", "");
            if (bare.IndexOf('\r') >= 0 || text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 && bare.IndexOf('\n') >= 0)
                throw new InvalidDataException("Mixed line endings cannot be edited losslessly. The installed file was kept.");
            bom = text.StartsWith("\uFEFF", StringComparison.Ordinal);
            eol = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
            finalNewline = text.EndsWith("\n", StringComparison.Ordinal);
            var list = new List<string>(text.TrimStart('\uFEFF').Replace("\r\n", "\n").Split('\n'));
            if (finalNewline) list.RemoveAt(list.Count - 1);
            lines = list.ToArray();
            bool inside = false, found = false;
            int start = -1, end = lines.Length;
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string schema = "";
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    if (!line.EndsWith("]", StringComparison.Ordinal)) throw new InvalidDataException("Invalid shared profile section header.");
                    if (inside) end = i;
                    inside = line.Equals("[Controls]", StringComparison.OrdinalIgnoreCase);
                    if (inside && found) throw new InvalidDataException("Repeated [Controls] section.");
                    if (inside) { found = true; start = i; }
                    continue;
                }
                if (!inside || line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) throw new InvalidDataException("Invalid shared profile key/value line.");
                string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                if (!keys.Add(key)) throw new InvalidDataException("Repeated shared profile key: " + key);
                positions[key] = i;
                switch (key)
                {
                    case "Schema": schema = value; break;
                    case "Profile": Name = value; break;
                    case "Revision": Revision = value; break;
                    case "Transmission": Transmission = value; break;
                    default:
                        if (ControlBinding.ShapeOf(key) == ControlShape.Unknown) break;
                        if (!ControlBinding.TryParse(value, out var binding, out var why) || binding == null ||
                            (why = binding.Fits(key)).Length != 0)
                            throw new InvalidDataException("Cannot import " + key + ": " + why);
                        // Unknown attributes cannot silently disappear in a later canonical rewrite.
                        if (binding.ToString() != value) throw new InvalidDataException("Cannot losslessly import " + key + ": noncanonical or extended binding value.");
                        if (binding.Device == Guid.Empty || binding.Product == Guid.Empty)
                            throw new InvalidDataException("Both saved device identities are required for " + key + ".");
                        bindings.Add(key, binding); break;
                }
            }
            if (!found || schema != "1") throw new InvalidDataException("This editor reads shared controls Schema 1.");
            int sectionBytes = 0;
            for (int i = start; i < end; i++)
                sectionBytes += Encoding.UTF8.GetByteCount(lines[i]) + (i < lines.Length - 1 || finalNewline ? eol.Length : 0);
            if (sectionBytes > 64 * 1024) throw new InvalidDataException("The shared [Controls] section exceeds its 64 KiB limit.");
            if (Transmission != "Automatic" && Transmission != "Sequential" && Transmission != "H-pattern")
                throw new InvalidDataException("Unsupported shared controls Transmission.");
            sectionEnd = end;
            if (extras != null)
                foreach (var pair in new DeclaredExtrasDocument(text, extras).Bindings) bindings.Add(pair.Key, pair.Value);
        }

        /// <summary>Only changed action keys and Revision are touched. Null means an explicit Clear.</summary>
        public string Edit(ProfileEdits changes, string revision)
        {
            if (revision.Length == 0 || Encoding.UTF8.GetByteCount(revision) > 128)
                throw new InvalidDataException("Invalid shared controls revision.");
            foreach (char c in revision) if (char.IsControl(c)) throw new InvalidDataException("Invalid shared controls revision.");
            var replacements = new Dictionary<int, string?>(); var added = new List<string>();
            var extraChanges = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var pair in changes)
            {
                if (extras != null && extras.Contains(pair.Key)) { extraChanges.Add(pair.Key, pair.Value); continue; }
                if (ControlBinding.ShapeOf(pair.Key) == ControlShape.Unknown) throw new InvalidDataException("Unknown shared action: " + pair.Key);
                string? value = pair.Value;
                if (value != null)
                {
                    if (!ControlBinding.TryParse(value, out var binding, out var why) || binding == null ||
                        (why = binding.Fits(pair.Key)).Length != 0 || binding.Device == Guid.Empty || binding.Product == Guid.Empty)
                        throw new InvalidDataException("Invalid edited binding: " + pair.Key + ". " + why);
                    value = binding.ToString();
                }
                if (positions.TryGetValue(pair.Key, out int at)) replacements[at] = value == null ? null : pair.Key + " = " + value;
                else if (value != null) added.Add(pair.Key + " = " + value);
            }
            if (positions.TryGetValue("Revision", out int revisionAt)) replacements[revisionAt] = "Revision = " + revision;
            else added.Add("Revision = " + revision);
            var output = new List<string>();
            for (int i = 0; i <= lines.Length; i++)
            {
                if (i == sectionEnd) output.AddRange(added);
                if (i == lines.Length) break;
                if (replacements.TryGetValue(i, out var replacement)) { if (replacement != null) output.Add(replacement); }
                else output.Add(lines[i]);
            }
            string result = (bom ? "\uFEFF" : "") + string.Join(eol, output.ToArray()) + (finalNewline ? eol : "");
            if (extras != null) result = new DeclaredExtrasDocument(result, extras).Edit(extraChanges, revision);
            var checkedResult = new ControlProfileDocument(result, extras);
            if (!checkedResult.bindings.ContainsKey("steer")) throw new InvalidDataException("A usable steering binding is required. Nothing was saved.");
            return result;
        }
    }
}
