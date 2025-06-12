using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RemoteClient.Enums;
using static System.Runtime.CompilerServices.RuntimeHelpers;

namespace RemoteClient
{

    public class KeyMessageEventArgs : EventArgs
    {
        public Keys KeyCode { get; private set; }
        public KeyState KeyType { get; private set; }
        public KeyMessageEventArgs(Keys keyCode, KeyState keyType)
        {
            KeyCode = keyCode;
            KeyType = keyType;  
        }
    }
    public class KeyboardHook
    {
        private IntPtr hookID = IntPtr.Zero;
        private LowLevelKeyboardProc proc;
        public event EventHandler<KeyMessageEventArgs> KeyPressed;

        public void Start()
        {
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

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                if(wParam == (IntPtr)WM_KEYDOWN) 
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    Keys key = (Keys)vkCode;

                    if (key == Keys.A && IsControlPressed())
                    {
                        // Ctrl + A được nhấn!
                        Console.WriteLine("Ctrl + A detected!");
                        // Xử lý logic của bạn ở đây
                    }
                    KeyPressed?.Invoke(this, new KeyMessageEventArgs(key, KeyState.KeyDown));
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    int vkCode = Marshal.ReadInt32(lParam);
                    Keys key = (Keys)vkCode;
                    KeyPressed?.Invoke(this, new KeyMessageEventArgs(key, KeyState.KeyUp));
                }
            }
            return CallNextHookEx(hookID, nCode, wParam, lParam);
        }
        private bool IsControlPressed()
        {
            return (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
        }

        private bool IsShiftPressed()
        {
            return (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
        }

        private bool IsAltPressed()
        {
            return (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
        }
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12; // Alt
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook,
            LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
            IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

    }
    public class KeyboardSendEventHandler
    {
        public byte[] KeyBuilder(KeyMessageEventArgs e)
        {
            string stringBuilder = new StringBuilder()
                    .Append((int)DataSendType.KEYBOARD)
                    .Append("|")
                    .Append((int)e.KeyType)
                    .Append("|")
                    .Append((int)e.KeyCode)
                    .Append("|")
                    .ToString();
            return Encoding.UTF8.GetBytes(stringBuilder);
        }
    }
    public class KeyboardReceivedEventHandler
    {
        public Keys KeyboardReceived(byte[] data)
        {
            string[] result = Encoding.UTF8.GetString(data).Split(new[] {'|'},StringSplitOptions.RemoveEmptyEntries);
            int keyType = int.Parse(result[1]);
            int keyCode = int.Parse(result[2]);
            Keys key = (Keys)keyCode;
            return key;
        }
    }
}
