// Reader of dbce-mod.json for Wheelkit and the Unity mods (netstandard2.0 and Mono 2.0; no dependencies). The model
// fills in every default from docs/DBCE-MOD-JSON.md, so it agrees with the installer (installer/DbceModFile.ps1) and
// the panel core (native/panel/dbce_mod_file.h). Dump() writes the canonical dump the readers' tests compare.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Dbce.Wheel.Mod
{
    public sealed class FromTo { public string From = "", To = ""; }
    public sealed class ModLoader { public string Name = "", Version = "", Bundle = ""; }
    public sealed class KnownBuild { public string File = "", Sha256 = "", Label = ""; }
    public sealed class ModPackage
    {
        public string Kind = "";
        public ModLoader Loader;
        public List<FromTo> Payload = new List<FromTo>(), Optional = new List<FromTo>(), SeedConfig = new List<FromTo>();
        public List<string> Conflicts = new List<string>();
        public List<KnownBuild> KnownBuilds = new List<KnownBuild>();
        public bool Strict;
        public string UiHost = "game";
    }
    public sealed class RowStore { public string File = "", Section = "", Key = "", On = "", Off = ""; public double Scale = 1; }
    public sealed class SettingsRow
    {
        public string Page = "", Id = "", Kind = "", Label = "", Help = "", Level = "simple", Apply = "live", Unit = "", Text = "", Role = "";
        public List<string> Choices = new List<string>();
        public double Min, Max, Default;
        public int Decimals;
        public RowStore Store;
    }
    public sealed class ModSettings
    {
        public string Product = "", Version = "", ModPage = "", Keys = "", Config = "", Format = "ini";
        public List<SettingsRow> Rows = new List<SettingsRow>();
    }
    public sealed class ControlsOptIn { public string Section = "", Key = "", Value = "", Default = ""; public bool AppliesAfterRestart; }
    public sealed class ControlsCondition
    {
        public List<string> Actions = new List<string>(), SettingsValues = new List<string>();
        public string Transmission = "", SettingsKey = "", SettingsDefault = "", Note = "";
    }
    public sealed class GameControl { public string Id = "", Kind = "", Label = "", Group = ""; }
    public sealed class ModControls
    {
        public string Store = "", Profile = "", Settings = "";
        public ControlsOptIn OptIn;
        public List<string> Delivered = new List<string>();
        public List<KeyValuePair<string, string>> Unsupported = new List<KeyValuePair<string, string>>();
        public List<ControlsCondition> Conditions = new List<ControlsCondition>();
        public List<GameControl> GameControls = new List<GameControl>();
    }
    public sealed class CameraStart { public string Camera = "", Start = "", File = "", Section = "", Key = "", Realistic = "", Game = ""; }
    public sealed class SelfTestProbe { public string Id = "", Type = "", Label = ""; }
    public sealed class ModSelfTest
    {
        public string Folder = "";
        public List<string> Steps = new List<string>();
        public List<SelfTestProbe> Probes = new List<SelfTestProbe>();
    }
    public sealed class TestLaunch { public long SteamAppId; public string Exe = ""; public List<string> Args = new List<string>(); }
    public sealed class TestState { public string State = "", Field = "", Is = "", Text = "", Picture = ""; }
    public sealed class TestEntry { public string Command = "", Route = ""; }
    public sealed class RouteStep { public string Key = "", Input = "", WaitState = "", WaitText = ""; public long Timeout; }
    public sealed class TestQuit { public string Command = "", Key = "", Input = ""; }
    public sealed class ModTest
    {
        public TestLaunch Launch;
        public List<string> Protect = new List<string>();
        public FromTo TestSave;
        public List<TestState> States = new List<TestState>();
        public TestEntry Entry;
        public List<RouteStep> Route = new List<RouteStep>();
        public TestQuit Quit;
    }
    public sealed class InstalledFile { public string Path = "", Sha256 = ""; }
    public sealed class ModInstalled
    {
        public string Version = "", Commit = "", InstalledUtc = "", Backups = "";
        public bool LoaderInstalledByUs;
        public List<InstalledFile> Files = new List<InstalledFile>();
        public List<KeyValuePair<string, bool>> Flags = new List<KeyValuePair<string, bool>>();
    }

    public sealed class DbceModFile
    {
        public const string SchemaId = "dbce.mod@1";
        public string Schema = "", Id = "", Name = "", Version = "", Folder = "", Repository = "", LogFile = "", LogStart = "";
        public long SteamAppId;
        public List<string> Exe = new List<string>();
        public ModPackage Package = new ModPackage();
        public ModSettings Settings;
        public ModControls Controls;
        public List<CameraStart> Cameras = new List<CameraStart>();
        public ModSelfTest SelfTest;
        public ModTest Test;
        public ModInstalled Installed;

        public static DbceModFile Load(string path) { return Parse(System.IO.File.ReadAllText(path, Encoding.UTF8)); }

        public static DbceModFile Parse(string json)
        {
            var j = Json.Parse(json);
            var m = new DbceModFile();
            m.Schema = j.Str("schema");
            if (m.Schema != SchemaId) throw new FormatException("not a dbce-mod.json (schema " + SchemaId + ")");
            m.Id = j.Str("id"); m.Name = j.Str("name"); m.Version = j.Str("version");
            if (m.Id.Length == 0 || m.Name.Length == 0 || m.Version.Length == 0) throw new FormatException("dbce-mod.json needs id, name and version");
            m.SteamAppId = (long)j.Num("steamAppId", 0); m.Folder = j.Str("folder"); m.Exe = j.Strings("exe"); m.Repository = j.Str("repository");
            var log = j.Get("log");
            if (log != null) { m.LogFile = log.Str("file"); m.LogStart = log.Str("start"); }

            var pk = j.Get("package");
            if (pk == null || pk.Str("kind").Length == 0) throw new FormatException("dbce-mod.json needs a package kind");
            var p = m.Package;
            p.Kind = pk.Str("kind");
            var loader = pk.Get("loader");
            if (loader != null && !loader.IsNull) p.Loader = new ModLoader { Name = loader.Str("name"), Version = loader.Str("version"), Bundle = loader.Str("bundle") };
            p.Payload = FromTos(pk, "payload"); p.Optional = FromTos(pk, "optional"); p.SeedConfig = FromTos(pk, "seedConfig");
            p.Conflicts = pk.Strings("conflicts");
            foreach (var b in pk.Items("knownBuilds")) p.KnownBuilds.Add(new KnownBuild { File = b.Str("file"), Sha256 = b.Str("sha256"), Label = b.Str("label") });
            p.Strict = pk.Bool("strict");
            if (pk.Str("uiHost").Length > 0) p.UiHost = pk.Str("uiHost");

            var s = j.Get("settings");
            if (s != null && !s.IsNull)
            {
                var ms = m.Settings = new ModSettings
                {
                    Product = s.Str("product"), Version = s.Str("version"), ModPage = s.Str("modPage"), Keys = s.Str("keys"), Config = s.Str("config")
                };
                if (s.Str("format").Length > 0) ms.Format = s.Str("format");
                var pages = s.Get("pages");
                if (pages != null)
                    foreach (var page in pages.Members)
                        foreach (var r in page.Value.Elements) ms.Rows.Add(Row(page.Key, r, ms.Config));
            }

            var c = j.Get("controls");
            if (c != null && !c.IsNull)
            {
                var mc = m.Controls = new ModControls { Store = c.Str("store"), Profile = c.Str("profile"), Settings = c.Str("settings") };
                var oi = c.Get("optIn");
                if (oi != null && !oi.IsNull)
                    mc.OptIn = new ControlsOptIn { Section = oi.Str("section"), Key = oi.Str("key"), Value = oi.Str("value"), Default = oi.Str("default"), AppliesAfterRestart = oi.Bool("appliesAfterRestart") };
                mc.Delivered = c.Strings("delivered");
                var un = c.Get("unsupported");
                if (un != null) foreach (var kv in un.Members) mc.Unsupported.Add(new KeyValuePair<string, string>(kv.Key, kv.Value.Text));
                foreach (var x in c.Items("conditions"))
                    mc.Conditions.Add(new ControlsCondition
                    {
                        Actions = x.Strings("actions"), Transmission = x.Str("transmission"), SettingsKey = x.Str("settingsKey"),
                        SettingsValues = x.Strings("settingsValues"), SettingsDefault = x.Str("settingsDefault"), Note = x.Str("note")
                    });
                foreach (var g in c.Items("gameControls")) mc.GameControls.Add(new GameControl { Id = g.Str("id"), Kind = g.Str("kind"), Label = g.Str("label"), Group = g.Str("group") });
            }
            foreach (var cam in j.Items("cameras"))
                m.Cameras.Add(new CameraStart
                {
                    Camera = cam.Str("camera"), Start = cam.Str("start"), File = cam.Str("file"), Section = cam.Str("section"),
                    Key = cam.Str("key"), Realistic = cam.Str("realistic"), Game = cam.Str("game")
                });
            var t = j.Get("selfTest");
            if (t != null && !t.IsNull)
            {
                m.SelfTest = new ModSelfTest { Folder = t.Str("folder"), Steps = t.Strings("steps") };
                foreach (var pr in t.Items("probes")) m.SelfTest.Probes.Add(new SelfTestProbe { Id = pr.Str("id"), Type = pr.Str("type"), Label = pr.Str("label") });
            }
            var tt = j.Get("test");
            if (tt != null && !tt.IsNull)
            {
                var mt = m.Test = new ModTest { Protect = tt.Strings("protect") };
                var la = tt.Get("launch");
                if (la != null && !la.IsNull)
                    mt.Launch = new TestLaunch { SteamAppId = (long)la.Num("steamAppId", 0), Exe = la.Str("exe"), Args = la.Strings("args") };
                var ts = tt.Get("testSave");
                if (ts != null && !ts.IsNull) mt.TestSave = new FromTo { From = ts.Str("from"), To = ts.Str("to") };
                foreach (var st in tt.Items("states")) mt.States.Add(new TestState { State = st.Str("state"), Field = st.Str("field"), Is = st.Str("is"), Text = st.Str("text"), Picture = st.Str("picture") });
                var en = tt.Get("entry");
                if (en != null && !en.IsNull) mt.Entry = new TestEntry { Command = en.Str("command"), Route = en.Str("route") };
                foreach (var r in tt.Items("route")) mt.Route.Add(new RouteStep { Key = r.Str("key"), Input = r.Str("input"), WaitState = r.Str("waitState"), WaitText = r.Str("waitText"), Timeout = (long)r.Num("timeout", 0) });
                var qu = tt.Get("quit");
                if (qu != null && !qu.IsNull) mt.Quit = new TestQuit { Command = qu.Str("command"), Key = qu.Str("key"), Input = qu.Str("input") };
            }
            var i = j.Get("installed");
            if (i != null && !i.IsNull)
            {
                var mi = m.Installed = new ModInstalled
                {
                    Version = i.Str("version"), Commit = i.Str("commit"), InstalledUtc = i.Str("installedUtc"), Backups = i.Str("backups"),
                    LoaderInstalledByUs = i.Bool("loaderInstalledByUs")
                };
                foreach (var f in i.Items("files")) mi.Files.Add(new InstalledFile { Path = f.Str("path"), Sha256 = f.Str("sha256") });
                var fl = i.Get("flags");
                if (fl != null) foreach (var kv in fl.Members) mi.Flags.Add(new KeyValuePair<string, bool>(kv.Key, kv.Value.Kind == Json.Type.Bool && kv.Value.Flag));
            }
            return m;
        }

        static List<FromTo> FromTos(Json o, string name)
        {
            var list = new List<FromTo>();
            foreach (var e in o.Items(name)) list.Add(new FromTo { From = e.Str("from"), To = e.Str("to") });
            return list;
        }

        static SettingsRow Row(string page, Json r, string config)
        {
            var row = new SettingsRow
            {
                Page = page, Id = r.Str("id"), Kind = r.Str("kind"), Label = r.Str("label"), Help = r.Str("help"),
                Unit = r.Str("unit"), Text = r.Str("text"), Role = r.Str("role"), Choices = r.Strings("choices")
            };
            if (r.Str("level").Length > 0) row.Level = r.Str("level");
            if (r.Str("apply").Length > 0) row.Apply = r.Str("apply");
            bool numeric = row.Kind == "percent" || row.Kind == "slider" || row.Kind == "number";
            row.Min = r.Num("min", 0);
            row.Max = r.Num("max", row.Kind == "percent" ? 100 : 0);
            row.Decimals = (int)r.Num("decimals", 0);
            var d = r.Get("default");
            if (row.Kind == "toggle") row.Default = d != null && d.Kind == Json.Type.Bool && d.Flag ? 1 : 0;
            else if (row.Kind == "choice" && d != null) row.Default = d.Kind == Json.Type.String ? row.Choices.IndexOf(d.Text) : d.Number;
            else if (numeric) row.Default = d != null && d.Kind == Json.Type.Number ? d.Number : row.Min;
            var st = r.Get("store");
            if (st != null && !st.IsNull)
                row.Store = new RowStore
                {
                    File = st.Str("file").Length > 0 ? st.Str("file") : config, Section = st.Str("section"), Key = st.Str("key"),
                    On = st.Str("on"), Off = st.Str("off"), Scale = st.Num("scale", 1)
                };
            return row;
        }

        // ---- canonical dump (docs/DBCE-MOD-JSON.md) ----
        public string Dump()
        {
            var w = new Dumper();
            w.S("schema", Schema); w.S("id", Id); w.S("name", Name); w.S("version", Version);
            w.N("steamAppId", SteamAppId); w.S("folder", Folder); w.L("exe", Exe); w.S("repository", Repository);
            w.S("log.file", LogFile); w.S("log.start", LogStart);
            var p = Package;
            w.S("package.kind", p.Kind);
            if (p.Loader == null) w.Null("package.loader");
            else { w.S("package.loader.name", p.Loader.Name); w.S("package.loader.version", p.Loader.Version); w.S("package.loader.bundle", p.Loader.Bundle); }
            w.FT("package.payload", p.Payload); w.FT("package.optional", p.Optional); w.L("package.conflicts", p.Conflicts); w.FT("package.seedConfig", p.SeedConfig);
            w.N("package.knownBuilds.count", p.KnownBuilds.Count);
            for (int k = 0; k < p.KnownBuilds.Count; k++)
            {
                string q = "package.knownBuilds[" + k + "]";
                w.S(q + ".file", p.KnownBuilds[k].File); w.S(q + ".sha256", p.KnownBuilds[k].Sha256); w.S(q + ".label", p.KnownBuilds[k].Label);
            }
            w.B("package.strict", p.Strict); w.S("package.uiHost", p.UiHost);
            if (Settings == null) w.Null("settings");
            else
            {
                var s = Settings;
                w.S("settings.product", s.Product); w.S("settings.version", s.Version); w.S("settings.modPage", s.ModPage); w.S("settings.keys", s.Keys);
                w.S("settings.config", s.Config); w.S("settings.format", s.Format);
                w.N("settings.rows.count", s.Rows.Count);
                for (int k = 0; k < s.Rows.Count; k++)
                {
                    var r = s.Rows[k];
                    string q = "settings.rows[" + k + "]";
                    w.S(q + ".page", r.Page); w.S(q + ".id", r.Id); w.S(q + ".kind", r.Kind); w.S(q + ".label", r.Label); w.S(q + ".help", r.Help);
                    w.S(q + ".level", r.Level); w.S(q + ".apply", r.Apply); w.L(q + ".choices", r.Choices);
                    w.N(q + ".min", r.Min); w.N(q + ".max", r.Max); w.N(q + ".default", r.Default); w.S(q + ".unit", r.Unit); w.N(q + ".decimals", r.Decimals);
                    w.S(q + ".text", r.Text); w.S(q + ".role", r.Role);
                    if (r.Store == null) w.Null(q + ".store");
                    else
                    {
                        w.S(q + ".store.file", r.Store.File); w.S(q + ".store.section", r.Store.Section); w.S(q + ".store.key", r.Store.Key);
                        w.S(q + ".store.on", r.Store.On); w.S(q + ".store.off", r.Store.Off); w.N(q + ".store.scale", r.Store.Scale);
                    }
                }
            }
            if (Controls == null) w.Null("controls");
            else
            {
                var c = Controls;
                w.S("controls.store", c.Store); w.S("controls.profile", c.Profile); w.S("controls.settings", c.Settings);
                if (c.OptIn == null) w.Null("controls.optIn");
                else
                {
                    w.S("controls.optIn.section", c.OptIn.Section); w.S("controls.optIn.key", c.OptIn.Key); w.S("controls.optIn.value", c.OptIn.Value);
                    w.S("controls.optIn.default", c.OptIn.Default); w.B("controls.optIn.appliesAfterRestart", c.OptIn.AppliesAfterRestart);
                }
                w.L("controls.delivered", c.Delivered);
                w.N("controls.unsupported.count", c.Unsupported.Count);
                for (int k = 0; k < c.Unsupported.Count; k++)
                {
                    w.S("controls.unsupported[" + k + "].action", c.Unsupported[k].Key); w.S("controls.unsupported[" + k + "].reason", c.Unsupported[k].Value);
                }
                w.N("controls.conditions.count", c.Conditions.Count);
                for (int k = 0; k < c.Conditions.Count; k++)
                {
                    var x = c.Conditions[k];
                    string q = "controls.conditions[" + k + "]";
                    w.L(q + ".actions", x.Actions); w.S(q + ".transmission", x.Transmission); w.S(q + ".settingsKey", x.SettingsKey);
                    w.L(q + ".settingsValues", x.SettingsValues); w.S(q + ".settingsDefault", x.SettingsDefault); w.S(q + ".note", x.Note);
                }
                w.N("controls.gameControls.count", c.GameControls.Count);
                for (int k = 0; k < c.GameControls.Count; k++)
                {
                    var g = c.GameControls[k];
                    string q = "controls.gameControls[" + k + "]";
                    w.S(q + ".id", g.Id); w.S(q + ".kind", g.Kind); w.S(q + ".label", g.Label); w.S(q + ".group", g.Group);
                }
            }
            w.N("cameras.count", Cameras.Count);
            for (int k = 0; k < Cameras.Count; k++)
            {
                var m = Cameras[k];
                string q = "cameras[" + k + "]";
                w.S(q + ".camera", m.Camera); w.S(q + ".start", m.Start); w.S(q + ".file", m.File); w.S(q + ".section", m.Section);
                w.S(q + ".key", m.Key); w.S(q + ".realistic", m.Realistic); w.S(q + ".game", m.Game);
            }
            if (SelfTest == null) w.Null("selfTest");
            else
            {
                w.S("selfTest.folder", SelfTest.Folder); w.L("selfTest.steps", SelfTest.Steps);
                w.N("selfTest.probes.count", SelfTest.Probes.Count);
                for (int k = 0; k < SelfTest.Probes.Count; k++)
                {
                    string q = "selfTest.probes[" + k + "]";
                    w.S(q + ".id", SelfTest.Probes[k].Id); w.S(q + ".type", SelfTest.Probes[k].Type); w.S(q + ".label", SelfTest.Probes[k].Label);
                }
            }
            if (Test == null) w.Null("test");
            else
            {
                var tt = Test;
                if (tt.Launch == null) w.Null("test.launch");
                else { w.N("test.launch.steamAppId", tt.Launch.SteamAppId); w.S("test.launch.exe", tt.Launch.Exe); w.L("test.launch.args", tt.Launch.Args); }
                w.L("test.protect", tt.Protect);
                if (tt.TestSave == null) w.Null("test.testSave");
                else { w.S("test.testSave.from", tt.TestSave.From); w.S("test.testSave.to", tt.TestSave.To); }
                w.N("test.states.count", tt.States.Count);
                for (int k = 0; k < tt.States.Count; k++)
                {
                    string q = "test.states[" + k + "]";
                    w.S(q + ".state", tt.States[k].State); w.S(q + ".field", tt.States[k].Field); w.S(q + ".is", tt.States[k].Is);
                    w.S(q + ".text", tt.States[k].Text); w.S(q + ".picture", tt.States[k].Picture);
                }
                if (tt.Entry == null) w.Null("test.entry");
                else { w.S("test.entry.command", tt.Entry.Command); w.S("test.entry.route", tt.Entry.Route); }
                w.N("test.route.count", tt.Route.Count);
                for (int k = 0; k < tt.Route.Count; k++)
                {
                    string q = "test.route[" + k + "]";
                    w.S(q + ".key", tt.Route[k].Key); w.S(q + ".input", tt.Route[k].Input);
                    w.S(q + ".waitState", tt.Route[k].WaitState); w.S(q + ".waitText", tt.Route[k].WaitText);
                    w.N(q + ".timeout", tt.Route[k].Timeout);
                }
                if (tt.Quit == null) w.Null("test.quit");
                else { w.S("test.quit.command", tt.Quit.Command); w.S("test.quit.key", tt.Quit.Key); w.S("test.quit.input", tt.Quit.Input); }
            }
            if (Installed == null) w.Null("installed");
            else
            {
                var i = Installed;
                w.S("installed.version", i.Version); w.S("installed.commit", i.Commit); w.S("installed.installedUtc", i.InstalledUtc);
                w.B("installed.loaderInstalledByUs", i.LoaderInstalledByUs);
                w.N("installed.files.count", i.Files.Count);
                for (int k = 0; k < i.Files.Count; k++) { w.S("installed.files[" + k + "].path", i.Files[k].Path); w.S("installed.files[" + k + "].sha256", i.Files[k].Sha256); }
                w.S("installed.backups", i.Backups);
                w.N("installed.flags.count", i.Flags.Count);
                for (int k = 0; k < i.Flags.Count; k++) { w.S("installed.flags[" + k + "].name", i.Flags[k].Key); w.B("installed.flags[" + k + "].value", i.Flags[k].Value); }
            }
            return w.ToString();
        }

        sealed class Dumper
        {
            readonly StringBuilder sb = new StringBuilder();
            public void S(string p, string v) { sb.Append(p).Append('=').Append((v ?? "").Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n")).Append('\n'); }
            public void B(string p, bool v) { sb.Append(p).Append('=').Append(v ? "true" : "false").Append('\n'); }
            public void N(string p, double v) { sb.Append(p).Append('=').Append(v.ToString("R", CultureInfo.InvariantCulture)).Append('\n'); }
            public void Null(string p) { sb.Append(p).Append("=null\n"); }
            public void L(string p, List<string> list)
            {
                N(p + ".count", list.Count);
                for (int k = 0; k < list.Count; k++) S(p + "[" + k + "]", list[k]);
            }
            public void FT(string p, List<FromTo> list)
            {
                N(p + ".count", list.Count);
                for (int k = 0; k < list.Count; k++) { S(p + "[" + k + "].from", list[k].From); S(p + "[" + k + "].to", list[k].To); }
            }
            public override string ToString() { return sb.ToString(); }
        }
    }

    // A small RFC 8259 reader: objects keep member order; duplicate members are refused.
    internal sealed class Json
    {
        internal enum Type { Null, Bool, Number, String, Array, Object }
        internal Type Kind;
        internal bool Flag;
        internal double Number;
        internal string Text = "";
        internal readonly List<Json> Elements = new List<Json>();
        internal readonly List<KeyValuePair<string, Json>> Members = new List<KeyValuePair<string, Json>>();
        internal bool IsNull { get { return Kind == Type.Null; } }

        internal Json Get(string name)
        {
            if (Kind != Type.Object) return null;
            foreach (var kv in Members) if (kv.Key == name) return kv.Value;
            return null;
        }
        internal string Str(string name)
        {
            var v = Get(name);
            if (v == null || v.Kind == Type.Null) return "";
            if (v.Kind != Type.String) throw new FormatException("'" + name + "' must be a string");
            return v.Text;
        }
        internal double Num(string name, double fallback)
        {
            var v = Get(name);
            if (v == null || v.Kind == Type.Null) return fallback;
            if (v.Kind != Type.Number) throw new FormatException("'" + name + "' must be a number");
            return v.Number;
        }
        internal bool Bool(string name)
        {
            var v = Get(name);
            return v != null && v.Kind == Type.Bool && v.Flag;
        }
        internal List<Json> Items(string name)
        {
            var v = Get(name);
            return v != null && v.Kind == Type.Array ? v.Elements : new List<Json>();
        }
        internal List<string> Strings(string name)
        {
            var list = new List<string>();
            foreach (var e in Items(name)) list.Add(e.Text);
            return list;
        }

        internal static Json Parse(string text)
        {
            int at = 0;
            if (text.Length > 0 && text[0] == '\uFEFF') at = 1;
            var v = Value(text, ref at, 0);
            Space(text, ref at);
            if (at != text.Length) throw Bad(at, "text after the JSON value");
            return v;
        }
        static FormatException Bad(int at, string what) { return new FormatException("JSON: " + what + " at " + at); }
        static void Space(string t, ref int at) { while (at < t.Length && (t[at] == ' ' || t[at] == '\t' || t[at] == '\r' || t[at] == '\n')) at++; }
        static Json Value(string t, ref int at, int depth)
        {
            if (depth > 32) throw Bad(at, "nested too deeply");
            Space(t, ref at);
            if (at >= t.Length) throw Bad(at, "unexpected end");
            var j = new Json();
            char c = t[at];
            if (c == '{')
            {
                j.Kind = Type.Object; at++; Space(t, ref at);
                if (at < t.Length && t[at] == '}') { at++; return j; }
                for (;;)
                {
                    Space(t, ref at);
                    if (at >= t.Length || t[at] != '"') throw Bad(at, "expected a member name");
                    string key = String(t, ref at);
                    foreach (var kv in j.Members) if (kv.Key == key) throw Bad(at, "duplicate member '" + key + "'");
                    Space(t, ref at);
                    if (at >= t.Length || t[at] != ':') throw Bad(at, "expected ':'");
                    at++;
                    j.Members.Add(new KeyValuePair<string, Json>(key, Value(t, ref at, depth + 1)));
                    Space(t, ref at);
                    if (at < t.Length && t[at] == ',') { at++; continue; }
                    if (at < t.Length && t[at] == '}') { at++; return j; }
                    throw Bad(at, "expected ',' or '}'");
                }
            }
            if (c == '[')
            {
                j.Kind = Type.Array; at++; Space(t, ref at);
                if (at < t.Length && t[at] == ']') { at++; return j; }
                for (;;)
                {
                    j.Elements.Add(Value(t, ref at, depth + 1));
                    Space(t, ref at);
                    if (at < t.Length && t[at] == ',') { at++; continue; }
                    if (at < t.Length && t[at] == ']') { at++; return j; }
                    throw Bad(at, "expected ',' or ']'");
                }
            }
            if (c == '"') { j.Kind = Type.String; j.Text = String(t, ref at); return j; }
            if (Word(t, ref at, "true")) { j.Kind = Type.Bool; j.Flag = true; return j; }
            if (Word(t, ref at, "false")) { j.Kind = Type.Bool; return j; }
            if (Word(t, ref at, "null")) return j;
            int start = at;
            if (at < t.Length && t[at] == '-') at++;
            while (at < t.Length && (char.IsDigit(t[at]) || t[at] == '.' || t[at] == 'e' || t[at] == 'E' || t[at] == '+' || t[at] == '-')) at++;
            double n;
            if (at == start || !double.TryParse(t.Substring(start, at - start), NumberStyles.Float, CultureInfo.InvariantCulture, out n)) throw Bad(start, "bad value");
            j.Kind = Type.Number; j.Number = n;
            return j;
        }
        static bool Word(string t, ref int at, string w)
        {
            if (string.CompareOrdinal(t, at, w, 0, w.Length) != 0) return false;
            at += w.Length;
            return true;
        }
        static string String(string t, ref int at)
        {
            var sb = new StringBuilder();
            at++;
            while (at < t.Length && t[at] != '"')
            {
                char c = t[at++];
                if (c < ' ') throw Bad(at, "control character in a string");
                if (c != '\\') { sb.Append(c); continue; }
                if (at >= t.Length) break;
                char e = t[at++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (at + 4 > t.Length) throw Bad(at, "bad \\u escape");
                        sb.Append((char)int.Parse(t.Substring(at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        at += 4;
                        break;
                    default: throw Bad(at, "bad escape");
                }
            }
            if (at >= t.Length) throw Bad(at, "unterminated string");
            at++;
            return sb.ToString();
        }
    }
}
