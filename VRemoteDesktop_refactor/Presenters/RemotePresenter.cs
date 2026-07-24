using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.Mouse;
using Vsign4.VRemoteDesktop.Services.Mouse.DTOs;
using Vsign4.VRemoteDesktop.Services.Mouse.Enums;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Events;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.GDI;
using Vsign4.VRemoteDesktop.Services.SessionManagement;
using Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Presenters
{
    public class RemotePresenter : IDisposable
    {
        private int _disposed = 0;

        private readonly string _partnerId;
        private readonly string _partnerName;
        private readonly int _width;
        private readonly int _height;
        private readonly int _stride;
        private readonly IntPtr _bits;

        public Bitmap Picture;
        private readonly byte[] _buffer;


        private readonly IMouseExtensions _mouseExtension;
        private readonly ClientSession _clientSession;
        private readonly RemoteService _remoteService;
        private readonly IVScreenReceiver _screenReceived;

        public event EventHandler<KeyboardEventArgs> OnKeyboard;
        public event EventHandler<OnScreenEventArgs> UpdateScreen;
        public event EventHandler<RemoteDesktopSessionDisconnectEventArgs> OnDisconnect;

        public RemotePresenter(ClientSession clientSession, RemoteService remoteService)
        {
            _clientSession = clientSession;
            _remoteService = remoteService;
            _mouseExtension = new MouseExtensions();

            _partnerId = clientSession.PartnerInfo.PartnerId;
            _partnerName = clientSession.PartnerInfo.ComputerName;
            _width = _clientSession.PartnerInfo.Width;
            _height = _clientSession.PartnerInfo.Height;
            _stride = clientSession.GetStride(_width, clientSession.BytePerPixel);

            _buffer = VArrayPool.Rent(_stride * _height);
            var screenTask = new ScreenTask(_buffer);
            _screenReceived = new VScreenReceiver(screenTask, _width, _height, _clientSession.BytePerPixel, pixelFormat: _clientSession.PixelFormat);
            Picture = new Bitmap(_screenReceived.Width, _screenReceived.Height, _screenReceived.Stride, _screenReceived.PixelFormat, _screenReceived.ScreenHDC);


            _remoteService.OnSessionKeyboard += OnSessionKeyboardEventHandler;
            _remoteService.OnSessionDisconnected += OnSessionDisconnectedEventHandler;
            _clientSession.OnScreenReceived += OnScreenReceivedEventHandler;
        }
        #region Properties
        public int ClientWidth
        {
            get
            {
                return _width;  
            }
        }
        public int ClientHeight
        {
            get
            {
                return _height; 
            }
        }
        public string Id
        {
            get
            {
                return _partnerId;
            }
        }
        public string Name
        {
            get
            {
                return _partnerName;    
            }
        }
        #endregion
        #region Methods
        public void AddKeyboardHook(IntPtr handle)
        {
            _remoteService.AddKeyboardHook(handle);
        }
        public void RemoteKeyboardHook(IntPtr handle)
        {
            _remoteService.RemoveKeyboardHook(handle);
        }
        public RectangleF TransformSize(Size source, Size img, Rectangle rect)
        {
            RectangleF displayRect = _mouseExtension.TransformImageToDisplay(source, img, rect);
            return displayRect;
        }
        public void GetClipboard(KeyboardEventArgs e)
        {
            string clipboard = _remoteService.GetClipboard();
            if (string.IsNullOrEmpty(clipboard)) return;

            _clientSession.AddWork(
                QueuePriority.High,
                new TaskObject
                {
                    TaskType = SocketDataType.ClipboardSend,
                    Data = Encoding.UTF8.GetBytes(clipboard),
                    IsSendHeader = true,
                    SessionId = _clientSession.SessionId,
                });
        }
        public void ProcessKeyboard(KeyboardEventArgs e)
        {
            string keyboard = Helpers.StringHelper.StringBuilderWithSeparator(_remoteService.Separator, (int)e.Command, (int)e.KeyModifier, (int)e.KeyCode, (int)e.KeyType);

            //return if data is empty
            if (string.IsNullOrEmpty(keyboard)) return;

            _clientSession.AddWork(
                QueuePriority.High,
                new TaskObject
                {
                    TaskType = SocketDataType.KeyboardSend,
                    Data = Encoding.ASCII.GetBytes(keyboard),
                    IsSendHeader = true,
                    SessionId = _clientSession.SessionId
                });
        }

        public void ProcessMouseEvent(MouseEventType mouseEvent, PictureBox p, MouseEventArgs e, WindowsMouseMessage mouseMsg = WindowsMouseMessage.None, MouseAction mouseType = MouseAction.None)
        {
            try
            {
                //check image nullable
                if (p.Image == null) return;

                //get actual mouse coordinate before send
                Point adjustedPoint = _mouseExtension.GetImagePointFromMouse(UISizeMode.Zoom, p.Size, p.Image.Size, e.X, e.Y);

                var adjustedMouseEventArgs = new MouseData((VMouseButtons)e.Button, e.Clicks, adjustedPoint.X, adjustedPoint.Y, e.Delta);

                string mouseEventString = _mouseExtension.MouseEventToString(mouseEvent, p.Image.Width, p.Image.Height, adjustedMouseEventArgs, mouseMsg, mouseType);
                if (string.IsNullOrEmpty(mouseEventString))
                    return;

                _clientSession.AddWork(
                     QueuePriority.High,
                    new TaskObject
                    {
                        TaskType = SocketDataType.MouseSend,
                        Data = Encoding.ASCII.GetBytes(mouseEventString),
                        IsSendHeader = true,
                        SessionId = _clientSession.SessionId
                    });
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "FormRemote").Error(ex, "MouseEvents error");
            }

        }
        #endregion
        #region Events
        private void OnScreenReceivedEventHandler(object sender, ClientSessionScreenReceivedEventArgs e)
        {
            try
            {
                if (e == null || e.Data == null)
                    return;

                var isFullScreen = (e.Type == Services.SessionManagement.Events.ClientSession.ScreenType.FullScreen) ? true : false;

                var rectangle = _screenReceived.DecompressedRawData(e.Data, 0, e.Data.Length);

                if (UpdateScreen != null)
                    UpdateScreen.Invoke(this, new OnScreenEventArgs(isFullScreen, rectangle));
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "OnScreenReceivedEventHandler error");
            }
        }

        private void OnSessionKeyboardEventHandler(object sender, KeyboardEventArgs e)
        {
            if (OnKeyboard != null)
            {
                OnKeyboard.Invoke(sender, e);
            }
        }
        private void OnSessionDisconnectedEventHandler(object sender, RemoteDesktopSessionDisconnectEventArgs e)
        {
            if (OnDisconnect != null)
            {
                OnDisconnect.Invoke(this, e);
            }
        }
        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            VArrayPool.Return(_buffer);

            if (disposing)
            {
                if (_clientSession != null)
                {
                    _clientSession.OnScreenReceived -= OnScreenReceivedEventHandler;
                    _clientSession.Close();
                }
                if (_screenReceived != null)
                    _screenReceived.Dispose();

                if (_remoteService != null)
                {
                    _remoteService.OnSessionDisconnected -= OnSessionDisconnectedEventHandler;
                    _remoteService.OnSessionKeyboard -= OnSessionKeyboardEventHandler;
                }

                if (_mouseExtension != null)
                    _mouseExtension.Dispose();

                if (Picture != null)
                    Picture.Dispose();

            }
        }
    }
}
