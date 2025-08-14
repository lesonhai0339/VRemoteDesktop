using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Authentication;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.TCPClient;
using VRemoteDesktop.Utils;
using VRemoteServer.Models;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly object _lock = new object();
        private string _partnerId;
        private string _partnerPassword;
        private string _myId;
        private string _myPassword;
        private bool _isConnected;
        private ClientInfo _myInfo;
        private TCPClient _tcpClient;
        private Authentication _authentication;
        private ConnectionManager _connectionManager;
        public MainViewModel()
        {
            TCPClient = new TCPClient();
            Authentication = new Authentication(TCPClient);
            ConnectionManager = new ConnectionManager();
            _myInfo = ConnectionManager.Me;
            MyId = _myInfo.Id;
            MyPassword = _myInfo.Password;
            IsConnected = false;
        }

        #region Properties
        public TCPClient TCPClient
        {
            get
            {
                lock (_lock)
                {
                    return _tcpClient;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_tcpClient != null)
                    {
                        _tcpClient.ConnectEvent -= ConnectEventHandler;
                        _tcpClient.LoginEvent -= LoginEventHandler;
                        _tcpClient.P2PConnectEvent -= P2PConnectEventHandler;
                    }
                    _tcpClient = value;
                    if (_tcpClient != null)
                    {
                        _tcpClient.ConnectEvent += ConnectEventHandler;
                        _tcpClient.LoginEvent += LoginEventHandler;
                        _tcpClient.P2PConnectEvent += P2PConnectEventHandler;

                    }
                }
            }
        }


        public Authentication Authentication
        {
            get => _authentication;
            set => _authentication = value;
        }
        public ConnectionManager ConnectionManager
        {
            get
            {
                lock (_lock)
                {
                    return _connectionManager;
                }
            }
            set
            {
                lock (_lock)
                {
                    _connectionManager = value;
                }
            }
        }

        public string PartnerId
        {
            get { return _partnerId; }
            set
            {
                _partnerId = value;
                OnPropertyChanged(nameof(PartnerId));
            }
        }
        public string PartnerPassword
        {
            get { return _partnerPassword; }
            set
            {
                _partnerPassword = value;
                OnPropertyChanged(nameof(PartnerPassword));
            }
        }
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
        public void Connect()
        {
            string ip = AppSettingHelper.Getvalue("RemoteServerIP");
            string port = AppSettingHelper.Getvalue("RemoteServerPort");

            if(string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(port))
            {
                Log.ForContext("FileName", nameof(P2PConnect)).Error("Error at Connect");
                return;
            }
            if(int.TryParse(port, out int validPort))
            {
                Authentication.Connect(ip, validPort);
            }
        }
        public void Login()
        {
            Authentication.Login(_myInfo.ToNetworkString());
        }
        public void P2PConnect(string ip, string password)
        {
            try
            {
                Authentication.P2PConnect(ip, password, _myInfo);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", nameof(P2PConnect)).Error(ex, "Error at P2PConnect");
            }
        }
        #endregion
        #region Events
        private void ConnectEventHandler(object sender, ConnectEventArgs e)
        {
            if (e.IsConnected)
            {
                Login();
            }
        }
        private void LoginEventHandler(object sender, LoginEventArgs e)
        {
            IsConnected = e.IsSuccess;
            ConnectionManager.UpdateMyInfo(e.Data);
        }
        private void P2PConnectEventHandler(object sender, P2PConnectEventArgs e)
        {
            byte[] data = e.Data;
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
