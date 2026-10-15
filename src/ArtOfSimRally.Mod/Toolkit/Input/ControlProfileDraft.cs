// Shared explicit-save lifecycle for in-game hosts. File I/O occurs only on an explicit Save.
// No Unity/WPF/device/native/force calls. The active profile is never changed during the process.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dbce.Wheel.Input
{
    public sealed class ControlProfileDraft
    {
        public ControlProfileDocument Active { get; }
        public ControlProfileDocument Saved { get; private set; }
        private readonly Dictionary<string, string?> changes = new Dictionary<string, string?>(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, string?> Changes => changes;
        private string savedHash;
        private readonly string activeHash;
        private readonly int maxProfileBytes;
        public bool SavedPendingRestart => savedHash != activeHash;
        public bool HasDraft => changes.Count > 0;

        public ControlProfileDraft(byte[] bytes, int maxProfileBytes = 1024 * 1024)
        {
            if (maxProfileBytes < 1 || maxProfileBytes > 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(maxProfileBytes));
            this.maxProfileBytes = maxProfileBytes;
            if (bytes.Length == 0 || bytes.Length > maxProfileBytes) throw new InvalidDataException("The shared profile exceeds this host's size limit.");
            Active = Saved = new ControlProfileDocument(new UTF8Encoding(false, true).GetString(bytes));
            savedHash = activeHash = Hash(bytes);
        }
        public ControlBinding? Binding(string action)
        {
            if (changes.TryGetValue(action, out var text))
            {
                if (text == null) return null;
                ControlBinding.TryParse(text, out var parsed, out _); return parsed;
            }
            return Saved.Bindings.TryGetValue(action, out var saved) ? saved : null;
        }
        public void Set(string action, string? value)
        {
            if (ControlBinding.ShapeOf(action) == ControlShape.Unknown) throw new ArgumentException("Unknown shared action.", nameof(action));
            string? saved = Saved.Bindings.TryGetValue(action, out var binding) ? binding.ToString() : null;
            if (value == saved) changes.Remove(action); else changes[action] = value;
        }
        public void Discard() => changes.Clear();

        public bool Save(string path, out string message)
        {
            message = "No binding changes to save.";
            if (!HasDraft) return false;
            string temp = path + ".bindings-" + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                if (Hash(ReadExisting(path)) != savedHash)
                    throw new IOException("The installed controls changed outside this editor. Restart to reread them; the draft was kept.");
                string text = Saved.Edit(new Dictionary<string, string?>(changes, StringComparer.Ordinal), Guid.NewGuid().ToString("N"));
                byte[] next = new UTF8Encoding(false, true).GetBytes(text);
                if (next.Length > maxProfileBytes) throw new InvalidDataException("The edited profile would exceed this host's size limit.");
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(next, 0, next.Length); stream.Flush(true); }
                if (Hash(ReadExisting(path)) != savedHash)
                    throw new IOException("The installed controls changed before Save. Nothing was replaced; the draft was kept.");
                string backup = path + ".bindings-backup-" + Guid.NewGuid().ToString("N");
                File.Replace(temp, path, backup);
                string nextHash = Hash(next);
                if (Hash(ReadExisting(path)) != nextHash)
                    throw new IOException("Controls were replaced but did not read back as expected. The backup and draft were kept; restart before editing again.");
                Saved = new ControlProfileDocument(text); savedHash = nextHash; changes.Clear();
                message = "Bindings saved. Restart the game to use them; active bindings are unchanged.";
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is ArgumentException)
            { message = "Bindings not saved: " + ex.Message; return false; }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        }
        private byte[] ReadExisting(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > maxProfileBytes) throw new InvalidDataException("The installed profile exceeds this host's size limit; it was kept.");
                using (var output = new MemoryStream())
                {
                    var buffer = new byte[4096]; int count;
                    while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (output.Length + count > maxProfileBytes) throw new InvalidDataException("The installed profile grew beyond this host's size limit; it was kept.");
                        output.Write(buffer, 0, count);
                    }
                    return output.ToArray();
                }
            }
        }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
