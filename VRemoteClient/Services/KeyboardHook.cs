using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Enums;
using static System.Runtime.CompilerServices.RuntimeHelpers;
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
        /// <summary>
        /// Sẽ được gọi khi có sự kiện bàn phím xảy ra. hiện tại đang kiểm tra xem FormRemote có được focus hay không.
        /// Nếu có thì sẽ gọi invoke action và gửi keyboard đến receiver, máy hiện tại sẽ không thực hiện bất kỳ thao tác bản phím nào.
        /// Nếu FormRemote không được focus thì sẽ gọi CallNextHookEx thực hiện phím trên chính máy này.
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsTargetWindowFocused())
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
                    //Keys modifier = IsControlPressed() ? Keys.Control :
                    //                IsAltPressed() ? Keys.Alt:
                    //                IsShiftPressed() ? Keys.Shift:
                    //                isLeftWindowKeyPressed() ? Keys.LWin : Keys.None;
                    //keyEventArgs = new KeyMessageEventArgs(wParam,modifier, key, keyState);

                    keyEventArgs = new KeyMessageEventArgs(wParam, Keys.None, key, keyState);
                    Console.WriteLine(keyEventArgs.KeyCode + " - "+ keyEventArgs.KeyType);
                    //bool isModifierKey = IsModifierKey(vkCode);
                    //bool hasModifier = (modifier != Keys.None);

                    //if (!isModifierKey || hasModifier)
                    //{
                    //    KeyPressed?.Invoke(this, keyEventArgs);

                    //    return (IntPtr)1;
                    //}
                    KeyPressed?.Invoke(this, keyEventArgs);
                    return (IntPtr)1;
                }
            }
            return CallNextHookEx(hookID, nCode, wParam, lParam);
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
        private static object _lock = new object(); 
        private static List<Keys> _modifiers = new List<Keys>();
        private static Keys _key = Keys.None;
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
        public static void Method_1(Keys key, KeyState state)
        {
            if(state == KeyState.KeyDown)
            {
                switch (key)
                {
                    case Keys.Control:
                    case Keys.ControlKey:
                    case Keys.LControlKey:
                    case Keys.RControlKey:
                    case Keys.Shift:
                    case Keys.ShiftKey:
                    case Keys.LShiftKey:
                    case Keys.RShiftKey:
                    case Keys.Alt:
                    case Keys.Menu:
                    case Keys.LMenu:
                    case Keys.RMenu:
                    case Keys.LWin:
                    case Keys.RWin:
                        lock (_lock)
                        {
                            if(!_modifiers.Any(x=> x == key))
                            _modifiers.Add(key);
                        }
                        break;
                    default:
                        lock (_lock)
                        {
                            //case for holding key (example: aaaaaaaaaaaaa)
                            if(_key == key)
                            {
                                SendKey(_key);
                            }
                            else
                            {
                                _key = key;
                            }
                        }
                        break;
                }
                return;
            }
            else
            {
                lock (_lock)
                {
                    // key press event
                    string a = string.Join(" - ", _modifiers);
                    Console.WriteLine(a + " - " + _key);
                    if (_modifiers.Count > 1 && _key != Keys.None)
                    {
                        SendMultiCombo(_modifiers, _key);
                    }
                    else if (_modifiers.Count == 1  && _key != Keys.None)
                    {
                        SendKeyCombo(_modifiers[0], _key);
                    }
                    else if (_modifiers.Count == 1)
                    {
                        SendKey(_modifiers[0]);
                    }
                    else
                    {
                        SendKey(_key);
                    }


                    // remove
                    if (_key == key)
                    {
                        _key = Keys.None;
                    }
                    if (_modifiers.Contains(key))
                    {
                        _modifiers.RemoveAll(k => k == key);
                    }
                }
            }
        }
        public static uint SendMultiCombo(List<Keys> modifiers, Keys key)
        {
            int inputCount = modifiers.Count * 2 + 2; // down/up for each modifier + down/up for main key
            INPUT[] inputs = new INPUT[inputCount];
            int index = 0;

            // 1. Modifier key down
            foreach (var mod in modifiers)
            {
                ushort modVK = GetKeyValue(mod);
                inputs[index++] = CreateKeyInput(modVK, 0); // key down
            }

            // 2. Main key down
            ushort keyVK = GetKeyValue(key);
            inputs[index++] = CreateKeyInput(keyVK, 0); // key down

            // 3. Main key up
            inputs[index++] = CreateKeyInput(keyVK, KEYEVENTF_KEYUP); // key up

            // 4. Modifier key up (in reverse order)
            for (int i = modifiers.Count - 1; i >= 0; i--)
            {
                ushort modVK = GetKeyValue(modifiers[i]);
                inputs[index++] = CreateKeyInput(modVK, KEYEVENTF_KEYUP); // key up
            }

            // 5. Send inputs
            return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
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
