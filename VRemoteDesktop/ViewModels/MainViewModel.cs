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
        private bool _isConnected;
        private ManualResetEvent _resetEvent;

        private readonly RemoteDesktopService _remoteDesktopService;
        public event EventHandler<P2PClientDataReceived> ClientAcceptRequestRemote;
        public MainViewModel(RemoteDesktopService remoteDesktopService)
        {
            IsConnected = false;
            _resetEvent = new ManualResetEvent(false);

            _remoteDesktopService = remoteDesktopService;
            _remoteDesktopService.DataReceivedEvent += TCPClientManagerEventHandler;

            MyId = _remoteDesktopService.GetMe().Id;
            MyPassword = _remoteDesktopService.GetMe().Password;
            Init();
        }
        private void Init()
        {
            _id = StringHelper.RandomStringNumber(SOCKET_ID_LENGTH);
            _remoteDesktopService.NewClient(_id, VClientType.None);
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
        public bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                _isConnected = value;
                OnPropertyChanged(nameof(IsConnected));
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
                _remoteDesktopService.P2PConnect(id, password);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", nameof(RequestP2PConnect)).Error(ex, "Error at P2PConnect");
            }
        }
        #endregion
        #region Events
        private void PartnerAcceptP2PConnect(object sender, P2PClientDataReceived e)
        {
            _resetEvent.Set();
            ClientAcceptRequestRemote?.Invoke(sender, e);
        }   
        private void TCPClientManagerEventHandler(object sender, P2PClientDataReceived e)
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
                        Console.WriteLine("LoginFailed");
                        break;
                    case SocketDataType.P2PRequestConnect:
                    case SocketDataType.P2PAcceptConnect:
                        PartnerAcceptP2PConnect(sender, e);
                        break;
                    case SocketDataType.Error:
                        _resetEvent.Set();
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
                IsConnected = true;
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
                    _remoteDesktopService.DataReceivedEvent -= TCPClientManagerEventHandler;

                _resetEvent.Dispose();
            }
        }
    }
}
