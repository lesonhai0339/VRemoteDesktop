using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Authentication;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.ScreenCapture;
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
        private ConcurrentDictionary<string, RemoteViewModel> _remoteViewModel;
        private ConcurrentBag<string> _connector;
        private IScreenCaptureServiceListener _globakScreenHook;

        public Action<ClientInfo> ClientAcceptRequestRemote;
        public MainViewModel(TCPClient tcpClient, Authentication authentication, ConnectionManager connectionManager)
        {
            TCPClient = tcpClient;
            Authentication = authentication;
            ConnectionManager = connectionManager;
            _myInfo = ConnectionManager.Me;
            MyId = _myInfo.Id;
            MyPassword = _myInfo.Password;
            IsConnected = false;
            _remoteViewModel = new ConcurrentDictionary<string, RemoteViewModel>();
            _connector = new ConcurrentBag<string>();
            Task.Factory.StartNew(() =>
            {
                ScreenHook = new ScreenCaptureServiceListener(null, null);
            }, TaskCreationOptions.LongRunning);
        }

        #region Properties
        public IScreenCaptureServiceListener ScreenHook
        {
            get
            {
                lock (_lock)
                {
                    return _globakScreenHook;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_globakScreenHook != null)
                    {
                        _globakScreenHook.ScreenEvent -= ScreenHookEventHandler;
                    }
                    _globakScreenHook = value;
                    if (_globakScreenHook != null)
                    {
                        _globakScreenHook.ScreenEvent += ScreenHookEventHandler;
                    }
                }
            }
        }
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
                        _tcpClient.Connected -= ConnectEventHandler;
                        _tcpClient.LoggedIn -= LoginEventHandler;
                        _tcpClient.P2PrequestConnect -= P2PRequestConnectEventHandler;
                        _tcpClient.P2PAcceptConnect -= P2PAcceptConnectEventHandler;
                        _tcpClient.ScreenReceived -= ScreenReceivedEventHandler;
                    }
                    _tcpClient = value;
                    if (_tcpClient != null)
                    {
                        _tcpClient.Connected += ConnectEventHandler;
                        _tcpClient.LoggedIn += LoginEventHandler;
                        _tcpClient.P2PrequestConnect += P2PRequestConnectEventHandler;
                        _tcpClient.P2PAcceptConnect += P2PAcceptConnectEventHandler;
                        _tcpClient.ScreenReceived += ScreenReceivedEventHandler;

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
        public void AddRemoteForm(string id, RemoteViewModel remoteViewModel)
        {
            _remoteViewModel.TryAdd(id, remoteViewModel);
        }
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
        private void P2PRequestConnectEventHandler(object sender, P2PRequestConnectEventArgs e)
        {
            var result =  Authentication.P2PAuthentication(e.Data, _myInfo);
            if (!result.IsLogged)
                return;

            string me = _myInfo.ToNetworkString();
            byte[] data = Helpers.ByteArrayHelper.ConvertStringToByteArray(me, Enums.EncodingType.ASCII).GetResult();

            _connector.Add(result.ConnectorInfo.Id);
            ScreenHook.StartCapture();
            TCPClient.Send(DataType.P2PAcceptConnect, data, result.ConnectorInfo.Id);
                //Todo: logging failed
            //Todo: add connector to dictionary, start send screen
        }


        private void P2PAcceptConnectEventHandler(object sender, P2PAcceptConnectEventArgs e)
        {
            string data = ByteArrayHelper.ConvertByteArrayToString(e.Data, 8 , e.Data.Length - 8, Enums.EncodingType.ASCII).GetResult();
            string[] stringArray = Helpers.StringHelper.StringToStringArrayWithSeparator(data, "|");
            ClientInfo connecter = new ClientInfo
            {
                Id = stringArray[0],
                Password = stringArray[1],
                ComputerName = stringArray[2],
                Width = int.Parse(stringArray[3]),
                Height = int.Parse(stringArray[4]),
                MajorVersion = stringArray[5],
                MinorVersion = stringArray[6],
                Ip = stringArray[7],
                Port = stringArray[8],
                PublicIP = stringArray[9],
            };
            ClientAcceptRequestRemote?.Invoke(connecter);
        }
        private void ScreenReceivedEventHandler(object sender, P2PScreenEventArgs e)
        {
            string id = Helpers.ByteArrayHelper.ConvertByteArrayToString(e.Data, 0, 8, Enums.EncodingType.ASCII).GetResult();
        }
        private void ScreenHookEventHandler(object sender, ScreenEvent e)
        {
            try
            {
                if (e.Data.Count == 0 || e.TotalSize == 0)
                {
                    Log.ForContext("FileName", GetType().Name).Error("Screen missing some value");
                    return;
                }
                byte[] screenHeader = new byte[13];
                Buffer.BlockCopy(BitConverter.GetBytes(e.TotalSize + 13), 0, screenHeader, 0, 4);
                screenHeader[4] = (byte)e.Type;
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(_connector.FirstOrDefault()), 0, screenHeader, 5, 8);

                List<TaskObject> tasks = new List<TaskObject>();
                tasks.Add(new TaskObject
                {
                    TaskType = e.Type,
                    Data = screenHeader,
                    IsSendHeader = false
                });

                //data
                for (int i = 0; i < e.Data.Count; i++)
                {
                    var task = new TaskObject
                    {
                        TaskType = e.Type,
                        Data = e.Data[i],
                        IsSendHeader = false
                    };

                    tasks.Add(task);
                }
                foreach(var task in tasks)
                {
                    TCPClient.Send(task.Data);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", GetType().Name).Error(ex, "ScreenHookEventHandler error");
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
