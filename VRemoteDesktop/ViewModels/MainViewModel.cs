using System;
using System.ComponentModel;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.VTCPClient;
using static VRemoteDesktop.Utils.Logger;
using static VRemoteDesktop.Utils.RandomLength;

namespace VRemoteDesktop.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool _disposed = false;
        private bool _isLogged = false;
        private string _id;
        private string _myId;
        private string _myPassword;
        private string _errorMessage;
        private ConnectionStatus _connectStatus;
        private ManualResetEvent _resetEvent;
        private VClient _host;

        private readonly RemoteDesktopService _remoteDesktopService;
        public event EventHandler<RemoteDesktopEventArgs> ClientAcceptRequestRemote;
        public event EventHandler<EventArgs> SocketDisconnectEvent;
        public MainViewModel(RemoteDesktopService remoteDesktopService)
        {
            ConnectStatus = ConnectionStatus.None;
            _resetEvent = new ManualResetEvent(false);

            _remoteDesktopService = remoteDesktopService;
            _remoteDesktopService.RespondEvent += TCPClientManagerEventHandler;

            MyId = _remoteDesktopService.GetMe().Id;
            MyPassword = _remoteDesktopService.GetMe().Password;
            Init();
        }

        private void Init()
        {
            _id = StringHelper.RandomStringNumber(SOCKET_ID_LENGTH);
            _host =  _remoteDesktopService.NewClient(_id, VClientType.None, true);
        }
        #region Properties
        public string MyId
        {
            get { return _myId; }
            set
            {
                _myId = value;
                OnPropertyChanged(nameof(MyId));
            }
        }
        public string MyPassword
        {
            get { return _myPassword; }
            set
            {
                _myPassword = value;
                OnPropertyChanged(nameof(MyPassword));
            }
        }
        public ConnectionStatus ConnectStatus
        {
            get { return _connectStatus; }
            set
            {
                _connectStatus = value;
                OnPropertyChanged(nameof(ConnectStatus));
            }
        }
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set
            {
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }
        #endregion
        #region Methods
        public void Connect(VClient client = null)
        {
            string ip = AppSettingHelper.GetValue("ServerIP");// ?? "27.0.12.78";
            string port = AppSettingHelper.GetValue("LoginPort");// ?? "2399";

            if(string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
            {
                Log.ForContext("FileName", nameof(Connect)).Error("Error at Connect");
                return;
            }
            if(int.TryParse(port, out int validPort))
            {
                if(client == null)
                {
                    var client1 = _remoteDesktopService.GetClientById(_id);
                    client1.Connect(ip, validPort);
                }
                else
                {
                    client.Connect(ip, validPort);
                }
            }
        }
        public void Login()
        {
            _remoteDesktopService.Login(_id);
        }
        public void RequestP2PConnect(string id, string password)
        {
            try
            {
                if(string.Equals(id, MyId, StringComparison.CurrentCultureIgnoreCase))
                {
                    ErrorMessage = "Không thể kết nối với chính mình";
                    return;
                }
                //_remoteDesktopService.P2PConnect(id, password);
                _remoteDesktopService.P2PConnect(_host, id, password);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", nameof(RequestP2PConnect)).Error(ex, "Error at P2PConnect");
            }
        }
        #endregion
        #region Events
        private void PartnerRespond(object sender, RemoteDesktopEventArgs e)
        {
            _resetEvent.Set();
            ClientAcceptRequestRemote?.Invoke(sender, e);
        }
        private void TCPClientManagerEventHandler(object sender, RemoteDesktopEventArgs e)
        {
            if(sender  is VClient client)
            {
                switch (e.Type)
                {
                    case SocketDataType.Connect:
                        ConnectEventHandler(e.Flag);
                        break;
                    case SocketDataType.Login:
                        LoginEventHandler(e.Flag, e.Data);
                        break;
                    case SocketDataType.LoginFailed:
                        ConnectStatus = ConnectionStatus.Disconnected;
                        break;
                    case SocketDataType.Disconnect:
                        ConnectStatus = ConnectionStatus.Disconnected;
                        break;
                    case SocketDataType.Error:
                        _resetEvent.Set();
                        break;
                    case SocketDataType.RemoteControlRequestToConnect:
                    case SocketDataType.RemoteControlConnectFailed:
                    case SocketDataType.RemoteControlAcceptedRequestToConnect:
                    case SocketDataType.RemoteControlRefusedRequestToConnect:
                        PartnerRespond(sender, e);
                        break;
                    default:
                        break;
                }
            }
        }
        private void ConnectEventHandler(bool flag)
        {
            if (flag)
            {
                if (!_isLogged)
                {
                    _isLogged = true;
                    Login();
                }
                else
                {
                    _resetEvent.Set();
                }
            }
        }
        private void LoginEventHandler(bool flag, byte[] data)
        {
            if (flag)
            {
                ConnectStatus = ConnectionStatus.Connected;
                _remoteDesktopService.UpdateMyInfo(data);
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

                if(_remoteDesktopService != null)
                {
                    _remoteDesktopService.RespondEvent -= TCPClientManagerEventHandler;
                }
                _host?.Dispose();
                _resetEvent.Dispose();
            }
        }
    }
}
