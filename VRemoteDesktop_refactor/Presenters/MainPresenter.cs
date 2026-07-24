using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.Helpers;
using Vsign4.VRemoteDesktop.Presenters.DTOs;
using Vsign4.VRemoteDesktop.Presenters.Enums;
using Vsign4.VRemoteDesktop.Presenters.Events;
using Vsign4.VRemoteDesktop.Services.Machine.DTOs;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.DTOs;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.Enums;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.Events;
using Vsign4.VRemoteDesktop.Services.SessionManagement;
using Vsign4.VRemoteDesktop.Utils;
using LoginResponse = Vsign4.VRemoteDesktop.Presenters.DTOs.LoginResponse;

namespace Vsign4.VRemoteDesktop.Presenters
{
    public class MainPresenter : IDisposable
    {
        private int _disposed = 0;
        private int _index = 0;

        private AutoResetEvent _autoReset;

        private System.Threading.Timer _background;
        private int _waitCount = 0;


        private MachineInfo _machineInfo;
        private ClientSession _serverSocket;

        private readonly RemoteService _remoteService;



        public event EventHandler<MainDataEventArgs> OnData;
        public event EventHandler<MainErrorEventArgs> OnError;
        public event EventHandler<P2PConnectionChangedEventArgs> OnP2PConnectionChanged;

        public MainPresenter(RemoteService remoteService)
        {

            _autoReset = new AutoResetEvent(false);
            _background = new Timer(ChangeP2PStatusText, null, Timeout.Infinite, Timeout.Infinite);

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
                if(OnData != null)
                    OnData.Invoke(this, new MainDataEventArgs(_machineInfo));
            }
            catch (Exception ex)
            {
                var handler = OnError;
                if (handler != null)
                    handler.Invoke(this, new MainErrorEventArgs(ex));
            }
        }
        public void StartP2PConnectStatus()
        {
            if(_background != null)
                _background.Change(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));
        }
        public void StopP2PConnectStatus()
        {
            _waitCount = 0;
            if (OnP2PConnectionChanged != null)
                OnP2PConnectionChanged.Invoke(this, new P2PConnectionChangedEventArgs(string.Empty));

            if (_background != null)
                this._background.Change(Timeout.Infinite, Timeout.Infinite);
        }
        private void ChangeP2PStatusText(object obj)
        {
            string[] stringArray = new string[4] { "Đang kết nối", "Đang kết nối.", "Đang kết nối..", "Đang kết nối..." };

            if (OnP2PConnectionChanged != null)
                OnP2PConnectionChanged.Invoke(this, new P2PConnectionChangedEventArgs(stringArray[_index]));

            _index++;
            _waitCount++;
            if (_index > 3)
            {
                _index = 0;
            }
            if(_waitCount >= 30)
            {
                RemoteControlConnectFailed();
            }
        }
        public void Login()
        {
            try
            {
                _autoReset.Reset();

                UpdateStatus(LoginStatus.Connecting);

                string ip = string.Empty;
                int port = 0;
                if (!_remoteService.GetServerIP(out ip) || !_remoteService.GetServerPort(out port))
                    throw new Exception("Server IP or Server Port not found");

                _remoteService.ConnectToServer(_serverSocket, ip, port);
                bool isSucceed = _autoReset.WaitOne(10 * 1000);

                if (!isSucceed)
                {
                    UpdateStatus(LoginStatus.Disconnected);

                    if(OnError != null) 
                        OnError.Invoke(this, new MainErrorEventArgs(new Exception("Không thể kết nối đến máy chủ")));
                }
                _remoteService.ServerSocketLogin(_serverSocket);
            }
            catch (Exception ex)
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
            if (handler != null && st != LoginStatus.None)
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
            catch (Exception ex)
            {
                var handler = OnError;
                if (handler != null) handler.Invoke(this, new MainErrorEventArgs(ex));
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
            catch (Exception ex)
            {
                var handler = OnError;
                if (handler != null) handler.Invoke(this, new MainErrorEventArgs(ex));
                return string.Empty;
            }
        }

        #region Events
        private void OnDataCallbackEventHandler(object sender, RemoteDesktopEventArgs e)
        {
            // This runs on socket/worker threads. An unhandled exception here would crash the
            // whole process, so isolate it and surface it through OnError instead.
            try
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
                case ResponseType.Disconnect:
                    UpdateStatus(LoginStatus.Disconnected);
                    break;
                case ResponseType.P2PFailed:
                    RemoteControlConnectFailed();
                    break;
                default:
                    break;
            }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "MainPresenter").Error(ex, "OnDataCallbackEventHandler error");
                var handler = OnError;
                if (handler != null)
                    handler.Invoke(this, new MainErrorEventArgs(ex));
            }
        }
        private void RemoteControlConnectFailed()
        {
            StopP2PConnectStatus();
            if (OnError != null)
                OnError.Invoke(this, new MainErrorEventArgs(new UnauthorizedAccessException("Thiết lập kết nối vói máy khách thất bại")));
        }
        private void NewRemoteConnectionCallback(object sender, ResponseType type)
        {
            StopP2PConnectStatus();
            var clientSession = sender as ClientSession;
            if (clientSession != null)
            {
                if (OnData != null)
                {
                    var isController = (type == ResponseType.AddRemoteController) ? true : false;
                    OnData.Invoke(this, new MainDataEventArgs(new NewRemoteConnection(isController, clientSession)));
                }
            }
        }

        internal bool SetDefaultPassword(string password)
        {
             return _remoteService.SetDefaultPassword(password);
        }

        private void GetPartnerInfoFailedCallback()
        {
            StopP2PConnectStatus();

            if(OnData != null)
                OnData.Invoke(this, new MainDataEventArgs(new PartnerInfoResponse("Lấy thông tin máy khách thất bại")));

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

                if (_background != null)
                {
                    _background.Change(Timeout.Infinite, Timeout.Infinite);
                    _background.Dispose();
                }

                if (_autoReset != null)
                    _autoReset.Close();
            }
        }

        internal void SaveConnectInfo(ClientSession clientSession)
        {
            try
            {
                var connectionInfo = new ConnectHistoryInfo
                {
                    Id = clientSession.PartnerInfo.PartnerId,
                    Name = clientSession.PartnerInfo.ComputerName,
                    LastConnect = DateTimeOffset.UtcNow.ToString()
                };
                _remoteService.SaveConnectHistory(connectionInfo);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "MainPresenter").Error(ex, "SaveConnectInfo err");
            }
        }
    }
}
