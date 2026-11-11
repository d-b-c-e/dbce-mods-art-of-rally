// Reader of the shared store (docs/STORE.md) for Wheelkit and the Unity mods (netstandard2.0 and Mono 2.0; no
// dependencies): profiles.json (profiles, the shared cockpit, which profile each game uses), written by Wheelkit only,
// and runtime\<game-id>.json, one game's runtime settings, written by that game's mod only. Files only gain fields;
// this reader ignores every field it does not know. It agrees with installer/DbceStore.ps1 and native/panel/dbce_store.h:
// Dump() writes the canonical dump their tests compare with schema/fixtures/store/*.dump.txt.
using System;
using System.Collections.Generic;
using System.Text;

namespace Dbce.Wheel.Mod
{
    /// <summary>Screen layout and measurements (Wheelkit's ScreenSetup); a missing field has Wheelkit's default.</summary>
    public sealed class StoreScreens
    {
        public int Count = 3, WidthPixels = 2560, HeightPixels = 1440;
        public bool SeparateMonitors = true;
        public double DiagonalInches = 32, AspectWidth = 16, AspectHeight = 9, EyeDistanceMm = 660, SideAngleDegrees = 70, BezelGapMm = 8, CurveRadiusMm;
        /// <summary>NaN when not measured (null in the file): derive the width from the diagonal.</summary>
        public double MeasuredWidthMm = double.NaN;
    }

    public sealed class StoreBinding
    {
        public string Action = "", InstanceGuid = "", ProductGuid = "", DeviceName = "", Kind = "";
        public int Index, Direction, Rest, Minimum, Maximum = 65535;
        public bool Calibrated, Inverted;
        /// <summary>The measured endpoints (STD-037), or null when not calibrated.</summary>
        public int? CalLow, CalCentre, CalHigh;
    }

    /// <summary>One profile: a whole setup (devices, bindings, transmission, FFB strength), optionally its own screens.</summary>
    public sealed class StoreProfile
    {
        public const int DefaultFfbStrength = 50;
        public string Id = "", Name = "", Transmission = "Automatic", Wheel = "", Shifter = "";
        public int FfbStrength = DefaultFfbStrength;
        public bool FfbOn = true;
        /// <summary>This profile's own screens, or null: it uses the store's cockpit.</summary>
        public StoreScreens Screens;
        public List<StoreBinding> Bindings = new List<StoreBinding>();
    }

    /// <summary>The profile a game uses and the screens that come with it.</summary>
    public sealed class StoreChoice
    {
        public StoreProfile Profile;
        /// <summary>The profile's own screens, else the cockpit, else null (the mod keeps its own screen settings).</summary>
        public StoreScreens Screens;
        /// <summary>"profile", "cockpit" or "none": where Screens came from.</summary>
        public string ScreensFrom = "none";
    }

    public sealed class DbceStore
    {
        public const int ProfilesSchema = 2;
        public int Schema;
        public string ActiveId = "";
        public StoreScreens Cockpit;
        public List<StoreProfile> Profiles = new List<StoreProfile>();
        /// <summary>Game id to profile Id, in file order.</summary>
        public List<KeyValuePair<string, string>> Games = new List<KeyValuePair<string, string>>();

        /// <summary>%LOCALAPPDATA%\Wheelkit\store, the store's fixed folder.</summary>
        public static string Folder()
        {
            return System.IO.Path.Combine(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wheelkit"), "store");
        }
        public static string ProfilesPath() { return System.IO.Path.Combine(Folder(), "profiles.json"); }
        public static string RuntimePath(string gameId) { return System.IO.Path.Combine(System.IO.Path.Combine(Folder(), "runtime"), gameId + ".json"); }

        public static DbceStore Load(string path) { return Parse(System.IO.File.ReadAllText(path, Encoding.UTF8)); }

        public static DbceStore Parse(string json)
        {
            var j = Json.Parse(json);
            var store = new DbceStore();
            store.Schema = (int)j.Num("Schema", 0);
            if (store.Schema != ProfilesSchema) throw new FormatException("not a store profiles.json (Schema " + ProfilesSchema + ")");
            store.ActiveId = j.Str("ActiveId");
            store.Cockpit = Screens(j.Get("Cockpit"));
            var profiles = j.Get("Profiles");
            if (profiles == null || profiles.Kind != Json.Type.Array || profiles.Elements.Count == 0) throw new FormatException("profiles.json needs at least one profile");
            foreach (var e in profiles.Elements)
            {
                var c = e.Get("Profile");
                if (e.Str("Id").Length == 0 || c == null || c.Kind != Json.Type.Object) throw new FormatException("each profile needs an Id and a Profile");
                foreach (var seen in store.Profiles) if (seen.Id == e.Str("Id")) throw new FormatException("duplicate profile Id '" + seen.Id + "'");
                var p = new StoreProfile { Id = e.Str("Id"), Name = c.Str("Name"), Wheel = c.Str("Wheel"), Shifter = c.Str("Shifter") };
                if (c.Str("Transmission").Length > 0) p.Transmission = c.Str("Transmission");
                var bindings = c.Get("Bindings");
                if (bindings != null && bindings.Kind == Json.Type.Object)
                    foreach (var kv in bindings.Members)
                    {
                        var b = kv.Value;
                        if (b.Kind != Json.Type.Object) continue;
                        p.Bindings.Add(new StoreBinding
                        {
                            Action = kv.Key, InstanceGuid = b.Str("InstanceGuid"), ProductGuid = b.Str("ProductGuid"), DeviceName = b.Str("DeviceName"),
                            Kind = b.Str("Kind"), Index = (int)b.Num("Index", 0), Direction = (int)b.Num("Direction", 0), Rest = (int)b.Num("Rest", 0),
                            Minimum = (int)b.Num("Minimum", 0), Maximum = (int)b.Num("Maximum", 65535),
                            Calibrated = b.Bool("Calibrated"), Inverted = b.Bool("Inverted"),
                            CalLow = Int(b, "CalLow"), CalCentre = Int(b, "CalCentre"), CalHigh = Int(b, "CalHigh"),
                        });
                    }
                var rig = e.Get("Rig");
                if (rig != null && rig.Kind == Json.Type.Object)
                {
                    p.FfbStrength = (int)rig.Num("FfbStrength", StoreProfile.DefaultFfbStrength);
                    var on = rig.Get("FfbOn");
                    if (on != null && on.Kind == Json.Type.Bool) p.FfbOn = on.Flag;
                    p.Screens = Screens(rig.Get("Screens"));
                }
                store.Profiles.Add(p);
            }
            var games = j.Get("Games");
            if (games != null && games.Kind == Json.Type.Object)
                foreach (var kv in games.Members)
                    if (kv.Value.Kind == Json.Type.Object) store.Games.Add(new KeyValuePair<string, string>(kv.Key, kv.Value.Str("Profile")));
            return store;
        }

        public StoreProfile Find(string id)
        {
            foreach (var p in Profiles) if (p.Id == id) return p;
            return null;
        }

        /// <summary>The profile a game uses: its assignment when that names a profile, else ActiveId's, else the first;
        /// with the profile's own screens, else the cockpit.</summary>
        public StoreChoice For(string gameId)
        {
            StoreProfile p = null;
            foreach (var g in Games) if (g.Key == gameId) p = Find(g.Value);
            if (p == null) p = Find(ActiveId);
            if (p == null) p = Profiles[0];
            var choice = new StoreChoice { Profile = p };
            if (p.Screens != null) { choice.Screens = p.Screens; choice.ScreensFrom = "profile"; }
            else if (Cockpit != null) { choice.Screens = Cockpit; choice.ScreensFrom = "cockpit"; }
            return choice;
        }

        static int? Int(Json o, string name)
        {
            var v = o.Get(name);
            if (v == null || v.Kind != Json.Type.Number) return null;
            return (int)v.Number;
        }

        static StoreScreens Screens(Json s)
        {
            if (s == null || s.Kind != Json.Type.Object) return null;
            var r = new StoreScreens();
            r.Count = (int)s.Num("Count", r.Count); r.WidthPixels = (int)s.Num("WidthPixels", r.WidthPixels); r.HeightPixels = (int)s.Num("HeightPixels", r.HeightPixels);
            var sep = s.Get("SeparateMonitors");
            if (sep != null && sep.Kind == Json.Type.Bool) r.SeparateMonitors = sep.Flag;
            r.DiagonalInches = s.Num("DiagonalInches", r.DiagonalInches); r.AspectWidth = s.Num("AspectWidth", r.AspectWidth); r.AspectHeight = s.Num("AspectHeight", r.AspectHeight);
            r.EyeDistanceMm = s.Num("EyeDistanceMm", r.EyeDistanceMm); r.SideAngleDegrees = s.Num("SideAngleDegrees", r.SideAngleDegrees);
            r.BezelGapMm = s.Num("BezelGapMm", r.BezelGapMm); r.CurveRadiusMm = s.Num("CurveRadiusMm", r.CurveRadiusMm);
            r.MeasuredWidthMm = s.Num("MeasuredWidthMm", double.NaN);
            return r;
        }

        /// <summary>The canonical dump (docs/STORE.md): one path=value line per value read, then how each assigned game,
        /// and a game with no assignment, resolves.</summary>
        public string Dump()
        {
            var w = new StoreDumper();
            w.N("schema", Schema); w.S("activeId", ActiveId);
            w.Screens("cockpit", Cockpit);
            w.N("profiles.count", Profiles.Count);
            for (int k = 0; k < Profiles.Count; k++)
            {
                var p = Profiles[k]; string q = "profiles[" + k + "]";
                w.S(q + ".id", p.Id); w.S(q + ".name", p.Name); w.S(q + ".transmission", p.Transmission); w.S(q + ".wheel", p.Wheel); w.S(q + ".shifter", p.Shifter);
                w.N(q + ".ffbStrength", p.FfbStrength); w.B(q + ".ffbOn", p.FfbOn);
                w.Screens(q + ".screens", p.Screens);
                w.N(q + ".bindings.count", p.Bindings.Count);
                for (int i = 0; i < p.Bindings.Count; i++)
                {
                    var b = p.Bindings[i]; string r = q + ".bindings[" + i + "]";
                    w.S(r + ".action", b.Action); w.S(r + ".instanceGuid", b.InstanceGuid); w.S(r + ".productGuid", b.ProductGuid); w.S(r + ".deviceName", b.DeviceName);
                    w.S(r + ".kind", b.Kind); w.N(r + ".index", b.Index); w.N(r + ".direction", b.Direction); w.N(r + ".rest", b.Rest);
                    w.N(r + ".minimum", b.Minimum); w.N(r + ".maximum", b.Maximum); w.B(r + ".calibrated", b.Calibrated); w.B(r + ".inverted", b.Inverted);
                    w.I(r + ".calLow", b.CalLow); w.I(r + ".calCentre", b.CalCentre); w.I(r + ".calHigh", b.CalHigh);
                }
            }
            w.N("games.count", Games.Count);
            for (int k = 0; k < Games.Count; k++) { w.S("games[" + k + "].id", Games[k].Key); w.S("games[" + k + "].profile", Games[k].Value); }
            var ids = new List<string>();
            foreach (var g in Games) ids.Add(g.Key);
            ids.Add("unassigned-game");
            foreach (var id in ids)
            {
                var c = For(id);
                w.S("for[" + id + "].profile", c.Profile.Id); w.S("for[" + id + "].screens", c.ScreensFrom);
            }
            return w.ToString();
        }
    }

    /// <summary>One value in a game's runtime settings.</summary>
    public sealed class RuntimeValue
    {
        public string Key = "";
        /// <summary>"number", "bool" or "string".</summary>
        public string Type = "";
        public double Number;
        public bool Flag;
        public string Text = "";
    }

    /// <summary>runtime\&lt;game-id&gt;.json: the values one game's F6 panel changes. Only that game's mod writes it.</summary>
    public sealed class DbceRuntimeSettings
    {
        public const int RuntimeSchema = 1;
        public int Schema;
        public string Game = "", Profile = "";
        public List<RuntimeValue> Values = new List<RuntimeValue>();

        public static DbceRuntimeSettings Load(string path) { return Parse(System.IO.File.ReadAllText(path, Encoding.UTF8)); }

        public static DbceRuntimeSettings Parse(string json)
        {
            var j = Json.Parse(json);
            var r = new DbceRuntimeSettings();
            r.Schema = (int)j.Num("Schema", 0);
            if (r.Schema != RuntimeSchema) throw new FormatException("not a store runtime file (Schema " + RuntimeSchema + ")");
            r.Game = j.Str("Game");
            if (r.Game.Length == 0) throw new FormatException("a runtime file needs its Game");
            r.Profile = j.Str("Profile");
            var values = j.Get("Values");
            if (values != null && values.Kind == Json.Type.Object)
                foreach (var kv in values.Members)
                {
                    var v = kv.Value;
                    if (v.Kind == Json.Type.Number) r.Values.Add(new RuntimeValue { Key = kv.Key, Type = "number", Number = v.Number });
                    else if (v.Kind == Json.Type.Bool) r.Values.Add(new RuntimeValue { Key = kv.Key, Type = "bool", Flag = v.Flag });
                    else if (v.Kind == Json.Type.String) r.Values.Add(new RuntimeValue { Key = kv.Key, Type = "string", Text = v.Text });
                }
            return r;
        }

        public RuntimeValue Get(string key)
        {
            foreach (var v in Values) if (v.Key == key) return v;
            return null;
        }

        public string Dump()
        {
            var w = new StoreDumper();
            w.N("schema", Schema); w.S("game", Game); w.S("profile", Profile);
            w.N("values.count", Values.Count);
            for (int k = 0; k < Values.Count; k++)
            {
                var v = Values[k]; string q = "values[" + k + "]";
                w.S(q + ".key", v.Key);
                if (v.Type == "number") w.N(q + ".number", v.Number);
                else if (v.Type == "bool") w.B(q + ".bool", v.Flag);
                else w.S(q + ".string", v.Text);
            }
            return w.ToString();
        }
    }

    sealed class StoreDumper
    {
        readonly StringBuilder sb = new StringBuilder();
        public void S(string p, string v) { sb.Append(p).Append('=').Append((v ?? "").Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n")).Append('\n'); }
        public void B(string p, bool v) { sb.Append(p).Append('=').Append(v ? "true" : "false").Append('\n'); }
        public void N(string p, double v) { sb.Append(p).Append('=').Append(v.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append('\n'); }
        public void I(string p, int? v) { if (v.HasValue) N(p, v.Value); else sb.Append(p).Append("=null\n"); }
        public void Screens(string p, StoreScreens s)
        {
            if (s == null) { sb.Append(p).Append("=null\n"); return; }
            N(p + ".count", s.Count); N(p + ".widthPixels", s.WidthPixels); N(p + ".heightPixels", s.HeightPixels); B(p + ".separateMonitors", s.SeparateMonitors);
            N(p + ".diagonalInches", s.DiagonalInches); N(p + ".aspectWidth", s.AspectWidth); N(p + ".aspectHeight", s.AspectHeight);
            N(p + ".eyeDistanceMm", s.EyeDistanceMm); N(p + ".sideAngleDegrees", s.SideAngleDegrees); N(p + ".bezelGapMm", s.BezelGapMm);
            N(p + ".curveRadiusMm", s.CurveRadiusMm);
            if (double.IsNaN(s.MeasuredWidthMm)) sb.Append(p).Append(".measuredWidthMm=null\n"); else N(p + ".measuredWidthMm", s.MeasuredWidthMm);
        }
        public override string ToString() { return sb.ToString(); }
    }
}
