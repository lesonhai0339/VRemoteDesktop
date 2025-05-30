using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient
{
    public class KeyboardSimulator
    {

        [DllImport("user32.dll", SetLastError =true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public int type;
            public InputUnion u;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public uint SendKey(Keys key)
        {
            INPUT[] inputs = new INPUT[2];

            // Key down
            inputs[0] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            // Key up
            inputs[1] = new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = (ushort)key,
                        wScan = 0,
                        dwFlags = KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };

            uint status =  SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return status;
        }

        public uint SendKeyCombo(Keys modifier, Keys key)
        {
            INPUT[] inputs = new INPUT[4];

            // Modifier down
            inputs[0] = CreateKeyInput((ushort)modifier, 0);
            // Key down
            inputs[1] = CreateKeyInput((ushort)key, 0);
            // Key up
            inputs[2] = CreateKeyInput((ushort)key, KEYEVENTF_KEYUP);
            // Modifier up
            inputs[3] = CreateKeyInput((ushort)modifier, KEYEVENTF_KEYUP);

            uint status = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return status;
        }

        private INPUT CreateKeyInput(ushort key, uint flags)
        {
            return new INPUT
            {
                type = INPUT_KEYBOARD,
                u = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = key,
                        wScan = 0,
                        dwFlags = flags,
                        time = 0,
                        dwExtraInfo = GetMessageExtraInfo()
                    }
                }
            };
        }
    }
}
