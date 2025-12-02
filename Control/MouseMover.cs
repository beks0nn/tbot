using OpenCvSharp;
using System;
using System.Runtime.InteropServices;

namespace Bot.Control;

public sealed class MouseMover
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out System.Drawing.Point lpPoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP = 0x0010;

    private const byte VK_CONTROL = 0x11;
    private const int KEYEVENTF_KEYUP = 0x0002;
    private static readonly Random _rng = new();


    public void RightClickSlow(int x, int y)
    {
        GetCursorPos(out var p);

        // small human inaccuracy around target
        int tx = x + _rng.Next(-2, 3);
        int ty = y + _rng.Next(-2, 3);

        SmoothMoveHuman(p.X, p.Y, tx, ty);

        Thread.Sleep(_rng.Next(12, 35));

        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, 0);
        Thread.Sleep(_rng.Next(20, 45));
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
    }

    public void LeftClickSlow(int x, int y)
    {
        GetCursorPos(out var p);

        int tx = x + _rng.Next(-2, 3);
        int ty = y + _rng.Next(-2, 3);

        SmoothMoveHuman(p.X, p.Y, tx, ty);

        Thread.Sleep(_rng.Next(12, 35));

        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        Thread.Sleep(_rng.Next(20, 45));
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
    }

    public void LeftClickTile((int X, int Y) tileSlot, IClientProfile profile)
    {
        var (px, py) = TileToScreenPixel(tileSlot, profile);
        LeftClickSlow(px, py);
    }

    public void RightClickTile((int X, int Y) tileSlot, IClientProfile profile)
    {
        var (px, py) = TileToScreenPixel(tileSlot, profile);
        RightClickSlow(px, py);
    }

    public void CtrlDragLeftTile((int X, int Y) fromTile, (int X, int Y) toTile, IClientProfile profile)
    {
        var (fromX, fromY) = TileToScreenPixel(fromTile, profile);
        var (toX, toY) = TileToScreenPixel(toTile, profile);
        CtrlDragLeft(fromX, fromY, toX, toY);
    }

    public void CtrlDragLeft(int fromX, int fromY, int toX, int toY)
    {
        int fx = fromX + _rng.Next(-2, 3);
        int fy = fromY + _rng.Next(-2, 3);
        int tx = toX + _rng.Next(-2, 3);
        int ty = toY + _rng.Next(-2, 3);

        GetCursorPos(out var p);
        SmoothMoveHuman(p.X, p.Y, fx, fy);
        Thread.Sleep(_rng.Next(20, 50));

        keybd_event(VK_CONTROL, 0, 0, 0);
        Thread.Sleep(_rng.Next(30, 75));

        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        Thread.Sleep(_rng.Next(25, 60));

        SmoothMoveHuman(fx, fy, tx, ty);

        Thread.Sleep(_rng.Next(20, 50));
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);

        Thread.Sleep(_rng.Next(30, 80));
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
    }

    private static void SmoothMoveHuman(int fromX, int fromY, int toX, int toY)
    {
        int dx = toX - fromX;
        int dy = toY - fromY;

        double distance = Math.Sqrt(dx * dx + dy * dy);

        int minSteps = 8;
        int maxSteps = 26;

        int steps = (int)Math.Clamp(
            distance / 20.0,
            minSteps,
            maxSteps
        );

        // Control point spread scales with distance
        double spread = Math.Min(1.0, distance / 300.0) * 80;

        // Randomized control points for natural curve
        double c1x = fromX + dx * 0.25 + _rng.NextDouble() * spread - spread / 2;
        double c1y = fromY + dy * 0.25 + _rng.NextDouble() * spread - spread / 2;

        double c2x = fromX + dx * 0.75 + _rng.NextDouble() * spread - spread / 2;
        double c2y = fromY + dy * 0.75 + _rng.NextDouble() * spread - spread / 2;

        for (int i = 1; i <= steps; i++)
        {
            double t = i / (double)steps;

            // Ease-out for braking near target
            double eased = 1 - Math.Pow(1 - t, 3);

            double x =
                Math.Pow(1 - eased, 3) * fromX +
                3 * Math.Pow(1 - eased, 2) * eased * c1x +
                3 * (1 - eased) * eased * eased * c2x +
                Math.Pow(eased, 3) * toX;

            double y =
                Math.Pow(1 - eased, 3) * fromY +
                3 * Math.Pow(1 - eased, 2) * eased * c1y +
                3 * (1 - eased) * eased * eased * c2y +
                Math.Pow(eased, 3) * toY;

            SetCursorPos((int)x, (int)y);

            int baseDelay =
                distance > 300 ? 2 :
                distance > 150 ? 4 :
                                  7;

            int braking = (int)(Math.Pow(t, 2.3) * 16);

            Thread.Sleep(baseDelay + braking + _rng.Next(0, 3));
        }

        // Final micro-adjust
        SetCursorPos(toX, toY);
    }


    private static (int X, int Y) TileToScreenPixel((int X, int Y) tileSlot, IClientProfile profile)
    {
        var (visibleX, visibleY) = profile.VisibleTiles;
        int centerTileX = visibleX / 2;
        int centerTileY = visibleY / 2;

        int absTileX = centerTileX + tileSlot.X;
        int absTileY = centerTileY + tileSlot.Y;

        var gameRect = profile.GameWindowRect;

        int baseX = gameRect.X + absTileX * profile.TileSize + profile.TileSize / 2;
        int baseY = gameRect.Y + absTileY * profile.TileSize + profile.TileSize / 2;

        // Small human jitter around center (±3 px or ±10% of tile, whichever is smaller)
        int maxJitter = Math.Min(3, profile.TileSize / 10);

        int jitterX = _rng.Next(-maxJitter, maxJitter + 1);
        int jitterY = _rng.Next(-maxJitter, maxJitter + 1);

        return (baseX + jitterX, baseY + jitterY);
    }
}
