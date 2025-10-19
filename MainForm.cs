using Bot.Capture;
using System.Windows.Forms;

namespace Bot;

public partial class MainForm : Form
{
    private PictureBox _pictureBox;
    private CaptureService _capture;
    private BotBrain _bot;

    public MainForm()
    {
        Text = "TibiaBot";
        Width = 800;
        Height = 600;

        _pictureBox = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom };
        Controls.Add(_pictureBox);

        var startButton = new Button { Text = "Start Bot", Dock = DockStyle.Top };
        startButton.Click += StartBot;
        Controls.Add(startButton);
    }

    private async void StartBot(object? sender, EventArgs e)
    {
        _capture = new CaptureService();
        _capture.Start();

        //_capture.FrameReady += OnFirstFrame;
        // Give DirectX capture a moment to initialize (300–500ms)
        await Task.Delay(500);

        var initialFrame = _capture.CaptureSingleFrame();
        _bot = new BotBrain(_capture, initialFrame);

        _capture.FrameReady += (bmp) =>
        {
            // Display latest frame
            _pictureBox.Invoke((Action)(() =>
            {
                _pictureBox.Image?.Dispose();
                _pictureBox.Image = (System.Drawing.Image)bmp.Clone();
            }));

            // Forward to bot brain
            _bot.ProcessFrame(bmp);
        };
    }

    //private void OnFirstFrame(Bitmap bmp)
    //{
    //    _capture.FrameReady -= OnFirstFrame; // only once

    //    _bot = new BotBrain(_capture, (Bitmap)bmp.Clone());

    //    // Now hook up regular capture
    //    _capture.FrameReady += (frame) =>
    //    {
    //        _pictureBox.Invoke((Action)(() =>
    //        {
    //            _pictureBox.Image?.Dispose();
    //            _pictureBox.Image = (Image)frame.Clone();
    //        }));

    //        _bot.ProcessFrame(frame);
    //    };
    //}
}
