using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Enums;
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
    public class KeyboardHook: IDisposable
    {
        private bool ctrlPressed = false;
        private bool altPressed = false;
        private bool shiftPressed = false;
        private bool winPressed = false;
        private Keys previousState = Keys.None;
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_LCONTROL = 0xA2;  // Left Control
        private const int VK_RCONTROL = 0xA3;  // Right Control
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12; // Alt
        private uint _targetProcessId;
        private IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc proc;
        private IntPtr _targetWindowHandle = IntPtr.Zero; // Optional: If you want to target a specific window
        public event EventHandler<KeyMessageEventArgs> KeyPressed;
        private bool _disposed = false;
        public KeyboardHook() { }
        public void Start(uint pId, IntPtr handler)
        {
            _targetProcessId = pId;
            _targetWindowHandle = handler; // Set the target window handle if provided
            proc = HookCallback;
            hookID = SetHook(proc);
        }

        public void Stop()
        {
            UnhookWindowsHookEx(hookID);
            hookID = IntPtr.Zero;
            _targetWindowHandle = IntPtr.Zero; // Reset the target window handle
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
            if (nCode >= 0 && IsTargetWindowFocused())
            {
                // Only process key down and key up messages
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP)
                {
                    KBDLLHOOKSTRUCT hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int vkCode = hookStruct.vkCode;
                    Keys key = (Keys)vkCode;

                    // Determine if it's key down or up
                    KeyState keyState = (wParam == (IntPtr)WM_KEYDOWN) ? KeyState.KeyDown : KeyState.KeyUp;

                    // Update modifier state BEFORE getting current modifier
                    UpdateModifierState(vkCode, keyState);

                    // Get current modifier based on tracked state
                    Keys modifier = GetCurrentModifier();

                    KeyMessageEventArgs keyEventArgs = new KeyMessageEventArgs(wParam, modifier, key, keyState);

                    bool isModifierKey = IsModifierKey(vkCode);
                    bool hasModifier = (modifier != Keys.None);

                    if (!isModifierKey)
                    {
                        KeyPressed?.Invoke(this, keyEventArgs);
                        return (IntPtr)1;
                    }
                    else if (hasModifier && modifier != key)
                    {
                        KeyPressed?.Invoke(this, keyEventArgs);
                        return (IntPtr)1;
                    }
                    else if (!hasModifier)
                    {
                        KeyPressed?.Invoke(this, keyEventArgs);
                    }
                }
            }
            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }

        private void UpdateModifierState(int vkCode, KeyState keyState)
        {
            bool isPressed = (keyState == KeyState.KeyDown);

            switch (vkCode)
            {
                case 0x11: // VK_CONTROL
                case 0xA2: // VK_LCONTROL
                case 0xA3: // VK_RCONTROL
                    ctrlPressed = isPressed;
                    break;

                case 0x12: // VK_MENU (Alt)
                case 0xA4: // VK_LMENU
                case 0xA5: // VK_RMENU
                    altPressed = isPressed;
                    break;

                case 0x10: // VK_SHIFT
                case 0xA0: // VK_LSHIFT
                case 0xA1: // VK_RSHIFT
                    shiftPressed = isPressed;
                    break;

                case 0x5B: // VK_LWIN
                case 0x5C: // VK_RWIN
                    winPressed = isPressed;
                    break;
            }
        }

        private Keys GetCurrentModifier()
        {
            if (ctrlPressed) return Keys.Control;
            if (altPressed) return Keys.Alt;
            if (shiftPressed) return Keys.Shift;
            if (winPressed) return Keys.LWin;
            return Keys.None;
        }

        private bool IsModifierKey(int vkCode)
        {
            return vkCode == 0x10 || vkCode == 0xA0 || vkCode == 0xA1 || // Shift
                   vkCode == 0x11 || vkCode == 0xA2 || vkCode == 0xA3 || // Control
                   vkCode == 0x12 || vkCode == 0xA4 || vkCode == 0xA5 || // Alt
                   vkCode == 0x5B || vkCode == 0x5C;                     // Windows
        }

        private bool IsControlPressed()
        {
            return ctrlPressed;
        }

        private bool IsShiftPressed()
        {
            return shiftPressed;
        }

        private bool IsAltPressed()
        {
            return altPressed;
        }

        private bool isLeftWindowKeyPressed()
        {
            return winPressed;
        }
        private bool IsTargetAppFocused()
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint foregroundPid);
            return foregroundPid == _targetProcessId;
        }
        private bool IsTargetWindowFocused()
        {
            IntPtr hwnd = GetForegroundWindow();
            return hwnd == _targetWindowHandle;
        }
        public string KeyboardEventTostring(IntPtr command, Keys modifier, Keys code, KeyState type)
        {
            return new StringBuilder()
                    .Append((int)command)
                    .Append("|")
                    .Append((int)modifier)
                    .Append("|")
                    .Append((int)code)
                    .Append("|")
                    .Append((int)type).ToString();
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    // Clear event handlers to prevent memory leaks
                    KeyPressed = null;
                }

                // Dispose unmanaged resources
                Stop(); // This will unhook the Windows hook

                _disposed = true;
            }
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
