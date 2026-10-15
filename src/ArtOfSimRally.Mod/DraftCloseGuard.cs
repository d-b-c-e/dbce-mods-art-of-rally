namespace ArtOfSimRally.Mod
{
    /// <summary>
    /// One close intent with unsaved shared-binding edits (kiki 123963). The first close is held once and the panel
    /// says why; every later close in the same open session goes through, until the panel actually hides. Both close
    /// paths (Main.CloseSettings and UMM's ToggleWindow guard) ask this, so evaluating the same intent twice never re-arms
    /// the warning. Closing never saves the draft. Pure: no Unity, no files.
    /// </summary>
    internal static class DraftCloseGuard
    {
        internal static bool Warned { get; private set; }

        /// <summary>True when this close must be held: a draft exists and the player has not been told in this session.</summary>
        internal static bool HoldClose(bool hasDraft)
        {
            if (!hasDraft) { Warned = false; return false; }
            if (Warned) return false;
            Warned = true;
            return true;
        }

        /// <summary>The panel hid: the next close with a draft is held again.</summary>
        internal static void Reset() => Warned = false;
    }
}
