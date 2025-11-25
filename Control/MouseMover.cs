using OpenCvSharp;
using System;
using System.Runtime.InteropServices;

namespace Bot.Control;

public sealed class MouseMover
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int X, int Y);

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

    public void RightClick(int x, int y)
    {
        SetCursorPos(x, y);
        mouse_event(MOUSEEVENTF_RIGHTDOWN, (uint)x, (uint)y, 0, 0);
        mouse_event(MOUSEEVENTF_RIGHTUP, (uint)x, (uint)y, 0, 0);
    }

    public void RightClickSlow(int x, int y)
    {
        SetCursorPos(x, y);

        // tiny human-like settle pause (10–25 ms)
        Thread.Sleep(_rng.Next(10, 26));

        mouse_event(MOUSEEVENTF_RIGHTDOWN, (uint)x, (uint)y, 0, 0);

        // variable click hold (18–32 ms)
        Thread.Sleep(_rng.Next(18, 33));

        mouse_event(MOUSEEVENTF_RIGHTUP, (uint)x, (uint)y, 0, 0);
    }

    public void LeftClickTile((int X, int Y) tileSlot, IClientProfile profile)
    {
        var (px, py) = TileToScreenPixel(tileSlot, profile);
        LeftClickSlow(px, py);
    }

    public void LeftClickSlow(int x, int y)
    {
        SetCursorPos(x, y);
        // tiny human-like settle
        Thread.Sleep(_rng.Next(10, 26));
        mouse_event(MOUSEEVENTF_LEFTDOWN, (uint)x, (uint)y, 0, 0);
        Thread.Sleep(_rng.Next(18, 33));
        mouse_event(MOUSEEVENTF_LEFTUP, (uint)x, (uint)y, 0, 0);
    }

    public void RightClickTile((int X, int Y) tileSlot, IClientProfile profile)
    {
        var (px, py) = TileToScreenPixel(tileSlot, profile);
        RightClickSlow(px, py);
    }

    public void CtrlDragLeft(int fromX, int fromY, int toX, int toY)
    {
        keybd_event(VK_CONTROL, 0, 0, 0);          // hold Ctrl
        Thread.Sleep(30);

        SetCursorPos(fromX, fromY);
        Thread.Sleep(25);

        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
        Thread.Sleep(25);                          // hold click before moving

        SmoothMove(fromX, fromY, toX, toY, 6);     // smooth cursor path

        Thread.Sleep(20);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        Thread.Sleep(30);

        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
    }

    /// <summary>
    /// Moves cursor gradually from A to B over N steps.
    /// </summary>
    private static void SmoothMove(int fromX, int fromY, int toX, int toY, int steps)
    {
        double dx = (toX - fromX) / (double)steps;
        double dy = (toY - fromY) / (double)steps;

        for (int i = 1; i <= steps; i++)
        {
            SetCursorPos((int)(fromX + dx * i), (int)(fromY + dy * i));
            Thread.Sleep(15);
        }
    }

    private static (int X, int Y) TileToScreenPixel((int X, int Y) tileSlot, IClientProfile profile)
    {
        var (visibleX, visibleY) = profile.VisibleTiles;
        int centerTileX = visibleX / 2;
        int centerTileY = visibleY / 2;
        int absTileX = centerTileX + tileSlot.X;
        int absTileY = centerTileY + tileSlot.Y;
        var gameRect = profile.GameWindowRect;
        int px = gameRect.X + absTileX * profile.TileSize + profile.TileSize / 2;
        int py = gameRect.Y + absTileY * profile.TileSize + profile.TileSize / 2;
        return (px, py);
    }
}
