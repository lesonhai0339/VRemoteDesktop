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

            Init();
        }

        private void Init()
        {
            try
            {
                var myInfo = _remoteDesktopService.GetMe();

                MyId = myInfo.Id;
                MyPassword = myInfo.Password;

                _id = StringHelper.RandomStringNumber(SOCKET_ID_LENGTH);
                _host = _remoteDesktopService.NewClient(_id, VClientType.None, true);
            }
            catch
            {
                ShowMessage("Khởi tạo thất bại, vui lòng đóng FormRemote và mở lại!");
            }
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
        public bool IsRemoteConnected(string id)
        {
            return _remoteDesktopService.CheckRemoteConnected(id);
        }
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
                    if(client1 == null)
                    {
                        ShowMessage("Không tồn tại client");
                        return;
                    }
                    client1.TryConnect(ip: ip, port: validPort);
                }
                else
                {
                    client.TryConnect(ip: ip, port: validPort);
                }
            }
        }
        public void Login()
        {
            _remoteDesktopService.Login(_id);
        }
        public void RequestP2PConnect(string id, string password, bool useTURNSERVER = false)
        {
            try
            {
                if(string.Equals(id, MyId, StringComparison.CurrentCultureIgnoreCase))
                {
                    ShowMessage("Không thể kết nối với chính mình");
                    return;
                }
                if (!useTURNSERVER)
                {
                    //try P2P first
                    _remoteDesktopService.P2PConnect(_host, id, password);
                }
                else
                {
                    //use TURN SERVER
                    if(!_remoteDesktopService.P2PConnect(id, password))
                    {
                        ShowMessage("Kết nối thất bại");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(RequestP2PConnect)).Error(ex, "Error at P2PConnect");
            }
        }
        private void ShowMessage(string message)
        {
            ErrorMessage = message;
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
            switch (e.Type)
            {
                case SocketDataType.Connect:
                    ConnectEventHandler(e.Flag);
                    break;
                case SocketDataType.Login:
                case SocketDataType.LoginFailed:
                case SocketDataType.Disconnect:
                    LoginEventHandler(e.Flag, e.Data);
                    break;
                case SocketDataType.P2PInvalidConnectData:
                    ShowMessage("Dữ liệu kết nối không hợp lệ");
                    break;
                default:
                    PartnerRespond(sender, e);
                    break;
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
            }
            else
            {
                ConnectStatus = ConnectionStatus.Disconnected;
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
