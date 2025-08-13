using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Services.TCPClient;

namespace VRemoteDesktop.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly object _lock = new object();
        private TCPClient _tcpClient;
        private string _partnerId;
        private string _partnerPassword;
        private string _myId;
        private string _myPassword;
        private bool _isConnected;
       
        public MainViewModel()
        {
            Client = new TCPClient();
            MyId = Services.ConnectionManager.ConnectionManager.Me.Id;
            MyPassword = Services.ConnectionManager.ConnectionManager.Me.Password;
            IsConnected = false;
        }

        #region Properties
        public TCPClient Client
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
                    }
                    _tcpClient = value;
                    if (_tcpClient != null)
                    {
                        _tcpClient.ConnectEvent += ConnectEventHandler;
                    }
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
            _tcpClient.Connect();
        }
        #endregion
        #region Events
        private void ConnectEventHandler()
        {
            IsConnected = true;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
