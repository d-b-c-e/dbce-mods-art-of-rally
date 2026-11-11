// A game's controls from the shared store (docs/STORE.md), as the shim's [Controls] profile (ini-controls-1,
// docs/controls-contract.md). A mod builds it at start from DbceStore.For(gameId) and hands the text to the shim, instead
// of reading a dbce-controls.ini that Wheelkit wrote (CFG-2; dbce-project-mgmt docs/architecture/2026-11/
// 08-install-and-configuration.md item 7). The rules are those of Wheelkit's SharedControlsWriter, which this replaces:
//  - only the actions the build delivers are written, in the canonical action order;
//  - gears follow the profile's Transmission: shift up/down for Sequential, gates for H-pattern, none otherwise;
//    two gear actions on one control write no gear binding;
//  - no text at all without a usable steering binding (the shim would own the wheel with nothing to steer).
// The consumer's own parser (ControlProfileDocument, the native shim) still checks every binding it reads.
// Engine-free and LINQ-free so Mono 2.0 plugins compile it as source.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dbce.Wheel.Mod
{
    public static class DbceStoreControls
    {
        /// <summary>The canonical action ids, in the order the profile writes them (docs/controls-contract.md).</summary>
        public static readonly string[] Actions = {
            "steer", "throttle", "brake", "clutch", "handbrake", "shiftUp", "shiftDown", "neutral", "reverse",
            "confirm", "back", "start", "select", "navUp", "navDown", "navLeft", "navRight",
            "camera", "lookBack", "reset", "horn", "modMenu", "muteFfb",
            "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8" };
        static readonly string[] Shifts = { "shiftUp", "shiftDown" };
        static readonly string[] Gates = { "neutral", "reverse", "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8" };

        /// <summary>The [Controls] text for <paramref name="profile"/>, limited to the actions this build delivers. Null,
        /// with the reason in <paramref name="why"/>, when the profile has no steering the build can use or its name does
        /// not fit the shim (128 UTF-8 bytes, no control characters). <paramref name="why"/> also lists bindings left out.</summary>
        public static string Text(StoreProfile profile, ICollection<string> delivered, out string why)
        {
            var skipped = new List<string>();
            var lines = new Dictionary<string, string>();
            var keys = new Dictionary<string, string>();
            foreach (var b in profile.Bindings)
            {
                if (!delivered.Contains(b.Action) || Array.IndexOf(Actions, b.Action) < 0) continue;
                string key;
                string text = Binding(b, out key);
                if (text == null) { skipped.Add(b.Action); continue; }
                lines[b.Action] = text; keys[b.Action] = key;
            }
            if (!Fits(profile.Name) || !Fits(profile.Transmission))
            { why = "the profile name or transmission must fit 128 UTF-8 bytes without control characters"; return null; }
            if (!lines.ContainsKey("steer")) { why = "the profile " + profile.Name + " has no steering this game can use"; return null; }

            string[] used;
            switch (profile.Transmission)
            {
                case "Sequential": Drop(lines, Gates); used = Shifts; break;
                case "H-pattern":
                    Drop(lines, Shifts); used = Gates;
                    bool gate = false;
                    foreach (var g in Gates) if (g != "neutral" && lines.ContainsKey(g)) gate = true;
                    if (!gate) Drop(lines, Gates); // neutral alone selects no gear
                    break;
                default: Drop(lines, Shifts); Drop(lines, Gates); used = new string[0]; break;
            }
            // One physical control selects one gear: two gear actions on one control write neither.
            var seen = new Dictionary<string, string>();
            var clash = new List<string>();
            foreach (var a in used)
            {
                if (!lines.ContainsKey(a)) continue;
                string other;
                if (seen.TryGetValue(keys[a], out other)) { clash.Add(a); clash.Add(other); } else seen[keys[a]] = a;
            }
            if (clash.Count > 0) { Drop(lines, used); skipped.Add("gears (two share one control)"); }

            var body = new StringBuilder();
            body.Append("Profile = ").Append(profile.Name).Append("\r\n");
            string revision = Revision(profile, lines);
            body.Append("Revision = ").Append(revision).Append("\r\n");
            body.Append("Transmission = ").Append(profile.Transmission).Append("\r\n");
            foreach (var a in Actions)
            {
                string line;
                if (lines.TryGetValue(a, out line)) body.Append(a).Append(" = ").Append(line).Append("\r\n");
            }
            why = skipped.Count == 0 ? "" : "not written: " + string.Join(", ", skipped.ToArray());
            return "[Controls]\r\nSchema = 1\r\n" + body;
        }

        /// <summary>One binding in the ini-controls-1 grammar (schema 1: calibration points are not written), or null when it
        /// lacks a device identity, a device name has control characters, or an axis has no travel direction.</summary>
        static string Binding(StoreBinding b, out string key)
        {
            key = null;
            string kind = b.Kind.ToLowerInvariant();
            if (kind != "axis" && kind != "button" && kind != "hat") return null;
            Guid device, product;
            if (!Id(b.InstanceGuid, out device) || !Id(b.ProductGuid, out product)) return null;
            foreach (char c in b.DeviceName) if (char.IsControl(c)) return null;
            var t = new StringBuilder(kind).Append(' ').Append(N(b.Index));
            if (kind == "hat") t.Append(' ').Append(N(b.Direction));
            t.Append(" dev=").Append(device.ToString("B")).Append(" prod=").Append(product.ToString("B"));
            if (kind == "axis")
            {
                if (b.Direction != 1 && b.Direction != -1) return null;
                t.Append(" range=").Append(N(b.Minimum)).Append("..").Append(N(b.Maximum)).Append(" rest=").Append(N(b.Rest))
                    .Append(b.Direction == 1 ? " travel=+1" : " travel=-1");
                if (b.Inverted) t.Append(" inverted");
            }
            if (b.Calibrated) t.Append(" calibrated");
            if (b.DeviceName.Length != 0) t.Append(" name=\"").Append(b.DeviceName.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            key = device.ToString("B") + "|" + kind + "|" + N(b.Index) + "|" + (kind == "hat" ? N(b.Direction) : "-1");
            return t.ToString();
        }

        static bool Id(string text, out Guid guid)
        {
            guid = Guid.Empty;
            if (string.IsNullOrEmpty(text)) return false;
            try { guid = new Guid(text); } catch (FormatException) { return false; } catch (OverflowException) { return false; }
            return guid != Guid.Empty;
        }

        static bool Fits(string value)
        {
            if (value == null) return false;
            foreach (char c in value) if (char.IsControl(c)) return false;
            return Encoding.UTF8.GetByteCount(value) <= 128;
        }

        static void Drop(Dictionary<string, string> lines, string[] actions) { foreach (var a in actions) lines.Remove(a); }

        static string N(int n) { return n.ToString(CultureInfo.InvariantCulture); }

        // Identifies what the shim was given: the same profile and bindings give the same revision on every start.
        static string Revision(StoreProfile profile, Dictionary<string, string> lines)
        {
            var s = new StringBuilder(profile.Id).Append('\n').Append(profile.Name).Append('\n').Append(profile.Transmission).Append('\n');
            foreach (var a in Actions)
            {
                string line;
                if (lines.TryGetValue(a, out line)) s.Append(a).Append('=').Append(line).Append('\n');
            }
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(s.ToString()));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }
    }
}
