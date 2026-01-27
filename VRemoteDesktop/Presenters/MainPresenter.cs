using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Presenters.DTOs;
using VRemoteDesktop.Presenters.Enums;
using VRemoteDesktop.Presenters.Events;
using VRemoteDesktop.Services.Client;
using VRemoteDesktop.Services.Machine.DTOs;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.RemoteDesktop.Enums;

namespace VRemoteDesktop.Presenters
{
    public class MainPresenter: IDisposable
    {
        private int _disposed = 0;

        private AutoResetEvent _autoReset;

        private MachineInfo _machineInfo;
        private ClientSession _serverSocket;

        private readonly RemoteService _remoteService;

        public event EventHandler<MainDataEventArgs> OnData;
        public event EventHandler<MainErrorEventArgs> OnError;
        public MainPresenter(RemoteService remoteService)
        {

            _autoReset = new AutoResetEvent(false);

            _remoteService = remoteService;
            _remoteService.OnSessionData += OnDataCallbackEventHandler;
        }
        public void Initialize()
        {
            try
            {
                _machineInfo = _remoteService.GetMachineInfo();
                _serverSocket = _remoteService.NewSocketServer();
                //Send id and password to view
                OnData?.Invoke(this, new MainDataEventArgs(_machineInfo));
            }
            catch (Exception ex)
            {
                var handler = OnError;
                if (handler != null)
                    handler.Invoke(this, new MainErrorEventArgs(ex));
            }
        }
        public void Login()
        {
            try
            {
                _autoReset.Reset();

               UpdateStatus(LoginStatus.Connecting);

                if (!_remoteService.GetServerIP(out string ip) || !_remoteService.GetServerPort(out int port))
                    throw new Exception("Server IP or Server Port not found");

                _remoteService.ConnectToServer(_serverSocket, ip, port);
                bool isSucceed = _autoReset.WaitOne(10 * 1000);

                if (!isSucceed)
                {
                    UpdateStatus(LoginStatus.Disconnected);
                    OnError?.Invoke(this, new MainErrorEventArgs(new Exception("Không thể kết nối đến máy chủ")));
                }
                _remoteService.ServerSocketLogin(_serverSocket);
            }
            catch(Exception ex)
            {
                var handler = OnError;
                if (handler != null)
                    handler.Invoke(this, new MainErrorEventArgs(ex));
            }
        }    
        private void UpdateStatus(LoginStatus status)
        {
            var handler = OnData;
            LoginStatus st = LoginStatus.None;
            string stString = string.Empty;
            switch (status)
            {

                case LoginStatus.Connecting:
                    st = LoginStatus.Connecting;
                    stString = "Đang kết nối...";
                    break;
                case LoginStatus.Connected:
                    st = LoginStatus.Connected;
                    stString = "Đã kết nối";
                    break;
                case LoginStatus.Disconnected:
                    st = LoginStatus.Disconnected;
                    stString = "Mất kết nối";
                    break;
                default:
                    st = LoginStatus.None;
                    stString = "Không xác định";
                    break;
            }
            if(handler != null && st != LoginStatus.None)
            {
                handler.Invoke(this, new MainDataEventArgs(
                    new LoginResponse(st, stString)
                ));
            }
        }
        public void GetPartnerInfo(string id, string password)
        {
            try
            {
                _remoteService.GetPartnerInfo(_serverSocket, id, password);
            }
            catch(Exception ex)
            {
                var handler = OnError;  
                if(handler != null) handler.Invoke(this, new MainErrorEventArgs(ex)); 
            }
        }
        public bool CheckIdConnected(string id)
        {
            try
            {
                return _remoteService.FindClient(id);
            }
            catch (Exception ex)
            {
                var handler = OnError;
                if (handler != null) handler.Invoke(this, new MainErrorEventArgs(ex));
                return false;
            }
        }
        public string StringToStringWithDelimiter(string input, string delimiter, int length)
        {
            try
            {
                return StringHelper.StringToStringWithDelimiter(input, delimiter, length);
            }
            catch(Exception ex)
            {
                var handler = OnError;
                if (handler != null) handler.Invoke(this, new MainErrorEventArgs(ex));
                return string.Empty;
            }
        }
      
        #region Events
        private void OnDataCallbackEventHandler(object sender, RemoteDesktopEventArgs e)
        {
            switch (e.Type)
            {
                case Services.RemoteDesktop.Enums.ResponseType.ConnectSuccess:
                    ConnectCallback(true);
                    break;
                case Services.RemoteDesktop.Enums.ResponseType.ConnectFailed:
                    ConnectCallback(false);
                    break;
                case Services.RemoteDesktop.Enums.ResponseType.LoginSuccess:
                    LoginCallback(true);
                    break;
                case Services.RemoteDesktop.Enums.ResponseType.LoginFailed:
                    LoginCallback(false);
                    break;
                case Services.RemoteDesktop.Enums.ResponseType.GetPartnerInfoFailed:
                    GetPartnerInfoFailedCallback();
                    break;
                case Services.RemoteDesktop.Enums.ResponseType.AddRemoteController:
                case Services.RemoteDesktop.Enums.ResponseType.AddRemoteControlled:
                    NewRemoteConnectionCallback(sender, e.Type);
                    break;
                case ResponseType.P2PFailed:
                    if (OnError != null)
                        OnError.Invoke(this, new MainErrorEventArgs(new UnauthorizedAccessException("Thiết lập kết nối vói máy khách thất bại")));
                    break;
                default:
                    break;
            }
        }

        private void NewRemoteConnectionCallback(object sender, ResponseType type)
        {
            var clientSession = sender as ClientSession;
            if(clientSession != null)
            {
                if(OnData != null)
                {
                    var isController = type == ResponseType.AddRemoteController ? true : false;
                    OnData.Invoke(this, new MainDataEventArgs(new NewRemoteConnection(isController, clientSession)));
                }
            }
        }

        private void GetPartnerInfoFailedCallback()
        {
            OnData?.Invoke(this, new MainDataEventArgs(
                new PartnerInfoResponse("Lấy thông tin máy khách thất bại")));
        }

        private void LoginCallback(bool IsSuccess)
        {
            var responseType = IsSuccess ? LoginStatus.Connected : LoginStatus.Disconnected;
            UpdateStatus(responseType);
        }
        private void ConnectCallback(bool connected)
        {
            if (!connected)
                throw new UnauthorizedAccessException("Cannot connect to server");

            _autoReset.Set();
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
                if (_remoteService != null)
                {
                    _remoteService.OnSessionData -= OnDataCallbackEventHandler;
                }
            }
        }
    }
}
