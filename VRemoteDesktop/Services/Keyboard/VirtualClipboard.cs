using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using VRemoteDesktop.Enums;
using static VRemoteDesktop.Interop.Win32Apis;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.Keyboard
{
    internal static class VirtualClipboard
    {
        public static byte[] DecodeClipboard(byte[] data, int offset, int length)
        {
            try
            {
                byte[] clipboardData = new byte[length];
                Buffer.BlockCopy(data, offset, clipboardData, 0, length);
                string dataString = Encoding.UTF8.GetString(clipboardData);

                //default setclipboard use CF_UNICODETEXT(UTF-16), need to convert data to utf-16
                byte[] clipboardReformatted = Encoding.Unicode.GetBytes(dataString + '\0');

                return clipboardReformatted;
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "DecodeClipboard").Error(ex, "Decode Clipboard error");
            }
            return new byte[0];
        }
        public static string GetClipboardString()
        {
            if (!ClipboardApis.OpenClipboard(IntPtr.Zero))
            {
                return string.Empty;
            }
            try
            {
                if (ClipboardApis.IsClipboardFormatAvailable((uint)WindowsClipboardFormat.CF_UNICODETEXT))
                {
                    string data = ExtractUnicodeText();
                    if (data.Length > 1000000)
                    {
                        Log.ForContext("FileName", "VirtualClipboard").Warning($"Clipboard too large: {data.Length} characters");
                        return string.Empty;
                    }
                    else
                    {
                        return data;
                    }
                }
                if (ClipboardApis.IsClipboardFormatAvailable((uint)WindowsClipboardFormat.CF_TEXT))
                {
                    string data = ExtractAnsiText();
                    if (data.Length > 1000000)
                    {
                        Log.ForContext("FileName", "VirtualClipboard").Warning($"Clipboard too large: {data.Length} characters");
                        return string.Empty;
                    }
                    else
                    {
                        return data;
                    }
                }
            }
            finally
            {
                ClipboardApis.CloseClipboard();
            }
            return string.Empty;
        }
        public static bool SetClipboard(byte[] data, uint format)
        {
            if (!ClipboardApis.OpenClipboard(IntPtr.Zero))
            {
                return false;
            }
            try
            {
                if (!ClipboardApis.EmptyClipboard())
                    return false;

                IntPtr hGlobal = MemoryApis.GlobalAlloc((uint)WindowsClipboardFormat.GMEM_MOVEABLE, (UIntPtr)data.Length);
                if (hGlobal == IntPtr.Zero)
                    return false;

                IntPtr pGlobal = MemoryApis.GlobalLock(hGlobal);
                if (pGlobal == IntPtr.Zero)
                    return false;

                Marshal.Copy(data, 0, pGlobal, data.Length);
                MemoryApis.GlobalUnlock(hGlobal);

                IntPtr result = ClipboardApis.SetClipboardData(format, hGlobal);

                return result != IntPtr.Zero;
            }
            finally
            {
                ClipboardApis.CloseClipboard();
            }
        }
        private static string ExtractUnicodeText()
        {
            IntPtr hData = ClipboardApis.GetClipboardData((uint)WindowsClipboardFormat.CF_UNICODETEXT);
            if (hData == IntPtr.Zero)
                return string.Empty;

            IntPtr pData = MemoryApis.GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringUni(pData) ?? string.Empty;
            }
            finally
            {
                MemoryApis.GlobalUnlock(hData);
            }
        }
        private static string ExtractAnsiText()
        {
            IntPtr hData = ClipboardApis.GetClipboardData((uint)WindowsClipboardFormat.CF_TEXT);
            if (hData == IntPtr.Zero)
                return string.Empty;

            IntPtr pData = MemoryApis.GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringAnsi(pData) ?? string.Empty;
            }
            finally
            {
                MemoryApis.GlobalUnlock(hData);
            }
        }
    }
}
