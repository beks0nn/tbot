using Bot.Capture;
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

        private Button _btnRecord;
        private Button _btnAddWp;
        private Button _btnStart;
        private Button _btnStop;

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
                Height = 50,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(5)
            };

            _btnRecord = new Button { Text = " Toggle Record", Width = 140 };
            _btnRecord.Click += (s, e) => _bot?.ToggleRecord();

            _btnAddWp = new Button { Text = " Add Waypoint", Width = 140 };
            _btnAddWp.Click += (s, e) => _bot?.AddWaypoint();

            _btnStart = new Button { Text = "Start Bot", Width = 120 };
            _btnStart.Click += (s, e) => _bot?.StartBot();

            _btnStop = new Button { Text = "Stop Bot", Width = 120 };
            _btnStop.Click += (s, e) => _bot?.StopBot();

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
                btnInit, _btnRecord, _btnAddWp, _btnStart, _btnStop, _statusLabel
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

            await Task.Delay(500); // let capture pipeline stabilize
            var initialFrame = _capture.CaptureSingleFrame();

            _bot = new BotBrain();
            _statusLabel.Text = "Status: Running";

            _capture.FrameReady += (bmp) =>
            {
                //// Update UI image
                //_pictureBox.Invoke(() =>
                //{
                //    _pictureBox.Image?.Dispose();
                //    _pictureBox.Image = (Image)bmp.Clone();
                //});

                // Forward frame to bot brain
                _bot.ProcessFrame(bmp);
            };
        }
    }
}