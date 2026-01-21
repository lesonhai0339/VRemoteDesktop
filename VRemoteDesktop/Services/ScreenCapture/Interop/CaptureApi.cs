using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using static VRemoteDesktop.Interop.Win32Apis;
using static VRemoteDesktop.Services.ScreenCapture.Interop.CaptureApi;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.ScreenCapture.Interop
{
    public static class CaptureApi
    {
        #region Methods

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

        [DllImport("gdi32.dll")]
        public static extern bool StretchBlt(IntPtr hdc, int nXDest, int nYDest, int wDest, int hDest, IntPtr hdcSrc, int nXSrc, int nYSrc, int wSrc, int hSrc, uint dwRop);

        [DllImport("user32.dll")]

        public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern bool IsChild(IntPtr hWndParent, IntPtr hWnd);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

        [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr memcpy(IntPtr dest, IntPtr src, UIntPtr count);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern bool GdiFlush();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateFileMappingA(IntPtr hFile, IntPtr lpFileMappingAttributes, uint flProtect, uint dwMaximumSizeHigh, uint dwMaximumSizeLow, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr SetStretchBltMode(IntPtr hdc, int mode);


        //Draw direct on user control
        [DllImport("gdi32.dll")]
        public static extern int StretchDIBits(IntPtr hdc, int xDest, int yDest, int DestWidth, int DestHeight, int xSrc, int ySrc, int SrcWidth, int SrcHeight, IntPtr lpBits, ref BITMAPINFO lpbmi, uint iUsage, uint dwRop);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr BeginPaint(IntPtr handle,out PAINTSTRUCT paint);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);


        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        #endregion
        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        public struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFO
        {
            public BITMAPINFOHEADER Header;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BITMAPINFOHEADER
        {
            public uint biSize;          // DWORD
            public int biWidth;          // LONG
            public int biHeight;         // LONG
            public ushort biPlanes;      // WORD
            public ushort biBitCount;    // WORD
            public uint biCompression;   // DWORD
            public uint biSizeImage;     // DWORD
            public int biXPelsPerMeter;  // LONG
            public int biYPelsPerMeter;  // LONG
            public uint biClrUsed;       // DWORD
            public uint biClrImportant;  // DWORD
        }
        #endregion
    }
}
