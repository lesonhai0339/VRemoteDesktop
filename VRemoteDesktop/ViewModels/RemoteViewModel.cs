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

namespace VRemoteDesktop.ViewModels
{
    public class RemoteViewModel : INotifyPropertyChanged
    {
        private VClient _vClient;
        private ClientInfo _connectionInfo;
        private readonly IMouseExtensions _mouseExtension;
        private readonly GlobalHookService _globalHook;

        public Action<byte[]> ScreenEvent;
        public Action<byte[]> ScreenChunksEvent;
        public RemoteViewModel(VClient vClient, ClientInfo connectionInfo, IMouseExtensions mouseExtension, GlobalHookService globalHook)
        {
            _vClient = vClient;
            ConnectionInfo = connectionInfo;
            _mouseExtension = mouseExtension;
            _globalHook = globalHook;
        }
        #region Properties
        public ClientInfo ConnectionInfo
        {
            get => _connectionInfo;
            private set
            {
                _connectionInfo = value;
            }
        }
        #endregion
        #region Methods
        public void DataReceived(DataType type, byte[] data)
        {
            if(type == DataType.Screen)
            {
                ScreenEvent?.Invoke(data);
            }
            if(type == DataType.Chunks)
            {
                ScreenChunksEvent?.Invoke(data);
            }
        }
        public RectangleF TransformSize(Size source, Size img, Rectangle rect)
        {
            RectangleF displayRect = _mouseExtension.TransformImageToDisplay(source, img, rect);
            return displayRect;
        }
        public void GetClipboard(KeyboardEventArgs e)
        {
            string clipboard = _globalHook.GetClipboard();
            if (string.IsNullOrEmpty(clipboard)) return;

            _vClient.AddWork(new TaskObject
            {
                TaskType = DataType.Clipboard,
                Data = Encoding.ASCII.GetBytes(clipboard),
                IsSendHeader = true,
                SessionId = _vClient.SocketId
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
                SessionId = _vClient.SocketId
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
                    SessionId = _vClient.SocketId
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
        #endregion
    }
}
