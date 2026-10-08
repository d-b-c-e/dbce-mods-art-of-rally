using System;
using System.Collections.Generic;

namespace RallyCustomVehicles
{
    // Reverse-order ownership journal; an exception in one restoration does not
    // prevent subsequent restorations. Also used by failure-injection tests.
    public sealed class MutationJournal
    {
        private readonly Stack<Action> undo=new Stack<Action>();
        public void Change(Action apply, Action restore)
        {
            undo.Push(restore ?? throw new ArgumentNullException(nameof(restore)));
            apply();
        }
        public void Rollback(Action<Exception> error)
        {
            while(undo.Count>0)
                try { undo.Pop()(); } catch(Exception ex) { error(ex); }
        }
    }
}
