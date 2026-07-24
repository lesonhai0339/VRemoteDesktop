using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Vsign4.VRemoteDesktop.Services.Keyboard.Enums;

namespace Vsign4.VRemoteDesktop.Services.Keyboard
{
    internal static class VirtualClipboard
    {
        private const int MAX_CLIPBOARD_LENGTH = 1000000;

        public static byte[] DecodeClipboard(byte[] data, int offset, int length)
        {
            try
            {
                byte[] clipboardData = new byte[length];
                Buffer.BlockCopy(data, offset, clipboardData, 0, length);
                string dataString = Encoding.UTF8.GetString(clipboardData);

                //default set clipboard use CF_UNICODETEXT(UTF-16), need to convert data to utf-16
                byte[] clipboardReformatted = Encoding.Unicode.GetBytes(dataString + '\0');

                return clipboardReformatted;
            }
            catch (Exception ex)
            {
                Vsign4.VRemoteDesktop.Utils.Logger.Log.ForContext("FileName", "DecodeClipboard").Error(ex, "Decode Clipboard error");
            }
            return new byte[0];
        }
        public static string GetClipboardString()
        {
            if (!Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.OpenClipboard(IntPtr.Zero))
            {
                return string.Empty;
            }
            try
            {
                if (Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.IsClipboardFormatAvailable((uint)WindowsClipboardFormat.CF_UNICODETEXT))
                {
                    string data = ExtractUnicodeText();
                    if (data.Length > MAX_CLIPBOARD_LENGTH)
                    {
                        Vsign4.VRemoteDesktop.Utils.Logger.Log.ForContext("FileName", "VirtualClipboard").Warning(string.Format("Clipboard too large: {0} characters", data.Length));
                        return string.Empty;
                    }
                    else
                    {
                        return data;
                    }
                }
                if (Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.IsClipboardFormatAvailable((uint)WindowsClipboardFormat.CF_TEXT))
                {
                    string data = ExtractAnsiText();
                    if (data.Length > MAX_CLIPBOARD_LENGTH)
                    {
                        Vsign4.VRemoteDesktop.Utils.Logger.Log.ForContext("FileName", "VirtualClipboard").Warning(string.Format("Clipboard too large: {0} characters", data.Length));
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
                Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.CloseClipboard();
            }
            return string.Empty;
        }
        public static bool SetClipboard(byte[] data, uint format)
        {
            if (!Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.OpenClipboard(IntPtr.Zero))
            {
                return false;
            }
            try
            {
                if (!Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.EmptyClipboard())
                    return false;

                IntPtr hGlobal = Vsign4.VRemoteDesktop.Interop.Win32Apis.MemoryApis.GlobalAlloc((uint)WindowsClipboardFormat.GMEM_MOVEABLE, (UIntPtr)data.Length);
                if (hGlobal == IntPtr.Zero)
                    return false;

                IntPtr pGlobal = Vsign4.VRemoteDesktop.Interop.Win32Apis.MemoryApis.GlobalLock(hGlobal);
                if (pGlobal == IntPtr.Zero)
                    return false;

                Marshal.Copy(data, 0, pGlobal, data.Length);
                Vsign4.VRemoteDesktop.Interop.Win32Apis.MemoryApis.GlobalUnlock(hGlobal);

                IntPtr result = Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.SetClipboardData(format, hGlobal);

                return result != IntPtr.Zero;
            }
            finally
            {
                Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.CloseClipboard();
            }
        }
        private static string ExtractUnicodeText()
        {
            IntPtr hData = Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.GetClipboardData((uint)WindowsClipboardFormat.CF_UNICODETEXT);
            if (hData == IntPtr.Zero)
                return string.Empty;

            IntPtr pData = Vsign4.VRemoteDesktop.Interop.Win32Apis.MemoryApis.GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringUni(pData) ?? string.Empty;
            }
            finally
            {
                Vsign4.VRemoteDesktop.Interop.Win32Apis.MemoryApis.GlobalUnlock(hData);
            }
        }
        private static string ExtractAnsiText()
        {
            IntPtr hData = Vsign4.VRemoteDesktop.Interop.Win32Apis.ClipboardApis.GetClipboardData((uint)WindowsClipboardFormat.CF_TEXT);
            if (hData == IntPtr.Zero)
                return string.Empty;

            IntPtr pData = Vsign4.VRemoteDesktop.Interop.Win32Apis.MemoryApis.GlobalLock(hData);
            if (pData == IntPtr.Zero)
                return string.Empty;

            try
            {
                return Marshal.PtrToStringAnsi(pData) ?? string.Empty;
            }
            finally
            {
                Vsign4.VRemoteDesktop.Interop.Win32Apis.MemoryApis.GlobalUnlock(hData);
            }
        }
    }
}
