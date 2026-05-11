using System;
using System.Drawing;
using System.Text;
using System.Threading;
using VRemoteDesktop.Events;
using VRemoteDesktop.Services.Mouse;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Models;
using static VRemoteDesktop.Utils.Logger;
using System.Windows.Forms;
using VRemoteDesktop.Services.RemoteDesktop.Events;

namespace VRemoteDesktop.Presenters
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
            _stride = clientSession.GetStride(_width , clientSession.BytePerPixel);

            _buffer = VArrayPool.Rent(_stride * _height);
            var screenTask = new ScreenTask(_buffer);
            _screenReceived = new VScreenReceiver(screenTask, _width, _height, _clientSession.BytePerPixel, pixelFormat: _clientSession.PixelFormat);
            Picture = new Bitmap(_screenReceived.Width, _screenReceived.Height, _screenReceived.Stride, _screenReceived.PixelFormat, _screenReceived.ScreenHDC);


            _remoteService.OnSessionKeyboard += OnSessionKeyboardEventHandler;
            _remoteService.OnSessionDisconnected += OnSessionDisconnectedEventHandler;
            _clientSession.OnScreenReceived += OnScreenReceivedEventHandler;
        }
        #region Properties
        public int ClientWidth => _width;
        public int ClientHeight => _height;
        public string Id => _partnerId;
        public string Name => _partnerName;
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
                Log.ForContext("FileName", "FormRemote").Error(ex, "MouseEvents error");
            }

        }
        #endregion
        #region Events
        private void OnScreenReceivedEventHandler(object sender, ClientSessionScreenReceivedEventArgs e)
        {
            var isFullScreen = (e.Type == Services.SessionManagement.Events.ClientSession.ScreenType.FullScreen) ? true : false;

            var rectangle = _screenReceived.DecompressedRawData(e.Data, 0, e.Data.Length);

            if (UpdateScreen != null)
                UpdateScreen.Invoke(this, new OnScreenEventArgs(isFullScreen, rectangle));
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
            if(OnDisconnect != null)
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

                if(_remoteService != null)
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
