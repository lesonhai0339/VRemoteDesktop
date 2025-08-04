using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteClient.Models.Enums;
using VRemoteClient.Utils;
namespace VRemoteClient.Services.KeyboardService
{
    public static class VirtualClipboard
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
            if (!Libraries.ClipboardApis.OpenClipboard(IntPtr.Zero))
            {
                return string.Empty;
            }
            try
            {
                if (Libraries.ClipboardApis.IsClipboardFormatAvailable((uint)ClipboardFormat.CF_UNICODETEXT))
                {
                    string data=  ExtractUnicodeText();
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
                if (Libraries.ClipboardApis.IsClipboardFormatAvailable((uint)ClipboardFormat.CF_TEXT))
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
                Libraries.ClipboardApis.CloseClipboard();
            }
            return string.Empty;
        }
        public static bool SetClipboard(byte[] data, uint format)
        {
            if (!Libraries.ClipboardApis.OpenClipboard(IntPtr.Zero))
            {
                return false;
            }
            try
            {
                if (!Libraries.ClipboardApis.EmptyClipboard())
                    return false;

                IntPtr hGlobal = Libraries.MemoryApis.GlobalAlloc((uint)ClipboardFormat.GMEM_MOVEABLE, (UIntPtr)data.Length);
                if (hGlobal == IntPtr.Zero)
                    return false;

                IntPtr pGlobal = Libraries.MemoryApis.GlobalLock(hGlobal);
                if (pGlobal == IntPtr.Zero)
                    return false;

                Marshal.Copy(data, 0 , pGlobal, data.Length);
                Libraries.MemoryApis.GlobalUnlock(hGlobal);

                IntPtr result = Libraries.ClipboardApis.SetClipboardData(format, hGlobal);

                return result != IntPtr.Zero;
            }
            finally
            {
                Libraries.ClipboardApis.CloseClipboard();
            }
        }
        private static string ExtractUnicodeText()
        {
            IntPtr hData = Libraries.ClipboardApis.GetClipboardData((uint)ClipboardFormat.CF_UNICODETEXT);
            if (hData == IntPtr.Zero)
                return string.Empty;

            IntPtr pData = Libraries.MemoryApis.GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringUni(pData) ?? string.Empty;
            }
            finally
            {
                Libraries.MemoryApis.GlobalUnlock(hData);
            }
        }
        private static string ExtractAnsiText()
        {
            IntPtr hData = Libraries.ClipboardApis.GetClipboardData((uint)ClipboardFormat.CF_TEXT);
            if (hData == IntPtr.Zero)
                return string.Empty;

            IntPtr pData = Libraries.MemoryApis.GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringAnsi(pData) ?? string.Empty;
            }
            finally
            {
                Libraries.MemoryApis.GlobalUnlock(hData);
            }
        }
    }
}
