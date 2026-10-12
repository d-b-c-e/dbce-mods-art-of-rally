using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ArtOfSimRally.Mod.Shim
{
    // The physical controls an applied profile binds, read with Core's own grammar (art-shim-bridge
    // shim/parse.cpp read_profile and vendor/controls/dbce_controls.hpp parse/fits), so an Art fallback
    // binding that reuses one can be refused: one control never drives two actions. Missing a binding Core
    // accepts would allow a double mapping, so this mirrors the grammar's tokenisation exactly; attribute
    // checks beyond kind, index, angle, device and shape are not repeated (an extra entry only refuses a
    // fallback, never maps a control twice).
    internal static class ProfileObjects
    {
        internal struct PhysicalObject
        {
            internal string Action, Kind;
            internal int Index, Angle;
            internal Guid Device;
        }

        internal static List<PhysicalObject> Parse(string profile)
        {
            var objects = new List<PhysicalObject>();
            if (String.IsNullOrEmpty(profile)) return objects;
            if (profile[0] == '﻿') profile = profile.Substring(1);
            bool inside = false;
            var bound = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in profile.Split('\n'))
            {
                string line = Trim(raw);
                if (line.Length > 0 && line[0] == '[')
                {
                    // Only the [Controls] section binds (section names are case-insensitive).
                    int close = line.IndexOf(']');
                    inside = close > 0 && Trim(line.Substring(1, close - 1)).ToLowerInvariant() == "controls";
                    continue;
                }
                if (!inside || line.Length == 0 || line[0] == ';' || line[0] == '#') continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string action = Trim(line.Substring(0, eq)), value = Trim(line.Substring(eq + 1));
                // Core binds only its known actions; a repeat of an already bound action is invalid.
                if (Array.IndexOf(ShimFrame.Names, action) < 0 || value.Length == 0 || bound.Contains(action)) continue;
                var t = Tokens(value);
                if (t == null || t.Count < 2 || (t[0] != "axis" && t[0] != "button" && t[0] != "hat")) continue;
                if (!int.TryParse(t[1], NumberStyles.None, CultureInfo.InvariantCulture, out int index) || index > (t[0] == "axis" ? 7 : t[0] == "hat" ? 3 : 127)) continue;
                int angle = 0, k = 2;
                if (t[0] == "hat")
                {
                    if (t.Count < 3 || !int.TryParse(t[2], NumberStyles.None, CultureInfo.InvariantCulture, out angle) || angle > 35999) continue;
                    k = 3;
                }
                var device = Guid.Empty; bool devices = false;
                for (; k < t.Count; k++)
                {
                    if (!t[k].StartsWith("dev=", StringComparison.Ordinal)) continue;
                    string g = t[k].Substring(4);
                    // Core accepts only the braced 38-character form; a second dev= is a duplicate attribute.
                    if (devices || g.Length != 38 || !Guid.TryParseExact(g, "B", out device)) { device = Guid.Empty; break; }
                    devices = true;
                }
                if (device == Guid.Empty || !Fits(action, t[0])) continue;
                bound.Add(action);
                objects.Add(new PhysicalObject { Action = action, Kind = t[0], Index = index, Angle = angle, Device = device });
            }
            return objects;
        }

        // Core's shapes: steer centred (axis only); pedals axis or button; every other action button or hat.
        private static bool Fits(string action, string kind)
        {
            if (action == "steer") return kind == "axis";
            if (action == "throttle" || action == "brake" || action == "clutch" || action == "handbrake") return kind == "axis" || kind == "button";
            return kind == "button" || kind == "hat";
        }
        private static string Trim(string s) => s.Trim(' ', '\t', '\r', '\n');
        // Space or tab separated; name="..." is one token (backslash escapes quote and backslash).
        private static List<string> Tokens(string text)
        {
            var tokens = new List<string>();
            for (int i = 0; i < text.Length;)
            {
                while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;
                if (i >= text.Length) break;
                int j = i;
                if (String.CompareOrdinal(text, i, "name=\"", 0, 6) == 0)
                {
                    var name = new StringBuilder(); bool closed = false;
                    for (j = i + 6; j < text.Length;)
                    {
                        char c = text[j];
                        if (c == '\\' && j + 1 < text.Length && (text[j + 1] == '"' || text[j + 1] == '\\')) { name.Append(text[j + 1]); j += 2; continue; }
                        if (c == '"') { closed = true; j++; break; }
                        name.Append(c); j++;
                    }
                    if (!closed) return null; // Core: bad-name
                    tokens.Add("name=\"" + name);
                    i = j;
                    continue;
                }
                while (j < text.Length && text[j] != ' ' && text[j] != '\t') j++;
                tokens.Add(text.Substring(i, j - i));
                i = j;
            }
            return tokens;
        }

        // The profile action already bound to this physical control, or null. Same device instance, kind and
        // index; hat directions share a control when their 90-degree sectors meet. Core and Art both press a
        // direction within 4500 inclusive, so sectors exactly 9000 apart share their boundary angle.
        internal static string BoundBy(List<PhysicalObject> objects, string kind, int index, int angle, Guid device)
        {
            if (objects == null) return null;
            foreach (var o in objects)
            {
                if (o.Device != device || o.Kind != kind || o.Index != index) continue;
                if (kind == "hat")
                {
                    int d = Math.Abs(o.Angle - angle) % 36000;
                    if (Math.Min(d, 36000 - d) > 9000) continue;
                }
                return o.Action;
            }
            return null;
        }
    }
}
