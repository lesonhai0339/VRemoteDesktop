using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace VRemoteClient.Utils
{
    public static class VirtualClipboard
    {
        // Standard clipboard formats
        public static uint CF_TEXT = 1;
        public static uint CF_BITMAP = 2;
        public static uint CF_METAFILEPICT = 3;
        public static uint CF_SYLK = 4;
        public static uint CF_DIF = 5;
        public static uint CF_TIFF = 6;
        public static uint CF_OEMTEXT = 7;
        public static uint CF_DIB = 8;
        public static uint CF_PALETTE = 9;
        public static uint CF_PENDATA = 10;
        public static uint CF_RIFF = 11;
        public static uint CF_WAVE = 12;
        public static uint CF_UNICODETEXT = 13;
        public static uint CF_ENHMETAFILE = 14;
        public static uint CF_HDROP = 15;
        public static uint CF_LOCALE = 16;
        public static uint CF_DIBV5 = 17;

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
