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

namespace VRemoteDesktop.ViewModels
{
    public class RemoteViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly VClient _vClient;
        private readonly IMouseExtensions _mouseExtension;
        private readonly RemoteDesktopService _remoteDesktopService;

        public Action<byte[]> ScreenEvent;
        public Action<byte[]> ScreenChunksEvent;
        public RemoteViewModel(VClient vClient, IMouseExtensions mouseExtension, RemoteDesktopService remoteDesktopService)
        {
            _vClient = vClient;
            _mouseExtension = mouseExtension;
            _remoteDesktopService = remoteDesktopService;

            _vClient.P2PScreenReceived += P2PScreenReceivedEventHandler;
        }
        #region Properties
        #endregion
        #region Methods
        public void P2PScreenReceivedEventHandler(object sender, P2PScreenEventArgs e)
        {
            if(e.Type == DataType.Screen)
            {
                ScreenEvent?.Invoke(e.Data);
            }
            if(e.Type == DataType.Chunks)
            {
                ScreenChunksEvent?.Invoke(e.Data);
            }
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
                TaskType = DataType.Clipboard,
                Data = Encoding.UTF8.GetBytes(clipboard),
                IsSendHeader = true,
                SessionId = _vClient.SocketId,
                Priority = QueuePriority.High
            });
        }
        public void ProcessKeyboard(KeyboardEventArgs e)
        {
            string keyboard = Helpers.StringHelper.StringBuilderWithSeparator("|",(int)e.Command, (int)e.KeyModifier, (int)e.KeyCode, (int)e.KeyType);

            //return if data is empty
            if (string.IsNullOrEmpty(keyboard)) return;

            _vClient.AddWork(new TaskObject
            {
                TaskType = DataType.Keyboard,
                Data = Encoding.ASCII.GetBytes(keyboard),
                IsSendHeader = true,
                SessionId = _vClient.SocketId,
                Priority = QueuePriority.High
            });
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
                    TaskType = DataType.Mouse,
                    Data = Encoding.ASCII.GetBytes(mouseEventString),
                    IsSendHeader = true,
                    SessionId = _vClient.SocketId,
                    Priority = QueuePriority.High
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if(_vClient != null)
                {
                    _vClient.P2PScreenReceived -= P2PScreenReceivedEventHandler;
                    _vClient.Dispose();
                }
            }
        }
        #endregion
    }
}
