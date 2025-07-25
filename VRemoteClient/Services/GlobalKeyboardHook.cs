using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using static VRemoteClient.Utils.Libraries;
using System.Windows.Forms;
using VRemoteClient.Models.CustomEvents;
using static VRemoteClient.Models.Enums.KeyboardEnums;

namespace VRemoteClient.Services
{
    public class GlobalKeyboardHook: IDisposable
    {
        private uint _targetProcessId;
        private IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc proc;
        public event EventHandler<KeyMessageEventArgs> KeyPressed;
        private bool _disposed = false;
        public GlobalKeyboardHook() { }
        public void Start(uint pId)
        {
            _targetProcessId = pId;
            proc = HookCallback;
            hookID = SetHook(proc);
        }
        public void Stop()
        {
            UnhookWindowsHookEx(hookID);
            hookID = IntPtr.Zero;
        }
        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                ////get specifi windows handle
                //return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                //    GetModuleHandle(curModule.ModuleName), 0);

                //get all of this process
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    IntPtr.Zero, 0);
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
            //event do not be register, do not need to listen
            if (KeyPressed == null) return (IntPtr)1;
            
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP)
                {
                    KBDLLHOOKSTRUCT hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int vkCode = hookStruct.vkCode;
                    Keys key = (Keys)vkCode;

                    KeyState keyState = (wParam == (IntPtr)WM_KEYDOWN) ? KeyState.KeyDown : KeyState.KeyUp;

                    KeyMessageEventArgs keyEventArgs = null;

                    keyEventArgs = new KeyMessageEventArgs(wParam, Keys.None, key, keyState);

                    KeyPressed?.Invoke(this, keyEventArgs);
                    return (IntPtr)1;
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
