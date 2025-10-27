using Bot.Capture;
using Bot.Navigation;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Bot;

public partial class MainForm : Form
{
    private CaptureService? _capture;
    private BotBrain? _bot;
    private CancellationTokenSource? _loopCts;

    private PictureBox _pictureBox;
    private Label _statusLabel;
    private ListBox _waypointList;

    private Button _btnRecord;
    private Button _btnAddWp;
    private Button _btnStart;
    private Button _btnStop;
    private Button _btnWpNorth;
    private Button _btnWpSouth;
    private Button _btnWpWest;
    private Button _btnWpEast;
    private Button _btnSave;
    private Button _btnLoad;

    public MainForm()
    {
        Text = "TibiaBot Controller";
        Width = 1000;
        Height = 700;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        // --- Top control panel ---
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 150,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(5)
        };

        // --- UI Elements ---
        _waypointList = new ListBox
        {
            Dock = DockStyle.Right,
            Width = 250,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle
        };
        Controls.Add(_waypointList);

        _btnAddWp = new Button { Text = "Add Waypoint", Width = 140 };
        _btnAddWp.Click += (s, e) => _bot?.AddWaypoint();

        _btnWpNorth = new Button { Text = "Ramp North", Width = 100 };
        _btnWpNorth.Click += (s, e) => _bot?.AddRamp(Direction.North);

        _btnWpSouth = new Button { Text = "Ramp South", Width = 100 };
        _btnWpSouth.Click += (s, e) => _bot?.AddRamp(Direction.South);

        _btnWpWest = new Button { Text = "Ramp West", Width = 100 };
        _btnWpWest.Click += (s, e) => _bot?.AddRamp(Direction.West);

        _btnWpEast = new Button { Text = "Ramp East", Width = 100 };
        _btnWpEast.Click += (s, e) => _bot?.AddRamp(Direction.East);

        _btnSave = new Button { Text = "Save Path", Width = 120 };
        _btnSave.Click += (s, e) => { if (_bot != null) { _bot.SavePath("path.json"); RefreshWaypointList(); } };

        _btnLoad = new Button { Text = "Load Path", Width = 120 };
        _btnLoad.Click += (s, e) => { if (_bot != null) { _bot.LoadPath("path.json"); RefreshWaypointList(); } };

        _btnStart = new Button { Text = "Start Bot", Width = 120 };
        _btnStart.Click += (s, e) => _bot?.StartBot();

        _btnStop = new Button { Text = "Stop Bot", Width = 120 };
        _btnStop.Click += (s, e) => _bot?.StopBot();

        _btnRecord = new Button { Text = "Toggle Record", Width = 140 };
        _btnRecord.Click += (s, e) => _bot?.ToggleRecord();

        var btnInit = new Button { Text = "Initialize Capture", Width = 180 };
        btnInit.Click += async (s, e) => await InitializeBotAsync();

        _statusLabel = new Label
        {
            Text = "Status: Idle",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 15, 0, 0)
        };

        panel.Controls.AddRange(
        [
            _statusLabel, btnInit, _btnRecord, _btnAddWp, _btnStart, _btnStop,
            _btnWpNorth, _btnWpSouth, _btnWpWest, _btnWpEast,
            _btnSave, _btnLoad
        ]);

        _pictureBox = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BorderStyle = BorderStyle.FixedSingle
        };

        Controls.Add(_pictureBox);
        Controls.Add(panel);

        FormClosing += (s, e) => StopAll();
    }

    private async Task InitializeBotAsync()
    {
        _statusLabel.Text = "Status: Initializing capture...";
        _capture = new CaptureService();
        _capture.Start();

        // Warm up capture pipeline
        for (int i = 0; i < 3; i++)
        {
            using var warmup = _capture.CaptureSingleFrame();
            await Task.Delay(30);
        }

        _bot = new BotBrain();
        _statusLabel.Text = "Status: Running";

        _loopCts = new CancellationTokenSource();
        _ = Task.Run(() => MainLoop(_loopCts.Token));
    }

    private async Task MainLoop(CancellationToken token)
    {
        const int TickRateMs = 45;

        while (!token.IsCancellationRequested)
        {
            var start = DateTime.UtcNow;

            using var frame = _capture?.GetLatestFrameCopy();
            if (frame != null)
                _bot?.ProcessFrame(frame);

            // UI updates should stay responsive
            Invoke(new Action(RefreshWaypointList));

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var delay = Math.Max(0, TickRateMs - (int)elapsed);

            await Task.Delay(delay, token);
        }
    }

    private void RefreshWaypointList()
    {
        if (_bot == null) return;
        var (wps, currentIndex) = _bot.GetWaypoints();

        _waypointList.BeginUpdate();
        _waypointList.Items.Clear();

        for (int i = 0; i < wps.Count; i++)
        {
            var wp = wps[i];
            var marker = (i == currentIndex) ? "~ " : "  ";
            _waypointList.Items.Add($"{marker}{wp.Type,-6} {wp.Info}");
        }

        _waypointList.EndUpdate();
    }

    private void StopAll()
    {
        try
        {
            _loopCts?.Cancel();
            _capture?.Dispose();
        }
        catch { /* ignore */ }
    }
}