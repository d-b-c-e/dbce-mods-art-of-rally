using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ArtOfSimRally.Mod
{
    /// <summary>A Unity world point, unconverted; the toolkit's CourseAxes.FromUnity maps it offline.</summary>
    internal struct CoursePoint
    {
        public readonly float X, Y, Z;
        public CoursePoint(float x, float y, float z) { X = x; Y = y; Z = z; }
        public bool Finite => CourseRouteCapture.IsFinite(X) && CourseRouteCapture.IsFinite(Y) && CourseRouteCapture.IsFinite(Z);
    }

    /// <summary>
    /// One-launch request for a private course-route capture, pinned to the installed game build. It exists only when
    /// an operator creates it, and it is claimed (renamed) before it is read, so a stale, crashed or invalid request
    /// can never repeat.
    /// </summary>
    internal sealed class CourseRouteRequest
    {
        public const int CurrentVersion = 1;
        public const string FileName = "art-course-route-next-launch.json";
        public const int MaxRequestBytes = 4096;

        public Guid Id { get; private set; }
        public int MaxSeconds { get; private set; }
        public string GameAssemblySha256 { get; private set; }

        /// <summary>Null when there is no request. A present one is claimed, then parsed; anything else throws.</summary>
        public static CourseRouteRequest Consume(string path, DateTimeOffset now, Func<string> gameHash)
        {
            if (!File.Exists(path)) return null;
            CourseRouteCapture.NoLinks(path);
            string claimed = path + ".consumed-" + Guid.NewGuid().ToString("N");
            File.Move(path, claimed);
            if (new FileInfo(claimed).Length > MaxRequestBytes) throw new InvalidDataException("Oversized course request");
            return Parse(File.ReadAllText(claimed), now, gameHash());
        }

        private static readonly Regex Member = new Regex(
            "\\s*\"(?<key>[A-Za-z0-9]+)\"\\s*:\\s*(?<value>\"[^\"\\\\\\r\\n]*\"|-?[0-9]+)\\s*", RegexOptions.CultureInvariant);

        /// <summary>The exact flat object {"Version":1,"Id":guid,"ExpiresUtc":ISO 8601 (within 20 minutes),
        /// "GameAssemblySha256":64 hex of the installed game,"MaxSeconds":10-600 (optional, 600)}, each key once.</summary>
        internal static CourseRouteRequest Parse(string json, DateTimeOffset now, string installedGameSha256)
        {
            string text = (json ?? "").Trim();
            if (text.Length < 2 || text[0] != '{' || text[text.Length - 1] != '}') throw Invalid();
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            string body = text.Substring(1, text.Length - 2);
            if (body.Trim().Length > 0)
                foreach (string part in body.Split(','))
                {
                    var m = Member.Match(part);
                    if (!m.Success || m.Length != part.Length || values.ContainsKey(m.Groups["key"].Value)) throw Invalid();
                    values[m.Groups["key"].Value] = m.Groups["value"].Value;
                }
            foreach (string key in values.Keys)
                if (key != "Version" && key != "Id" && key != "ExpiresUtc" && key != "GameAssemblySha256" && key != "MaxSeconds") throw Invalid();
            int version, maxSeconds = CourseRouteCapture.MaxSecondsLimit;
            Guid id;
            DateTimeOffset expires;
            if (!values.TryGetValue("Version", out var v) || !int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out version) ||
                version != CurrentVersion ||
                !values.TryGetValue("Id", out var idText) || !Quoted(idText, out idText) || !Guid.TryParse(idText, out id) || id == Guid.Empty ||
                !values.TryGetValue("ExpiresUtc", out var e) || !Quoted(e, out e) ||
                !DateTimeOffset.TryParse(e, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out expires) ||
                expires <= now || expires > now.AddMinutes(20) ||
                !values.TryGetValue("GameAssemblySha256", out var h) || !Quoted(h, out h) || !Hex64(h) ||
                !string.Equals(h, installedGameSha256, StringComparison.OrdinalIgnoreCase) ||
                values.TryGetValue("MaxSeconds", out var s) &&
                (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out maxSeconds) || maxSeconds < 10 || maxSeconds > CourseRouteCapture.MaxSecondsLimit))
                throw Invalid();
            return new CourseRouteRequest { Id = id, MaxSeconds = maxSeconds, GameAssemblySha256 = h.ToLowerInvariant() };
        }

        private static bool Quoted(string raw, out string text)
        {
            text = raw.Length >= 2 && raw[0] == '"' && raw[raw.Length - 1] == '"' ? raw.Substring(1, raw.Length - 2) : null;
            return text != null;
        }

        internal static bool Hex64(string text)
        {
            if (text == null || text.Length != 64) return false;
            foreach (char c in text) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        private static InvalidDataException Invalid() => new InvalidDataException("Expired, invalid or wrong-build course request; prepare a new one.");
    }

    /// <summary>The current stage's identity and the game's own route, read once when the stage is underway.</summary>
    internal sealed class CourseRouteFacts
    {
        public string GameAssemblySha256;
        public string GameMode;
        public string Area;
        public string StageName;
        public int SceneConstant;
        /// <summary>Stage.IsForwardStage, the stage's own direction.</summary>
        public bool StageForward;
        /// <summary>OutOfBoundsManager.IsForwardStage(), the direction the route was ordered for.</summary>
        public bool RouteForward;
        /// <summary>Stage.Distance, which the game displays as km.</summary>
        public float StageDistance;
        public IList<CoursePoint> Waypoints;
        /// <summary>The game's private StartingWaypointIndex and LastWaypointIndex, when they could be read.</summary>
        public int? StartIndex, LastIndex;
    }

    internal enum CourseRouteState { WaitingForRoute, Sampling, Completed, LimitReached, Refused, Faulted }

    /// <summary>
    /// Bounded, read-only capture of one art of rally stage: its identity, the game's own waypoint route and the game's
    /// own coarse progress (the current waypoint index) per physics tick. Written once, atomically, to a private folder
    /// as <c>route.json</c>, <c>progress.tsv</c> and a <c>complete.tsv</c> seal.
    /// </summary>
    /// <remarks>
    /// Raw extraction only. Units, direction and the meaning of progress are recorded as the game reports them and are
    /// marked unverified; course identity, axes, projection and the SimHub layout are the toolkit's and are applied
    /// offline. Limits: at most <see cref="MaxWaypoints"/> points (more is refused, not truncated), <see cref="MaxRows"/>
    /// rows and the request's seconds, and <see cref="MaxBytes"/> on disk. An existing capture is never overwritten,
    /// and a failure leaves nothing partial. Pure: no Unity or game types, so it is tested without the game.
    /// </remarks>
    internal sealed class CourseRouteCapture
    {
        public const string Schema = "dbce.art-course-route@1";
        public const string Game = "art of rally";
        public const int MaxWaypoints = 20000;
        public const int MaxRows = 36000;
        public const int MaxSecondsLimit = 600;
        public const long MaxBytes = 8L * 1024 * 1024;
        public const string ProgressHeader = "tick\tsim_s\tpx\tpy\tpz\twaypoint_index\tstatus";

        private readonly StringBuilder _rows = new StringBuilder();
        private readonly int _maxSeconds;
        private double _firstSeconds = double.NaN, _lastSeconds = double.NegativeInfinity;
        private bool _written;

        public CourseRouteCapture(Guid requestId, int maxSeconds = MaxSecondsLimit)
        {
            if (requestId == Guid.Empty) throw new ArgumentException("request id required", nameof(requestId));
            if (maxSeconds < 10 || maxSeconds > MaxSecondsLimit) throw new ArgumentOutOfRangeException(nameof(maxSeconds));
            RequestId = requestId;
            _maxSeconds = maxSeconds;
        }

        public Guid RequestId { get; }
        public CourseRouteState State { get; private set; } = CourseRouteState.WaitingForRoute;
        public string Reason { get; private set; }
        public CourseRouteFacts Route { get; private set; }
        public int Rows { get; private set; }
        public int SkippedRows { get; private set; }
        public bool Terminal => State == CourseRouteState.Completed || State == CourseRouteState.LimitReached ||
                                State == CourseRouteState.Refused || State == CourseRouteState.Faulted;

        /// <summary>Accepts the route once, while waiting for it; anything unusable refuses the whole capture.</summary>
        public void SetRoute(CourseRouteFacts facts)
        {
            if (State != CourseRouteState.WaitingForRoute) return;
            string problem = Problem(facts);
            if (problem != null) { State = CourseRouteState.Refused; Reason = problem; return; }
            Route = facts;
            State = CourseRouteState.Sampling;
        }

        /// <summary>One physics tick. Ignored unless sampling; unusable rows are counted, never written.</summary>
        public void AddSample(long tick, double simSeconds, CoursePoint position, int waypointIndex, string status)
        {
            if (State != CourseRouteState.Sampling) return;
            if (double.IsNaN(simSeconds) || double.IsInfinity(simSeconds) || !position.Finite || simSeconds < _lastSeconds ||
                string.IsNullOrEmpty(status) || status.IndexOfAny(RowBreaks) >= 0)
            { SkippedRows++; return; }
            if (double.IsNaN(_firstSeconds)) _firstSeconds = simSeconds;
            if (Rows >= MaxRows || simSeconds - _firstSeconds > _maxSeconds)
            {
                State = CourseRouteState.LimitReached;
                Reason = Rows >= MaxRows ? MaxRows + " rows" : _maxSeconds + " s";
                return;
            }
            _lastSeconds = simSeconds;
            _rows.Append(tick.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(simSeconds.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                .Append(F(position.X)).Append('\t').Append(F(position.Y)).Append('\t').Append(F(position.Z)).Append('\t')
                .Append(waypointIndex.ToString(CultureInfo.InvariantCulture)).Append('\t')
                .Append(status).Append('\n');
            Rows++;
        }

        /// <summary>The stage ended (finish animation or finished) or the mod unloaded.</summary>
        public void Complete(string reason)
        {
            if (State == CourseRouteState.Sampling) { State = CourseRouteState.Completed; Reason = reason; }
            else if (State == CourseRouteState.WaitingForRoute) { State = CourseRouteState.Refused; Reason = "no route before " + reason; }
        }

        /// <summary>A read failed. The capture is abandoned and nothing is written.</summary>
        public void Fault(string reason)
        {
            if (Terminal) return;
            State = CourseRouteState.Faulted;
            Reason = reason;
        }

        /// <summary>Writes a completed or limit-reached capture with rows once, under <paramref name="root"/>; returns its folder.</summary>
        public string Write(string root, DateTimeOffset utcNow)
        {
            if (_written) throw new InvalidOperationException("capture already written");
            if (State != CourseRouteState.Completed && State != CourseRouteState.LimitReached || Route == null || Rows == 0)
                throw new InvalidOperationException("only a completed or limit-reached capture with rows is written (state " + State + ")");
            _written = true;

            byte[] route = Encoding.UTF8.GetBytes(RouteJson(Route, utcNow));
            byte[] progress = Encoding.UTF8.GetBytes(ProgressHeader + "\n" + _rows);
            if (route.LongLength + progress.LongLength + 1024 > MaxBytes)
                throw new IOException("capture exceeds " + MaxBytes + " bytes; nothing written");

            NoLinks(root);
            string name = FolderName(Route, utcNow);
            string final = Path.Combine(root, name);
            if (Directory.Exists(final) || File.Exists(final)) throw new IOException("capture folder already exists: " + name);
            Directory.CreateDirectory(root);
            string partial = Path.Combine(root, "." + name + ".partial-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(partial);
            try
            {
                WriteNew(Path.Combine(partial, "route.json"), route);
                WriteNew(Path.Combine(partial, "progress.tsv"), progress);
                var seal = new StringBuilder("dbce-playback-seal-v1\n");
                seal.Append("progress.tsv\t").Append(progress.Length).Append('\t').Append(Sha256(progress)).Append('\n');
                seal.Append("route.json\t").Append(route.Length).Append('\t').Append(Sha256(route)).Append('\n');
                WriteNew(Path.Combine(partial, "complete.tsv"), Encoding.UTF8.GetBytes(seal.ToString()));
                Directory.Move(partial, final);
            }
            catch
            {
                try { Directory.Delete(partial, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                throw;
            }
            return final;
        }

        /// <summary><c>art-&lt;area&gt;-&lt;stage&gt;-&lt;fwd|rev&gt;-&lt;yyyyMMddTHHmmssZ&gt;</c>: a folder name, not a course id.</summary>
        public static string FolderName(CourseRouteFacts route, DateTimeOffset utcNow) =>
            "art-" + PathToken(route.Area) + "-" + PathToken(route.StageName) + "-" + (route.StageForward ? "fwd" : "rev") + "-" +
            utcNow.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

        /// <summary>Lowercase ASCII letters and digits; every other run of characters becomes one '-'.</summary>
        public static string PathToken(string text)
        {
            var sb = new StringBuilder();
            bool dash = false;
            foreach (char raw in text ?? "")
            {
                char c = char.ToLowerInvariant(raw);
                if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9')
                {
                    if (dash && sb.Length > 0) sb.Append('-');
                    dash = false;
                    sb.Append(c);
                }
                else dash = true;
            }
            string token = sb.Length > 40 ? sb.ToString(0, 40).TrimEnd('-') : sb.ToString();
            return token.Length == 0 ? "unnamed" : token;
        }

        /// <summary>Refuses a linked (reparse point) path or ancestor.</summary>
        internal static void NoLinks(string path)
        {
            for (string p = Path.GetFullPath(path); p != null; p = Path.GetDirectoryName(p))
                if ((File.Exists(p) || Directory.Exists(p)) && (File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Linked course path refused");
        }

        internal static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        private string RouteJson(CourseRouteFacts r, DateTimeOffset utcNow)
        {
            var w = new StringBuilder("{\n");
            void Field(string key, string jsonValue, bool last = false) =>
                w.Append("  ").Append(Q(key)).Append(": ").Append(jsonValue).Append(last ? "\n" : ",\n");
            Field("schema", Q(Schema));
            Field("game", Q(Game));
            Field("requestId", Q(RequestId.ToString("N")));
            Field("gameAssemblySha256", Q(r.GameAssemblySha256.ToLowerInvariant()));
            Field("gameMode", Q(r.GameMode));
            Field("area", Q(r.Area));
            Field("stageName", Q(r.StageName));
            Field("sceneConstant", r.SceneConstant.ToString(CultureInfo.InvariantCulture));
            Field("stageForward", r.StageForward ? "true" : "false");
            Field("routeForward", r.RouteForward ? "true" : "false");
            Field("stageDistance", F(r.StageDistance));
            Field("stageDistanceUnits", Q("Stage.Distance, displayed by the game as km; not verified against the route"));
            Field("frame", Q("unity-world"));
            Field("axes", Q("x east, y up, z north (toolkit CourseAxes.FromUnity); not verified on screen"));
            Field("routeSource", Q("EventManager.outOfBoundsManager.GetWaypointList(): children of 'Waypoints Parent' named *Waypoint*"));
            Field("routeOrder", Q("the game's list order; the game reverses it for reverse stages (routeForward false)"));
            Field("directionStatus", Q("unverified; driving direction along the list is not checked here"));
            Field("gameStartIndex", r.StartIndex is int s ? s.ToString(CultureInfo.InvariantCulture) : "null");
            Field("gameLastIndex", r.LastIndex is int l ? l.ToString(CultureInfo.InvariantCulture) : "null");
            Field("progressSource", Q("OutOfBoundsManager.GetCurrentWaypointIndex()"));
            Field("progressUnits", Q("coarse waypoint index; the HUD shows (index - start) / (last - start); not distance"));
            Field("waypointCount", r.Waypoints.Count.ToString(CultureInfo.InvariantCulture));
            w.Append("  \"waypoints\": [");
            for (int i = 0; i < r.Waypoints.Count; i++)
            {
                var p = r.Waypoints[i];
                w.Append(i == 0 ? "\n    [" : ",\n    [").Append(F(p.X)).Append(", ").Append(F(p.Y)).Append(", ").Append(F(p.Z)).Append(']');
            }
            w.Append(r.Waypoints.Count == 0 ? "],\n" : "\n  ],\n");
            Field("state", Q(State.ToString()));
            Field("reason", Reason == null ? "null" : Q(Reason));
            Field("rows", Rows.ToString(CultureInfo.InvariantCulture));
            Field("skippedRows", SkippedRows.ToString(CultureInfo.InvariantCulture));
            Field("capturedUtc", Q(utcNow.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)), last: true);
            return w.Append("}\n").ToString();
        }

        private static string Problem(CourseRouteFacts f)
        {
            if (f == null) return "no route facts";
            if (!CourseRouteRequest.Hex64(f.GameAssemblySha256)) return "game assembly hash missing";
            if (string.IsNullOrWhiteSpace(f.Area) || f.Area.Length > 64) return "stage area missing";
            if (string.IsNullOrWhiteSpace(f.StageName) || f.StageName.Length > 128) return "stage name missing";
            if (string.IsNullOrWhiteSpace(f.GameMode) || f.GameMode.Length > 64) return "game mode missing";
            if (!IsFinite(f.StageDistance)) return "stage distance not finite";
            if (f.Waypoints == null || f.Waypoints.Count < 2) return "fewer than 2 waypoints";
            if (f.Waypoints.Count > MaxWaypoints) return "more than " + MaxWaypoints + " waypoints";
            for (int i = 0; i < f.Waypoints.Count; i++) if (!f.Waypoints[i].Finite) return "waypoint " + i + " is not finite";
            if (f.StartIndex is int s && (s < 0 || s >= f.Waypoints.Count) || f.LastIndex is int l && (l < -2 || l >= f.Waypoints.Count))
                return "game waypoint indices outside the route";
            return null;
        }

        private static readonly char[] RowBreaks = { '\t', '\r', '\n' };
        private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>A JSON string with quotes, backslashes and control characters escaped.</summary>
        internal static string Q(string text)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in text ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }

        private static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                var sb = new StringBuilder(64);
                foreach (byte b in sha.ComputeHash(bytes)) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static void WriteNew(string path, byte[] bytes)
        {
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                output.Write(bytes, 0, bytes.Length);
                output.Flush(true);
            }
        }
    }
}
