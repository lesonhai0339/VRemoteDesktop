using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.Mouse;
using VRemoteDesktop.Services.ScreenCapture;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.SystemService
{
    public class GlobalHookService : IDisposable
    {
        private readonly object _lock;
        private bool _disposed;

        private readonly IKeyboardService _keyboardService;
        private IScreenCaptureServiceListener _screenCaptureService;
        public event EventHandler<KeyboardEventArgs> KeyboardReceived;
        public event EventHandler<ScreenCaptureEventArgs> ScreenCaptureChanged;
        public GlobalHookService(IKeyboardService keyboardService, IScreenCaptureServiceListener screenCaptureService)
        {
            _lock = new object();
            _disposed = false;
            _keyboardService = keyboardService;
            _keyboardService.KeyPressed += KeyPressedEventHandler;
            _screenCaptureService = screenCaptureService;
            _screenCaptureService.ScreenEvent += ScreenCaptureEventHandler;
        }
        #region Properties
        #endregion
        #region Keyboard
        /// <summary>
        /// Start global keyboard listener
        /// </summary>
        /// <param name="handle"></param>
        public void StartKeyboardListener(uint handle = 0)
        {
            if (handle == 0)
            {
                //listen on this app
                uint h = (uint)Process.GetCurrentProcess().Id;
                _keyboardService.Start(h);
            }
            else
            {
                _keyboardService.Start(handle);
            }
        }
        /// <summary>
        /// Stop global keyboard listener
        /// </summary>
        /// <param name="handle"></param>
        public void StopKeyboardListener()
        {
            _keyboardService.Stop();
        }
        /// <summary>
        /// Start listen keyboard event on specific windows form by their handle
        /// </summary>
        /// <param name="handle"></param>
        public void AddKeyboardHook(IntPtr handle)
        {
            _keyboardService.AddHook(handle);
        }
        /// <summary>
        /// Stop listen keyboard event on specific windows form by their handle
        /// </summary>
        /// <param name="handle"></param>
        public void RemoveKeyboardHook(IntPtr handle)
        {
            _keyboardService.RemoveHook(handle);
        }
        public bool CheckClipboard(KeyboardEventArgs e, out byte[] clipboardBytes, out SocketDataType type)
        {
            clipboardBytes = null;
            type = SocketDataType.None;
            if (e.Combination == KeyCombination.Copy && e.Handle == IntPtr.Zero && e.IsSynthetic)
            {
                type = SocketDataType.ClipboardSend;
                clipboardBytes = Encoding.UTF8.GetBytes(GetClipboard());
                return true;
            }
            return false;
        }
        public void MouseReceivedEventHandler(int width, int height, byte[] data)
        {
            var mouseEvent = VirtualMouse.BytesToCustomMouseEvent(data, width, height);
            if (mouseEvent == null)
                return;

            bool flag = VirtualMouse.MouseEvent(mouseEvent);
            if (!flag)
            {
                Log.ForContext("Filename", GetType().Name).Error("Failed handler mouse event");
            }
        }
        public void KeyboardReceivedEventHandler(byte[] data)
        {
            var keyEvent = VirtualKeyboard.BytesToCustomKeyboardEvent(data);
            if (keyEvent == null)
                return;
            VirtualKeyboard.ProcessKeyboardReceived(keyEvent.Key, keyEvent.Type);
        }
        public string GetClipboard()
        {
            return VirtualClipboard.GetClipboardString();
        }
        /// <summary>
        /// Default using CF_UNICODETEXT format then need to convert string data to UTF-16
        /// (like this: <c>byte[] formatted = Encoding.Unicode.GetBytes(<paramref name="data"/> + '\0');</c>)
        /// </summary>
        /// <param name="data">The input string that will be encoded as UTF-16.</param>
        /// <returns>Formatted byte array.</returns>
        public bool SetClipboard(byte[] data)
        {
            string text = Encoding.UTF8.GetString(data);

            byte[] unicodeData = Encoding.Unicode.GetBytes(text + '\0');

            return VirtualClipboard.SetClipboard(unicodeData, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
        }
        public bool SetClipboard(byte[] data, int index, int length)
        {
            byte[] clipboardData = new byte[length];
            Buffer.BlockCopy(data, index, clipboardData, 0, length);
            return VirtualClipboard.SetClipboard(clipboardData, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
        }
        #endregion
        #region Screen
        public void StartScreenCapture()
        {
            _screenCaptureService.StartCapture();
            _screenCaptureService.IsCapturing = true;
        }
        public void StopScreenCapture()
        {
            _screenCaptureService.StopCapture();
            _screenCaptureService.IsCapturing = false;
        }
        public List<byte[]> GetFirstScreen()
        {
            return _screenCaptureService.GetScreenPackets();
        }
        #endregion
        #region Events
        private void KeyPressedEventHandler(object sender, KeyboardEventArgs e)
        {
            KeyboardReceived?.Invoke(this, e);
        }
        private void ScreenCaptureEventHandler(object sender, ScreenCaptureEventArgs e)
        {
            ScreenCaptureChanged?.Invoke(this, e);
        }
        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_disposed) return;

                if (_keyboardService != null)
                    _keyboardService.KeyPressed -= KeyPressedEventHandler;

                if (_screenCaptureService != null)
                    _screenCaptureService.ScreenEvent -= ScreenCaptureEventHandler;

                _keyboardService?.Dispose();
                _screenCaptureService?.Dispose();
                _disposed = true;
            }
        }
    }
}
