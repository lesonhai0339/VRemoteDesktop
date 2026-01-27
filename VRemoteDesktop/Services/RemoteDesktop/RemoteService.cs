using System;
using System.Collections.Generic;
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
        private const int RETRY = 0;
        private const int TIMEOUT = 3000;
        private const int SESSION_ID_LENGTH = 8;
        private const string SEPARATOR = "|";
        private const string SUCCESS = "1";
        private const string FAILED = "0";
#if DEBUG
        private readonly string DEFAULT_SERVER_IP = AppSettingHelper.GetValue("ServerIP");
        private readonly string DEFAULT_LOGIN_PORT = "2399";
        private readonly string DEFAULT_REMOTE_PORT = "2400";
#endif
        //private readonly string DEFAULT_SERVER_IP = AppSettingHelper.GetValue("ServerIP");
        //private readonly string DEFAULT_LOGIN_PORT = AppSettingHelper.GetValue("LoginPort");
        //private readonly string DEFAULT_REMOTE_PORT = AppSettingHelper.GetValue("RemotePort");
        private volatile bool _disposed;
        private bool _isCapturing = false;

#if DEBUG
        private readonly IVScreenSender _screenSender;
        private readonly IKeyboardService _keyboardService;

#endif
        private readonly IMachineProfile _machineProfile;
        private readonly SessionManager _sessionManager;
        private ManualResetEvent _reset;

        private Dictionary<SocketDataType, Action<object, EventArgs>> _eventHandlers;


        public event EventHandler<KeyboardEventArgs> OnSessionKeyboard;
        public event EventHandler<RemoteDesktopEventArgs> OnSessionData;
        public event EventHandler<EventArgs> OnError;
        public RemoteService(IVScreenSender screenSender, SessionManager sessionManager, IMachineProfile machineProfile, IKeyboardService keyboardService)
        {
            _disposed = false;
            _machineProfile = machineProfile;
            _reset = new ManualResetEvent(false);

            _keyboardService = keyboardService;
            _sessionManager = sessionManager;

            _screenSender = screenSender;
            _screenSender.OnFrame += OnRegionEventHandler;


            _keyboardService.KeyPressed += KeyPressedEventHandler;
            _sessionManager.SessionDataReceived += ClientSessionDataReceivedEventHandler;
            _sessionManager.SessionClosed += ClientSessionClosedEventHandler;
            StartKeyboardListener();
        }


        public string Separator => SEPARATOR;
        public bool Disposed => _disposed;
        
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
                clientSession.Send(type, data);
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
            if(handler != null)
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
                        GetPartnerInfoFailedCallback(sender,  e.Data);
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
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, string.Format("Error handling {0}: {1}", e.Type, ex.Message));
            }
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

                if (_disposed) return;

                StopKeyboardListener();
                if (_keyboardService != null)
                {
                    _keyboardService.KeyPressed -= KeyPressedEventHandler;
                }
                if (_sessionManager != null)
                {
                    _sessionManager.SessionDataReceived -= ClientSessionDataReceivedEventHandler;
                    _sessionManager.SessionClosed -= ClientSessionClosedEventHandler;
                }

                _keyboardService?.Dispose();
                _sessionManager?.Dispose();
                _reset?.Dispose();
                _disposed = true;
            }
        }
    }
}
