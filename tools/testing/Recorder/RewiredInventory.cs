using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ArtOfSimRally.Testing
{
    /// <summary>Diagnostic only (sana 44749/44771): names every joystick Rewired lists, so a shared-input refusal
    /// "Rewired identity/path unavailable" can be traced to its row. Read-only reflection on the live Rewired objects:
    /// each joystick's simple public properties (name, instance and hardware identifiers, connection), every member whose
    /// value is a controller extension (its runtime type; a RawInput extension's devicePath and productGuid), and whether
    /// the mod's rule would accept the row (instance, product and path all present). Only when the recording asks for it
    /// (rewiredInventory=1, sana 47008): written twice to rewired-inventory.tsv, at the first frame Rewired is ready and
    /// once 5 s later (RecordingDiagnostics schedules the passes). Never changes input.</summary>
    internal static class RewiredInventory
    {
        internal const string Header = "pass\tseconds\tindex\tname\tdeviceInstanceGuid\tproductGuid\tinterfacePath\tacceptedByModRule\textensions\tproperties";

        /// <summary>Writes one pass when the request's RecordingDiagnostics says it is due (opt-in, rewiredInventory=1;
        /// sana 47008); returns the event line.</summary>
        internal static string Write(string dir, int pass, float now)
        {
            if (dir == null) throw new InvalidOperationException("no tape folder");
            var path = Path.Combine(dir, "rewired-inventory.tsv");
            var rows = Rows(pass, now);
            bool header = !File.Exists(path);
            using (var w = new StreamWriter(path, true, new UTF8Encoding(false)))
            {
                if (header) w.WriteLine(Header);
                foreach (var r in rows) w.WriteLine(r);
            }
            if (IdentityProbe.Armed) IdentityProbe.Probe("inventory pass " + pass);
            return "rewired inventory pass " + pass + ": " + (rows.Count) + " joystick(s)";
        }

        private static string Clean(object v) => (v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture)).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        private static List<string> Rows(int pass, float now)
        {
            var rows = new List<string>(); int index = 0;
            foreach (var joystick in Rewired.ReInput.controllers.Joysticks)
            {
                if (joystick == null) { rows.Add(pass + "\t" + now.ToString("F1", CultureInfo.InvariantCulture) + "\t" + index++ + "\t(null)"); continue; }
                var props = new List<string>(); string devicePath = null, productGuid = null; var extensions = new List<string>();
                foreach (var p in joystick.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (p.GetIndexParameters().Length != 0) continue;
                    var t = p.PropertyType;
                    bool simple = t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(Guid);
                    bool extensionLike = !simple && t.Name.IndexOf("Extension", StringComparison.Ordinal) >= 0;
                    if (!simple && !extensionLike) continue;
                    object value;
                    try { value = p.GetValue(joystick, null); } catch (Exception ex) { props.Add(p.Name + "=!" + (ex.InnerException ?? ex).GetType().Name); continue; }
                    if (simple) props.Add(p.Name + "=" + Clean(value));
                    else if (value != null) extensions.Add(p.Name + ":" + value.GetType().FullName);
                }
                // The official list (sana 44832): Controller.Extensions holds every extension (.extension only the first).
                // Each one's runtime type and its identity members where the type has them: interfacePath (DirectInput),
                // devicePath and productGuid (RawInput), userIndex (XInput).
                var all = joystick.GetType().GetProperty("Extensions", BindingFlags.Public | BindingFlags.Instance);
                if (all == null) extensions.Add("Extensions:property-absent");
                else
                {
                    object list = null;
                    try { list = all.GetValue(joystick, null); } catch (Exception ex) { extensions.Add("Extensions:!" + (ex.InnerException ?? ex).GetType().Name); }
                    if (list is System.Collections.IEnumerable items)
                    {
                        int n = 0;
                        foreach (var ext in items)
                        {
                            n++;
                            if (ext == null) { extensions.Add("Extensions[" + n + "]:null"); continue; }
                            var et = ext.GetType(); var parts = new List<string> { "Extensions[" + n + "]:" + et.FullName };
                            foreach (var member in new[] { "interfacePath", "devicePath", "productGuid", "userIndex", "deviceInstanceGuid", "hardwareIdentifier" })
                            {
                                var mp = et.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
                                if (mp == null) continue;
                                try { parts.Add(member + "=" + Clean(mp.GetValue(ext, null))); } catch (Exception ex) { parts.Add(member + "=!" + (ex.InnerException ?? ex).GetType().Name); }
                            }
                            extensions.Add(string.Join(",", parts));
                        }
                        if (n == 0) extensions.Add("Extensions:empty");
                    }
                    else if (list != null) extensions.Add("Extensions:not-enumerable " + list.GetType().FullName);
                }
                // Every field (any visibility, through the base types) whose value is an extension or source object: its
                // runtime type, so coexisting extensions and the backend show even without a public property.
                for (var type = joystick.GetType(); type != null && type != typeof(object); type = type.BaseType)
                    foreach (var fld in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    {
                        object value;
                        try { value = fld.GetValue(joystick); } catch (Exception ex) { extensions.Add(type.Name + "." + fld.Name + ":!" + ex.GetType().Name); continue; }
                        if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum) continue;
                        string vt = value.GetType().FullName ?? value.GetType().Name;
                        if (vt.IndexOf("Extension", StringComparison.Ordinal) >= 0 || vt.IndexOf("Source", StringComparison.Ordinal) >= 0 || vt.IndexOf("RawInput", StringComparison.Ordinal) >= 0 ||
                            vt.IndexOf("XInput", StringComparison.Ordinal) >= 0 || vt.IndexOf("DirectInput", StringComparison.Ordinal) >= 0 || vt.IndexOf("Hid", StringComparison.Ordinal) >= 0)
                            extensions.Add("field " + type.Name + "." + fld.Name + ":" + vt);
                    }
                // Named extension probes: the DualShock 4 extension may coexist with the RawInput one (sana 44811).
                foreach (var m in joystick.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "GetExtension" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0))
                {
                    var ds4 = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Rewired.ControllerExtensions.DualShock4Extension", false)).FirstOrDefault(x => x != null);
                    if (ds4 == null) { extensions.Add("DualShock4Extension:type-not-loaded"); break; }
                    try { extensions.Add("DualShock4Extension:" + (m.MakeGenericMethod(ds4).Invoke(joystick, null) != null ? "present" : "absent")); }
                    catch (Exception ex) { extensions.Add("DualShock4Extension:!" + (ex.InnerException ?? ex).GetType().Name); }
                    break;
                }
                // The mod reads the RawInput extension by GetExtension<RawInputControllerExtension>(): look for it by name.
                foreach (var m in joystick.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == "GetExtension" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0))
                {
                    var rawType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Rewired.Platforms.Windows.RawInput.RawInputControllerExtension", false)).FirstOrDefault(x => x != null);
                    if (rawType == null) { extensions.Add("RawInputControllerExtension:type-not-loaded"); break; }
                    object raw = null;
                    try { raw = m.MakeGenericMethod(rawType).Invoke(joystick, null); } catch (Exception ex) { extensions.Add("RawInputControllerExtension:!" + (ex.InnerException ?? ex).GetType().Name); }
                    if (raw != null)
                    {
                        extensions.Add("RawInputControllerExtension:present");
                        try { devicePath = Clean(rawType.GetProperty("devicePath")?.GetValue(raw, null)); } catch (Exception ex) { devicePath = "!" + ex.GetType().Name; }
                        try { productGuid = Clean(rawType.GetProperty("productGuid")?.GetValue(raw, null)); } catch (Exception ex) { productGuid = "!" + ex.GetType().Name; }
                    }
                    else extensions.Add("RawInputControllerExtension:absent");
                    break;
                }
                string instance = Clean(joystick.deviceInstanceGuid);
                bool accepted = Guid.TryParse(instance, out var g) && g != Guid.Empty && Guid.TryParse(productGuid ?? "", out var pg) && pg != Guid.Empty && !string.IsNullOrEmpty(devicePath);
                rows.Add(string.Join("\t", new[] { pass.ToString(CultureInfo.InvariantCulture), now.ToString("F1", CultureInfo.InvariantCulture), (index++).ToString(CultureInfo.InvariantCulture),
                    Clean(joystick.name), instance, productGuid ?? "", devicePath ?? "", accepted ? "1" : "0", string.Join(";", extensions), string.Join(";", props) }));
            }
            return rows;
        }
    }
}
