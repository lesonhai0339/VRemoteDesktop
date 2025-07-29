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
using VRemoteClient.Utils;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace VRemoteClient.Services.KeyboardService
{
    public class GlobalKeyboardHook: IDisposable
    {
        private readonly object _lockObject = new object();
        private uint _targetProcessId;
        private IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc proc;
        public HashSet<IntPtr> _windowsHandle = new HashSet<IntPtr>(); 
        public event EventHandler<CustomKeyMessageEventArgs> KeyPressed;

        private bool _disposed = false;
        public GlobalKeyboardHook() 
        {
            WindowsHandle = new HashSet<IntPtr>();
        }
        public void Start(uint pId)
        {
            _targetProcessId = pId;
            proc = HookCallback;
            hookID = SetHook(proc);
        }
        #region Properties
        public HashSet<IntPtr> WindowsHandle
        {
            get
            {
                lock (_lockObject)
                {
                    return _windowsHandle;
                }
            }
            set
            {
                lock (_lockObject)
                {
                    _windowsHandle = value;
                }
            }
        }
        public void AddHook(IntPtr handle)
        {
            lock (_lockObject)
            {
                WindowsHandle.Add(handle);
            }
        }
        public void RemoveHook(IntPtr handle)
        {
            lock (_lockObject)
            {
                WindowsHandle.Remove(handle);
            }
        }
        #endregion
        public void Stop()
        {
            UnhookWindowsHookEx(hookID);
            hookID = IntPtr.Zero;
        }
        private bool IsHandleFocus(IntPtr handle)
        {
            return GetForegroundWindow() == handle;
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
            //if (KeyPressed == null) return (IntPtr)1;
            
            if (nCode >= 0)
            {
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_KEYUP)
                {
                    KBDLLHOOKSTRUCT hookStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                    int vkCode = hookStruct.vkCode;
                    Keys key = (Keys)vkCode;
                    KeyState keyState = wParam == (IntPtr)WM_KEYDOWN ? KeyState.KeyDown : KeyState.KeyUp;

                    CustomKeyMessageEventArgs keyEventArgs = null;

                    if (WindowsHandle.Count > 0)
                    {
                        var focusedHandles = WindowsHandle.Where(x => IsHandleFocus(x)).ToList();
                        if (focusedHandles.Any())
                        {
                            keyEventArgs = new CustomKeyMessageEventArgs
                            {
                                Command = wParam,
                                Handle = focusedHandles.First(),
                                KeyModifier = Keys.None,
                                KeyCode = key,
                                KeyType = keyState,
                            };
                            if (IsControlPressed() && key == Keys.C)
                            {
                                keyEventArgs.KeyModifier = Keys.Control;
                                keyEventArgs.Combination = KeyCombination.Copy;
                            }
                            KeyPressed?.Invoke(this, keyEventArgs);
                            return (IntPtr)1;
                        }
                    }
                    if (IsControlPressed() && key == Keys.C)
                    {
                        keyEventArgs = new CustomKeyMessageEventArgs
                        {
                            Command = wParam,
                            Handle = IntPtr.Zero,
                            KeyModifier = Keys.Control,
                            KeyCode = key,
                            KeyType = keyState,
                            Combination = KeyCombination.Copy
                        };
                        KeyPressed?.Invoke(this, keyEventArgs);
                        return CallNextHookEx(hookID, nCode, wParam, lParam);
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
            return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0 || (GetAsyncKeyState(VK_LMENU) & 0x8000) != 0 || (GetAsyncKeyState(VK_RMENU) & 0x8000) != 0;
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
