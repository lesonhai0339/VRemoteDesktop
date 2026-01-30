using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
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
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using System.Drawing.Imaging;
using static System.Net.Mime.MediaTypeNames;
using System.Threading.Tasks;
using System.Diagnostics;
using VRemoteDesktop.Layouts;
using static VRemoteDesktop.Services.ScreenCapture.Interop.CaptureApi;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;

namespace VRemoteDesktop.ViewModels
{
    public class RemoteViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool _disposed;
        private readonly ClientSession _clientSession;
        private readonly IMouseExtensions _mouseExtension;
        private readonly IScreenCaptureExtensions _screenCaptureExtension;
        private readonly RemoteService _remoteControlService;
#if DEBUG
        private byte[] _buffer;
        private readonly IVScreenReceiver _screenReceiver;



        public Bitmap Picture;
        public int Width;
        public int Height;
        public int Stride;
        public BITMAPINFO BitmapInfo;
        public IntPtr Bits;
        public event EventHandler<OnScreenEventArgs> UpdateScreen;
#endif

        public Action<Bitmap> screenEvent;
        public Action<List<ScreenRegion>> screenRegionsChangedEvent;
        public event EventHandler<KeyboardEventArgs> keyboardEvent;
        public event EventHandler<ClientSessionDisconnectedEventArgs> DisconnectedEvent; 
        public RemoteViewModel(ClientSession clientSession, IScreenCaptureExtensions screenCaptureExtensions, IMouseExtensions mouseExtension, RemoteService remoteControlService)
        {
            _disposed = false;
            _clientSession = clientSession;
#if DEBUG
            _buffer = VArrayPool.Rent(10 * 1024 * 1024);
            var receiverScreenTask = new ScreenTask(_buffer);
            _screenReceiver = new VScreenReceiver(receiverScreenTask, _clientSession.PartnerInfo.Width, _clientSession.PartnerInfo.Height);
            Picture = new Bitmap(_screenReceiver.Width, _screenReceiver.Height, _screenReceiver.Stride, _screenReceiver.PixelFormat, _screenReceiver.ScreenHDC);
            Width = _screenReceiver.Width;
            Height = _screenReceiver.Height;
            BitmapInfo = _screenReceiver.BITMAPINFO;
            Bits = _screenReceiver.Bits;
            Stride = _screenReceiver.Stride;
#endif
            _mouseExtension = mouseExtension;
            _screenCaptureExtension = screenCaptureExtensions;
            _remoteControlService = remoteControlService;

            _clientSession.OnScreenReceived += P2PScreenReceivedEventHandler;
            _clientSession.OnDisconnected += SocketDisconnectedEventHandler;
            _remoteControlService.OnSessionKeyboard += KeyboardReceivedEventHandler;
        }
        //private void SocketDisposingEventHandler(object sender, SocketDisposeEventArgs e)
        private void SocketDisconnectedEventHandler(object sender, ClientSessionDisconnectedEventArgs e)
        {
            DisconnectedEvent?.Invoke(this, e);
        }
        #region Properties
        #endregion
        #region Methods
        public void StartKeyboardListener(IntPtr handler)
        {
            _remoteControlService.AddKeyboardHook(handler);
        }

        public void StopKeyboardListener(IntPtr handler)
        {
            _remoteControlService.RemoveKeyboardHook(handler);
        }

        public RectangleF TransformSize(Size source, Size img, Rectangle rect)
        {
            RectangleF displayRect = _mouseExtension.TransformImageToDisplay(source, img, rect);
            return displayRect;
        }

        public void GetClipboard(KeyboardEventArgs e)
        {
            string clipboard = _remoteControlService.GetClipboard();
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

        private Bitmap ParseScreenByteArrayToBitmapImage(byte[] bytes)
        {
            try
            {
                var screenData = _screenCaptureExtension.RawScreenToScreenData(bytes);

                //var screenData = _screenCaptureExtension.RawScreenToScreenDataWithoutChecksum(bytes);

                Bitmap image = _screenCaptureExtension.WriteToBitmap(screenData);

                return image;
            }
            catch
            {
                return null;
            }
        }

        private List<ScreenRegion> ParseScreenRegionsChangedByteArrayToList(byte[] bytes)
        {
            //var regions = _screenCaptureExtension.RawChunksToRegions(bytes);

            var regions = _screenCaptureExtension.RawChunksToRegionsWithoutChecksum(bytes);

            if (regions == null || regions.Count == 0)
                return null;
            return regions;
        }

        private void ProcessScreenReceived(byte[] screen)
        {
            Bitmap image = ParseScreenByteArrayToBitmapImage(screen);
            if (image != null)
            {
                _clientSession.AddWork(
                    QueuePriority.High,
                    new TaskObject(
                        type: SocketDataType.ScreenOk,
                        _clientSession.SessionId, 
                        isSendHeader: true, 
                        data: new byte[0]
                    ));
                screenEvent?.Invoke(image);
            }
        }

        private void ProcessScreenRegionsChangedReceived(byte[] screenRegionsChanged)
        {
            var regions = ParseScreenRegionsChangedByteArrayToList(screenRegionsChanged);
            screenRegionsChangedEvent?.Invoke(regions);
        }

        public void ProcessKeyboard(KeyboardEventArgs e)
        {
            string keyboard = Helpers.StringHelper.StringBuilderWithSeparator(DEFAULT_SEPARATOR,(int)e.Command, (int)e.KeyModifier, (int)e.KeyCode, (int)e.KeyType);

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
        #region EventHandlers
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        public void AddWork(QueuePriority priority, TaskObject obj)
        {
            _clientSession.AddWork(priority, obj);
        }
        //public void P2PScreenReceivedEventHandler(object sender, P2PScreenEventArgs e)
        public void P2PScreenReceivedEventHandler(object sender, EventArgs g)
        {
//#if DEBUG
//            var e = (P2PScreenEventArgs)g;
//            var type = (e.Type == SocketDataType.ScreenSend) ? true : false;

//            var rectangle = _screenReceiver.DecompressedRawData(e.Data, 0, e.Data.Length);

//            if (UpdateScreen != null)
//                UpdateScreen.Invoke(this, new OnScreenEventArgs(type, rectangle));

//            _clientSession.AddWork(
//                QueuePriority.High,
//                new TaskObject(
//                    type: (type) ? SocketDataType.ScreenOk : SocketDataType.RegionsChangedOk, 
//                    _clientSession.SessionId, 
//                    isSendHeader: true, 
//                    data: new byte[0]));
//            return;
//#endif
//            if (e.Type == SocketDataType.ScreenSend)
//            {
//                ProcessScreenReceived(e.Data);
//            }
//            if (e.Type == SocketDataType.ScreenRegionsChangedSend)
//            {
//                ProcessScreenRegionsChangedReceived(e.Data);
//            }
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

#if DEBUG
                VArrayPool.Return(_buffer);
#endif

                if (_clientSession != null)
                {
                    _clientSession.OnScreenReceived -= P2PScreenReceivedEventHandler;
                    _clientSession.OnDisconnected -= SocketDisconnectedEventHandler;
                }
                if (_remoteControlService != null)
                    _remoteControlService.OnSessionKeyboard -= KeyboardReceivedEventHandler;
                _disposed = true;
            }
        }
    }
}
