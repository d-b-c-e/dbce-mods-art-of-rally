using ArtOfSimRally.Mod;
using Dbce.Wheel.Input;

int checks = 0;
void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
const string a = "11111111-1111-1111-1111-111111111111", b = "22222222-2222-2222-2222-222222222222";
SharedBindingDeviceView Device(string guid, string name) => new() { InstanceGuid = guid, Name = name, IsDefault = guid == a };
SharedBindingRowView Row(string action, string note = "") => new() { Action = action, Label = action, IsAxis = true, Active = new(true, "Saved axis"), Note = note };
var host = new FakeHost(); host.DeviceList.Add(Device(a, "Wheel")); host.DeviceList.Add(Device(b, "Shifter")); host.RowList.Add(Row("steer"));
SettingsPanel.Reset();
var initial = SettingsPanel.Draw(host);
Check(host.Calls.Count == 0 && initial.Buttons["bind-steer"], "drawing an idle Saved-mode panel does not start capture or write a profile");
SettingsPanel.Draw(host, pick: 1);
Check(host.Calls.Count == 0, "choosing an explicit device does not begin capture");
SettingsPanel.Draw(host, click: "bind-steer");
Check(host.Calls.Last() == "bind steer " + a, "actual production row uses the explicit already-open device");
host.DeviceList.RemoveAt(0); int before = host.Calls.Count;
var missing = SettingsPanel.Draw(host, click: "bind-steer");
Check(!missing.Buttons["bind-steer"] && host.Calls.Count == before && missing.Texts.Contains("The input connection changed. Choose a device again to bind controls."), "removed A stays visibly unavailable while B remains");
Check(missing.Selected == -1, "missing explicit choice is not displayed as Saved or B");
host.DeviceList.Insert(0, Device(a, "Wheel")); SettingsPanel.Draw(host, click: "bind-steer");
Check(host.Calls.Count == before, "reappearing A requires explicit confirmation");
SettingsPanel.Draw(host, pick: 2); SettingsPanel.Draw(host, click: "bind-steer");
Check(host.Calls.Last() == "bind steer " + b, "explicit B resolves the unavailable choice");
var replacement = new FakeHost(); replacement.DeviceList.Add(Device(b, "Shifter")); replacement.RowList.Add(Row("steer"));
var changed = SettingsPanel.Draw(replacement, click: "bind-steer");
Check(!changed.Buttons["bind-steer"] && replacement.Calls.Count == 0, "replacement host does not inherit an explicit choice even for the same GUID");
SettingsPanel.Draw(replacement, pick: 1); SettingsPanel.Draw(replacement, click: "bind-steer");
Check(replacement.Calls.Last() == "bind steer " + b, "explicit choice confirms replacement host");
replacement.DeviceList.Clear(); before = replacement.Calls.Count;
var empty = SettingsPanel.Draw(replacement, click: "bind-steer");
Check(!empty.Buttons["bind-steer"] && empty.Buttons["clear-steer"] && replacement.Calls.Count == before, "zero devices blocks Bind while preserving supported Clear");
replacement.DeviceList.Add(Device(b, "Shifter")); SettingsPanel.Draw(replacement, pick: 0); SettingsPanel.Draw(replacement, click: "bind-steer");
Check(replacement.Calls.Last() == "bind steer", "deliberate Saved mode calls the unchanged host default");
foreach (string reason in new[] { "This Art candidate does not consume this shared action.", "This profile does not select H-pattern." })
{
    replacement.RowList.Add(Row("unavailable", reason));
    foreach (string command in new[] { "bind-unavailable", "clear-unavailable", "invert-unavailable" })
    {
        before = replacement.Calls.Count; var ui = SettingsPanel.Draw(replacement, click: command);
        Check(!ui.Buttons[command] && replacement.Calls.Count == before && ui.Texts.Any(t => t.Contains(reason)), "actual unavailable row explains and disables " + command);
    }
    replacement.RowList.RemoveAt(1);
}
replacement.CapturingAction = "steer"; replacement.RowList.Add(Row("throttle"));
var listening = SettingsPanel.Draw(replacement);
Check(listening.Buttons["bind-steer"] && listening.Labels["bind-steer"].StartsWith("Cancel") && !listening.Buttons["bind-throttle"] && !listening.Buttons["clear-steer"] && !listening.Buttons["invert-steer"], "capture preserves Cancel and locks other row edits");
SettingsPanel.Draw(replacement, click: "bind-steer"); Check(replacement.Calls.Last() == "cancel", "actual Cancel reaches only the existing host cancellation");
SettingsPanel.LegacyEditing = true; var locked = SettingsPanel.Draw(replacement);
Check(!locked.Buttons["bind-steer"] && !locked.Buttons["clear-steer"] && !locked.Buttons["invert-steer"], "legacy calibration/editor lock remains");
SettingsPanel.LegacyEditing = false; replacement.Access = SharedBindingAccess.ReadOnly; var readOnly = SettingsPanel.Draw(replacement);
Check(!readOnly.Buttons["bind-steer"] && !readOnly.Buttons["clear-steer"], "read-only profile still refuses edits");
Check(readOnly.StatusIds.SequenceEqual(new[] { "bindings-access" }) && readOnly.Texts.Contains("Shared profile is read-only") && readOnly.Texts.Contains(replacement.AccessReason), "actual production read-only status card keeps its title and host reason");
replacement.Access = SharedBindingAccess.Missing; var missingProfile = SettingsPanel.Draw(replacement);
Check(missingProfile.StatusIds.SequenceEqual(new[] { "bindings-access" }) && missingProfile.Texts.Contains("No shared profile") && !missingProfile.Buttons["bind-steer"] && !missingProfile.Buttons["clear-steer"], "missing-profile status card and rows remain read-only");
Check(!host.Calls.Contains("save") && !replacement.Calls.Contains("save"), "presentation checks never save implicitly");
var warningHost = new FakeHost { Draft = true, SaveSucceeds = false };
warningHost.RowList.Add(Row("steer")); warningHost.DeviceList.Add(Device(a, "Wheel"));
SettingsPanel.Reset();
var failed = SettingsPanel.Draw(warningHost, click: "bindings-save");
string failure = warningHost.SaveFailure;
Check(failure.Length > 0 && failed.Texts.Contains(failure), "actual production panel renders failed Save status");
warningHost.CaptureText = "Listening for a new control"; warningHost.CapturingAction = "steer";
var afterCapture = SettingsPanel.Draw(warningHost);
Check(afterCapture.Texts.Contains(failure) && afterCapture.Texts.Contains(warningHost.CaptureText), "warning remains rendered alongside new transient capture text");
warningHost.CapturingAction = null; warningHost.Access = SharedBindingAccess.ReadOnly;
Check(SettingsPanel.Draw(warningHost).Texts.Contains(failure), "later read-only access does not hide unresolved Save failure");
Check(!SettingsPanel.Draw(warningHost, click: "bindings-discard").Texts.Contains(failure) && warningHost.SaveFailure.Length == 0,
    "explicit read-only Discard removes the warning from the production panel");
warningHost.Access = SharedBindingAccess.Editable;
warningHost.Draft = true; SettingsPanel.Draw(warningHost, click: "bindings-save"); warningHost.SaveSucceeds = true;
Check(!SettingsPanel.Draw(warningHost, click: "bindings-save").Texts.Contains(failure) && warningHost.SaveFailure.Length == 0,
    "successful checked Save resolves its earlier warning");
Check(new FakeHost().SaveFailure.Length == 0, "new host begins with no inherited Save failure");
var lostReader = new FakeHost { Access = SharedBindingAccess.ReadOnly, Draft = true, NextStartPending = true };
lostReader.RowList.Add(Row("steer"));
SettingsPanel.Reset();
var recovery = SettingsPanel.Draw(lostReader, click: "bindings-save");
Check(recovery.Buttons["bindings-discard"] && !recovery.Buttons["bindings-save"] && lostReader.Calls.Count == 0,
    "lost reader retains Discard while checked Save stays disabled");
lostReader.CapturingAction = "steer";
Check(!SettingsPanel.Draw(lostReader).Buttons["bindings-discard"], "capture still locks Discard");
lostReader.CapturingAction = null; SettingsPanel.LegacyEditing = true;
Check(!SettingsPanel.Draw(lostReader).Buttons["bindings-discard"], "legacy editing still locks Discard");
SettingsPanel.LegacyEditing = false;
var discarded = SettingsPanel.Draw(lostReader, click: "bindings-discard");
Check(lostReader.Calls.SequenceEqual(new[] { "discard" }) && !lostReader.HasDraft && lostReader.SavedPendingRestart &&
    discarded.Texts.Contains("Saved. Restart the game to use these bindings."),
    "read-only Discard clears only the draft and retains the saved next-start state");
var unsavedOnly = new FakeHost { Access = SharedBindingAccess.ReadOnly, Draft = true };
SettingsPanel.Reset();
Check(!SettingsPanel.Draw(unsavedOnly, click: "bindings-discard").Texts.Contains("Saved. Restart the game to use these bindings."),
    "discarding a draft without saved changes never claims a restart is needed");
Console.WriteLine($"PASS {checks} source-linked Art binding presentation checks; no game, file or device.");

internal sealed class FakeHost : ISharedBindingHost, Dbce.Wheel.Ui.ISharedBindingSaveStatus
{
    private readonly Dbce.Wheel.Ui.BindingSaveStatus saveStatus = new();
    public string SaveFailure => saveStatus.SaveFailure;
    public bool Draft, NextStartPending, SaveSucceeds = true;
    public string CaptureText = "Fixture capture";
    public SharedBindingAccess Access { get; set; } = SharedBindingAccess.Editable;
    public string AccessReason => "Fixture access";
    public string ActiveSource => "Fixture profile";
    public readonly List<SharedBindingDeviceView> DeviceList = new();
    public readonly List<SharedBindingRowView> RowList = new();
    public readonly List<string> Calls = new();
    public IReadOnlyList<SharedBindingDeviceView> Devices => DeviceList;
    public IReadOnlyList<SharedBindingRowView> Rows => RowList;
    public string CapturingAction { get; set; }
    public double CaptureSecondsLeft => 5;
    public string CaptureMessage => CaptureText;
    public bool HasDraft => Draft;
    public bool SavedPendingRestart => NextStartPending;
    public void BeginCapture(string action) => Calls.Add("bind " + action);
    public void BeginCapture(string action, string guid) => Calls.Add("bind " + action + " " + guid);
    public void CancelCapture() { Calls.Add("cancel"); CapturingAction = null; }
    public void Clear(string action) => Calls.Add("clear " + action);
    public void SetInverted(string action, bool inverted) => Calls.Add("invert " + action);
    public bool Save(out string message) { Calls.Add("save"); message = SaveSucceeds ? "Fixture saved" : "Fixture checked Save refused"; if (SaveSucceeds) Draft = false; return saveStatus.Record(SaveSucceeds, message); }
    public void Discard() { Calls.Add("discard"); Draft = false; saveStatus.Discard(); }
}
