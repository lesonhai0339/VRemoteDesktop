using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.CustomEvents;
using VRemoteClient.Models.Enums;
using VRemoteClient.Utils;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using static VRemoteClient.Models.Enums.KeyboardEnums;
using static VRemoteClient.Utils.Libraries;

namespace VRemoteClient.Services
{
    public class KeyboardHook: IDisposable
    {
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
        /// Listen keyboard pressed
        /// </summary>
        /// <param name="nCode"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <returns></returns>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                // Only process key down and key up messages
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP)
                {
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
                    //bool isModifierKey = IsModifierKey(vkCode);
                    //bool hasModifier = (modifier != Keys.None);

                    //if (!isModifierKey || hasModifier)
                    //{
                    //    KeyPressed?.Invoke(this, keyEventArgs);

                    //    return (IntPtr)1;
                    //}
                    if (IsTargetWindowFocused())
                    {
                        KeyPressed?.Invoke(this, keyEventArgs);
                        return (IntPtr)1;
                    }
                    else
                    {
                        if(IsControlPressed() && key == Keys.C)
                        {
                            string clipboardData = Clipboard.GetText();
                            Console.WriteLine("No copy ngoai form, data: "+ clipboardData);
                        }
                        if(IsControlPressed() && key == Keys.V)
                        {
                            Clipboard.SetText("Hello world");
                            Console.WriteLine("No parse ngoai form, data: "+ Clipboard.GetText());
                        }
                    } 
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
    }
}
