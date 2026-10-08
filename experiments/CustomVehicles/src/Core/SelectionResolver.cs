using System.Collections.Generic;
using System.Linq;

namespace RallyCustomVehicles
{
    internal static class SelectionResolver
    {
        // Resolve against the current successful registrations for one class.
        // Never persist or trust yesterday's numeric custom slot.
        internal static int Resolve(string id,int nativeDonorIndex,IEnumerable<KeyValuePair<string,SelectionState>> current)
        {
            if(id==null) return nativeDonorIndex;
            var match=current.FirstOrDefault(p=>p.Key==id).Value;
            return match!=null && nativeDonorIndex==match.Donor ? match.Slot : nativeDonorIndex;
        }
    }
}
