using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ArtOfSimRally.Mod;

// Pure checks of the opt-in course route capture: request claim and refusal, route refusal, sampling limits and the
// atomic sealed write. Temporary folders only; no game, Unity, device or owner file.
int checks = 0;
void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException("FAIL: " + message); }
T Throws<T>(Action action, string message) where T : Exception
{
    try { action(); } catch (T e) { checks++; return e; }
    throw new InvalidOperationException("FAIL (no " + typeof(T).Name + "): " + message);
}

var now = new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
string hash = new string('A', 60) + "B7C8";
var id = Guid.Parse("0f8b3d36-9c2e-4a51-8e0d-2a7f1c4b5e60");
string Request(string extra = "", int minutes = 10, string h = null, string version = "1") =>
    "{ \"Version\": " + version + ", \"Id\": \"" + id + "\", \"ExpiresUtc\": \"" +
    now.AddMinutes(minutes).ToString("O", CultureInfo.InvariantCulture) + "\", \"GameAssemblySha256\": \"" + (h ?? hash) + "\"" + extra + " }";

// Request: exact flat object, pinned to the installed game build, short-lived, claimed before reading.
var parsed = CourseRouteRequest.Parse(Request(), now, hash.ToLowerInvariant());
Check(parsed.Id == id && parsed.MaxSeconds == 600 && parsed.GameAssemblySha256 == hash.ToLowerInvariant(), "valid request, default 600 s, lowercase hash");
Check(CourseRouteRequest.Parse(Request(", \"MaxSeconds\": 30"), now, hash).MaxSeconds == 30, "explicit seconds");
foreach (var (bad, why) in new[]
{
    (Request(", \"MaxSeconds\": 5"), "too short"), (Request(", \"MaxSeconds\": 601"), "too long"),
    (Request(minutes: -1), "expired"), (Request(minutes: 21), "over 20 minutes"), (Request(h: new string('C', 64)), "another game build"),
    (Request(version: "2"), "unknown version"), (Request(", \"Extra\": 1"), "unknown key"), (Request(", \"Version\": 1"), "duplicate key"),
    (Request().Replace("\"Id\": \"" + id + "\", ", ""), "missing id"), (Request() + " x", "trailing text"), ("[]", "not an object"),
    (Request().Replace(id.ToString(), Guid.Empty.ToString()), "empty id"), (Request(h: "zz" + hash.Substring(2)), "non-hex hash")
})
    Throws<InvalidDataException>(() => CourseRouteRequest.Parse(bad, now, hash), "request refused: " + why);

string temp = Path.Combine(Path.GetTempPath(), "art-course-route-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    string requestPath = Path.Combine(temp, CourseRouteRequest.FileName);
    Check(CourseRouteRequest.Consume(requestPath, now, () => hash) == null, "no request file, no capture");
    File.WriteAllText(requestPath, Request());
    Check(CourseRouteRequest.Consume(requestPath, now, () => hash)?.Id == id, "request consumed");
    Check(!File.Exists(requestPath) && Directory.GetFiles(temp, CourseRouteRequest.FileName + ".consumed-*").Length == 1, "request claimed by rename");
    Check(CourseRouteRequest.Consume(requestPath, now, () => hash) == null, "a claimed request never repeats");
    File.WriteAllText(requestPath, Request(minutes: -5));
    Throws<InvalidDataException>(() => CourseRouteRequest.Consume(requestPath, now, () => hash), "an expired request is refused");
    Check(!File.Exists(requestPath), "an invalid request is still claimed");
    File.WriteAllText(requestPath, Request() + new string(' ', 5000));
    Throws<InvalidDataException>(() => CourseRouteRequest.Consume(requestPath, now, () => hash), "an oversized request is refused");

    // Route: anything unusable refuses the whole capture; nothing is ever written for it.
    CourseRouteFacts Facts(int count = 4, string name = "Stage 1", int? start = 1, int? last = 2) => new CourseRouteFacts
    {
        GameAssemblySha256 = hash, GameMode = "CAREER", Area = "FINLAND", StageName = name, SceneConstant = 12,
        StageForward = true, RouteForward = true, StageDistance = 4.2f,
        Waypoints = Enumerable.Range(0, count).Select(i => new CoursePoint(i * 10f, 1f, i * -5f)).ToList(),
        StartIndex = start, LastIndex = last
    };
    foreach (var (facts, why) in new[]
    {
        (Facts(1), "one waypoint"), (Facts(CourseRouteCapture.MaxWaypoints + 1), "too many waypoints"), (Facts(start: 9), "start index outside"),
        (Facts(last: -3), "last index outside"), (Facts(name: " "), "no stage name")
    })
    {
        var refused = new CourseRouteCapture(id);
        refused.SetRoute(facts);
        Check(refused.State == CourseRouteState.Refused && refused.Terminal, "route refused: " + why);
        Throws<InvalidOperationException>(() => refused.Write(temp, now), "refused capture is never written: " + why);
    }
    var nonFinite = Facts(); nonFinite.Waypoints[2] = new CoursePoint(float.NaN, 0, 0);
    var nf = new CourseRouteCapture(id); nf.SetRoute(nonFinite);
    Check(nf.State == CourseRouteState.Refused && nf.Reason.Contains("waypoint 2"), "non-finite waypoint refused");
    var early = new CourseRouteCapture(id); early.Complete("stage FINISHED");
    Check(early.State == CourseRouteState.Refused && early.Reason == "no route before stage FINISHED", "no route before the end");

    // Sampling: unusable rows are counted, never written; time and row limits stop the capture.
    var capture = new CourseRouteCapture(id);
    capture.AddSample(1, 0.0, new CoursePoint(0, 0, 0), 0, "UNDERWAY");
    Check(capture.Rows == 0 && capture.State == CourseRouteState.WaitingForRoute, "no rows before the route");
    capture.SetRoute(Facts(name: "Lake \"Pihla\"\\ Run"));
    Check(capture.State == CourseRouteState.Sampling, "sampling after the route");
    capture.AddSample(1, 10.0, new CoursePoint(1, 2, 3), 1, "UNDERWAY");
    capture.AddSample(2, 10.02, new CoursePoint(float.PositiveInfinity, 2, 3), 1, "UNDERWAY");
    capture.AddSample(3, 9.0, new CoursePoint(1, 2, 3), 1, "UNDERWAY");
    capture.AddSample(4, 10.04, new CoursePoint(1, 2, 3), 1, "UNDER\tWAY");
    capture.AddSample(5, 10.06, new CoursePoint(4, 5, 6), 2, "FINISHING_STAGE_ANIMATION");
    Check(capture.Rows == 2 && capture.SkippedRows == 3, "two rows kept, three unusable rows skipped");
    capture.Complete("stage FINISHING_STAGE_ANIMATION");
    Check(capture.State == CourseRouteState.Completed && capture.Terminal, "completed at the finish animation");
    capture.AddSample(6, 10.08, new CoursePoint(4, 5, 6), 2, "FINISHED");
    Check(capture.Rows == 2, "no rows after completion");

    var timed = new CourseRouteCapture(id, 10); timed.SetRoute(Facts());
    for (int i = 0; i <= 700; i++) timed.AddSample(i, i / 60.0, new CoursePoint(i, 0, 0), 1, "UNDERWAY");
    Check(timed.State == CourseRouteState.LimitReached && timed.Reason == "10 s" && timed.Rows == 601, "request seconds stop the capture");
    var counted = new CourseRouteCapture(id); counted.SetRoute(Facts());
    for (int i = 0; i <= CourseRouteCapture.MaxRows; i++) counted.AddSample(i, i / 1000.0, new CoursePoint(i, 0, 0), 1, "UNDERWAY");
    Check(counted.State == CourseRouteState.LimitReached && counted.Rows == CourseRouteCapture.MaxRows, "row cap stops the capture");
    Throws<ArgumentOutOfRangeException>(() => new CourseRouteCapture(id, 601), "request seconds bounded");
    Throws<ArgumentException>(() => new CourseRouteCapture(Guid.Empty), "request id required");

    // Write: one sealed folder, exact files, escaped JSON, never overwritten, nothing partial left behind.
    string root = Path.Combine(temp, "CourseExtraction");
    string folder = capture.Write(root, now);
    Check(Path.GetFileName(folder) == "art-finland-lake-pihla-run-fwd-20261019T120000Z", "folder name from area, stage and direction");
    Check(Directory.GetFiles(folder).Select(Path.GetFileName).OrderBy(n => n).SequenceEqual(new[] { "complete.tsv", "progress.tsv", "route.json" }), "exactly three files");
    byte[] routeBytes = File.ReadAllBytes(Path.Combine(folder, "route.json")), progressBytes = File.ReadAllBytes(Path.Combine(folder, "progress.tsv"));
    string Hex(byte[] b) => Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    Check(File.ReadAllText(Path.Combine(folder, "complete.tsv")) == "dbce-playback-seal-v1\nprogress.tsv\t" + progressBytes.Length + "\t" + Hex(progressBytes) +
          "\nroute.json\t" + routeBytes.Length + "\t" + Hex(routeBytes) + "\n", "seal names sizes and sha256 of both files");
    string[] rows = Encoding.UTF8.GetString(progressBytes).Split('\n');
    Check(rows[0] == CourseRouteCapture.ProgressHeader && rows[1] == "1\t10\t1\t2\t3\t1\tUNDERWAY" && rows[2] == "5\t10.06\t4\t5\t6\t2\tFINISHING_STAGE_ANIMATION" && rows[3] == "",
        "progress rows exact");
    using (var json = JsonDocument.Parse(routeBytes))
    {
        var r = json.RootElement;
        Check(r.GetProperty("schema").GetString() == "dbce.art-course-route@1" && r.GetProperty("game").GetString() == "art of rally", "schema and game");
        Check(r.GetProperty("stageName").GetString() == "Lake \"Pihla\"\\ Run", "names escaped and round-trip");
        Check(r.GetProperty("gameAssemblySha256").GetString() == hash.ToLowerInvariant() && r.GetProperty("requestId").GetString() == id.ToString("N"), "build and request identity");
        Check(r.GetProperty("waypointCount").GetInt32() == 4 && r.GetProperty("waypoints")[3][0].GetSingle() == 30f &&
              r.GetProperty("waypoints")[3][2].GetSingle() == -15f, "waypoints in the game's order, unconverted");
        Check(r.GetProperty("gameStartIndex").GetInt32() == 1 && r.GetProperty("gameLastIndex").GetInt32() == 2, "game start and last indices");
        Check(r.GetProperty("directionStatus").GetString().StartsWith("unverified") && r.GetProperty("progressUnits").GetString().Contains("not distance") &&
              r.GetProperty("stageDistanceUnits").GetString().Contains("not verified"), "unknown units and direction stated");
        Check(r.GetProperty("state").GetString() == "Completed" && r.GetProperty("rows").GetInt32() == 2 && r.GetProperty("skippedRows").GetInt32() == 3, "outcome recorded");
    }
    Throws<InvalidOperationException>(() => capture.Write(root, now), "a capture is written once");
    var twin = new CourseRouteCapture(id); twin.SetRoute(Facts(name: "Lake \"Pihla\"\\ Run"));
    twin.AddSample(1, 1.0, new CoursePoint(0, 0, 0), 1, "UNDERWAY"); twin.Complete("stage FINISHED");
    Throws<IOException>(() => twin.Write(root, now), "an existing capture is never overwritten");
    Check(Directory.GetDirectories(root).Length == 1 && Directory.GetDirectories(root, ".*").Length == 0, "no partial folder left behind");
    var empty = new CourseRouteCapture(id); empty.SetRoute(Facts()); empty.Complete("stage FINISHED");
    Throws<InvalidOperationException>(() => empty.Write(root, now.AddSeconds(1)), "a capture without rows is not written");
    var faulted = new CourseRouteCapture(id); faulted.SetRoute(Facts()); faulted.AddSample(1, 1.0, new CoursePoint(0, 0, 0), 1, "UNDERWAY");
    faulted.Fault("NullReferenceException: x");
    Throws<InvalidOperationException>(() => faulted.Write(root, now.AddSeconds(2)), "a faulted capture is not written");
    Check(CourseRouteCapture.PathToken("  ") == "unnamed" && CourseRouteCapture.PathToken(new string('a', 50)).Length == 40, "path tokens bounded");
}
finally { Directory.Delete(temp, true); }
Console.WriteLine($"PASS {checks} course route capture checks; temporary files only.");
