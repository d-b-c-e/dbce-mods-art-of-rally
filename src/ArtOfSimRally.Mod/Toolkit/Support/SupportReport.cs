// MIT. Portable C# 7.3 source. No UI, network, token, game or Wheelkit dependency.
// Legacy hosts have no nullable annotations; these API arguments intentionally accept null.
#pragma warning disable CS8600, CS8601, CS8603, CS8604, CS8618, CS8625
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Dbce.Wheel.Support
{
    /// <summary>Packaged host metadata, never discovered with git on a player's computer.</summary>
    public sealed class ReportProduct
    {
        public string Game { get; private set; }
        public string Version { get; private set; }
        public string Repository { get; private set; }
        public string Client { get; private set; }
        public Uri ModPage { get; private set; }
        public Uri WheelkitPage { get; private set; }

        public ReportProduct(string game, string version, string repository, string client = null,
            string wheelkitRepository = "d-b-c-e/dbce-wheelkit")
        {
            if (!SupportReport.ValidRepository(repository)) throw new ArgumentException("Choose a valid GitHub issue repository.");
            if (!SupportReport.ValidRepository(wheelkitRepository)) throw new ArgumentException("Choose a valid Wheelkit repository link.");
            Game = SupportReport.Required(game, 120, "game");
            Version = SupportReport.Required(version, 120, "mod version");
            Repository = repository;
            Client = client == null ? null : SupportReport.Required(client, 120, "reporting app");
            ModPage = new Uri("https://github.com/" + repository);
            WheelkitPage = new Uri("https://github.com/" + wheelkitRepository);
        }
    }

    /// <summary>An immutable redacted snapshot. Changing any input requires a new preview and review.</summary>
    public sealed class PreparedReport
    {
        private readonly string _summary;
        public string Repository { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public string Preview { get { return "# " + Title + "\n\n" + Body; } }
        public int SelectedLogCount { get; private set; }

        internal PreparedReport(string repository, string title, string body, string summary, int logCount)
        { Repository = repository; Title = title; Body = body; _summary = summary; SelectedLogCount = logCount; }

        /// <summary>Explicit local export of this snapshot; never rereads logs or overwrites a file.</summary>
        public void WriteReviewFile(string destination)
        {
            if (!string.Equals(Path.GetExtension(destination), ".txt", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a new .txt report file.");
            SupportReport.RefuseLinks(Path.GetDirectoryName(Path.GetFullPath(destination)));
            using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(Preview);
        }

        /// <summary>Produces data only. The host opens this URL after reviewing this same snapshot.</summary>
        public BrowserSubmission CreateBrowserSubmission(bool reviewed)
        {
            if (!reviewed) throw new InvalidOperationException("Review the prepared report before opening GitHub.");
            string prefix = "https://github.com/" + Repository + "/issues/new?title=" + Uri.EscapeDataString(Title) + "&body=";
            string full = prefix + Uri.EscapeDataString(Body);
            if (full.Length <= SupportReport.BrowserUrlLimit)
                return new BrowserSubmission(new Uri(full), false, Body,
                    "GitHub opens with the reviewed report. Check it and submit the issue there.");
            // Always preserve useful issue text; the complete reviewed report goes in one local attachment.
            string fallback = _summary + "\n\nPlease see the attached support-report.txt for the complete reviewed report" +
                (SelectedLogCount == 0 ? "." : " and selected log excerpts.");
            if ((prefix + Uri.EscapeDataString(fallback)).Length > SupportReport.BrowserUrlLimit)
                throw new InvalidOperationException("The title is too long for the browser draft. Shorten it and review again.");
            return new BrowserSubmission(new Uri(prefix + Uri.EscapeDataString(fallback)), true, fallback,
                "Save the reviewed report as support-report.txt, then attach that one file in GitHub's issue box. " +
                "Attaching uploads the file immediately. Check the draft and submit the issue there.");
        }
    }

    public sealed class BrowserSubmission
    {
        public Uri Url { get; private set; }
        public bool NeedsAttachment { get; private set; }
        public string IssueBody { get; private set; }
        public string Instructions { get; private set; }
        internal BrowserSubmission(Uri url, bool needsAttachment, string issueBody, string instructions)
        { Url = url; NeedsAttachment = needsAttachment; IssueBody = issueBody; Instructions = instructions; }
    }

    /// <summary>Shared report composition, redaction, bounded text-log reads and browser drafts.</summary>
    public static class SupportReport
    {
        public const int MaxLogs = 5;
        public const int MaxLogBytes = 24000;
        // Local conservative policy, not a claimed universal browser/GitHub limit.
        public const int BrowserUrlLimit = 4096;
        private static readonly string[] TextExtensions = { ".log", ".txt", ".json", ".jsonl", ".csv", ".tsv", ".xml" };

        public static PreparedReport Prepare(ReportProduct product, string title, string description,
            IReadOnlyList<string> selectedLogPaths = null, string home = null)
        {
            if (product == null) throw new ArgumentNullException("product");
            title = Required(Redact(Required(title, 200, "title"), home), 200, "redacted title");
            description = Redact(Required(description, 10000, "description"), home);
            int count = selectedLogPaths == null ? 0 : selectedLogPaths.Count;
            string logs = count == 0 ? null : SelectedLogs(selectedLogPaths);
            string body = Compose(product.Game, product.Version, description, logs, product.Client, home);
            // Bounded description and setup still make the issue useful before its single attachment.
            string summary = Redact("## What happened\n" + Shorten(description, 80) + "\n\n## Setup\n- Game: " +
                Shorten(product.Game, 20) + "\n- Mod: " + Shorten(product.Version, 20), home);
            return new PreparedReport(product.Repository, title, body, summary, count);
        }

        public static string Redact(string text, string home = null)
        {
            if (text == null) throw new ArgumentNullException("text");
            if (home == null) home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home != null) home = home.TrimEnd('\\', '/');
            if (!string.IsNullOrEmpty(home))
                text = Regex.Replace(text, Regex.Escape(home), "%USERPROFILE%",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            text = Replace(text, @"(?i)[a-z]:[\\/]Users[\\/][^\\/\s""<>]+", "%USERPROFILE%");
            text = Replace(text, @"(?i)(?:gh[pousr]_[a-z0-9]{15,}|github_pat_[a-z0-9_]+)", "[redacted token]");
            // Accept ordinary key=value, quoted JSON keys and HTTP headers. Redact the rest of that line.
            text = Replace(text, @"(?im)((?:authorization|password|passwd|access[_-]?token|api[_-]?key|secret)[""']?\s*[:=]\s*)[^\r\n]+", "$1[redacted]");
            text = Replace(text, @"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", "[redacted email]");
            return text;
        }

        private static string Replace(string text, string pattern, string replacement)
        { return Regex.Replace(text, pattern, replacement, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)); }

        public static bool ValidRepository(string repository)
        {
            if (string.IsNullOrEmpty(repository) || repository.Length > 201) return false;
            string[] parts = repository.Split('/');
            if (parts.Length != 2 || parts[0].Length > 100 || parts[1].Length > 100) return false;
            foreach (string part in parts)
                if (part == "." || part == ".." || !Regex.IsMatch(part, @"\A[A-Za-z0-9_][A-Za-z0-9_.-]*\z", RegexOptions.CultureInvariant)) return false;
            return true;
        }

        public static string Compose(string game, string version, string description, string log = null,
            string client = null, string home = null)
        {
            string body = "## What happened\n" + description + "\n\n## Setup\n- Game: " + game + "\n- Mod: " + version + "\n";
            if (!string.IsNullOrEmpty(client)) body += "- Reporting app: " + client + "\n";
            body += log == null ? "\nNo logs included.\n" :
                "\n## Selected mod logs (redacted)\n<details><summary>Log excerpts</summary>\n\n```text\n" +
                log.Replace("```", "[code fence]").Replace("<", "&lt;").Replace(">", "&gt;") + "\n```\n</details>\n";
            body = Redact(body, home);
            if (body.Length > 50000) throw new InvalidDataException("The report is too long. Shorten the description or select fewer logs.");
            return body;
        }

        /// <summary>Only explicit host-known/user-selected text log paths; no directory or settings discovery.</summary>
        public static string SelectedLogs(IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count < 1 || paths.Count > MaxLogs)
                throw new InvalidDataException("Choose between one and five text logs.");
            int budget = MaxLogBytes / paths.Count;
            var result = new StringBuilder();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (string.IsNullOrWhiteSpace(path)) throw new InvalidDataException("Choose a known text log.");
                bool allowed = false;
                foreach (string extension in TextExtensions)
                    if (string.Equals(extension, Path.GetExtension(path), StringComparison.OrdinalIgnoreCase)) allowed = true;
                if (!allowed) throw new InvalidDataException("Choose a text log (.log, .txt, .json, .jsonl, .csv, .tsv or .xml).");
                if (!seen.Add(Path.GetFullPath(path))) throw new InvalidDataException("Select each log only once.");
                string name = Shorten(Path.GetFileName(path), 200).Replace('\r', ' ').Replace('\n', ' ');
                if (i > 0) result.Append("\n\n");
                result.Append("--- ").Append(name).Append(" ---\n").Append(LogTail(path, budget));
            }
            return Redact(result.ToString());
        }

        public static string LogTail(string path, int maxBytes = MaxLogBytes)
        {
            if (maxBytes < 256 || maxBytes > MaxLogBytes) throw new ArgumentOutOfRangeException("maxBytes");
            RefuseLinks(Path.GetFullPath(path));
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                long length = stream.Length;
                var prefix = new byte[4];
                int prefixRead = stream.Read(prefix, 0, prefix.Length);
                if (prefixRead >= 4 && ((prefix[0] == 0xff && prefix[1] == 0xfe && prefix[2] == 0 && prefix[3] == 0) ||
                    (prefix[0] == 0 && prefix[1] == 0 && prefix[2] == 0xfe && prefix[3] == 0xff)))
                    throw new InvalidDataException("Choose a UTF-8 or UTF-16 text log.");
                Encoding encoding = prefixRead >= 2 && prefix[0] == 0xff && prefix[1] == 0xfe ? new UnicodeEncoding(false, true, true) :
                    prefixRead >= 2 && prefix[0] == 0xfe && prefix[1] == 0xff ? new UnicodeEncoding(true, true, true) : (Encoding)new UTF8Encoding(false, true);
                bool utf16 = encoding.CodePage == 1200 || encoding.CodePage == 1201;
                long start = Math.Max(0, length - maxBytes);
                bool truncated = start > 0;
                if (utf16 && start % 2 != 0) start++;
                stream.Position = start;
                var buffer = new byte[(int)Math.Min(maxBytes, length - start)];
                int count = 0;
                while (count < buffer.Length)
                { int read = stream.Read(buffer, count, buffer.Length - count); if (read == 0) break; count += read; }
                int offset = 0;
                if (truncated)
                {
                    // Discard the partial first line before decoding (including split UTF-8 sequences).
                    offset = count;
                    for (int i = 0; i < count; i += utf16 ? 2 : 1)
                        if ((!utf16 && buffer[i] == 10) || (utf16 && i + 1 < count &&
                            ((encoding.CodePage == 1200 && buffer[i] == 10 && buffer[i + 1] == 0) ||
                             (encoding.CodePage == 1201 && buffer[i] == 0 && buffer[i + 1] == 10))))
                        { offset = i + (utf16 ? 2 : 1); break; }
                }
                string text;
                try { text = encoding.GetString(buffer, offset, count - offset).TrimStart('\ufeff'); }
                catch (DecoderFallbackException) { throw new InvalidDataException("Choose a valid UTF-8 or UTF-16 text log; it may still be writing a partial character."); }
                foreach (char c in text)
                    if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                        throw new InvalidDataException("Select a text log, not a binary file.");
                return (truncated ? "[Last " + maxBytes + " bytes only; partial first line omitted]\n" : "") + text;
            }
        }

        internal static void RefuseLinks(string path)
        {
            if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
                throw new InvalidDataException("Choose a local text log or report folder, not a network path.");
            // Conservative refusal, not a security sandbox against concurrent filesystem replacement.
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Choose a regular local text log or report folder, not a linked path.");
        }

        internal static string Required(string value, int limit, string field)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > limit)
                throw new ArgumentException("Enter a " + field + " (up to " + limit + " characters).");
            return value.Trim();
        }

        private static string Shorten(string value, int limit)
        {
            if (value.Length <= limit) return value;
            int count = limit;
            if (count > 0 && char.IsHighSurrogate(value[count - 1])) count--;
            return value.Substring(0, count) + "...";
        }
    }
}
