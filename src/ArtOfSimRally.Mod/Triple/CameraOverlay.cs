namespace ArtOfSimRally.Mod.Triple;

// Owner 2026-10-31: on every camera change, a short name near the bottom ("Bonnet Camera - Game FOV", "Cockpit",
// "Chase 2") and a small reminder of the keys that change the field of view.
internal static class CameraOverlay
{
    internal enum Kind { Chase, Bonnet, Bumper, Cockpit }

    internal static bool Mounted(Kind kind) => kind != Kind.Chase;

    // "(Custom)" when the set's field of view was changed from stock (owner 2026-11-01); the reset key clears it.
    internal static string Name(Kind kind, int chaseNumber, bool realistic, bool custom = false)
    {
        var name = kind == Kind.Chase ? "Chase " + chaseNumber : kind == Kind.Bonnet ? "Bonnet Camera" : kind == Kind.Bumper ? "Bumper Camera" : "Cockpit";
        return name + (realistic ? " - Realistic" : " - Game FOV") + (custom ? " (Custom)" : "");
    }

    internal static string Hint(Kind kind, bool triples, bool realistic, bool tuningKeys)
    {
        var hint = tuningKeys ? "Numpad + / - field of view" : "";
        if (triples) hint += (hint.Length > 0 ? " · " : "") + "F7 " + (realistic ? "Game set" : "Realistic set");
        return hint;
    }
}
