using Bot.Navigation;
using System;
using System.Drawing;
using System.Windows.Forms;
using Windows.UI.Input;

namespace Bot;

public partial class MainForm : Form
{
    private readonly string FontName = "Lucida Console";
    private readonly Color Pink = Color.FromArgb(255, 207, 234);
    private readonly Color Blue = Color.FromArgb(175, 233, 255);
    private readonly Color Teal = Color.FromArgb(203, 255, 230);
    private readonly Color Purple = Color.FromArgb(191, 185, 255);
    private readonly Color Yellow = Color.FromArgb(254, 255, 190);
    private readonly Color WGray = Color.FromArgb(192, 192, 192);
    private Label _titleLabel;
    private TabControl _tabs;
    private Panel _titleBar;
    private Button _closeBtn;
    private Button _minimizeBtn;
    private Point _dragStart;

    private readonly BotController _bot = new();

    public MainForm()
    {
        Text = "TBot";
        Width = 900;
        Height = 550;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(35, 35, 35);
        DoubleBuffered = true;

        BuildTitleBar();
        BuildTabs();

        _bot.StatusChanged += msg =>
        {
            if (IsHandleCreated)
                BeginInvoke(() => _titleLabel.Text = $"TBot — {msg}");
        };
    }

    private void BuildTitleBar()
    {
        _titleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32
        };

        // draw gradient background
        _titleBar.Paint += (s, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                _titleBar.ClientRectangle, Purple, Pink, 0f);
            e.Graphics.FillRectangle(brush, _titleBar.ClientRectangle);

            ControlPaint.DrawBorder(
                e.Graphics,
                _titleBar.ClientRectangle,
                WGray,
                ButtonBorderStyle.Solid);
        };

        _titleLabel = new Label
        {
            Text = "ＴＢＯＴ",
            ForeColor = Color.White,
            Font = new Font(FontName, 11, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 8),
            BackColor = Color.Transparent
        };
        _titleBar.Controls.Add(_titleLabel);

        _closeBtn = new Button
        {
            Text = "✖",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(26, 22),
            ForeColor = Color.Black,
            BackColor = SystemColors.Control,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(ClientSize.Width - 30, 5),
            TabStop = false,
            CausesValidation = false,
        };
        _closeBtn.FlatAppearance.BorderColor = Color.DarkGray;
        _closeBtn.FlatAppearance.BorderSize = 1;
        _closeBtn.Click += (s, e) => Close();
        _titleBar.Controls.Add(_closeBtn);

        // --- Minimize button ---
        _minimizeBtn = new Button
        {
            Text = "",
            FlatStyle = FlatStyle.Flat,
            Size = new Size(26, 22),
            ForeColor = Color.Black,
            BackColor = SystemColors.Control,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(ClientSize.Width - 60, 5),
            TabStop = false,
            CausesValidation = false,
        };
        _minimizeBtn.FlatAppearance.BorderColor = Color.DarkGray;
        _minimizeBtn.FlatAppearance.BorderSize = 1;
        _minimizeBtn.Click += (s, e) => WindowState = FormWindowState.Minimized;
        _minimizeBtn.GotFocus += (s, e) => _titleBar.Focus();
        _minimizeBtn.Paint += (s, e) =>
        {
            var btn = (Button)s;
            using var brush = new SolidBrush(btn.ForeColor);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("＿", btn.Font, brush, btn.ClientRectangle, sf);
        };
        _titleBar.Controls.Add(_minimizeBtn);


        _titleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _dragStart = new Point(e.X, e.Y);
        };

        _titleBar.MouseMove += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                Left += e.X - _dragStart.X;
                Top += e.Y - _dragStart.Y;
            }
        };

        _titleBar.Resize += (s, e) =>
        {
            _closeBtn.Left = _titleBar.Width - _closeBtn.Width - 4;
            _minimizeBtn.Left = _closeBtn.Left - _minimizeBtn.Width - 4;
        };

        Controls.Add(_titleBar);
    }

    private void BuildTabs()
    {
        _tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font(FontName, 10),
            Appearance = TabAppearance.FlatButtons,
            ItemSize = new Size(120, 28),
            SizeMode = TabSizeMode.Fixed,
        };
        Controls.Add(_tabs);
        _tabs.BringToFront();

        var tabBot = new TabPage("Controls") { BackColor = Blue, ForeColor = Color.White };
        var tabWp = new TabPage("Cavebot") { BackColor = Blue, ForeColor = Color.White };

        BuildBotTab(tabBot);
        BuildWpTab(tabWp);

        _tabs.TabPages.Add(tabBot);
        _tabs.TabPages.Add(tabWp);
    }

    private void BuildBotTab(TabPage tab)
    {
        // gradient background layer
        tab.UseVisualStyleBackColor = false;
        tab.Paint += (s, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                tab.ClientRectangle,
                Blue,   // top
                Pink,      // bottom
                90f);
            e.Graphics.FillRectangle(brush, tab.ClientRectangle);
        };

        // transparent overlay for controls
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(10),
            BackColor = Color.Transparent
        };

        var btnInit = MakeButton("Initialize Capture", async (s, e) => await _bot.InitializeAsync());
        var btnStart = MakeButton("Start Bot", (s, e) => _bot.Start());
        var btnStop = MakeButton("Stop Bot", (s, e) => _bot.Stop());
        var btnRecord = MakeButton("Toggle Record", (s, e) => _bot.ToggleRecord());

        panel.Controls.AddRange([btnInit, btnStart, btnStop, btnRecord]);
        tab.Controls.Add(panel);
    }

    private void BuildWpTab(TabPage tab)
    {
        tab.UseVisualStyleBackColor = false;
        tab.Paint += (s, e) =>
        {
            using var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                tab.ClientRectangle,
                Blue,   // top
                Pink,      // bottom
                90f);
            e.Graphics.FillRectangle(brush, tab.ClientRectangle);
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10),
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320)); // left list
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // right controls

        // --- Left side: waypoint list ---
        var list = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font(FontName, 11),
            BackColor = SystemColors.Control,
            ForeColor = Purple,
            BorderStyle = BorderStyle.FixedSingle
        };
        layout.Controls.Add(list, 0, 0);

        // --- Right side panel ---
        var rightPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            RowStyles =
        {
            new RowStyle(SizeType.AutoSize),   // direction row
            new RowStyle(SizeType.Percent, 100), // spacer for buttons
            new RowStyle(SizeType.Absolute, 90) // save/load bottom section
        },
            Padding = new Padding(5)
        };
        layout.Controls.Add(rightPanel, 1, 0);

        // --- Direction selector ---
        var dirRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Width = 220,
            Height = 35
        };

        var lblDir = new Label
        {
            Text = "Directon",
            ForeColor = Yellow,
            Width = 80,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font(FontName, 7)
        };

        var comboDir = new ComboBox
        {
            Width = 120,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font(FontName, 10),
            BackColor = Purple,
            ForeColor = Teal
        };
        comboDir.Items.AddRange(["North", "East", "South", "West"]);
        comboDir.SelectedIndex = 0;

        dirRow.Controls.Add(lblDir);
        dirRow.Controls.Add(comboDir);
        rightPanel.Controls.Add(dirRow, 0, 0);

        // --- Action buttons ---
        var btnPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Top,
            AutoSize = true
        };

        var btnWalk = MakeButton("WalkDir", (s, e) =>
        {
            if (comboDir.SelectedItem is string dirText)
            {
                var dir = dirText switch
                {
                    "North" => Direction.North,
                    "East" => Direction.East,
                    "South" => Direction.South,
                    "West" => Direction.West,
                    _ => Direction.North
                };
                _bot.AddRamp(dir);
            }
        });

        var btnAdd = MakeButton("Waypoint", (s, e) => _bot.AddWaypoint());

        var btnUse = MakeButton("Use", (s, e) =>
        {
            if (comboDir.SelectedItem is string dirText)
            {
                var dir = dirText switch
                {
                    "North" => Direction.North,
                    "East" => Direction.East,
                    "South" => Direction.South,
                    "West" => Direction.West,
                    _ => Direction.North
                };
                _bot.AddClickTile(dir);
            }
        });

        btnPanel.Controls.AddRange([btnWalk, btnAdd, btnUse]);
        rightPanel.Controls.Add(btnPanel, 0, 1);


        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 90,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        var saveRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Bottom,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0),
            Margin = new Padding(5, 0, 10, 2),
            AutoSize = true,
            WrapContents = false
        };

        var loadRow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Bottom,
            RightToLeft = RightToLeft.Yes,
            Padding = new Padding(0),
            Margin = new Padding(5, 0, 10, 4),
            AutoSize = true,
            WrapContents = false
        };

        var txtFile = new TextBox
        {
            Width = 160,
            Font = new Font(FontName, 10),
            BackColor = Purple,
            ForeColor = Teal,
            PlaceholderText = "Filename",
            Text = ""
        };

        var dropdown = new ComboBox
        {
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font(FontName, 10),
            BackColor = Purple,
            ForeColor = Teal,
        };

        void RefreshDropdown()
        {
            dropdown.Items.Clear();

            var folder = Path.Combine(AppContext.BaseDirectory, "Paths");
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            foreach (var file in Directory.GetFiles(folder, "*.json"))
                dropdown.Items.Add(Path.GetFileName(file));
        }
        RefreshDropdown();

        var btnSave = MakeButton("Save", (s, e) =>
        {
            var fileName = txtFile.Text.Trim();
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = "";
            var path = $"{fileName}.json";
            _bot.SavePath(path);
            RefreshDropdown();
        });

        var btnLoad = MakeButton("Load", (s, e) =>
        {
            if (dropdown.SelectedItem is string file)
                _bot.LoadPath(file);
        });

        // build layout right-aligned bottom corner
        saveRow.Controls.AddRange([btnSave, txtFile]);
        loadRow.Controls.AddRange([btnLoad, dropdown]);
        bottomPanel.Controls.Add(loadRow);
        bottomPanel.Controls.Add(saveRow);
        rightPanel.Controls.Add(bottomPanel, 0, 2);



        tab.Controls.Add(layout);

        // --- Hook waypoint list updates ---
        _bot.WayPointsUpdated += items =>
        {
            if (IsHandleCreated)
                BeginInvoke(() =>
                {
                    list.BeginUpdate();
                    list.Items.Clear();
                    foreach (var wp in items)
                        list.Items.Add(wp);
                    list.EndUpdate();
                });
        };
    }


    private Button MakeButton(string text, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            Width = 120,
            FlatStyle = FlatStyle.Flat,
            BackColor = SystemColors.Control,
            ForeColor = Color.Black,
            Font = new Font(FontName, 9),
            TextAlign = ContentAlignment.BottomCenter,
        };

        b.FlatAppearance.BorderColor = Yellow;
        b.FlatAppearance.BorderSize = 1;
        b.MouseEnter += (s, e) => b.BackColor = Pink;
        b.MouseLeave += (s, e) => b.BackColor = SystemColors.Control;
        b.Click += onClick;
        return b;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _bot.Dispose();
        base.OnFormClosing(e);
    }
}
