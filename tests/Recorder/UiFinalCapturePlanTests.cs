using System.Text.Json;
using System.Text.RegularExpressions;
using ArtOfSimRally.Testing;

static class UiFinalCapturePlanTests
{
    public static int Run(string repo)
    {
        int checks = 0;
        void Check(bool condition, string reason) { checks++; if (!condition) throw new Exception(reason); }
        var plan = UiFinalCapturePlan.Create().ToArray();
        Check(plan.Length == 18 && plan.Select(s => s.Name).Distinct().Count() == 18, "render queue count or duplicate names");
        var pairs = plan.Where(s => s.ExpandCameraBindings).ToArray();
        string panel = File.ReadAllText(Path.Combine(repo, "src/ArtOfSimRally.Mod/SettingsPanel.cs"));
        string keys = File.ReadAllText(Path.Combine(repo, "src/ArtOfSimRally.Mod/CameraKeys.cs"));
        int productPairs = Regex.Matches(keys, @"new Binding\(""").Count;
        Check(productPairs == 11 && pairs.Length == productPairs, "camera table coverage differs");
        for (int i = 0; i < productPairs; i++)
        {
            Check(pairs[i].View == "Simple" && pairs[i].Page == 2, "camera pair on wrong page");
            Check(pairs[i].Focus == (i + 1 == productPairs ? "cmd-Restore numpad defaults" : "camkey-bind-" + (i + 1)), "pair must target following row, including terminal pair");
        }
        Check(pairs.Last().Name == "final-cameras-lower", "parent disclosure assertion label lost");
        Check(panel.Contains("W.Command(\"camkey-bind-\" + index") && panel.Contains("ArtButtonBinding(c, WheelInput.CameraChannel(index), binding.Label + \" (USB button)\")"), "product keyboard/USB pairing changed");
        Check(panel.Contains("RightButton(\"Restore numpad defaults\", 180)") && panel.Contains("W.Command(\"cmd-\" + label"), "terminal focus ID unavailable");
        var lower = plan.Single(s => s.Name == "final-actions-lower");
        Check(lower.Focus == "disclosure-mod buttons and Settings key" && panel.Contains("Disclosure(_modButtons, \"mod buttons and Settings key\")"), "Look forward needs following disclosure anchor");
        var help = plan.Where(s => s.Page == 4).ToArray();
        Check(help.Length == 2 && help.All(s => s.View == "Simple" && s.Focus == null && !s.ExpandCameraBindings), "Help must only navigate/scroll");
        Check(help[0].Scroll == 0 && help[1].Scroll == 100000, "Help top/lower scroll missing");
        var names = new[] { "open" }.Concat(plan.Select(s => s.Name)).Concat(new[] { "bindings-state", "bindings-saved", "bindings-saved-row", "saved-closed" })
            .Select((s, i) => (i + 1).ToString("00") + "-" + s + ".png").ToArray();
        var expected = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Path.Combine(repo, "tools/testing/Recorder/fixtures/final-ui-expected-shots.json")));
        Check(names.Length == 23 && names.SequenceEqual(expected!), "successor ExpectedShots differs from production plan");
        Console.WriteLine(JsonSerializer.Serialize(new { status = "passed", checks, renderSteps = plan.Length, totalSteps = names.Length + 1, expectedShots = names }));
        return checks;
    }
}
