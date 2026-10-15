using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Dbce.Wheel.Input;

namespace ArtOfSimRally.Mod
{
    // The active document is immutable for the game process. Saved is the next-start document; edits overlay it.
    internal sealed class SharedBindingDraft
    {
        internal readonly ControlProfileDocument Active;
        internal ControlProfileDocument Saved { get; private set; }
        internal readonly Dictionary<string, string> Changes = new Dictionary<string, string>(StringComparer.Ordinal);
        private string savedHash;
        private readonly string activeHash;
        internal bool SavedPendingRestart => savedHash != activeHash;
        internal bool HasDraft => Changes.Count > 0;
        internal SharedBindingDraft(byte[] bytes)
        {
            if (bytes.Length == 0 || bytes.Length > 65536) throw new InvalidDataException("Art's shared profile must fit its 64 KiB inspection limit.");
            Active = Saved = new ControlProfileDocument(new UTF8Encoding(false, true).GetString(bytes));
            savedHash = activeHash = Hash(bytes);
        }
        internal ControlBinding Binding(string action)
        {
            if (Changes.TryGetValue(action, out var text))
            {
                if (text == null) return null;
                ControlBinding.TryParse(text, out var parsed, out _); return parsed;
            }
            return Saved.Bindings.TryGetValue(action, out var saved) ? saved : null;
        }
        internal void Set(string action, string value)
        {
            string saved = Saved.Bindings.TryGetValue(action, out var binding) ? binding.ToString() : null;
            if (value == saved) Changes.Remove(action); else Changes[action] = value;
        }
        internal void Discard() => Changes.Clear();
        internal bool Save(string path, out string message)
        {
            message = "No binding changes to save.";
            if (!HasDraft) return false;
            string temp = path + ".bindings-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (!File.Exists(path) || Hash(File.ReadAllBytes(path)) != savedHash)
                    throw new IOException("The installed controls changed outside this editor. Restart to reread them; the draft was kept.");
                var changes = new Dictionary<string, string>(Changes, StringComparer.Ordinal);
                string text = Saved.Edit(changes, Guid.NewGuid().ToString("N"));
                byte[] next = new UTF8Encoding(false, true).GetBytes(text);
                if (next.Length > 65536) throw new InvalidDataException("Art's edited shared profile would exceed its 64 KiB inspection limit.");
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(next, 0, next.Length); stream.Flush(true); }
                if (Hash(File.ReadAllBytes(path)) != savedHash)
                    throw new IOException("The installed controls changed before Save. Nothing was replaced; the draft was kept.");
                string backup = path + ".bindings-backup-" + Guid.NewGuid().ToString("N");
                File.Replace(temp, path, backup);
                string nextHash = Hash(next);
                if (Hash(File.ReadAllBytes(path)) != nextHash)
                    throw new IOException("Controls were replaced but did not read back as expected. The backup and draft were kept; restart before editing again.");
                Saved = new ControlProfileDocument(text); savedHash = nextHash; Changes.Clear();
                message = "Bindings saved. Restart the game to use them; active bindings are unchanged.";
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { message = "Bindings not saved: " + ex.Message; return false; }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
