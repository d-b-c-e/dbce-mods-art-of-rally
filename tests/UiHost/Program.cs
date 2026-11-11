// The F6 panel (UiHostHook): every descriptor control row maps to a Settings field with
// the same default, and a change writes only its own element of Settings.xml (STD-044 byte diff).
using System.Text;
using System.Text.Json;
using ArtOfSimRally.Mod;

int failures = 0;
void Check(bool ok, string what) { if (!ok) { failures++; Console.WriteLine("FAIL " + what); } }

string dir = Path.Combine(Path.GetTempPath(), "art-uihost-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try
{
    // Every control row of the descriptor maps to one store row with the same kind and the store's default.
    var pages = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "art-hook.settings.json")))
        .RootElement.GetProperty("settings").GetProperty("pages");
    var seen = new HashSet<string>();
    var defaults = new Settings();
    foreach (var page in pages.EnumerateObject())
    foreach (var row in page.Value.EnumerateArray())
    {
        string id = row.GetProperty("id").GetString(), kind = row.GetProperty("kind").GetString();
        if (kind == "status" || kind == "action") continue;   // neither stores a value
        seen.Add(id);
        var mapped = UiHostRows.Find(id);
        if (mapped == null) { Check(false, id + " has no store row"); continue; }
        Check(mapped.Kind == (kind == "toggle" ? UiHostRows.Kind.Toggle : UiHostRows.Kind.Number), id + " kind " + kind);
        double want = !row.TryGetProperty("default", out var d) ? 0 : d.ValueKind == JsonValueKind.True ? 1 : d.ValueKind == JsonValueKind.False ? 0 : d.GetDouble();
        double got = mapped.Get(defaults, false);
        Check(Math.Abs(want - got) < 1e-6, id + " default " + want + " vs store " + got);
    }
    foreach (var row in UiHostRows.Rows) Check(seen.Contains(row.Id), row.Id + " is not in the descriptor");

    // STD-044: a change rewrites only its own element; every other byte of a player's Settings.xml stays.
    string path = Path.Combine(dir, "Settings.xml");
    foreach (bool lf in new[] { false, true })
    foreach (var row in UiHostRows.Rows)
    {
        var c = new Settings();
        SettingsPersistence.Write(c, path);
        if (lf) File.WriteAllText(path, File.ReadAllText(path).Replace("\r\n", "\n"), new UTF8Encoding(false));
        byte[] before = File.ReadAllBytes(path);
        string key = row.Keys[0];
        string oldText = UiHostRows.Text(c, key);
        var snapshot = UiHostRows.Snapshot(c, row);
        double value = row.Kind == UiHostRows.Kind.Toggle ? 1 - row.Get(c, false) : row.Get(c, false) + (row.Id == "cameras.fov" ? -5 : 3);
        row.Set(c, false, value);
        var written = UiHostRows.Save(path, c, snapshot);
        string newText = UiHostRows.Text(c, key);
        Check(written.Count == 1 && written[0] == key, row.Id + " wrote " + string.Join(",", written));
        string expected = Encoding.UTF8.GetString(before).Replace("<" + key + ">" + oldText + "</" + key + ">", "<" + key + ">" + newText + "</" + key + ">");
        Check(Encoding.UTF8.GetString(before) != expected, row.Id + " changed " + key);
        Check(File.ReadAllBytes(path).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected)), row.Id + (lf ? " (LF)" : " (CRLF)") + " changed bytes other than <" + key + ">");
        // The same value again writes nothing.
        byte[] after = File.ReadAllBytes(path);
        var again = UiHostRows.Snapshot(c, row);
        row.Set(c, false, row.Get(c, false));
        Check(UiHostRows.Save(path, c, again).Count == 0 && File.ReadAllBytes(path).AsSpan().SequenceEqual(after), row.Id + " rewrote an unchanged value");
    }

    // The bumper camera's FOV is its own element.
    {
        var c = new Settings();
        SettingsPersistence.Write(c, path);
        var row = UiHostRows.Find("cameras.fov");
        var snapshot = UiHostRows.Snapshot(c, row);
        row.Set(c, true, 90);
        var written = UiHostRows.Save(path, c, snapshot);
        Check(written.Count == 1 && written[0] == "BumperFOV" && File.ReadAllText(path).Contains("<BumperFOV>90</BumperFOV>"), "bumper FOV writes BumperFOV");
    }

    // Values the descriptor's ranges allow are clamped into the store's.
    {
        var c = new Settings();
        UiHostRows.Find("ffb.strength").Set(c, false, 140);
        UiHostRows.Find("ffb.smoothing").Set(c, false, 25);
        Check(c.Strength == 100 && Math.Abs(c.Smoothing - .25f) < 1e-6, "clamped and scaled: Strength " + c.Strength + ", Smoothing " + c.Smoothing);
    }
}
finally { try { Directory.Delete(dir, true); } catch { } }

Console.WriteLine(failures == 0 ? "PASS ui-host (" + UiHostRows.Rows.Length + " rows)" : failures + " ui-host check(s) failed");
return failures == 0 ? 0 : 1;
