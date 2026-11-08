// Scalar declaration and editing envelope for native/shim extension2. No reader, contexts observation or force calls.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
#if DBCE_INPUT_NET35
using InvalidDataException = System.IO.IOException;
#endif
namespace Dbce.Wheel.Input
{
    public sealed class DeclaredControlExtra
    {
        public string Id { get; private set; }
        public ControlShape Shape { get; private set; }
        public uint Contexts { get; private set; }
        public DeclaredControlExtra(string id, ControlShape shape, uint contexts) { Id = id; Shape = shape; Contexts = contexts; }
    }
    /// <summary>Generates exact bounded scalar declaration bytes. Common defaults and action names are unchanged.</summary>
    public sealed class DeclaredControlExtras
    {
        private readonly Dictionary<string, DeclaredControlExtra> definitions = new Dictionary<string, DeclaredControlExtra>(StringComparer.Ordinal);
        private readonly byte[] declaration;
        public string Game { get; private set; }
        public string Hash { get; private set; }
        public uint AllowedContexts { get; private set; }
        public byte[] Declaration { get { return (byte[])declaration.Clone(); } }
        public IList<DeclaredControlExtra> Actions { get; private set; }
        public DeclaredControlExtras(string game, IList<string> contexts, IList<DeclaredControlExtra> actions)
        {
            if (!Word(game, 32, true) || contexts.Count == 0 || contexts.Count > 8 || actions.Count == 0 || actions.Count > 32)
                throw new ArgumentException("Invalid observed scalar extras declaration.");
            Game = game; AllowedContexts = (1u << contexts.Count) - 1;
            var unique = new HashSet<string>(StringComparer.Ordinal);
            var text = new StringBuilder("game " + game + "\ncontexts observed");
            foreach (string context in contexts) {
                if (!Word(context, 16, false) || !unique.Add(context)) throw new ArgumentException("Invalid or duplicate extras context.");
                text.Append(' ').Append(context);
            }
            text.Append('\n');
            var copied = new List<DeclaredControlExtra>();
            foreach (var action in actions) {
                string prefix = game + ".";
                if (!action.Id.StartsWith(prefix, StringComparison.Ordinal) || !Word(action.Id.Substring(prefix.Length), 32, false) ||
                    definitions.ContainsKey(action.Id) || action.Contexts == 0 || (action.Contexts & ~AllowedContexts) != 0)
                    throw new ArgumentException("Invalid or duplicate declared extra id/context.");
                string shape = action.Shape == ControlShape.Digital ? "digital" : action.Shape == ControlShape.Pedal ? "pedal" :
                    action.Shape == ControlShape.Centred ? "centred" : throw new ArgumentException("Only declared scalar shapes are supported.");
                definitions.Add(action.Id, action); copied.Add(action);
                text.Append(action.Id).Append(' ').Append(shape).Append(" contexts=");
                bool separator = false;
                for (int i = 0; i < contexts.Count; ++i) if ((action.Contexts & (1u << i)) != 0) {
                    if (separator) text.Append(','); text.Append(contexts[i]); separator = true;
                }
                text.Append('\n');
            }
            declaration = Encoding.ASCII.GetBytes(text.ToString());
            if (declaration.Length > 4096) throw new ArgumentException("Extras declaration exceeds 4096 bytes.");
            using (var sha = SHA256.Create()) Hash = BitConverter.ToString(sha.ComputeHash(declaration)).Replace("-", "");
            Actions = copied.AsReadOnly();
        }
        /// <summary>Edits a scalar subset of a host's exact declaration, which may also declare extras this editor
        /// never touches (a stick). The header and every subset line must appear verbatim; Hash is the exact text's.</summary>
        public DeclaredControlExtras(string game, IList<string> contexts, IList<DeclaredControlExtra> actions, byte[] exactDeclaration)
            : this(game, contexts, actions)
        {
            if (exactDeclaration == null || exactDeclaration.Length > 4096) throw new ArgumentException("Invalid exact extras declaration.");
            foreach (byte b in exactDeclaration) if (b == 0 || b > 0x7f || b == '\r') throw new ArgumentException("The exact extras declaration is not LF ASCII.");
            string exact = Encoding.ASCII.GetString(exactDeclaration);
            var exactLines = new List<string>(exact.Split('\n'));
            string[] subset = Encoding.ASCII.GetString(declaration).TrimEnd('\n').Split('\n');
            if (!exact.EndsWith("\n", StringComparison.Ordinal) || exactLines.Count < 3 || exactLines[0] != subset[0] || exactLines[1] != subset[1])
                throw new ArgumentException("The exact extras declaration has a different game or contexts header.");
            foreach (string line in subset) if (!exactLines.Contains(line)) throw new ArgumentException("The exact extras declaration lacks: " + line);
            declaration = (byte[])exactDeclaration.Clone();
            using (var sha = SHA256.Create()) Hash = BitConverter.ToString(sha.ComputeHash(declaration)).Replace("-", "");
        }
        private static bool Word(string value, int max, bool hyphen) {
            if (string.IsNullOrEmpty(value) || value.Length > max || value[0] < 'a' || value[0] > 'z') return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'z' || !hyphen && c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || hyphen && c == '-')) return false;
            return true;
        }
        public bool Contains(string id) { return definitions.ContainsKey(id); }
        public ControlShape ShapeOf(string id) {
            DeclaredControlExtra? definition;
            return definitions.TryGetValue(id, out definition) ? definition.Shape : ControlBinding.ShapeOf(id);
        }
    }
    // The existing full-file ControlProfileDraft still owns one hash, backup and guarded atomic Save for both sections.
    internal sealed class DeclaredExtrasDocument
    {
        private readonly DeclaredControlExtras declaration;
        private readonly string[] lines;
        private readonly string eol;
        private readonly bool finalNewline, bom;
        private readonly Dictionary<string, int> positions = new Dictionary<string, int>(StringComparer.Ordinal);
        internal readonly Dictionary<string, ControlBinding> Bindings = new Dictionary<string, ControlBinding>(StringComparer.Ordinal);
        private readonly bool found;
        private readonly int end;
        internal DeclaredExtrasDocument(string text, DeclaredControlExtras declaration) {
            this.declaration = declaration; bom = text.StartsWith("\uFEFF", StringComparison.Ordinal);
            eol = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n"; finalNewline = text.EndsWith("\n", StringComparison.Ordinal);
            var source = new List<string>(text.TrimStart('\uFEFF').Replace("\r\n", "\n").Split('\n'));
            if (finalNewline) source.RemoveAt(source.Count - 1); lines = source.ToArray();
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool inside = false; int bytes = 0; string schema = "", game = "", hash = ""; end = lines.Length;
            for (int i = 0; i < lines.Length; ++i) {
                string line = lines[i].Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)) {
                    if (inside) end = i;
                    inside = line.Equals("[Extras]", StringComparison.OrdinalIgnoreCase);
                    if (inside && found) throw new InvalidDataException("Repeated [Extras] section.");
                    if (inside) { found = true; bytes = 0; }
                }
                if (!inside) continue;
                bytes += Encoding.UTF8.GetByteCount(lines[i]) + eol.Length;
                if (bytes > 65536) throw new InvalidDataException("The [Extras] section exceeds 64 KiB.");
                if (line.Length == 0 || line[0] == '[' || line[0] == ';' || line[0] == '#') continue;
                int equal = line.IndexOf('='); if (equal <= 0) throw new InvalidDataException("Invalid Extras key/value.");
                string key = line.Substring(0, equal).Trim(), value = line.Substring(equal + 1).Trim();
                if (!keys.Add(key)) throw new InvalidDataException("Repeated Extras key: " + key);
                positions[key] = i;
                if (key == "Schema") schema = value;
                else if (key == "Game") game = value;
                else if (key == "Declaration") hash = value;
                else if (declaration.Contains(key) && value.Length != 0) {
                    ControlBinding? binding; string why;
                    if (!ControlBinding.TryParse(value, out binding, out why) || binding == null || binding.Fits(declaration.ShapeOf(key)).Length != 0 ||
                        binding.ToString() != value || binding.Device == Guid.Empty || binding.Product == Guid.Empty)
                        throw new InvalidDataException("Cannot losslessly import declared binding: " + key);
                    Bindings.Add(key, binding);
                }
            }
            if (found && (schema != "1" || game != declaration.Game || !hash.Equals(declaration.Hash, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Extras schema, game or exact declaration differs; active bytes were kept.");
        }
        internal string Edit(IDictionary<string, string?> changes, string revision) {
            var replaced = new Dictionary<int, string>(); var added = new List<string>();
            foreach (var pair in changes) {
                if (!declaration.Contains(pair.Key)) throw new InvalidDataException("Undeclared extra: " + pair.Key);
                string value = "";
                if (pair.Value != null) {
                    ControlBinding? binding; string why;
                    if (!ControlBinding.TryParse(pair.Value, out binding, out why) || binding == null || binding.Fits(declaration.ShapeOf(pair.Key)).Length != 0 ||
                        binding.Device == Guid.Empty || binding.Product == Guid.Empty) throw new InvalidDataException("Invalid declared binding: " + pair.Key);
                    value = binding.ToString();
                }
                string row = pair.Key + " = " + value;
                if (positions.TryGetValue(pair.Key, out int at)) replaced[at] = row; else added.Add(row);
            }
            if (changes.Count == 0) return (bom ? "\uFEFF" : "") + string.Join(eol, lines) + (finalNewline ? eol : "");
            if (positions.TryGetValue("Revision", out int rev)) replaced[rev] = "Revision = " + revision; else added.Add("Revision = " + revision);
            var output = new List<string>();
            for (int i = 0; i <= lines.Length; ++i) {
                if (found && i == end) output.AddRange(added);
                if (i < lines.Length) output.Add(replaced.TryGetValue(i, out string? replacement) ? replacement : lines[i]);
            }
            if (!found) { output.Add("[Extras]"); output.Add("Schema = 1"); output.Add("Game = " + declaration.Game); output.Add("Declaration = " + declaration.Hash); output.AddRange(added); }
            string result = (bom ? "\uFEFF" : "") + string.Join(eol, output.ToArray()) + (finalNewline ? eol : "");
            new DeclaredExtrasDocument(result, declaration);
            return result;
        }
    }
}
