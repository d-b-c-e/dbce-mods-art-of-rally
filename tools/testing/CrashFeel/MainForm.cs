using System.Diagnostics;
using Dbce.Wheel.Ffb;

namespace CrashFeel;

sealed class MainForm : Form
{
    static readonly string[] GameProcesses = { "artofrally", "art of rally" };
    static readonly string[] Ratings = { "Not felt", "Faint", "Clear", "Too strong" };

    readonly SessionLog _log = new();
    readonly ComboBox _devices = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 420 };
    readonly RadioButton[] _strength, _direction, _load;
    readonly TextBox _note = new() { Width = 520 };
    readonly Label _status = new() { AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };
    readonly Label _detail = new() { AutoSize = true, MaximumSize = new Size(900, 0) };
    readonly ListBox _events = new() { Width = 900, Height = 220, HorizontalScrollbar = true };
    readonly System.Windows.Forms.Timer _drain = new() { Interval = 50 };
    WheelFfbNative.DeviceInfo[] _found = Array.Empty<WheelFfbNative.DeviceInfo>();
    Engine? _engine;
    (string Candidate, string Strength, string Direction, string Load)? _last;

    public MainForm()
    {
        Text = "Crash feel comparison - art of sim rally";
        Font = new Font("Segoe UI", 10f);
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;

        var root = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Padding = new Padding(12) };
        Controls.Add(root);

        root.Controls.Add(Row(new Label { Text = "Wheel", AutoSize = true, Anchor = AnchorStyles.Left }, _devices,
            Button("Find wheels", FindWheels), Button("Connect", Connect), Button("Disconnect", Disconnect)));

        _strength = Radios(Candidates.Strengths.Select(s => s + "%").ToArray(), 1);
        _direction = Radios(new[] { "+", "-" }, 0);
        _load = Radios(new[] { "Off", "+20%", "-20%" }, 0);
        foreach (var radio in _load) radio.CheckedChanged += (_, _) => { if (radio.Checked) ApplyLoad(); };
        root.Controls.Add(Row(Group("Strength", _strength), Group("Direction", _direction), Group("Cornering load", _load)));
        root.Controls.Add(new Label { AutoSize = true, Text = "Strength is the Crash strength setting at full crash intensity. Cornering load holds a steady 20% steering force, like mid-corner." });

        var kicks = new FlowLayoutPanel { AutoSize = true, MaximumSize = new Size(940, 0) };
        var tips = new ToolTip();
        foreach (var candidate in Candidates.All)
        {
            var button = Button(candidate.Key + "   " + candidate.Name, () => Play(candidate));
            button.Size = new Size(300, 52); button.TextAlign = ContentAlignment.MiddleLeft;
            tips.SetToolTip(button, candidate.Detail);
            button.MouseEnter += (_, _) => _detail.Text = candidate.Key + ": " + candidate.Detail;
            kicks.Controls.Add(button);
        }
        root.Controls.Add(Caption("Kicks: click or press A-G. One kick per press."));
        root.Controls.Add(kicks);
        root.Controls.Add(_detail);

        root.Controls.Add(Caption("How did the last kick feel? Click or press 1-4."));
        root.Controls.Add(Row(Ratings.Select((text, i) => (Control)Button((i + 1) + "  " + text, () => Rate(i))).ToArray()));
        root.Controls.Add(Row(_note, Button("Add note", AddNote)));

        var stop = Button("STOP (Esc)", () => StopAll("STOP pressed"));
        stop.BackColor = Color.Firebrick; stop.ForeColor = Color.White; stop.Size = new Size(200, 44);
        root.Controls.Add(Row(stop, _status));
        root.Controls.Add(_events);
        root.Controls.Add(new Label { AutoSize = true, Text = "Log: " + _log.Path });

        _drain.Tick += (_, _) => Drain();
        _drain.Start();
        Deactivate += (_, _) => { if (_engine != null) StopAll("window lost focus"); };
        FormClosing += (_, _) => { _drain.Stop(); Drain(); _engine?.Dispose(); _engine = null; _log.Write("closed"); _log.Dispose(); };
        KeyDown += OnKey;

        string native = AppContext.BaseDirectory;
        if (!WheelFfbNative.Load(native, "WheelFfb.dll")) Status("Native toolkit failed to load: " + WheelFfbNative.LastError);
        else
        {
            WheelFfbNative.LogTo(Path.Combine(Path.GetDirectoryName(_log.Path)!, "ffb.log"));
            Status("Close art of rally, then Find wheels and Connect.");
        }
        _log.Write("opened", detail: "native " + WheelFfbNative.Version);
    }

    static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4) };
        row.Controls.AddRange(controls);
        return row;
    }
    static Label Caption(string text) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold), Margin = new Padding(0, 10, 0, 2) };
    static Button Button(string text, Action click)
    {
        var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(0, 34) };
        button.Click += (_, _) => click();
        return button;
    }
    static RadioButton[] Radios(string[] labels, int selected) =>
        labels.Select((text, i) => new RadioButton { Text = text, AutoSize = true, Checked = i == selected }).ToArray();
    static GroupBox Group(string title, RadioButton[] radios)
    {
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        flow.Controls.AddRange(radios);
        var box = new GroupBox { Text = title, AutoSize = true, Padding = new Padding(8), MinimumSize = new Size(TextRenderer.MeasureText(title, SystemFonts.MessageBoxFont).Width + 40, 0) };
        box.Controls.Add(flow);
        return box;
    }

    static string Selected(RadioButton[] radios) => radios.First(r => r.Checked).Text;
    float StrengthValue => int.Parse(Selected(_strength).TrimEnd('%')) / 100f;
    int Sign => Selected(_direction) == "+" ? 1 : -1;
    float LoadValue => Selected(_load) switch { "+20%" => Candidates.LoadMagnitude, "-20%" => -Candidates.LoadMagnitude, _ => 0f };

    void FindWheels()
    {
        _found = WheelFfbNative.ListFfbDevices();
        _devices.Items.Clear();
        foreach (var device in _found) _devices.Items.Add(device.Label + "  " + device.InstanceGuid);
        if (_found.Length > 0) _devices.SelectedIndex = 0;
        Status(_found.Length == 0 ? "No force feedback wheel found." : _found.Length + " wheel(s) found. Check the selection, then Connect.");
        _log.Write("find", detail: string.Join(" | ", _found.Select(d => d.Name + " " + d.InstanceGuid)));
    }

    void Connect()
    {
        if (_engine != null) { Status("Already connected."); return; }
        var running = Process.GetProcesses().Where(p => GameProcesses.Contains(p.ProcessName, StringComparer.OrdinalIgnoreCase)).ToList();
        if (running.Count > 0) { Status("Close art of rally first (it holds the wheel): PID " + running[0].Id); return; }
        if (_devices.SelectedIndex < 0) { Status("Find wheels and choose one first."); return; }
        var device = _found[_devices.SelectedIndex];
        _engine = Engine.Connect(device, Handle, out string message);
        Status(message);
        _log.Write(_engine == null ? "connect-failed" : "connected", detail: message);
        if (_engine != null) ApplyLoad();
    }

    void Disconnect()
    {
        if (_engine == null) return;
        _engine.Dispose(); _engine = null;
        Status("Disconnected.");
        _log.Write("disconnected");
    }

    void ApplyLoad()
    {
        if (_engine == null) return;
        _engine.SetLoad(LoadValue);
        _log.Write("load", load: Selected(_load));
        Status("Cornering load " + Selected(_load) + ".");
    }

    void Play(Candidate candidate)
    {
        if (_engine == null) { Status("Connect a wheel first."); return; }
        if (_engine.Busy) return;
        var plan = candidate.Build(StrengthValue, Sign);
        if (plan.Any(step => !_engine.Has(step))) { Status(candidate.Key + " needs an effect slot this wheel did not create."); return; }
        string label = candidate.Key + " " + candidate.Name;
        if (!_engine.Play(label, plan)) return;
        _last = (label, Selected(_strength).TrimEnd('%'), Selected(_direction), Selected(_load));
        _log.Write("kick", label, _last.Value.Strength, _last.Value.Direction, _last.Value.Load);
        Status("Played " + label + " at " + Selected(_strength) + ", direction " + Selected(_direction) + ". Rate it 1-4.");
    }

    void Rate(int index)
    {
        if (_last is not { } last) { Status("Play a kick first."); return; }
        _log.Write("rating", last.Candidate, last.Strength, last.Direction, last.Load, (index + 1) + " " + Ratings[index]);
        Add("rated " + last.Candidate + " @" + last.Strength + "% dir " + last.Direction + " load " + last.Load + ": " + Ratings[index]);
        Status("Recorded: " + Ratings[index] + ".");
    }

    void AddNote()
    {
        if (string.IsNullOrWhiteSpace(_note.Text)) return;
        _log.Write("note", _last?.Candidate ?? "", detail: _note.Text.Trim());
        Add("note: " + _note.Text.Trim());
        _note.Clear();
    }

    void StopAll(string reason)
    {
        _engine?.Stop();
        _load[0].Checked = true;
        _log.Write("stop", detail: reason);
        Status("Stopped: " + reason + ". Load is off.");
    }

    void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { StopAll("Esc"); e.Handled = true; return; }
        if (_note.Focused) { if (e.KeyCode == Keys.Enter) { AddNote(); e.SuppressKeyPress = true; } return; }
        if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D4) { Rate(e.KeyCode - Keys.D1); e.SuppressKeyPress = true; return; }
        var candidate = Candidates.All.FirstOrDefault(c => e.KeyCode == Keys.A + (c.Key - 'A'));
        if (candidate != null) { Play(candidate); e.SuppressKeyPress = true; }
    }

    void Drain()
    {
        while (_engine != null && _engine.Finished.TryDequeue(out var result))
        {
            _log.Write("result", detail: result);
            Add(result);
        }
    }

    void Add(string line)
    {
        _events.Items.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + line);
        if (_events.Items.Count > 200) _events.Items.RemoveAt(_events.Items.Count - 1);
    }

    void Status(string text) => _status.Text = text;
}
