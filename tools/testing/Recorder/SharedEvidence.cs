using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ArtOfSimRally.Testing
{
    /// <summary>
    /// The mod's input path as a player recording sees it (sana 36130/36236): identity at start (session.txt),
    /// one shared.tape row per rendered frame while shared input was requested, and the final shared outcome
    /// (end.txt). Read-only reflection on the mod's existing members, as native evidence does; a mod without
    /// them records "unavailable" and the recording itself is never refused for it. Analysis only: replay never
    /// reads shared.tape. Mod-internal shortcuts (settings, stop force, camera) appear only as held owned
    /// actions; they are not replayed.
    /// </summary>
    internal sealed class SharedEvidence
    {
        // Per row: frame, marker, phase, started, terminal, fresh, isolated and held describe this rendered frame.
        // The composition columns (steerLive through keyboardSteered) are the mod's last composition, made in the
        // frame named by lastFrame. The recorder ticks before the car reads input, so that is normally the previous
        // rendered frame. The header is unchanged since slice A (sana 37320).
        internal const string Header = "frame\tmarker\tphase\tstarted\tterminal\tfresh\tisolated\tlastFrame\tsteerLive\t" +
            "appliedSteer\tappliedThrottle\tappliedBrake\tappliedClutch\tappliedHandbrake\talignment\t" +
            "steer\tthrottle\tbrake\tclutch\thandbrake\tkeyboard\tkeyboardSteered\theld";
        // Shared action indexes 0..4 are the driving axes; the rest are buttons, reported as a held mask.
        private const int FirstButton = 5;
        private static readonly string[] Composed = { "Frame", "SteerLive", "AppliedSteer", "AppliedThrottle", "AppliedBrake", "AppliedClutch",
            "AppliedHandbrake", "Alignment", "Steer", "Throttle", "Brake", "Clutch", "Handbrake" };

        private readonly FieldInfo isolatedFrame, last, phase, refusal;
        private readonly FieldInfo[] composed;
        private readonly FieldInfo keyboard, keyboardSteered;
        private readonly object consumer;
        private readonly PropertyInfo started, terminal, requested, stopped, stopReason;
        private readonly MethodInfo fresh, value, owns;
        private readonly int count;
        private int attachedFrame = -1;
        internal bool Requested { get; }
        internal int Rows { get; private set; }

        private SharedEvidence(Type shim, Type startup, Type frame)
        {
            const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags I = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            isolatedFrame = Need(shim.GetField("IsolatedFrame", S), "ShimInput.IsolatedFrame");
            last = Need(shim.GetField("Last", S), "ShimInput.Last");
            composed = Composed.Select(n => Need(last.FieldType.GetField(n, I), "ShimInput.Composition." + n)).ToArray();
            keyboard = last.FieldType.GetField("Keyboard", I); keyboardSteered = last.FieldType.GetField("KeyboardSteered", I); // rc.17 and later
            consumer = Need(shim.GetField("Consumer", S), "ShimInput.Consumer").GetValue(null) ?? throw new MissingMemberException("ShimInput.Consumer is null");
            var c = consumer.GetType();
            started = Need(c.GetProperty("Started", I), "Started"); terminal = Need(c.GetProperty("Terminal", I), "Terminal");
            fresh = Need(c.GetMethod("Fresh", I, null, new[] { typeof(int) }, null), "Fresh");
            value = Need(c.GetMethod("Value", I, null, new[] { typeof(int), typeof(int) }, null), "Value");
            owns = Need(c.GetMethod("Owns", I, null, new[] { typeof(int) }, null), "Owns");
            requested = Need(startup.GetProperty("RequestedAtStartup", S), "SharedInputStartup.RequestedAtStartup");
            phase = Need(startup.GetField("phase", S), "SharedInputStartup.phase");
            refusal = startup.GetField("reason", S);
            stopped = startup.GetProperty("Stopped", S); stopReason = startup.GetProperty("StopReason", S);
            count = frame?.GetField("Count", S) is FieldInfo f && f.IsLiteral ? (int)f.GetRawConstantValue() : 31;
            if (count <= FirstButton || count > 64) throw new MissingMemberException("ShimFrame.Count " + count);
            Requested = (bool)requested.GetValue(null);
        }
        private static T Need<T>(T member, string name) where T : class => member ?? throw new MissingMemberException(name);

        /// <summary>Binds to the loaded mod, or returns null with the reason (never throws).</summary>
        internal static SharedEvidence Bind(out string why)
        {
            try
            {
                var shim = Find("ArtOfSimRally.Mod.Shim.ShimInput") ?? throw new MissingMemberException("ShimInput");
                var startup = Find("ArtOfSimRally.Mod.SharedInputStartup") ?? throw new MissingMemberException("SharedInputStartup");
                why = null; return new SharedEvidence(shim, startup, Find("ArtOfSimRally.Mod.Shim.ShimFrame"));
            }
            catch (Exception ex) { why = ex.GetType().Name + ": " + ex.Message; return null; }
        }
        internal static Type Find(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(fullName, false); } catch { continue; }
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>session.txt lines naming the input path in use at recording start. Files are hashed as found;
        /// every value is one line. Never throws (sana 36649): a value that cannot be read says so and the
        /// recording continues.</summary>
        internal static string Identity(SharedEvidence shared, string bindFailure, string modDirectory, string nativePath)
            => Identity(shared, bindFailure, () => modDirectory, () => nativePath);
        internal static string Identity(SharedEvidence shared, string bindFailure, Func<string> modDirectory, Func<string> nativePath)
        {
            var lines = new StringBuilder();
            void Add(string key, Func<string> value)
            {
                string text;
                try { text = value(); } catch (Exception ex) { text = "unreadable: " + ex.GetType().Name; }
                lines.Append(key).Append('=').Append(Clean(text)).Append('\n');
            }
            string mod = null, controls = null;
            Dictionary<string, string> profile = null;
            object settings = null;
            // build.json beside the mod, and the loaded assembly's own informational version, which the package
            // build pins to the same identity; a hand-staged copy can carry a stale build.json (sana 37320).
            Add("inputModIdentity", () => BuildIdentity(Path.Combine(mod = modDirectory(), "build.json")));
            Add("inputModVersion", () => Find("ArtOfSimRally.Mod.Main") is Type main
                ? main.Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).OfType<AssemblyInformationalVersionAttribute>()
                    .Select(a => a.InformationalVersion).FirstOrDefault() ?? "absent"
                : "unknown");
            Add("inputModSha256", () => HashOrAbsent(Path.Combine(mod ?? modDirectory(), "ArtOfSimRally.Mod.dll")));
            Add("settingsRead", () => { settings = Find("ArtOfSimRally.Mod.Main") is Type main ? Setting(main) : null; return settings == null ? "unknown" : "true"; });
            foreach (var name in new[] { "SharedInput", "WheelInputEnabled", "ShifterEnabled", "ForceFeedbackEnabled" })
                Add("setting" + name, () => settings == null ? "unknown" : Flag(settings, name));
            Add("sharedInputRequested", () => shared == null ? "unknown" : shared.Requested ? "true" : "false");
            Add("sharedEvidence", () => shared != null ? "bound" : "unavailable: " + bindFailure);
            // shared.tape's composition columns belong to lastFrame, not frame (see Header).
            Add("sharedTapeComposition", () => "lastFrame");
            Add("controlsSha256", () => HashOrAbsent(controls = Path.Combine(mod ?? modDirectory(), "dbce-controls.ini")));
            foreach (var key in new[] { "Profile", "Revision", "Transmission" })
                Add("controls" + key, () => (profile = profile ?? ControlsHeader(controls ?? Path.Combine(mod ?? modDirectory(), "dbce-controls.ini")))
                    .TryGetValue(key, out var v) ? v : "absent");
            Add("shimSha256", () => HashOrAbsent(Path.Combine(mod ?? modDirectory(), "dbce_shim.dll")));
            Add("nativeSha256", () => HashOrAbsent(nativePath()));
            return lines.ToString();
        }

        /// <summary>One shared.tape row for this rendered frame.</summary>
        internal string Row(int frame, int marker)
        {
            var row = new StringBuilder();
            void Add(string text) { if (row.Length > 0) row.Append('\t'); row.Append(text); }
            bool isStarted = (bool)started.GetValue(consumer, null);
            if (isStarted && attachedFrame < 0) attachedFrame = frame;
            Add(frame.ToString(CultureInfo.InvariantCulture)); Add(marker.ToString(CultureInfo.InvariantCulture));
            Add(phase.GetValue(null)?.ToString() ?? "null");
            Add(isStarted ? "1" : "0"); Add((bool)terminal.GetValue(consumer, null) ? "1" : "0");
            Add((bool)fresh.Invoke(consumer, new object[] { frame }) ? "1" : "0");
            Add((int)isolatedFrame.GetValue(null) == frame ? "1" : "0");
            var composition = last.GetValue(null);
            Add(((int)composed[0].GetValue(composition)).ToString(CultureInfo.InvariantCulture));
            Add((bool)composed[1].GetValue(composition) ? "1" : "0");
            for (int i = 2; i < composed.Length; i++) Add(N((float)composed[i].GetValue(composition)));
            Add(keyboard == null ? "-" : keyboard.GetValue(composition)?.ToString() ?? "null");
            Add(keyboardSteered == null ? "-" : (bool)keyboardSteered.GetValue(composition) ? "1" : "0");
            ulong held = 0;
            for (int a = FirstButton; a < count; a++)
                if ((bool)owns.Invoke(consumer, new object[] { a }) && (float)value.Invoke(consumer, new object[] { a, frame }) > .5f) held |= 1UL << a;
            Add(held.ToString("x", CultureInfo.InvariantCulture));
            Rows++;
            return row.ToString();
        }

        /// <summary>end.txt lines: the shared outcome when recording stopped.</summary>
        internal string Outcome()
        {
            var lines = new StringBuilder();
            void Add(string key, string text) => lines.Append(key).Append('=').Append(Clean(text)).Append('\n');
            Add("sharedPhase", phase.GetValue(null)?.ToString() ?? "null");
            Add("sharedAttachedFrame", attachedFrame < 0 ? "none" : attachedFrame.ToString(CultureInfo.InvariantCulture));
            Add("sharedStopped", stopped == null ? "unknown" : (bool)stopped.GetValue(null) ? "true" : "false");
            Add("sharedStopReason", stopped != null && (bool)stopped.GetValue(null) ? stopReason?.GetValue(null) as string ?? "unknown" : "none");
            Add("sharedRefusal", refusal?.GetValue(null) as string ?? "none");
            Add("sharedTapeRows", Rows.ToString(CultureInfo.InvariantCulture));
            return lines.ToString();
        }

        private static object Setting(Type main)
        {
            const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            return main.GetField("Settings", S)?.GetValue(null) ?? main.GetProperty("Settings", S)?.GetValue(null);
        }
        private static string Flag(object settings, string name)
        {
            var field = settings.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
            return field?.GetValue(settings) is bool b ? (b ? "true" : "false") : "unknown";
        }
        private static string BuildIdentity(string path)
        {
            try
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    int key = line.IndexOf("\"identity\"", StringComparison.Ordinal);
                    if (key < 0) continue;
                    int open = line.IndexOf('"', line.IndexOf(':', key) + 1), close = open < 0 ? -1 : line.IndexOf('"', open + 1);
                    if (close > open) return line.Substring(open + 1, close - open - 1);
                }
                return "unknown";
            }
            catch (Exception) { return File.Exists(path) ? "unreadable" : "absent"; }
        }
        /// <summary>The [Controls] Profile, Revision and Transmission lines, as the shared profile grammar writes them.</summary>
        internal static Dictionary<string, string> ControlsHeader(string path)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!File.Exists(path)) return values;
            bool controls = false;
            foreach (var raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)) { controls = line == "[Controls]"; continue; }
                int eq = line.IndexOf('=');
                if (!controls || eq <= 0 || line.StartsWith(";", StringComparison.Ordinal)) continue;
                string key = line.Substring(0, eq).Trim();
                if ((key == "Profile" || key == "Revision" || key == "Transmission") && !values.ContainsKey(key)) values[key] = line.Substring(eq + 1).Trim();
            }
            return values;
        }
        private static string HashOrAbsent(string path)
        {
            try
            {
                if (!File.Exists(path)) return "absent";
                using (var sha = SHA256.Create()) using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            }
            catch (Exception) { return "unreadable"; }
        }
        private static string Clean(string text) => new string((text ?? "").Select(ch => char.IsControl(ch) ? ' ' : ch).ToArray());
        private static string N(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
