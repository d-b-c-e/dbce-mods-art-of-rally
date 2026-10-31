namespace ArtOfSimRally.Mod.Triple;

// Owner 2026-10-31: whenever the camera changes, say which camera it is, its field of view and the keys that change it.
internal static class CameraOverlay
{
    internal enum Kind { Game, Bonnet, Bumper }

    internal static string Text(Kind kind, float fovDegrees, bool triples, bool realistic, bool tuningKeys, string fovUp, string fovDown)
    {
        if (kind == Kind.Game) return "Game camera · the game's field of view";
        var name = kind == Kind.Bonnet ? "Bonnet camera" : "Bumper camera";
        bool seat = triples && realistic;
        var text = name + " · field of view " + (seat ? "realistic (your seat)" : fovDegrees.ToString("0") + "°");
        if (tuningKeys && !seat) text += " · " + fovUp + " / " + fovDown + " adjust";
        if (triples) text += " · F7 " + (seat ? "game" : "realistic");
        return text;
    }
}
