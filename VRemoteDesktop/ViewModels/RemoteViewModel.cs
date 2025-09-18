using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using VRemoteServer.Models;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.VTCPClient;
using System.Drawing;
using System.Windows.Forms;
using VRemoteDesktop.Services.Mouse;
using static VRemoteDesktop.Utils.Logger;
using VRemoteDesktop.Events;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.RemoteDesktop;
using static VRemoteDesktop.Utils.DefaultValue;
using VRemoteDesktop.Services.ScreenCapture;

namespace VRemoteDesktop.ViewModels
{
    public class RemoteViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool _disposed;
        private readonly VClient _vClient;
        private readonly IMouseExtensions _mouseExtension;
        private readonly IScreenCaptureExtensions _screenCaptureExtension;
        private readonly RemoteDesktopService _remoteDesktopService;

        public Action<Bitmap> screenEvent;
        public Action<List<ScreenRegion>> screenRegionsChangedEvent;
        public event EventHandler<KeyboardEventArgs> keyboardEvent;
        public RemoteViewModel(VClient vClient, IScreenCaptureExtensions screenCaptureExtensions, IMouseExtensions mouseExtension, RemoteDesktopService remoteDesktopService)
        {
            _disposed = false;
            _vClient = vClient;
            _mouseExtension = mouseExtension;
            _screenCaptureExtension = screenCaptureExtensions;
            _remoteDesktopService = remoteDesktopService;

            _vClient.P2PScreenReceived += P2PScreenReceivedEventHandler;
            _remoteDesktopService.KeyboardEvent += KeyboardReceivedEventHandler;
        }
        #region Properties
        #endregion
        #region Methods
        public void StartKeyboardListener(IntPtr handler)
        {
            _remoteDesktopService.AddKeyboardListenerOnFormByHandle(handler);
        }
        public void StopKeyboardListener(IntPtr handler)
        {
            _remoteDesktopService.RemoveKeyboardListenerOnFormByHandle(handler);
        }
        private Bitmap ParseScreenByteArrayToBitmapImage(byte[] bytes)
        {
            var screenData = _screenCaptureExtension.RawScreenToScreenData(bytes);

            //var screenData = _screenCaptureExtension.RawScreenToScreenDataWithoutChecksum(bytes);

            Bitmap image = _screenCaptureExtension.WriteToBitmap(screenData);

            return image;
        }
        private void ProcessScreenReceived(byte[] screen)
        {
            Bitmap image = ParseScreenByteArrayToBitmapImage(screen);
            screenEvent?.Invoke(image);
        }
        private List<ScreenRegion> ParseScreenRegionsChangedByteArrayToList(byte[] bytes)
        {
            var regions = _screenCaptureExtension.RawChunksToRegions(bytes);

            //var regions = _screenCaptureExtension.RawChunksToRegionsWithoutChecksum(bytes);

            if (regions == null || regions.Count == 0)
                return null;

            return regions;
        }
        private void ProcessScreenRegionsChangedReceived(byte[] screenRegionsChanged)
        {
            var regions = ParseScreenRegionsChangedByteArrayToList(screenRegionsChanged);
            screenRegionsChangedEvent?.Invoke(regions);
        }
        public RectangleF TransformSize(Size source, Size img, Rectangle rect)
        {
            RectangleF displayRect = _mouseExtension.TransformImageToDisplay(source, img, rect);
            return displayRect;
        }
        public void GetClipboard(KeyboardEventArgs e)
        {
            string clipboard = _remoteDesktopService.GetClipboardString();
            if (string.IsNullOrEmpty(clipboard)) return;

            _vClient.AddWork(new TaskObject
            {
                TaskType = SocketDataType.Clipboard,
                Data = Encoding.UTF8.GetBytes(clipboard),
                IsSendHeader = true,
                SessionId = _vClient.SocketId
            }, QueuePriority.High);
        }
        public void ProcessKeyboard(KeyboardEventArgs e)
        {
            string keyboard = Helpers.StringHelper.StringBuilderWithSeparator(DEFAULT_SEPRATOR,(int)e.Command, (int)e.KeyModifier, (int)e.KeyCode, (int)e.KeyType);

            //return if data is empty
            if (string.IsNullOrEmpty(keyboard)) return;

            _vClient.AddWork(new TaskObject
            {
                TaskType = SocketDataType.Keyboard,
                Data = Encoding.ASCII.GetBytes(keyboard),
                IsSendHeader = true,
                SessionId = _vClient.SocketId
            }, QueuePriority.High);
        }
        public void ProcessMouseEvent(
            MouseEventType mouseEvent,
            PictureBox p,
            MouseEventArgs e,
            WindowsMouseMessage mouseMsg = WindowsMouseMessage.None,
            MouseAction mouseType = MouseAction.None)
        {
            try
            {
                //check image nullable
                if (p.Image == null) return;

                //get actual mouse coordinate before send
                Point adjustedPoint = _mouseExtension.GetImagePointFromMouse(UISizeMode.Zoom, p.Size,p.Image.Size, e.X, e.Y);

                var adjustedMouseEventArgs = new MouseData((VMouseButtons)e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

                string mouseEventString = _mouseExtension.MouseEventToString(mouseEvent, p.Image.Width, p.Image.Height, adjustedMouseEventArgs, mouseMsg, mouseType);
                if (string.IsNullOrEmpty(mouseEventString))
                    return;

                _vClient.AddWork(new TaskObject
                {
                    TaskType = SocketDataType.Mouse,
                    Data = Encoding.ASCII.GetBytes(mouseEventString),
                    IsSendHeader = true,
                    SessionId = _vClient.SocketId
                }, QueuePriority.High);            
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "FormRemote").Error(ex, "MouseEvents error");
            }
        }
        #endregion
        #region EventHandlers
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public void P2PScreenReceivedEventHandler(object sender, P2PScreenEventArgs e)
        {
            if (e.Type == SocketDataType.Screen)
            {
                ProcessScreenReceived(e.Data);
            }
            if (e.Type == SocketDataType.Chunks)
            {
                ProcessScreenRegionsChangedReceived(e.Data);
            }
        }
        private void KeyboardReceivedEventHandler(object sender, KeyboardEventArgs e)
        {
            //Direct to from
            keyboardEvent?.Invoke(sender, e);
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

                if (_vClient != null)
                    _vClient.P2PScreenReceived -= P2PScreenReceivedEventHandler;
                if(_remoteDesktopService != null)
                    _remoteDesktopService.KeyboardEvent -= KeyboardReceivedEventHandler;
                _disposed = true;
            }
        }
    }
}
