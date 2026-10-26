#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Dbce.Wheel.Display;

// WindowLanding.cs: the shared vectors, then a grid over both rigs proving the target is only ever the home display or
// the row's span. Pure: no Windows, engine, registry or window call.
internal static class WindowLandingChecks
{
    static MonitorRect M(int x, int y, int w, int h, bool primary = false) => new(x, y, w, h, primary);
    static List<MonitorRect> Monitors(string text) => text == "-" ? new() : text.Split("; ").Select(t =>
    {
        var m = t.Split(',');
        return M(int.Parse(m[0]), int.Parse(m[1]), int.Parse(m[2]), int.Parse(m[3]), m.Length == 5 && m[4] == "primary");
    }).ToList();
    static int? Index(string text) => text == "-" ? null : int.Parse(text);

    public static int Run(Action<bool, string> check)
    {
        int vectors = 0;
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "window-landing-vectors.txt")))
        {
            if (line.Length == 0 || line[0] == '#') continue;
            var f = line.Split(" | ");
            check(f.Length == 13 && f[0] == "landing" && f[3] is "yes" or "no", "landing vector line: " + line);
            var monitors = Monitors(f[2]);
            var window = f[4].Split(',').Select(int.Parse).ToArray();
            MonitorRect? opened = f[5] == "-" ? null : monitors[int.Parse(f[5])];
            List<EngineDisplay>? engine = f[6] == "none" ? null : f[6].Split("; ").Select(t =>
            {
                var wh = t.Split('x');
                return new EngineDisplay(int.Parse(wh[0]), int.Parse(wh[1]));
            }).ToList();
            var d = WindowLandingPolicy.Decide(monitors, RacingRowPolicy.Select(monitors), f[3] == "yes", window[0], window[1], window[2], window[3],
                opened, engine, Index(f[7]));
            var result = f[8].Split(' ');
            var rect = result[1].Split(',').Select(int.Parse).ToArray();
            string got = (d.Move ? "move " : "stay ") + d.X + "," + d.Y + "," + d.Width + "," + d.Height + " | " + (d.EngineIndex?.ToString() ?? "-") +
                " | " + (d.Persist?.ToString() ?? "-") + " | " + d.Reason + " | " + d.PersistReason;
            check(d.Move == (result[0] == "move") && d.X == rect[0] && d.Y == rect[1] && d.Width == rect[2] && d.Height == rect[3], "landing " + f[1] + ": target " + got);
            check(d.EngineIndex == Index(f[9]) && d.Persist == Index(f[10]), "landing " + f[1] + ": engine index and persist " + got);
            check(d.Reason.Contains(f[11], StringComparison.Ordinal) && d.PersistReason.Contains(f[12], StringComparison.Ordinal), "landing " + f[1] + ": reasons " + got);
            vectors++;
        }
        check(vectors >= 20, "landing vectors run: " + vectors);

        // Never an auxiliary display: over a grid of window centers on every display of both rigs, a move targets only
        // the home display (or the span), and the persisted index is only ever the home display's.
        var surround = new[] { M(0, 0, 7680, 1440, true), M(2886, -1080, 1920, 1080) };
        var separate = new[] { M(-2560, 0, 2560, 1440), M(0, 0, 2560, 1440, true), M(2560, 0, 2560, 1440), M(314, -1080, 1920, 1080) };
        foreach (var layout in new[] { surround, separate })
            foreach (bool spanning in new[] { false, true })
            {
                var row = RacingRowPolicy.Select(layout);
                var home = WindowLandingPolicy.Home(row)!.Value;
                bool span = spanning && row.Kind == RacingRowKind.Separate;
                var engine = layout.Select(m => new EngineDisplay(m.Width, m.Height)).ToList();
                foreach (var m in layout)
                    for (int i = 1; i < 4; i++)
                        for (int j = 1; j < 4; j++)
                        {
                            var d = WindowLandingPolicy.Decide(layout, row, spanning, m.X + m.Width * i / 4 - 320, m.Y + m.Height * j / 4 - 180, 640, 360,
                                m, engine, Array.IndexOf(layout, m));
                            bool onHome = d.X == home.X && d.Y == home.Y && d.Width == home.Width && d.Height == home.Height;
                            bool onSpan = d.X == row.X && d.Y == row.Y && d.Width == row.Width && d.Height == row.Height;
                            check(span ? onSpan : onHome, "the target is the home display or the span only: " + m + " -> " + d.X + "," + d.Y + " " + d.Width + "x" + d.Height);
                            check(span || d.Move == !m.Equals(home), "moves exactly when the window is off the home display: " + m);
                            check(d.Persist == null || d.Persist == Array.IndexOf(layout, home), "persists only the home display's index: " + m + " -> " + d.Persist);
                        }
            }
        check(WindowLandingPolicy.Home(RacingRowPolicy.Select(new[] { M(0, 0, 2560, 1440, true) })) == null, "one display has no home");
        check(WindowLandingPolicy.DisplayOf(surround, -32000, -32000, 160, 28) == null, "a minimized window is on no display");
        return vectors;
    }
}
