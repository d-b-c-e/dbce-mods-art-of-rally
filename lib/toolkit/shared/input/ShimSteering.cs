// Vendorable source (netstandard and Unity Mono consumers): the force wheel's identity from a shared [Controls]
// section that native Core accepted byte for byte. No devices, OS input, file I/O or force calls, and no second
// Controls grammar: the steer value is read only by ControlBinding. See
// knowledge/SHIM-IDENTITY-AND-NATIVE-ACTIONS-2026-10-11.md for the FFB target lifecycle that consumes it.
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Dbce.Wheel.Input
{
    public static class ShimSteering
    {
        public const string NoSteering = "The shared controls profile has no usable steering binding. Check it and restart.";
        public const string UnsupportedSchema = "The shared controls profile's Schema is missing or not one this mod reads. Check it and restart.";

        /// <summary>
        /// Throws <see cref="InvalidDataException"/> unless Core's profile report (dbce_shim_profile_info) says ok=1 for
        /// exactly these bytes: its sha256 equals the SHA-256 of <paramref name="section"/> (any case), with no repeated
        /// field. Core's ok=1 says the bytes parsed, not that the section applied: a reader at schema 1 reports ok=1 for a
        /// refused Schema 2 section. So this helper checks the schema itself, against <paramref name="reads"/>, the highest
        /// schema the caller's readers all apply (1 by default; 2 only when Core and the input reader also opt in).
        /// Returns null with <see cref="UnsupportedSchema"/> when the section has no Schema line, a malformed one, or one
        /// outside 1..reads (the last Schema line wins, as parseSection reads it; Core already refuses a repeated key). Otherwise returns the single steer line's binding,
        /// parsed in that schema (Device, Product, Name, and at schema 2 its Cal), or null with <see cref="NoSteering"/>
        /// when the bytes are not UTF-8, there is no steer line or more than one, or the value does not parse, does not fit
        /// steer, or names no device. A missing identity never fails the caller's input. Product policy is the consumer's:
        /// an empty product GUID is returned as written.
        /// </summary>
        public static ControlBinding? FromAccepted(byte[] section, string coreInfo, out string reason, int reads = 1)
        {
            if (section == null) throw new ArgumentNullException(nameof(section));
            if (coreInfo == null) throw new ArgumentNullException(nameof(coreInfo));
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in coreInfo.Split('\n'))
            {
                int at = line.IndexOf('=');
                if (at < 0) continue;
                string key = line.Substring(0, at);
                if (fields.ContainsKey(key)) throw new InvalidDataException("Duplicate Core profile field.");
                fields[key] = line.Substring(at + 1).TrimEnd('\r');
            }
            string hash;
            using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(section)).Replace("-", "");
            if (!fields.TryGetValue("ok", out var ok) || ok != "1" || !fields.TryGetValue("sha256", out var accepted) ||
                !string.Equals(hash, accepted, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Core did not accept the exact Controls bytes.");
            string text;
            try { text = new UTF8Encoding(false, true).GetString(section); }
            catch (DecoderFallbackException) { reason = NoSteering; return null; }
            string? value = null, schemaText = null;
            int count = 0;
            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                if (key == "Schema") { schemaText = line.Substring(eq + 1).Trim(); continue; }   // the last one wins, as parseSection (Core refuses a repeated key)
                if (key != "steer") continue;
                count++;
                value = line.Substring(eq + 1).Trim();
            }
            // Core's integer rule (dbce_controls.hpp detail::integer); a schema the caller's readers do not all apply gives
            // no target, even though Core reported ok=1 for the bytes.
            if (schemaText == null || !ControlBinding.Integer(schemaText, out int schema) || schema < 1 || schema > (reads >= 2 ? 2 : 1))
            { reason = UnsupportedSchema; return null; }
            if (count != 1 || value == null || !ControlBinding.TryParse(value, out var binding, out _, schema) || binding == null ||
                binding.Fits("steer") != "" || binding.Device == Guid.Empty)
            { reason = NoSteering; return null; }
            reason = "";
            return binding;
        }
    }
}
