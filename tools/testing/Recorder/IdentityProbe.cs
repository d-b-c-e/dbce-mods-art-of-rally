using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;

namespace ArtOfSimRally.Testing
{
    /// <summary>Diagnostic only, opt-in by the request key identityProbe=1 (sana 45604/45712). Two read-only observations:
    /// - the mod's identity mapper's own inputs: a Harmony prefix on ProbeIdentityMapper.Match records the owned identities
    ///   (instance, product, generation, interface path) and the Rewired rows it is given, then lets Match run unchanged;
    /// - for each Rewired joystick whose extension is the DualShock 4 one, the exact pinned private chain to the HID device
    ///   information object, read by field only (no property getter, no method, no IntPtr dereference), and its six String
    ///   fields. Each value is compared, case-insensitive ordinal as the mapper compares paths, with the owned paths seen.
    /// The chain is walked only when the loaded Rewired_Core and Rewired_Windows hashes equal the reviewed pair; a missing
    /// member or a differing runtime type is recorded and the walk stops. Nothing here changes matching.</summary>
    internal static class IdentityProbe
    {
        internal const string CoreSha256 = "2DD147A1E7EA3A8ED7FECE35EA6554784ED3F039F5E291F3F0B6F877379B0655";
        internal const string WindowsSha256 = "4389BCA4D00D68B600972FCD2A0D5A21208517DF7893A6A4602E740D6691417C";
        // The reviewed chain (metadata of the pinned pair): holder field, its declaring type, the expected runtime type name(s).
        internal static readonly (string Field, string Declaring, string[] Runtime)[] Chain =
        {
            ("gAsUGTlCEtTpsEhPucurQSACKleL", "Rewired.Controller", new[] { "DualShock4Extension" }),
            ("sUHnBwREVEYFPxdqqIRaVAMCiTIW", "Rewired.ControllerExtensions.DualShock4Extension", new[] { "bCAGPoVsjuhfnJyRuyREpdPDdAUT" }),
            ("QYKkhsALSllcmVrwQkNAjMiAhLuHA", "bCAGPoVsjuhfnJyRuyREpdPDdAUT", new[] { "DualShock4Driver" }),
            ("oNZzuYgyerFmIKdVWnZydeacAQSD", "Rewired.HID.Drivers.DualShock4Driver", new[] { "hGVykObfUvbkhQHpTnWBQJESEppf" }),
            ("oNZzuYgyerFmIKdVWnZydeacAQSD", "hGVykObfUvbkhQHpTnWBQJESEppf", new[] { "vUeJqYJdCDgSVsLYxtlhrEubOqiC", "eOjwogCoQuQOjGajnUgPmfeWSfsn" }),
        };
        internal static readonly string[] InfoStrings =
            { "htbZfJjNCYJKUSJuvqPeJYNDWTGr", "dCANkCDaitOZGMJDNGKkRJJgljXi", "jpuXGTBmLPeSeQwZxyECTTpTuztR", "BLoqJitbNDueVQUkUpsUeZmzJVBq", "ZUftjyyaoMyccyCxbMflZMyteTpy", "JGSKEokrVlftbwJIdtFutjHAgNtP" };

        private static Func<string> _dir;
        private static readonly List<string> _owned = new List<string>();
        internal static bool Armed => _dir != null;

        internal static void Arm(Harmony harmony, Func<string> dir)
        {
            _dir = dir;
            var match = AccessTools.Method("ArtOfSimRally.Mod.Shim.ProbeIdentityMapper:Match") ?? throw new MissingMethodException("ProbeIdentityMapper.Match");
            harmony.Patch(match, prefix: new HarmonyMethod(typeof(IdentityProbe), nameof(ObserveMatch)) { priority = Priority.First });
        }

        // Harmony prefix: reads Match's arguments, always lets the original run.
        private static void ObserveMatch(Guid[] required, object owned, object rewired)
        {
            try
            {
                var rows = new List<string>();
                foreach (var id in required ?? new Guid[0]) rows.Add("required\t" + id.ToString("D"));
                lock (_owned)
                {
                    foreach (var item in (owned as System.Collections.IEnumerable) ?? new object[0])
                    {
                        string path = Read(item, "InterfacePath") as string;
                        if (!string.IsNullOrEmpty(path) && !_owned.Contains(path, StringComparer.OrdinalIgnoreCase)) _owned.Add(path);
                        rows.Add("owned\t" + Clean(Read(item, "Instance")) + "\t" + Clean(Read(item, "Product")) + "\t" + Clean(Read(item, "Generation")) + "\t" + Clean(path));
                    }
                }
                foreach (var row in (rewired as System.Collections.IEnumerable) ?? new object[0])
                    rows.Add("rewired\t" + Clean(Read(row, "Name")) + "\t" + Clean(Read(row, "Instance")) + "\t" + Clean(Read(row, "ProductGuid")) + "\t" + Clean(Read(row, "InterfacePath")) +
                        "\t" + Clean(Read(row, "IdentityPathError")));
                Append("identity-match.tsv", "kind\ta\tb\tc\td\te", rows);
                Probe("at Match");
            }
            catch (Exception ex) { try { Append("identity-match.tsv", "kind\ta\tb\tc\td\te", new[] { "error\t" + Clean(ex.ToString()) }); } catch { } }
        }

        private static object Read(object o, string field)
        {
            if (o == null) return null;
            var f = o.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return f?.GetValue(o);
        }

        internal static string Clean(object v) => (v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture)).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        private static void Append(string name, string header, IEnumerable<string> rows)
        {
            var dir = _dir?.Invoke(); if (dir == null) return;
            var path = Path.Combine(dir, name); bool head = !File.Exists(path);
            using (var w = new StreamWriter(path, true, new UTF8Encoding(false))) { if (head) w.WriteLine(header); foreach (var r in rows) w.WriteLine(r); }
        }

        private static string FileSha256(Assembly a)
        {
            try { using (var s = File.OpenRead(a.Location)) using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", ""); }
            catch (Exception ex) { return "!" + ex.GetType().Name; }
        }

        private static FieldInfo FieldOn(Type type, string declaring, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
                if (t.FullName == declaring || t.Name == declaring)
                    return t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            return null;
        }

        /// <summary>One pass over the Rewired joysticks: the pinned chain for each DualShock 4 row, into ds4-identity-probe.tsv.</summary>
        internal static void Probe(string when)
        {
            if (!Armed || !Rewired.ReInput.isReady) return;
            var core = typeof(Rewired.ReInput).Assembly;
            var windows = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Rewired_Windows");
            string coreHash = FileSha256(core), windowsHash = windows == null ? "not-loaded" : FileSha256(windows);
            bool pinned = coreHash == CoreSha256 && windowsHash == WindowsSha256;
            string[] owned; lock (_owned) owned = _owned.ToArray();
            var rows = new List<string>(); int index = 0;
            foreach (var joystick in Rewired.ReInput.controllers.Joysticks)
            {
                int i = index++;
                if (joystick == null) continue;
                var steps = new List<string>(); object current = joystick; string stop = null;
                if (!pinned) stop = "assembly hashes differ from the reviewed pair; chain not walked";
                for (int s = 0; stop == null && s < Chain.Length; s++)
                {
                    var (field, declaring, runtime) = Chain[s];
                    var info = FieldOn(current.GetType(), declaring, field);
                    if (info == null) { stop = "step " + s + ": field " + declaring + "." + field + " absent on " + current.GetType().FullName; break; }
                    object next;
                    try { next = info.GetValue(current); } catch (Exception ex) { stop = "step " + s + ": read failed " + ex.GetType().Name; break; }
                    if (next == null) { stop = "step " + s + ": " + field + " is null"; break; }
                    steps.Add(field + "->" + next.GetType().FullName);
                    if (!runtime.Contains(next.GetType().Name))
                    {
                        // Step 0 on a non-DS4 joystick is not an error: the row is simply not a DualShock 4.
                        stop = s == 0 ? "not a DualShock 4 extension row" : "step " + s + ": runtime type " + next.GetType().FullName + " is not the reviewed one";
                        break;
                    }
                    current = next;
                }
                var strings = new List<string>(); var equal = new List<string>();
                if (stop == null)
                    foreach (var name in InfoStrings)
                    {
                        var f = current.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (f == null || f.FieldType != typeof(string)) { strings.Add(name + "=!absent-or-not-string"); continue; }
                        var value = f.GetValue(current) as string;
                        strings.Add(name + "=" + Clean(value));
                        if (!string.IsNullOrEmpty(value)) foreach (var o in owned) if (string.Equals(value, o, StringComparison.OrdinalIgnoreCase)) equal.Add(name + "==owned:" + o);
                    }
                if (stop == "not a DualShock 4 extension row") continue;
                rows.Add(string.Join("\t", new[] { when, i.ToString(CultureInfo.InvariantCulture), Clean(joystick.name), Clean(joystick.deviceInstanceGuid), coreHash, windowsHash, pinned ? "1" : "0",
                    string.Join(";", steps), stop ?? "", string.Join(";", strings), owned.Length.ToString(CultureInfo.InvariantCulture), string.Join(";", equal) }));
            }
            Append("ds4-identity-probe.tsv", "when\tindex\tname\tdeviceInstanceGuid\tcoreSha256\twindowsSha256\tpinned\tsteps\tstopped\tstrings\townedPathsSeen\tequalsOwned", rows);
        }
    }
}
