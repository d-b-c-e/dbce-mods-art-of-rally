using System;
using System.Collections.Generic;
using System.IO;
using ArtOfSimRally.Mod.Triple;

namespace Dbce.TripleScreen.Tests;
internal static class PersistenceChecks
{
    internal static void Run(Action<bool,string> check)
    {
        check(!LandingPersistence.MarkerPresent("missing", _ => throw new FileNotFoundException()), "confirmed marker absence");
        check(!LandingPersistence.MarkerPresent("missing parent", _ => throw new DirectoryNotFoundException()), "confirmed parent absence");
        check(LandingPersistence.MarkerPresent("present", _ => FileAttributes.Directory), "present marker detected");
        var denied = new LandingPersistence(() => LandingPersistence.MarkerPresent("denied", _ => throw new UnauthorizedAccessException()) ? "marker" : null);
        check(denied.Block == "launch classification unavailable", "denied marker inspection fails closed");
        var failed = new LandingPersistence(() => LandingPersistence.MarkerPresent("io", _ => throw new IOException()) ? "marker" : null);
        check(failed.Block != null, "IO marker inspection fails closed");
        string block = "initial marker";
        var guard = new LandingPersistence(() => block); block = null;
        check(guard.Block != null, "construction suppression latched");
        guard = new LandingPersistence(() => block); block = "late marker";
        int? value = 0; int sets = 0, flushes = 0; var logs = new List<string>();
        bool Save(int? expected, Action<int> setter = null, Action flush = null) => guard.Save(expected, 1,
            () => value, setter ?? (v => { sets++; value = v; }), () => value = null,
            flush ?? (() => flushes++), logs.Add);
        check(!Save(0) && sets == 0 && flushes == 0, "late marker blocks write boundary");
        block = null;
        check(!Save(2) && sets == 0, "expected-value mismatch refuses write");
        check(Save(0) && value == 1 && sets == 1 && flushes == 1, "admitted boundary sets and flushes");
        value = 0; logs.Clear();
        check(!Save(0, v => { value = v; if(v == 1) throw new IOException(); }) && value == 0,
            "SetInt failure restores previous memory");
        check(logs.Exists(s => s.Contains("write failed")), "set failure has specific log");
        logs.Clear(); value = null;
        check(!Save(null, flush: () => throw new IOException()) && value == null,
            "Save failure deletes previously absent in-memory key");
        check(logs.Exists(s => s.Contains("disk state unconfirmed")), "save failure never claims disk unchanged");
        value = 0; logs.Clear();
        check(!Save(0, _ => throw new UnauthorizedAccessException()) && logs.Exists(s => s.Contains("restore failed")),
            "restore failure contained and explicitly uncertain");
    }
}
