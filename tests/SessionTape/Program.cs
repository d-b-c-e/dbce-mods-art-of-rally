using ArtOfSimRally.Testing;

// Session replay segment alignment, without Unity. Covers the edge cases from
// docs/reviews/2026-10-04-session-replay-review.md (R1) plus transitions.
int assertions = 0;
void Check(bool ok, string what) { assertions++; if (!ok) throw new Exception("FAIL: " + what); }

var names = new Dictionary<int, string> { [0] = "A", [1] = "B", [2] = "C|UNDERWAY" };
float now = 0;
SessionAligner<int> Make(params int[] markers)
{
    var rows = markers.Select((m, i) => new KeyValuePair<int, int>(m, i)).ToList();
    return new SessionAligner<int>(rows, names, "t", () => now);
}

// R1.1: tape [A, B], live stays on A. Must not finish.
{
    var a = Make(0, 1); now = 0;
    a.Next(0, out _); a.Next(0, out _);
    Check(!a.Finished, "finished without observing B");
}

// R1.2: tape [A, A, A, B], live moves to B after one row: the two dropped rows are counted.
{
    var a = Make(0, 0, 0, 1);
    Check(a.Next(0, out var v) && v == 0, "first row");
    Check(a.Next(1, out v) && v == 3, "jumps to B's row");
    Check(a.Skipped == 2, "two skipped rows reported, got " + a.Skipped);
    Check(a.Finished, "finished after B");
}

// R1.4: stopping after one of three rows in the only segment leaves rows remaining.
{
    var a = Make(0, 0, 0);
    a.Next(0, out _);
    Check(a.InLastSegment && a.RemainingInSegment == 2 && !a.Finished, "remaining rows visible");
}

// R1.5: empty tape never finishes.
{
    var a = Make();
    Check(!a.Finished && !a.InLastSegment && !a.Next(0, out _), "empty tape");
}

// Transition input taped in the next segment: played early, at most EarlyLimit rows.
{
    var markers = new List<int> { 0 };
    markers.AddRange(Enumerable.Repeat(1, 20));
    var a = Make(markers.ToArray());
    Check(a.Next(0, out _), "A row");
    int early = 0;
    while (a.Next(0, out _)) early++;
    Check(early == SessionAligner<int>.EarlyLimit - 1 + 1, "early rows played: " + early);
    Check(a.Next(1, out _), "continues once live arrives");
}

// Waiting uses real time, not frame counts.
{
    var a = Make(0, 1); now = 0;
    a.Next(0, out _);
    a.Next(2, out _); now = 60; a.Next(2, out _);
    Check(a.Failed == null, "not failed at 60 s");
    now = 121; a.Next(2, out _);
    Check(a.Failed != null && a.Failed.Contains("'B'"), "failed after 120 s waiting for B: " + a.Failed);
}

// Skipped rows are attributed per marker, so driving skips can fail strictly.
{
    var a = Make(2, 2, 2, 1);
    a.Next(2, out _);
    a.Next(1, out _);
    Check(a.SkippedWhere(m => m.EndsWith("|UNDERWAY")) == 2, "driving rows skipped");
    Check(a.SkippedWhere(m => m == "B") == 0, "no menu rows skipped");
}

// A short taped state the live game never stops in: jump ahead, count the rows.
{
    var a = Make(0, 0, 1, 1, 2, 2);
    Check(a.Next(1, out var v) && v == 2, "starts at B, skipping A's two rows");
    Check(a.Skipped == 2, "two rows counted as skipped");
}

Console.WriteLine("{\"status\":\"passed\",\"assertions\":" + assertions + "}");
