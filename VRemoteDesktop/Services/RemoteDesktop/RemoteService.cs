using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Client;
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.Machine.DTOs;
using VRemoteDesktop.Services.RemoteDesktop.Enums;
using VRemoteDesktop.Services.RemoteDesktop.Events;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SessionManagement;
using VRemoteDesktop.Services.SessionManagement.Enums;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService : IDisposable
    {
        private readonly object _lock = new object();



        private const int BYTE_PER_PIXEL = 2;
        private const PixelFormat PIXEL_FORMAT = PixelFormat.Format16bppRgb555;
        private const int FPS = 10;



        private const int RETRY = 0;
        private const int TIMEOUT = 3000;
        private const int SESSION_ID_LENGTH = 8;
        private const string SEPARATOR = "|";
        private const string SUCCESS = "1";
        private const string FAILED = "0";

        private readonly string DEFAULT_SERVER_IP = AppSettingHelper.GetValue("ServerIP");
        private readonly string DEFAULT_LOGIN_PORT = AppSettingHelper.GetValue("LoginPort");
        private readonly string DEFAULT_REMOTE_PORT = AppSettingHelper.GetValue("RemotePort");

        private int _disposed = 0;
        private bool _isCapturing = false;

        private readonly IMachineProfile _machineProfile;
        private readonly IVScreenSender _screenSender;
        private readonly IKeyboardService _keyboardService;
        private readonly ISessionManager _sessionManager;

        private Dictionary<SocketDataType, Action<object, EventArgs>> _eventHandlers;

        public event EventHandler<RemoteDesktopErrorEventArgs> OnError;
        public event EventHandler<KeyboardEventArgs> OnSessionKeyboard;
        public event EventHandler<RemoteDesktopEventArgs> OnSessionData;
        public event EventHandler<RemoteDesktopSessionDisconnectEventArgs> OnSessionDisconnected;
        public RemoteService()
        {
            _machineProfile = new MachineProfile(DEFAULT_LOGIN_PORT);

            _screenSender = new VScreenSender(_machineProfile.Bounds.Width, _machineProfile.Bounds.Height, BYTE_PER_PIXEL, FPS);
            _screenSender.OnFrame += OnRegionEventHandler;

            _keyboardService = new KeyboardService();
            _keyboardService.KeyPressed += KeyPressedEventHandler;
            StartKeyboardListener();

            _sessionManager = new SessionManager();
            _sessionManager.SessionDataReceived += ClientSessionDataReceivedEventHandler;
            _sessionManager.SessionClosed += ClientSessionClosedEventHandler;
        }


        #region Properties
        public string Separator => SEPARATOR;
        #endregion

        #region Methods
        public bool GetServerIP(out string serverIp)
        {
            //Implement after. Now return default server ip
            serverIp = DEFAULT_SERVER_IP;
            return true;
        }
        public bool GetServerPort(out int serverPort)
        {
            //Implement after. Now return default server ip
            serverPort = int.Parse(DEFAULT_LOGIN_PORT);
            return true;
        }
        private void SendAck(object sender, byte[] data, SocketDataType type)
        {
            var clientSession = sender as ClientSession;
            if (clientSession != null)
            {
                clientSession.AddWork(VRemoteDesktop.Enums.QueuePriority.High, new TaskObject(type, data));
                //clientSession.Send(type, data);
            }
        }
        private void StartCapture()
        {
            var existed = _sessionManager.HasClientOfType(ClientType.Controlled);
            if (existed)
            {
                StartScreenCapture();
            }
        }
        private void StopCapture()
        {
            var existed = _sessionManager.HasClientOfType(ClientType.Controlled);
            if (!existed)
            {
                StopCapture();
            }
        }
        private void ErrorCallback(ResponseType type, string message)
        {
            var handler = OnSessionData;
            if (handler != null)
            {
                handler.Invoke(this, new RemoteDesktopEventArgs(type: type, message: message));
            }
        }
        private void ClientSessionDataReceivedEventHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            try
            {
                switch (e.Type)
                {
                    case SocketDataType.Connect:
                        ConnectCallback(sender, e);
                        break;
                    case SocketDataType.LoginResponse:
                        LoginEventHandler(sender, e);
                        break;
                    case SocketDataType.RequestRemoteConnect:
                        CreateRemoteConnection(sender, e.Data);
                        break;
                    case SocketDataType.GetPartnerInfoSuccess:
                        GetPartnerInfoSuccessCallback(e.Data);
                        break;
                    case SocketDataType.GetPartnerInfoFailed:
                        GetPartnerInfoFailedCallback(sender, e.Data);
                        break;
                    case SocketDataType.RemoteLogin:
                        RemoteLoginCallback(sender, e.Data);
                        break;
                    case SocketDataType.RemoteLoginFailed:
                        ErrorCallback(ResponseType.P2PFailed, "P2P failed");
                        break;
                    case SocketDataType.RemoteLoginSuccess:
                    case SocketDataType.ReadyToRemoteController:
                        ReadyToRemoteControllerHandler(sender, e);
                        break;
                    case SocketDataType.ReadyToRemoteControlled:
                        ReadyToRemoteControlledHandler(sender, e);
                        break;
                    case SocketDataType.Disconnect:
                        break;
                    case SocketDataType.RemoteControlDisconnect:
                        RemoteControlDisconnectHandler(sender, e);
                        break;
                    case SocketDataType.MouseSend:
                        MouseReceived(sender, e);
                        break;
                    case SocketDataType.KeyboardSend:
                        KeyboardReceivedEventHandler(sender, e);
                        break;
                    case SocketDataType.ClipboardSend:
                        break;
                    default:
                        break;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, string.Format("Error handling {0}: {1}", e.Type, ex.Message));
            }
        }
        class ImageInfo
        {
            public const int FPS = 10;
            public const int SRCCOPY = 0x00CC0020;
            public const uint DIB_RGB_COLORS = 0;
            public const uint pageProtect = 0x40;
            public const int REGION_SIZE = 16;

            //Format16bppRgb555 = 2, Format24bppRgb = 3, Format32bppRgb = 4
            public const int BYTE_PER_PIXEL = 2;
            //Can accept Format16bppRgb555, Format24bppRgb, Format32bppRgb. 
            public const PixelFormat PIXEL_FORMAT = PixelFormat.Format16bppRgb555;
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

            if (disposing)
            {
                if(_screenSender != null)
                {
                    _screenSender.OnFrame -= OnRegionEventHandler;
                    _screenSender.Cancel();
                    _screenSender.Dispose();
                }

                if (_keyboardService != null)
                {
                    _keyboardService.KeyPressed -= KeyPressedEventHandler;
                    StopKeyboardListener();
                    _keyboardService.Dispose();
                }
                if (_sessionManager != null)
                {
                    _sessionManager.SessionDataReceived -= ClientSessionDataReceivedEventHandler;
                    _sessionManager.SessionClosed -= ClientSessionClosedEventHandler;
                    _sessionManager.Dispose();
                }
                if (_machineProfile != null)
                    _machineProfile.Dispose();
            }
        }
    }
}
