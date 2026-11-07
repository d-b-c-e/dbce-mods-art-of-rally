#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// The rows of the developer-only hook F6 (UI-4, <c>[Dev] UiHost=hook</c>): which <see cref="Settings"/> field each
    /// descriptor row of <c>UiHost/art-hook.settings.json</c> reads and writes, and the STD-044 save that writes only
    /// the elements a change touched. No Unity or dbce_ui calls here, so the mapping is tested off the game.
    /// </summary>
    internal static class UiHostRows
    {
        internal const string DevFile = "dbce-dev.ini";

        internal enum Kind { Toggle, Number }

        internal sealed class Row
        {
            public readonly string Id;
            public readonly Kind Kind;
            /// <summary>The Settings.xml elements this row may change (the FOV is kept per camera and as offsets).</summary>
            public readonly string[] Keys;
            /// <summary>The row's value; bumper is true while the bumper camera is the one driven.</summary>
            public readonly Func<Settings, bool, double> Get;
            public readonly Action<Settings, bool, double> Set;

            public Row(string id, Kind kind, string[] keys, Func<Settings, bool, double> get, Action<Settings, bool, double> set)
            { Id = id; Kind = kind; Keys = keys; Get = get; Set = set; }
        }

        private static double B(bool v) => v ? 1 : 0;
        private static float F(double v, double min, double max) => (float)Math.Max(min, Math.Min(max, v));

        private static Row Toggle(string id, string key, Func<Settings, bool> get, Action<Settings, bool> set) =>
            new Row(id, Kind.Toggle, new[] { key }, (c, _) => B(get(c)), (c, _, v) => set(c, v != 0));

        internal static readonly Row[] Rows =
        {
            Toggle("ffb.enabled", "ForceFeedbackEnabled", c => c.ForceFeedbackEnabled, (c, v) => c.ForceFeedbackEnabled = v),
            new Row("ffb.strength", Kind.Number, new[] { "Strength" }, (c, _) => c.Strength,
                (c, _, v) => c.Strength = (int)Math.Round(F(v, 0, 100))),
            new Row("ffb.crash", Kind.Number, new[] { "CrashStrength" }, (c, _) => c.CrashStrength,
                (c, _, v) => c.CrashStrength = F(v, 0, 100)),
            Toggle("ffb.reverse", "Invert", c => c.Invert, (c, v) => c.Invert = v),
            new Row("ffb.smoothing", Kind.Number, new[] { "Smoothing" }, (c, _) => Math.Round(c.Smoothing * 100.0, 1),
                (c, _, v) => c.Smoothing = (float)(Math.Round(F(v, 0, 95)) / 100.0)),
            new Row("ffb.landing", Kind.Number, new[] { "LandingStrength" }, (c, _) => c.LandingStrength,
                (c, _, v) => c.LandingStrength = F(v, 0, 40)),
            new Row("ffb.shift", Kind.Number, new[] { "ShiftStrength" }, (c, _) => c.ShiftStrength,
                (c, _, v) => c.ShiftStrength = F(v, 0, 20)),
            // The live value is the driven camera's; CameraMountMemory.Capture then moves it into that camera's offset.
            new Row("cameras.fov", Kind.Number,
                new[] { "BonnetFOV", "BumperFOV", "BonnetFovOffset", "BumperFovOffset", "BonnetFovRealisticOffset", "BumperFovRealisticOffset" },
                (c, bumper) => Math.Round(bumper ? c.BumperFOV : c.BonnetFOV, 1),
                (c, bumper, v) => { if (bumper) c.BumperFOV = F(v, 20, 120); else c.BonnetFOV = F(v, 20, 120); }),
            Toggle("cameras.bonnet", "BonnetCameraEnabled", c => c.BonnetCameraEnabled, (c, v) => c.BonnetCameraEnabled = v),
            Toggle("cameras.bumper", "BumperCameraEnabled", c => c.BumperCameraEnabled, (c, v) => c.BumperCameraEnabled = v),
            Toggle("cameras.keys", "CameraTuningKeys", c => c.CameraTuningKeys, (c, v) => c.CameraTuningKeys = v),
            new Row("cameras.lean", Kind.Number, new[] { "BonnetLean" }, (c, _) => Math.Round(c.BonnetLean * 100.0, 1),
                (c, _, v) => c.BonnetLean = (float)(Math.Round(F(v, 0, 100)) / 100.0)),
        };

        internal static Row? Find(string? id)
        {
            foreach (var row in Rows) if (row.Id == id) return row;
            return null;
        }

        /// <summary>True when <c>dbce-dev.ini</c> beside the mod has <c>UiHost=hook</c> in its <c>[Dev]</c> section.
        /// Players have no such file, so they keep the standard F6.</summary>
        internal static bool HookRequested(string modDir)
        {
            string path = Path.Combine(modDir, DevFile);
            if (!File.Exists(path)) return false;
            bool dev = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal)) { dev = string.Equals(line, "[Dev]", StringComparison.OrdinalIgnoreCase); continue; }
                int eq = line.IndexOf('=');
                if (!dev || eq <= 0) continue;
                if (string.Equals(line.Substring(0, eq).Trim(), "UiHost", StringComparison.OrdinalIgnoreCase))
                    return string.Equals(line.Substring(eq + 1).Trim(), "hook", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        /// <summary>A field's text exactly as UMM's XmlSerializer writes it.</summary>
        internal static string Text(Settings c, string key)
        {
            var field = typeof(Settings).GetField(key) ?? throw new ArgumentException("No setting " + key);
            switch (field.GetValue(c))
            {
                case bool b: return b ? "true" : "false";
                case int i: return XmlConvert.ToString(i);
                case float f: return XmlConvert.ToString(f);
                case string s: return s;
                default: throw new ArgumentException("Not a scalar setting " + key);
            }
        }

        internal static Dictionary<string, string> Snapshot(Settings c, Row row)
        {
            var values = new Dictionary<string, string>();
            foreach (string key in row.Keys) values[key] = Text(c, key);
            return values;
        }

        /// <summary>Writes each element whose value differs from the snapshot, and only those (STD-044,
        /// SettingsPersistence.WriteElement). Returns the keys written.</summary>
        internal static List<string> Save(string path, Settings c, Dictionary<string, string> before)
        {
            var written = new List<string>();
            foreach (var entry in before)
            {
                string now = Text(c, entry.Key);
                if (now == entry.Value) continue;
                SettingsPersistence.WriteElement(path, entry.Key, now);
                written.Add(entry.Key);
            }
            return written;
        }
    }
}
