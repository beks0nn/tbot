using Bot.Capture;
using Bot.Navigation; // <-- Add this for Direction enum
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Bot
{
    public partial class MainForm : Form
    {
        private PictureBox _pictureBox;
        private Label _statusLabel;
        private CaptureService? _capture;
        private BotBrain? _bot;

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

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 150,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            _waypointList = new ListBox
            {
                Dock = DockStyle.Right,
                Width = 250,
                Font = new Font("Consolas", 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            Controls.Add(_waypointList);



            // --- Directional Waypoint Buttons ---
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
            _btnSave.Click += (s, e) =>
            {
                if (_bot == null) return;
                _bot.SavePath("path.json");
                RefreshWaypointList();
            };

            _btnLoad = new Button { Text = "Load Path", Width = 120 };
            _btnLoad.Click += (s, e) =>
            {
                if (_bot == null) return;
                _bot.LoadPath("path.json");
                RefreshWaypointList();
            };


            // --- Controls 
            _btnStart = new Button { Text = "Start Bot", Width = 120 };
            _btnStart.Click += (s, e) => _bot?.StartBot();

            _btnStop = new Button { Text = "Stop Bot", Width = 120 };
            _btnStop.Click += (s, e) => _bot?.StopBot();

            _btnRecord = new Button { Text = " Toggle Record", Width = 140 };
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

            panel.Controls.AddRange(new System.Windows.Forms.Control[] {
                btnInit, _btnRecord, _btnAddWp, _btnStart, _btnStop, _statusLabel,
                _btnWpNorth, _btnWpSouth, _btnWpWest, _btnWpEast,
                _btnSave, _btnLoad
            });

            _pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            Controls.Add(_pictureBox);
            Controls.Add(panel);

            FormClosing += (s, e) => _capture?.Dispose();
        }

        private async Task InitializeBotAsync()
        {
            _statusLabel.Text = "Status: Initializing capture...";
            _capture = new CaptureService();
            _capture.Start();

            // --- Warm up the DX11 capture pipeline ---
            for (int i = 0; i < 3; i++)
            {
                using var warmup = _capture.CaptureSingleFrame();
                await Task.Delay(20);
            }
            // -----------------------------------------
            var initialFrame = _capture.CaptureSingleFrame();

            _bot = new BotBrain();
            _statusLabel.Text = "Status: Running";

            _capture.FrameReady += (bmp) =>
            {
                _bot.ProcessFrame(bmp);
                Invoke(() => RefreshWaypointList());
            };
        }

        private void RefreshWaypointList()
        {
            if (_bot == null) return;
            var (wps, currentIndex) = _bot.GetWaypoints();

            _waypointList.Items.Clear();
            for (int i = 0; i < wps.Count; i++)
            {
                var wp = wps[i];
                var marker = (i == currentIndex) ? "~ " : "  ";
                _waypointList.Items.Add($"{marker}{wp.Type,-6} {wp.Info}");
            }
        }
    }
}