using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Dbce.Wheel.Mod;

namespace ArtOfSimRally.Mod
{
    // art of rally's controls from the shared store (CFG-2; dbce-project-mgmt docs/architecture/2026-11/
    // 08-install-and-configuration.md). Wheelkit assigns each game a profile and is the store's only editor. When the
    // store exists, art's profile replaces dbce-controls.ini: its [Controls] text goes to the shim and its transmission
    // to TransmissionMode, as Wheelkit's own art writer used to do.
    internal sealed class StoreProfileSource
    {
        internal const string GameId = "art-of-rally";
        // Every canonical action but select, which art has no consumer for (build.json's delivered list).
        internal static readonly string[] Delivered = Without(DbceStoreControls.Actions, "select");
        static readonly string[] Gears = { "reverse", "gear1", "gear2", "gear3", "gear4", "gear5", "gear6", "gear7", "gear8" };

        internal string Name, Transmission, Why;
        // The shim's [Controls] text as UTF-8, or null when the profile has no steering art can use (Why says why).
        internal byte[] Profile;
        // The TransmissionMode the profile selects, or null when its gear bindings cannot drive that gearbox (the
        // player's own setting stays).
        internal string Gearbox;

        /// <summary>Art's profile from the store at <paramref name="profilesPath"/>, or null without a store or profiles.</summary>
        internal static StoreProfileSource Read(string profilesPath)
        {
            if (!File.Exists(profilesPath)) return null;
            var store = DbceStore.Load(profilesPath);
            if (store.Profiles.Count == 0) return null;
            var p = store.For(GameId).Profile;
            string why;
            string text = DbceStoreControls.Text(p, Delivered, out why);
            return new StoreProfileSource
            {
                Name = p.Name, Transmission = p.Transmission, Why = why,
                Profile = text == null ? null : Encoding.UTF8.GetBytes(text),
                Gearbox = text == null ? null : GearboxFor(text, p.Transmission),
            };
        }

        // Wheelkit's rule: Sequential needs a shift bound, H-pattern a gear gate (neutral alone selects nothing); the
        // converter has already dropped gears that share one control. Anything else is Automatic.
        internal static string GearboxFor(string text, string transmission)
        {
            if (transmission == "Sequential") return Has(text, "shiftUp") || Has(text, "shiftDown") ? transmission : null;
            if (transmission == "H-pattern")
            {
                foreach (var g in Gears) if (Has(text, g)) return transmission;
                return null;
            }
            return "Automatic";
        }

        static bool Has(string text, string action) => text.IndexOf("\r\n" + action + " = ", StringComparison.Ordinal) >= 0;

        static string[] Without(string[] all, string drop)
        {
            var kept = new List<string>();
            foreach (var a in all) if (a != drop) kept.Add(a);
            return kept.ToArray();
        }
    }
}
