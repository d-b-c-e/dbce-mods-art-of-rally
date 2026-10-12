using System;
using System.Globalization;
using System.Text;

namespace ArtOfSimRally.Testing
{
    /// <summary>
    /// The partial replay outcome (sana 36382/36418/36495). A replay that played every recorded driving row, passed
    /// its driving checks and then waited out a later menu marker writes "partial:post-stage-divergence" plus
    /// key=value fields after the status line. It is never a full pass, and never used before or during the stage
    /// or with driving rows unplayed. Passed, failed and aborted results are unchanged.
    /// </summary>
    internal static class ReplayOutcome
    {
        internal const string Partial = "partial:post-stage-divergence";

        /// <summary>The partial first line, or null when the cursor failure must stay a failure.</summary>
        /// <param name="cursorFailure">The menu cursor's failure text.</param>
        /// <param name="waitedOut">The cursor stopped only because it waited out its next marker.</param>
        /// <param name="drivingRows">Recorded driving (UNDERWAY) car rows.</param>
        /// <param name="drivingUnplayed">Driving car and frame rows skipped or not yet reached.</param>
        /// <param name="drivingVerdict">The driving verdict the replay would give now.</param>
        internal static string PostStage(string cursorFailure, bool waitedOut, int drivingRows, int drivingUnplayed, string drivingVerdict)
        {
            if (cursorFailure == null || !waitedOut || drivingRows <= 0 || drivingUnplayed != 0) return null;
            if (drivingVerdict == null || !drivingVerdict.StartsWith("passed (", StringComparison.Ordinal)) return null;
            return Partial + ": all " + drivingRows.ToString(CultureInfo.InvariantCulture) + " driving rows played (" +
                drivingVerdict.Substring("passed (".Length).TrimEnd(')') + "); " + Clean(cursorFailure);
        }

        /// <summary>key=value lines after the status line: only for the partial outcome, empty otherwise.</summary>
        internal static string Fields(string result, int drivingSegments, string expectedMarker, float timeoutSeconds)
        {
            if (result == null || !result.StartsWith(Partial + ":", StringComparison.Ordinal)) return "";
            var lines = new StringBuilder();
            void Add(string key, string value) => lines.Append(key).Append('=').Append(Clean(value)).Append('\n');
            Add("scope", "stage-only");
            Add("fullSessionPassed", "false");
            // A partial result requires every driving row played, so every driving segment completed.
            Add("drivingSegmentsTotal", drivingSegments.ToString(CultureInfo.InvariantCulture));
            Add("drivingSegmentsCompleted", drivingSegments.ToString(CultureInfo.InvariantCulture));
            Add("expectedMarker", expectedMarker ?? "none");
            Add("timeoutSeconds", timeoutSeconds.ToString("0.###", CultureInfo.InvariantCulture));
            return lines.ToString();
        }
        private static string Clean(string text)
        {
            var chars = (text ?? "").ToCharArray();
            for (int i = 0; i < chars.Length; i++) if (char.IsControl(chars[i])) chars[i] = ' ';
            return new string(chars);
        }
    }
}
