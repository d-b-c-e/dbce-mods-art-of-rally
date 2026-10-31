using System.Collections.Generic;
namespace RallyCustomVehicles
{
    // The Free Roam "Custom" class: menu-only. Its list position is the next value after the stock classes, and its
    // remembered pick is the package id (the numeric slot changes when packages are added or removed).
    internal static class CustomClassMenu
    {
        internal static int Clamp(int index, int count) => index >= 0 && index < count ? index : 0;

        internal static int SlotFor(string id, int saved, IList<string> current)
        {
            int found = id == null ? -1 : current.IndexOf(id);
            return found >= 0 ? found : Clamp(saved, current.Count);
        }
    }
}
