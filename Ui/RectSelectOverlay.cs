
namespace Bot.Ui;

public sealed class RectSelectOverlay : Form
{
    private Point _start;
    private Point _current;
    private bool _dragging;

    public Rectangle SelectedRect { get; private set; } = Rectangle.Empty;

    public RectSelectOverlay()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = SystemInformation.VirtualScreen; // all monitors
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;

        Cursor = Cursors.Cross;
        KeyPreview = true;

        // Semi-transparent dim
        BackColor = Color.Black;
        Opacity = 0.25;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }
        base.OnKeyDown(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _start = PointToScreen(e.Location);
        _current = _start;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (!_dragging) return;
        _current = PointToScreen(e.Location);
        Invalidate();
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = false;

        var a = _start;
        var b = _current;

        var x1 = Math.Min(a.X, b.X);
        var y1 = Math.Min(a.Y, b.Y);
        var x2 = Math.Max(a.X, b.X);
        var y2 = Math.Max(a.Y, b.Y);

        SelectedRect = new Rectangle(x1, y1, x2 - x1, y2 - y1);

        if (SelectedRect.Width < 3 || SelectedRect.Height < 3)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        if (!_dragging) return;

        var a = _start;
        var b = _current;

        var x1 = Math.Min(a.X, b.X);
        var y1 = Math.Min(a.Y, b.Y);
        var x2 = Math.Max(a.X, b.X);
        var y2 = Math.Max(a.Y, b.Y);

        var rect = new Rectangle(x1 - Bounds.X, y1 - Bounds.Y, x2 - x1, y2 - y1);

        using var pen = new Pen(Color.Lime, 2);
        e.Graphics.DrawRectangle(pen, rect);

        using var fill = new SolidBrush(Color.FromArgb(40, Color.Lime));
        e.Graphics.FillRectangle(fill, rect);
    }

    public static Rectangle? Prompt()
    {
        using var overlay = new RectSelectOverlay();
        var res = overlay.ShowDialog();
        return res == DialogResult.OK ? overlay.SelectedRect : null;
    }
}
