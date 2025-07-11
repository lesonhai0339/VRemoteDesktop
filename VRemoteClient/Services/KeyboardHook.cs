using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static VRemoteClient.Models.Enums.KeyboardEnums;
using static VRemoteClient.Utils.Libraries;

namespace VRemoteClient.Services
{
    public class KeyMessageEventArgs : EventArgs
    {
        public KeyMessageEventArgs()
        {
        }
        public KeyMessageEventArgs(IntPtr command, Keys keyCode, KeyState keyType)
        {
            Command = command;
            KeyCode = keyCode;
            KeyType = keyType;
        }
        public KeyMessageEventArgs(IntPtr command ,Keys keyModifier, Keys keyCode)
        {
            Command = command;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
        }
        public KeyMessageEventArgs(IntPtr command, Keys keyModifier, Keys keyCode, KeyState keyType)
        {
            Command = command;
            KeyModifier = keyModifier;
            KeyCode = keyCode;
            KeyType = keyType;
        }
        public IntPtr Command { get;set; }
        public Keys KeyModifier { get; set; }
        public Keys KeyCode { get; set; }
        public KeyState KeyType { get; set; }   
    }
    public class KeyboardHook
    {

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_LCONTROL = 0xA2;  // Left Control
        private const int VK_RCONTROL = 0xA3;  // Right Control
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12; // Alt
      
        public KeyboardHook() { }
        private uint _targetProcessId;
        private IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc proc;
        public event EventHandler<KeyMessageEventArgs> KeyPressed;

        public void Start(uint pId)
        {
            _targetProcessId = pId;
            proc = HookCallback;
            hookID = SetHook(proc);
        }

        public void Stop()
        {
            UnhookWindowsHookEx(hookID);
        }
        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsTargetAppFocused())
            {
                // Only process key down and key up messages
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP)
                {
                    // Correct way to read the virtual key code
                    KBDLLHOOKSTRUCT hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int vkCode = hookStruct.vkCode;
                    Keys key = (Keys)vkCode;

                    // Determine if it's key down or up
                    KeyState keyState = (wParam == (IntPtr)WM_KEYDOWN) ? KeyState.KeyDown : KeyState.KeyUp;

                    KeyMessageEventArgs keyEventArgs = null;
                    Keys modifier = IsControlPressed() ? Keys.Control :
                                    IsAltPressed() ? Keys.Alt:
                                    IsShiftPressed() ? Keys.Shift:
                                    isLeftWindowKeyPressed() ? Keys.LWin : Keys.None;
                    keyEventArgs = new KeyMessageEventArgs(wParam,modifier, key, keyState);
                    if (keyEventArgs != null)
                    {
                        KeyPressed?.Invoke(this, keyEventArgs);
                    }
                }
            }
            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }
        private bool IsControlPressed()
        {
            return (GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0 || (GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
        }

        private bool IsShiftPressed()
        {
            return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        }

        private bool IsAltPressed()
        {
            return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        }
        private bool isLeftWindowKeyPressed()
        {
            return (GetAsyncKeyState((int)Keys.LWin) & 0x8000) != 0;
        }
        private bool IsTargetAppFocused()
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint foregroundPid);
            return foregroundPid == _targetProcessId;
        }
        public class KeyboardSendEventHandler
        {
            public byte[] KeyBuilder(KeyMessageEventArgs e)
            {
                string stringBuilder = new StringBuilder()
                        .Append((int)e.KeyType)
                        .Append("|")
                        .Append((int)e.KeyCode)
                        .Append("|")
                        .ToString();
                return Encoding.UTF8.GetBytes(stringBuilder);
            }
        }
    }
    public class KeyboardReceivedEventHandler
    {
        public Keys KeyboardReceived(byte[] data)
        {
            string[] result = Encoding.UTF8.GetString(data).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            int keyType = int.Parse(result[1]);
            int keyCode = int.Parse(result[2]);
            Keys key = (Keys)keyCode;
            return key;
        }
    }
    public static class KeyboardSimulator
    {
        private const int KEYEVENTF_EXTENDEDKEY = 0x0001; // Extended key flag
        private const int INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        public static uint SendKey(Keys key)
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

            uint status = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return status;
        }
        private static ushort GetKeyValue(Keys key)
        {
            switch (key)
            {
                // Control keys
                case Keys.Control:
                case Keys.LControlKey:
                case Keys.RControlKey:
                    return 0x11;

                // Alt keys  
                case Keys.Alt:
                case Keys.LMenu:
                case Keys.RMenu:
                    return 0x12;

                // Shift keys
                case Keys.Shift:
                case Keys.LShiftKey:
                case Keys.RShiftKey:
                    return 0x10;

                // Windows keys
                case Keys.LWin:
                    return 0x5B;
                case Keys.RWin:
                    return 0x5C;

                // All other keys
                default:
                    return (ushort)key;
            }
        }
        public static uint SendKeyCombo(Keys modifier, Keys key)
        {
            INPUT[] inputs = new INPUT[4];

            ushort keyVK = GetKeyValue(key);
            ushort modifierVK = GetKeyValue(modifier);

            // Modifier down
            inputs[0] = CreateKeyInput(modifierVK, 0);
            // Key down
            inputs[1] = CreateKeyInput(keyVK, 0);
            // Key up
            inputs[2] = CreateKeyInput(keyVK, KEYEVENTF_KEYUP);
            // Modifier up
            inputs[3] = CreateKeyInput(modifierVK, KEYEVENTF_KEYUP);

            uint status = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            return status;
        }

        private static INPUT CreateKeyInput(ushort key, uint flags)
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
