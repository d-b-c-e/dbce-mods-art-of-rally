// Engine drawing and the unrelated Art panel are inert. The shared-binding presentation methods
// and policy are linked from production; commands record their enabled state before any click.
namespace ArtOfSimRally.Mod
{
    internal static partial class SettingsPanel
    {
        private static RecordingWidgets W = new();
        private static Dbce.Wheel.Input.ISharedBindingHost Bindings;
        internal static bool LegacyEditing;
        internal static void Reset() { SharedDeviceSelection.SelectSaved(); LegacyEditing = false; }
        internal static RecordingWidgets Draw(Dbce.Wheel.Input.ISharedBindingHost host, string click = null, int pick = -1)
        { Bindings = host; W = new RecordingWidgets { Click = click, Pick = pick }; SharedBindings(); return W; }
        private static void Heading(string text) => W.Texts.Add(text);
        private static void Help(string text) => W.Texts.Add(text);
    }
    internal sealed class RecordingWidgets
    {
        internal string Click;
        internal int Pick = -1, Selected = -2;
        internal readonly Dictionary<string, bool> Buttons = new();
        internal readonly Dictionary<string, string> Labels = new();
        internal readonly List<string> Texts = new();
        internal readonly List<string> StatusIds = new();
        public int ChoiceRow(string id, string label, string help, string[] names, int selected, bool enabled)
        { Selected = selected; return enabled ? Pick : -1; }
        public bool Command(string id, string label, bool enabled = true)
        { Buttons[id] = enabled; Labels[id] = label; return enabled && Click == id; }
        public bool Segment(string id, string label, bool selected, bool enabled)
        { Buttons[id] = enabled; Labels[id] = label; return enabled && Click == id; }
        public void Caption(string text) => Texts.Add(text);
        public bool StatusCard(string id, string title, string detail, bool warning, string action)
        { StatusIds.Add(id); Texts.Add(title); Texts.Add(detail); return false; }
        public void Help(string text) => Texts.Add(text);
        public Dbce.Wheel.Ui.F6RowLayout BeginRow(string label, string text, int width) { Texts.Add(text); return new Dbce.Wheel.Ui.F6RowLayout(); }
        public void EndRow(Dbce.Wheel.Ui.F6RowLayout layout) { }
        public void Spacer() { }
    }
    internal static class SettingsViewPolicy
    {
        internal static string SetupHeading => "Controls setup";
        internal static string[] SetupGuidance => new[] { "Fixture setup" };
    }
}
namespace Dbce.Wheel.Ui
{
    internal struct F6RowLayout { public bool Stacked => false; }
    internal static class SettingsPresentation { internal static float BodyWidth => 800; }
}
namespace UnityEngine
{
    internal static class GUILayout
    {
        internal static object Width(float width) => null;
        internal static void BeginHorizontal(object width) { }
        internal static void EndHorizontal() { }
    }
}
