using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace VRemoteClient.Utils
{
    public static class Win32Apis
    {
        //Keyboard Magic number, use in dwExtraInfo  of INPUT[] used to classify key pressed from USB or from virtual keyboard
        public const int WH_MOUSE_LL = 14;
        public const int INPUT_MOUSE = 0;
        public const uint SYNTHETIC_KEY_MARKER = 0x12345678;
        public static class  KeyboardApis
        {
            [DllImport("user32.dll")]
            public static extern IntPtr MapVirtualKeyA(uint uCode, uint uMapType);

            [DllImport("user32.dll")]
            public static extern short GetAsyncKeyState(int vKey);
        }
        public static class  CaptureApis
        {
            [DllImport("user32.dll")]
            public static extern bool SetProcessDPIAware();

            [DllImport("user32.dll")]
            public static extern int GetSystemMetrics(int nIndex);

            [DllImport("gdi32.dll")]
            public static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
            [DllImport("user32.dll")]

            public static extern IntPtr GetDC(IntPtr hWnd);
            [DllImport("user32.dll")]
            public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

            [DllImport("user32.dll")]
            public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

            [DllImport("user32.dll")]
            public static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);
        }
        public static class  MouseApis
        {
            [DllImport("user32.dll")]
            public static extern bool SetCursorPos(int x, int y);

            [DllImport("user32.dll")]
            public static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        }
        public static class ClipboardApis
        {
            [DllImport("user32.dll")]
            public static extern IntPtr GetClipboardData(uint uFormat);

            [DllImport("user32.dll")]
            public static extern bool OpenClipboard(IntPtr hWndNewOwner);

            [DllImport("user32.dll")]
            public static extern bool CloseClipboard();

            [DllImport("user32.dll")]
            public static extern bool IsClipboardFormatAvailable(uint format);

            [DllImport("user32.dll")]
            public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

            [DllImport("user32.dll")]
            public static extern bool EmptyClipboard();

        }
        public static class  MemoryApis
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr GlobalLock(IntPtr hMem);
            [DllImport("kernel32.dll")]
            public static extern bool GlobalUnlock(IntPtr hMem);

            [DllImport("kernel32.dll")]
            public static extern UIntPtr GlobalSize(IntPtr hMem);

            [DllImport("kernel32.dll")]
            public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
        }
        public static class WindowApis
        {
            [DllImport("user32.dll", SetLastError = true)]
            public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

            [DllImport("user32.dll")]
            public static extern IntPtr GetForegroundWindow();

            [DllImport("user32.dll")]
            public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

            [DllImport("user32.dll")]
            public static extern IntPtr WindowFromPoint(Point pt);

            [DllImport("user32.dll")]
            public static extern IntPtr GetMessageExtraInfo();
        }
        public static class HookApis
        {
            public delegate IntPtr LowLevelProc(int nCode, IntPtr wParam, IntPtr lParam);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr SetWindowsHookEx(int idHook,
                LowLevelProc lpfn, IntPtr hMod, uint dwThreadId);

            [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool UnhookWindowsHookEx(IntPtr hhk);

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern IntPtr GetModuleHandle(string lpModuleName);


            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode,
                IntPtr wParam, IntPtr lParam);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }
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
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }
    }
}
