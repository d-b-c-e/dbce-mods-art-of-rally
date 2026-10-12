using System;
using System.Collections.Generic;
using System.Globalization;

namespace ArtOfSimRally.Mod.Shim
{
    // The physical controls an applied profile binds (Core's line grammar: `name = axis|button N dev={..}`,
    // `name = hat N angle dev={..}`), so an Art fallback binding that reuses one can be refused: one
    // control never drives two actions.
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
            foreach (var raw in profile.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0 || line.StartsWith("[", StringComparison.Ordinal) || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                var t = line.Substring(eq + 1).Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (t.Length < 3 || (t[0] != "axis" && t[0] != "button" && t[0] != "hat")) continue;
                if (!int.TryParse(t[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)) continue;
                int angle = 0, k = 2;
                if (t[0] == "hat")
                {
                    if (!int.TryParse(t[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out angle)) continue;
                    k = 3;
                }
                var device = Guid.Empty;
                for (; k < t.Length; k++) if (t[k].StartsWith("dev=", StringComparison.Ordinal)) Guid.TryParse(t[k].Substring(4), out device);
                if (device == Guid.Empty) continue;
                objects.Add(new PhysicalObject { Action = line.Substring(0, eq).Trim(), Kind = t[0], Index = index, Angle = angle, Device = device });
            }
            return objects;
        }

        // The profile action already bound to this physical control, or null. Same device instance, kind and
        // index; hat directions overlap when their 90-degree sectors meet.
        internal static string BoundBy(List<PhysicalObject> objects, string kind, int index, int angle, Guid device)
        {
            if (objects == null) return null;
            foreach (var o in objects)
            {
                if (o.Device != device || o.Kind != kind || o.Index != index) continue;
                if (kind == "hat")
                {
                    int d = Math.Abs(o.Angle - angle) % 36000;
                    if (Math.Min(d, 36000 - d) >= 9000) continue;
                }
                return o.Action;
            }
            return null;
        }
    }
}
