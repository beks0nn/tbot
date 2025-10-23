using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Bot.Control
{
    public sealed class KeyMover
    {
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public int type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        private static void PressKey(ushort key)
        {
            INPUT[] inputs = new INPUT[2];

            // Key down
            inputs[0].type = INPUT_KEYBOARD;
            inputs[0].U.ki.wVk = key;

            // Key up
            inputs[1].type = INPUT_KEYBOARD;
            inputs[1].U.ki.wVk = key;
            inputs[1].U.ki.dwFlags = KEYEVENTF_KEYUP;

            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        }

        private const ushort VK_UP = 0x26;
        private const ushort VK_DOWN = 0x28;
        private const ushort VK_LEFT = 0x25;
        private const ushort VK_RIGHT = 0x27;

        public void StepTowards((int x, int y) from, (int x, int y) to)
        {
            int dx = to.x - from.x;
            int dy = to.y - from.y;

            if (dx == 1 && dy == 0) Press(VK_RIGHT);
            else if (dx == -1 && dy == 0) Press(VK_LEFT);
            else if (dx == 0 && dy == -1) Press(VK_UP);
            else if (dx == 0 && dy == 1) Press(VK_DOWN);
        }

        private void Press(ushort key)
        {
            PressKey(key);
            Thread.Sleep(80); // small delay between moves
        }
    }
}