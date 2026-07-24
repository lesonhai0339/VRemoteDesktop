using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Helpers;
using Vsign4.VRemoteDesktop.Presenters.DTOs;
using Vsign4.VRemoteDesktop.Services.Keyboard;
using Vsign4.VRemoteDesktop.Services.Machine;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.GDI;
using Vsign4.VRemoteDesktop.Services.SessionManagement;
using Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService : IDisposable
    {
        private readonly object _lock = new object();

        //Phần này có thể tùy chỉnh
        private const int BYTE_PER_PIXEL = 2;
        private const PixelFormat PIXEL_FORMAT = PixelFormat.Format16bppRgb555;
        private const int FPS = 15;
        private const char SEP = '\u001F';

        // Ports are fixed for this client build.
        private const string DEFAULT_LOGIN_PORT = "2399";
        private const string DEFAULT_REMOTE_PORT = "2400";

        // Server IP is resolved from the remote API (not hardcoded / not from the local
        // config file) so the server address can be changed centrally without redeploying
        // clients. The result is cached after the first successful call.
        private const string SERVER_IP_API_URL = "https://ehoadondientu.com/myservice.asmx/VRemoteDesktopServer";
        private string _serverIpCache;
        private readonly object _serverIpLock = new object();

        private int _disposed = 0;

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
        public string Separator
        {
            get
            {
                return HeaderSchema.Separator;
            }
        }
        #endregion

        #region Methods
        /// <summary>
        /// Server IP resolved from <see cref="SERVER_IP_API_URL"/>. Cached after the first
        /// successful call. On API failure it falls back to the local config value
        /// ("ServerIP") and is NOT cached, so the next access retries the API.
        /// </summary>
        private string DEFAULT_SERVER_IP
        {
            get
            {
                var cached = _serverIpCache;
                if (!string.IsNullOrEmpty(cached))
                    return cached;

                lock (_serverIpLock)
                {
                    if (!string.IsNullOrEmpty(_serverIpCache))
                        return _serverIpCache;

                    string ip = FetchServerIpFromApi();
                    if (!string.IsNullOrEmpty(ip))
                    {
                        _serverIpCache = ip; // cache only a successful API result
                        return _serverIpCache;
                    }

                    // API failed -> fall back to config, but don't cache so we retry later.
                    return AppSettingHelper.GetValue("ServerIP");
                }
            }
        }
        private string FetchServerIpFromApi()
        {
            try
            {
                // .NET 4.0 defaults to SSL3/TLS1.0; enable TLS 1.2/1.1 (values cast because
                // the SecurityProtocolType.Tls12/Tls11 enum members don't exist in 4.0).
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)(3072 | 768 | 192);

                using (var client = new WebClient())
                {
                    client.Encoding = Encoding.UTF8;
                    string raw = client.DownloadString(SERVER_IP_API_URL);
                    if (string.IsNullOrEmpty(raw))
                        return null;

                    raw = raw.Trim();

                    // ASMX returns the value wrapped as <string ...>value</string>.
                    string value = raw;
                    try
                    {
                        var doc = new System.Xml.XmlDocument();
                        doc.LoadXml(raw);
                        if (doc.DocumentElement != null)
                            value = doc.DocumentElement.InnerText.Trim();
                    }
                    catch
                    {
                        // Response wasn't XML; use the raw text as-is.
                    }

                    return string.IsNullOrEmpty(value) ? null : value;
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "FetchServerIpFromApi failed, falling back to config ServerIP");
                return null;
            }
        }
        public bool GetServerIP(out string serverIp)
        {
            serverIp = DEFAULT_SERVER_IP;
            return !string.IsNullOrEmpty(serverIp);
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
                clientSession.AddWork(QueuePriority.High, new TaskObject(type, data));
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
                // Was recursively calling StopCapture() -> StackOverflow. It must stop the
                // screen capture pipeline when no controlled session remains.
                StopScreenCapture();
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
                        if (OnSessionData != null)
                            OnSessionData.Invoke(sender, new RemoteDesktopEventArgs(ResponseType.Disconnect, "Disconnected"));
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
                        ClipboardReceived(sender, e);
                        break;
                    case SocketDataType.Ping:
                        PingEventHandler(sender, e);    
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
                if (_screenSender != null)
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
