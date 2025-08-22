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
using VRemoteDesktop.Services.ScreenCapture;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.SystemService
{
    public class GlobalHookService : IDisposable
    {
        private readonly object _lock;
        private bool _disposed;

        private readonly KeyboardService _keyboardService;
        private IScreenCaptureServiceListener _screenCaptureService;
        public event EventHandler<KeyboardEventArgs> KeyboardReceived;
        public event EventHandler<ScreenCaptureEventArgs> ScreenCaptureChanged;
        public GlobalHookService(KeyboardService keyboardService, IScreenCaptureServiceListener screenCaptureService)
        {
            _lock = new object();
            _disposed = false;
            _keyboardService = keyboardService;
            _keyboardService.KeyPressed += KeyPressedEventHandler;
            Task.Factory.StartNew(() =>
            {
                _screenCaptureService = new ScreenCaptureService(null, null);
                _screenCaptureService.ScreenEvent += ScreenCaptureEventHandler;
            }, TaskCreationOptions.LongRunning);
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
            try
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
            catch (Exception ex)
            {

            }
        }
        /// <summary>
        /// Stop global keyboard listener
        /// </summary>
        /// <param name="handle"></param>
        public void StopKeyboardListener()
        {
            try
            {
                _keyboardService.Stop();
            }
            catch (Exception ex)
            {

            }
        }
        /// <summary>
        /// Start listen keyboard event on specific windows form by their handle
        /// </summary>
        /// <param name="handle"></param>
        public void AddKeyboardHook(IntPtr handle)
        {
            try
            {
                _keyboardService.AddHook(handle);
            }
            catch (Exception ex)
            {

            }
        }
        /// <summary>
        /// Stop listen keyboard event on specific windows form by their handle
        /// </summary>
        /// <param name="handle"></param>
        public void RemoveKeyboardHook(IntPtr handle)
        {
            try
            {
                _keyboardService.RemoveHook(handle);
            }
            catch (Exception ex)
            {

            }
        }
        public string GetClipboard()
        {
            try
            {
                return VirtualClipboard.GetClipboardString();
            }
            catch (Exception ex)
            {
                Log.ForContext("Filename", GetType().Name).Error(ex, "GetClipboard error");
                return string.Empty;
            }
        }
        /// <summary>
        /// Default using CF_UNICODETEXT format then need to convert string data to UTF-16
        /// (like this: <c>byte[] formatted = Encoding.Unicode.GetBytes(<paramref name="data"/> + '\0');</c>)
        /// </summary>
        /// <param name="data">The input string that will be encoded as UTF-16.</param>
        /// <returns>Formatted byte array.</returns>
        public bool SetClipboard(byte[] data)
        {
            try
            {
                var bytes = Encoding.Unicode.GetBytes(data + "\0");
                return VirtualClipboard.SetClipboard(bytes, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
            }
            catch (Exception ex)
            {
                Log.ForContext("Filename", GetType().Name).Error(ex, "SetClipboard error");
                return false;
            }
        }
        public bool SetClipboard(byte[] data, int index, int length)
        {
            try
            {
                byte[] clipboardData = new byte[length];
                Buffer.BlockCopy(data, index, clipboardData, 0, length);
                return VirtualClipboard.SetClipboard(clipboardData, (uint)WindowsClipboardFormat.CF_UNICODETEXT);
            }
            catch (Exception ex)
            {
                Log.ForContext("Filename", GetType().Name).Error(ex, "SetClipboard error");
                return false;
            }
        }
        #endregion
        #region Screen
        public void StartScreenCapture()
        {
            try
            {
                _screenCaptureService.StartCapture();
                _screenCaptureService.IsCapturing = true;
            }
            catch(Exception ex)
            {

            }
        }
        public void StopScreenCapture()
        {
            try
            {
                _screenCaptureService.StopCapture();
                _screenCaptureService.IsCapturing = false;
            }
            catch (Exception ex)
            {

            }
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
                if (!_disposed)
                {
                    if(_keyboardService != null)
                    {
                        _keyboardService.KeyPressed -= KeyPressedEventHandler;
                        _keyboardService.Dispose();
                    }
                    if (_screenCaptureService != null)
                    {
                        _screenCaptureService.ScreenEvent -= ScreenCaptureEventHandler;
                        _screenCaptureService.Dispose();
                    }
                    _disposed = true;
                }
            }
        }
    }
}
