using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Authentication;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.Mouse;
using VRemoteDesktop.Services.RemoteDesktop;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Utils;
using VRemoteServer.Models;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly object _lock = new object();
        private bool _isLogged = false;
        private string _id;
        private string _partnerId;
        private string _partnerPassword;
        private string _myId;
        private string _myPassword;
        private bool _isConnected;

        private ManualResetEvent _resetEvent;

        private readonly RemoteDesktopService _remoteDesktopService;
        public event EventHandler<EventArgs> ClientAcceptRequestRemote;
        public MainViewModel(RemoteDesktopService remoteDesktopService)
        {
            IsConnected = false;
            _resetEvent = new ManualResetEvent(false);

            _remoteDesktopService = remoteDesktopService;
            _remoteDesktopService.DataReceivedEvent += TCPClientManagerEventHandler;

            MyId = _remoteDesktopService.GetMe().Id;
            MyPassword = remoteDesktopService.GetMe().Password;
            Init();
        }
        private void Init()
        {
            _id = StringHelper.RandomStringNumber(8);
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
            string ip = AppSettingHelper.Getvalue("RemoteServerIP");
            string port = AppSettingHelper.Getvalue("RemoteServerPort");

            if(string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
            {
                Log.ForContext("FileName", nameof(Connect)).Error("Error at Connect");
                return;
            }
            if(int.TryParse(port, out int validPort))
            {
                if(client == null)
                {
                    var clientx = _remoteDesktopService.GetClientById(_id);
                    clientx.Connect(ip, validPort);
                }
                else
                {
                    client.Connect(ip, validPort);
                }
            }
        }
        public void Login()
        {
            var client = _remoteDesktopService.GetClientById(_id);
            client.Login(_remoteDesktopService.GetMe().ToNetworkString());
        }
        public void RequestP2PConnect(string id, string password)
        {
            try
            {
                string connectionId = StringHelper.RandomStringNumber(8);
                var newConnection = _remoteDesktopService.NewClient(connectionId, VClientType.Sender);
                if (newConnection == null)
                {
                    MessageBox.Show("Không thể thiết lập kết nối", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                Connect(newConnection);
                newConnection.P2PConnect(id, password, _remoteDesktopService.GetMe().ToNetworkString());               
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
            ClientAcceptRequestRemote?.Invoke(sender, new EventArgs());
        }   
        private void TCPClientManagerEventHandler(object sender, P2PClientDataReceived e)
        {
            if(sender  is VClient client)
            {
                switch (e.Type)
                {
                    case DataType.Connect:
                        ConnectEventHandler(e.Flag);
                        break;
                    case DataType.Login:
                        LoginEventHandler(e.Flag, e.Data);
                        break;
                    case DataType.LoginFailed:
                        Console.WriteLine("LoginFailed");
                        break;
                    case DataType.P2PAcceptConnect:
                        PartnerAcceptP2PConnect(sender, e);
                        break;
                    case DataType.Error:
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
    }
}
