namespace TurtleVan
{
    // Keep the extra menu slot out of native saves and season data. This state
    // belongs to the running mod only; native persistence always sees the donor.
    internal sealed class SelectionState
    {
        internal readonly int Slot, Donor;
        internal bool Chosen { get; private set; }
        internal SelectionState(int slot, int donor) { Slot = slot; Donor = donor; }
        internal int Choose(int index, bool matchingClass)
        {
            Chosen = matchingClass && index == Slot;
            return Chosen ? Donor : index;
        }
        internal int ToSavedIndex(int index) => index == Slot ? Donor : index;
    }
}
