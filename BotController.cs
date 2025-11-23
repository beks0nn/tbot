using Bot.Capture;
using Bot.Navigation;
using Bot.Tasks;
using OpenCvSharp;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Networking;

namespace Bot;

public sealed class BotController
{
    private readonly BotBrain _brain = new();
    private readonly BotContext _ctx = new();
    private readonly PathRepository _pathRepo = new();
    private CaptureService? _capture;
    private CancellationTokenSource? _loopCts;
    private IntPtr _tibiaHandle;
    private IntPtr _tibiaProcess;
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);
    const int PROCESS_WM_READ = 0x0010;


    public event Action<string>? StatusChanged;
    public event Action<IEnumerable<string>>? WayPointsUpdated;

    public bool IsInitialized => _capture != null;

    public async Task InitializeAsync()
    {
        if (IsInitialized)
        {
            Console.WriteLine("[Bot] Capture already initialized.");
            return;
        }

        _capture = new CaptureService();
        _capture.Start();

        StatusChanged?.Invoke("Preloading assets and maps...");
        // kick off background load right away
        var loadTask = Task.Run(async () =>
        {
            // assets
            var lootFolder = "Assets/Loot";
            var foodFolder = "Assets/Food";

            _ctx.LootTemplates = Directory.GetFiles(lootFolder, "*.png")
                .Select(path => Cv2.ImRead(path, ImreadModes.Grayscale))
                .ToArray();

            _ctx.FoodTemplates = Directory.GetFiles(foodFolder, "*.png")
                .Select(path => Cv2.ImRead(path, ImreadModes.Grayscale))
                .ToArray();

            Console.WriteLine("[Controller] Cached assets");

            // maps
            await _brain.InitializeAsync();
        });

        // while maps/assets load, show process picker
        var tibia = PromptForTargetProcess();
        if (tibia == null || tibia.MainWindowHandle == IntPtr.Zero)
            throw new InvalidOperationException("No valid process selected.");

        _ctx.BaseAddy = (int)tibia.MainModule.BaseAddress;

        _tibiaProcess = OpenProcess(PROCESS_WM_READ, false, tibia.Id);

        _tibiaHandle = tibia.MainWindowHandle;
        _ctx.GameWindowHandle = _tibiaHandle;
        Console.WriteLine($"[Controller] Attached to {tibia.ProcessName} window handle.");

        StatusChanged?.Invoke("Warming up capture...");
        // Warm up capture pipeline
        for (int i = 0; i < 3; i++)
        {
            using var warm = _capture.CaptureSingleFrame();
            await Task.Delay(30);
        }

        // wait for background work if not done yet
        await loadTask;

        //start main loop
        _loopCts = new CancellationTokenSource();
        _ = Task.Run(() => MainLoop(_loopCts.Token));

        Console.WriteLine("[Controller] Started main loop");
        StatusChanged?.Invoke("Ready to go....");
    }

    private async Task MainLoop(CancellationToken token)
    {
        const int TickRateMs = 45;

        while (!token.IsCancellationRequested)
        {
            var start = DateTime.UtcNow;
            using var frame = _capture?.GetLatestFrameCopy();

            if (frame != null && !ShouldSuspend())
                _brain.ProcessFrame(frame, _ctx, _pathRepo, _tibiaProcess);

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var delay = Math.Max(0, TickRateMs - (int)elapsed);
            await Task.Delay(delay, token);
        }
    }

    public void Start() 
    {
        _brain.StartBot(_ctx);
        StatusChanged?.Invoke($"Running...");
    }
    public void Stop() 
    { 
        _brain.StopBot(_ctx);
        StatusChanged?.Invoke($"Stopped...");
    }
    public void ToggleRecord() 
    {
        _ctx.RecordMode = !_ctx.RecordMode;
    }
    public void AddWaypoint()
    {
        if (!_ctx.PlayerPosition.IsValid)
        {
            Console.WriteLine("[Bot] Cannot add waypoint – player position unknown.");
            return;
        }

        _pathRepo.Add(new Waypoint(
            WaypointType.Move,
            _ctx.PlayerPosition.X,
            _ctx.PlayerPosition.Y,
            _ctx.PlayerPosition.Floor));
        WayPointsUpdated?.Invoke(GetWaypoints());
    }
    public void AddRamp(Direction dir)
    {
        if (!_ctx.PlayerPosition.IsValid)
        {
            Console.WriteLine("[Bot] Cannot add ramp – player position unknown.");
            return;
        }

        _pathRepo.Add(new Waypoint(
            WaypointType.Step,
            _ctx.PlayerPosition.X,
            _ctx.PlayerPosition.Y,
            _ctx.PlayerPosition.Floor,
            dir));
        WayPointsUpdated?.Invoke(GetWaypoints());
    }
    public void AddClickTile(Direction dir)
    {
        if (!_ctx.PlayerPosition.IsValid)
        {
            Console.WriteLine("[Bot] Cannot add click in tile – player position unknown.");
            return;
        }

        _pathRepo.Add(new Waypoint(
            WaypointType.RightClick,
            _ctx.PlayerPosition.X,
            _ctx.PlayerPosition.Y,
            _ctx.PlayerPosition.Floor,
            dir));
        WayPointsUpdated?.Invoke(GetWaypoints());
    }
    public void SavePath(string path)
    {
        _pathRepo.SaveToJson(path);
        WayPointsUpdated?.Invoke(GetWaypoints());
    }
    public void LoadPath(string path)
    {
        _pathRepo.LoadFromJson(path);
        WayPointsUpdated?.Invoke(GetWaypoints());
    }

    public List<string> GetWaypoints()
    {
        var list = _pathRepo.Waypoints
            .Select(wp => $"{wp.Type} {(wp.Type == WaypointType.Move ? $"({wp.X},{wp.Y},{wp.Z})" : $"{wp.Dir}")}")
            .ToList();

        return list;
    }

    public static Process? PromptForTargetProcess()
    {
        var processes = Process.GetProcesses()
            .Where(p => !string.IsNullOrEmpty(p.MainWindowTitle))
            .OrderBy(p => p.ProcessName)
            .ToList();

        var form = new Form
        {
            Width = 420,
            Height = 400,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterScreen,
            BackColor = Color.FromArgb(35, 35, 35),
            ShowIcon = false,
            ShowInTaskbar = false,
        };

        // --- Colors ---
        Color Purple = Color.FromArgb(191, 185, 255);
        Color Pink = Color.FromArgb(255, 207, 234);
        Color WGray = Color.FromArgb(192, 192, 192);
        Color Yellow = Color.FromArgb(254, 255, 190);
        string FontName = "Lucida Console";

        // --- Paint gradient background ---
        form.Paint += (s, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                form.ClientRectangle,
                Color.FromArgb(175, 233, 255),
                Color.FromArgb(255, 207, 234),
                90f);
            e.Graphics.FillRectangle(brush, form.ClientRectangle);
            // Draw border in SystemColors.Control
            using var pen = new Pen(SystemColors.Control, 2);
            e.Graphics.DrawRectangle(
                pen,
                new Rectangle(1, 1, form.ClientSize.Width - 2, form.ClientSize.Height - 2));
        };

        // --- Title bar ---
        var titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32
        };
        titleBar.Paint += (s, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                titleBar.ClientRectangle, Purple, Pink, 0f);
            e.Graphics.FillRectangle(brush, titleBar.ClientRectangle);
            ControlPaint.DrawBorder(e.Graphics, titleBar.ClientRectangle, WGray, ButtonBorderStyle.Solid);
        };

        var titleLabel = new Label
        {
            Text = "Select Game Window",
            ForeColor = Color.White,
            Font = new Font(FontName, 10, FontStyle.Bold),
            AutoSize = true,
            Location = new System.Drawing.Point(14, 8),
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(titleLabel);

        System.Drawing.Point dragStart = System.Drawing.Point.Empty;
        titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
                dragStart = new System.Drawing.Point(e.X, e.Y);
        };
        titleBar.MouseMove += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                form.Left += e.X - dragStart.X;
                form.Top += e.Y - dragStart.Y;
            }
        };

        form.Controls.Add(titleBar);

        // --- Main content container (under title bar) ---
        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 4, 8, 8),
            BackColor = Color.Transparent
        };

        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font(FontName, 10),
            BackColor = SystemColors.Control,
            ForeColor = Color.FromArgb(191, 185, 255),
            BorderStyle = BorderStyle.FixedSingle
        };
        foreach (var p in processes)
            list.Items.Add($"{p.ProcessName}  —  {p.MainWindowTitle}");

        var btnAttach = new Button
        {
            Text = "Attach",
            Width = 120,
            Height = 28,
            FlatStyle = FlatStyle.Flat,
            BackColor = SystemColors.Control,
            ForeColor = Color.Black,
            Font = new Font(FontName, 9),
            Anchor = AnchorStyles.None
        };
        btnAttach.FlatAppearance.BorderColor = Yellow;
        btnAttach.FlatAppearance.BorderSize = 1;
        btnAttach.MouseEnter += (s, e) => btnAttach.BackColor = Pink;
        btnAttach.MouseLeave += (s, e) => btnAttach.BackColor = SystemColors.Control;
        btnAttach.Click += (s, e) => form.DialogResult = DialogResult.OK;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.Controls.Add(list, 0, 0);
        layout.Controls.Add(btnAttach, 0, 1);
        contentPanel.Controls.Add(layout);

        form.Controls.Add(contentPanel);

        var result = form.ShowDialog();
        if (result == DialogResult.OK && list.SelectedIndex >= 0)
        {
            return processes[list.SelectedIndex];
        }
            

        form.Dispose();
        return null;
    }




    private bool ShouldSuspend()
    {
        var active = GetForegroundWindow();
        if (active != _tibiaHandle) return true;
        if (IsIconic(_tibiaHandle)) return true;
        return false;
    }

    public void Dispose()
    {
        _loopCts?.Cancel();
        _capture?.Dispose();
    }
}