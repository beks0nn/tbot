using Bot.Navigation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Bot.Control;

public sealed class KeyMover
{
    private readonly Random _rng = new();

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const ushort VK_F1 = 0x70; // virtual key code for F1

    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;

    // Virtual key codes for arrow keys
    private const ushort VK_UP = 0x26;
    private const ushort VK_DOWN = 0x28;
    private const ushort VK_LEFT = 0x25;
    private const ushort VK_RIGHT = 0x27;
    private const ushort VK_NUMPAD7 = 0x67; // NW
    private const ushort VK_NUMPAD9 = 0x69; // NE
    private const ushort VK_NUMPAD1 = 0x61; // SW
    private const ushort VK_NUMPAD3 = 0x63; // SE

    // Map movement deltas → key codes
    private static readonly Dictionary<(int dx, int dy), ushort> Directions = new()
    {
        { ( 1,  0), VK_RIGHT },
        { (-1,  0), VK_LEFT },
        { ( 0, -1), VK_UP },
        { ( 0,  1), VK_DOWN },

        { ( 1, -1), VK_NUMPAD9 }, // NE
        { (-1, -1), VK_NUMPAD7 }, // NW
        { ( 1,  1), VK_NUMPAD3 }, // SE
        { (-1,  1), VK_NUMPAD1 }, // SW
    };

    // Cache process handle for performance
    //private readonly IntPtr _windowHandle;

    //public KeyMover()
    //{
    //    var tibia = Process.GetProcessesByName("TibiaraDX-1762027267").FirstOrDefault();
    //    if (tibia == null || tibia.MainWindowHandle == IntPtr.Zero)
    //    {
    //        Console.WriteLine("⚠️ Could not find TibiaraDX process window.");
    //        throw new InvalidOperationException("⚠️ Could not find TibiaraDX process window.");
    //    }

    //    _windowHandle = tibia.MainWindowHandle;
    //}

    public void StepTowards((int x, int y) from, (int x, int y) to, IntPtr handle)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;

        if (Directions.TryGetValue((dx, dy), out var vk))
            PressKey(vk, handle);
    }

    public void StepDirection(Direction dir, IntPtr handle)
    {
        switch (dir)
        {
            case Direction.North: PressKey(VK_UP, handle); break;
            case Direction.South: PressKey(VK_DOWN, handle); break;
            case Direction.East: PressKey(VK_RIGHT, handle); break;
            case Direction.West: PressKey(VK_LEFT, handle); break;
        }
    }

    public void PressF1(IntPtr handle)
    {
        PressKey(VK_F1, handle);
    }

    private void PressKey(ushort vk, IntPtr handle)
    {
        PostMessage(handle, WM_KEYDOWN, (IntPtr)vk, IntPtr.Zero);
        Thread.Sleep(_rng.Next(40, 75)); // humanized key hold
        PostMessage(handle, WM_KEYUP, (IntPtr)vk, IntPtr.Zero);
    }
}