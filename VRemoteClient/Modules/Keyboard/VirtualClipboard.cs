using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace VRemoteClient.Modules.Keyboard
{
    public enum ClipboardFormat: uint
    {
        CF_TEXT = 1,
        CF_BITMAP = 2,
        CF_METAFILEPICT = 3,
        CF_SYLK = 4,
        CF_DIF = 5,
        CF_TIFF = 6,
        CF_OEMTEXT = 7,
        CF_DIB = 8,
        CF_PALETTE = 9,
        CF_PENDATA = 10,
        CF_RIFF = 11,
        CF_WAVE = 12,
        CF_UNICODETEXT = 13,
        CF_ENHMETAFILE = 14,
        CF_HDROP = 15,
        CF_LOCALE = 16,
        CF_DIBV5 = 17
    }
    public static class VirtualClipboard
    {
        // Standard clipboard formats
        private const uint CF_TEXT = 1;
        private const uint CF_BITMAP = 2;
        private const uint CF_METAFILEPICT = 3;
        private const uint CF_SYLK = 4;
        private const uint CF_DIF = 5;
        private const uint CF_TIFF = 6;
        private const uint CF_OEMTEXT = 7;
        private const uint CF_DIB = 8;
        private const uint CF_PALETTE = 9;
        private const uint CF_PENDATA = 10;
        private const uint CF_RIFF = 11;
        private const uint CF_WAVE = 12;
        private const uint CF_UNICODETEXT = 13;
        private const uint CF_ENHMETAFILE = 14;
        private const uint CF_HDROP = 15;
        private const uint CF_LOCALE = 16;
        private const uint CF_DIBV5 = 17;

        private const uint GMEM_MOVEABLE = 0x0002;


        [DllImport("user32.dll")]
        public static extern IntPtr GetClipboardData(uint uFormat);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);
        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern UIntPtr GlobalSize(IntPtr hMem);

        [DllImport("user32.dll")]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        public static string GetClipboardString()
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                return string.Empty;
            }
            try
            {
                if (IsClipboardFormatAvailable(CF_UNICODETEXT))
                {
                    return ExtractUnicodeText();
                }
                if (IsClipboardFormatAvailable(CF_TEXT))
                {
                    return ExtractAnsiText();
                }
            }
            finally
            {
                CloseClipboard();
            }
            return string.Empty;
        }
        public static bool SetClipboard(byte[] data, uint format = CF_UNICODETEXT)
        {
            if (!OpenClipboard(IntPtr.Zero))
            {
                return false;
            }
            try
            {
                if (!EmptyClipboard())
                    return false;

                IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)data.Length);
                if (hGlobal == IntPtr.Zero)
                    return false;

                IntPtr pGlobal = GlobalLock(hGlobal);
                if (pGlobal == IntPtr.Zero)
                    return false;

                Marshal.Copy(data, 0 , pGlobal, data.Length);
                GlobalUnlock(hGlobal);

                IntPtr result = SetClipboardData(format, hGlobal);

                return result != IntPtr.Zero;
            }
            finally
            {
                CloseClipboard();
            }
        }
        private static string ExtractUnicodeText()
        {
            IntPtr hData = GetClipboardData(CF_UNICODETEXT);
            if (hData == IntPtr.Zero)
                return string.Empty;

            IntPtr pData = GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringUni(pData) ?? string.Empty;
            }
            finally
            {
                GlobalUnlock(hData);
            }
        }
        private static string ExtractAnsiText()
        {
            IntPtr hData = GetClipboardData(CF_TEXT);
            if (hData == IntPtr.Zero)
                return string.Empty;

            IntPtr pData = GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringAnsi(pData) ?? string.Empty;
            }
            finally
            {
                GlobalUnlock(hData);
            }
        }
    }
}
