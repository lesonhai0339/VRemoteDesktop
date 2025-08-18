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
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Authentication;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.TCPClient;
using VRemoteDesktop.Services.VTCPClientManager;
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
        private ClientInfo _myInfo;
        private VTCPClientManagerService _vtcpClientManagerService;
        private Authentication _authentication;
        private ConnectionManager _connectionManager;
        private ConcurrentDictionary<string, RemoteViewModel> _remoteViewModel;
        private ConcurrentBag<string> _connector;
        private IScreenCaptureServiceListener _globakScreenHook;

        public Action<ClientInfo> ClientAcceptRequestRemote;
        public MainViewModel(VTCPClientManagerService vtcpClientManagerService, Authentication authentication, ConnectionManager connectionManager)
        {
            VTCPClientManagerService = vtcpClientManagerService;
            Authentication = authentication;
            ConnectionManager = connectionManager;
            _myInfo = ConnectionManager.Me;
            MyId = _myInfo.Id;
            MyPassword = _myInfo.Password;
            IsConnected = false;
            _resetEvent = new ManualResetEvent(false);
            _remoteViewModel = new ConcurrentDictionary<string, RemoteViewModel>();
            _connector = new ConcurrentBag<string>();
            Init();
            Task.Factory.StartNew(() =>
            {
                ScreenHook = new ScreenCaptureServiceListener(null, null);
            }, TaskCreationOptions.LongRunning);
        }
        private void Init()
        {
            _id = Helpers.StringHelper.RandomStringNumber(8);
            TCPClient client = new TCPClient(_id);
            VTCPClientManagerService.Add(_id, client);
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
        public VTCPClientManagerService VTCPClientManagerService
        {
            get
            {
                lock (_lock)
                {
                    return _vtcpClientManagerService;
                }
            }
            set
            {
                lock (_lock)
                {
                    if (_vtcpClientManagerService != null)
                    {
                        _vtcpClientManagerService.TCPClientReceivedEvent -= TCPClientManagerEventHandler;
                    }
                    _vtcpClientManagerService = value;
                    if (_vtcpClientManagerService != null)
                    {
                        _vtcpClientManagerService.TCPClientReceivedEvent += TCPClientManagerEventHandler;
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
        public void Connect(TCPClient client= null)
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
                    var clientx = VTCPClientManagerService.GetByKey(_id);
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
            byte[] encoder = Helpers.ByteArrayHelper.ConvertStringToByteArray(_myInfo.ToNetworkString(), Enums.EncodingType.ASCII).GetResult();
            var client = VTCPClientManagerService.GetByKey(_id);
            client.Send(DataType.Login, encoder);
        }
        public void RequestP2PConnect(string id, string password)
        {
            try
            {
                string clientId = Helpers.StringHelper.RandomStringNumber(8);
                var client = InitNewconnection(clientId);
                string data = Helpers.StringHelper.StringBuilderWithSeparator("|",id, clientId);
                byte[] dataBytes = Helpers.ByteArrayHelper.ConvertStringToByteArray(data, Enums.EncodingType.ASCII).GetResult();

                _resetEvent.Reset();
                client.Send(DataType.P2PRequestConnect, dataBytes, id, true);
                bool flag = _resetEvent.WaitOne(5000);
                if (flag)
                {
                    string a = Helpers.StringHelper.StringBuilderWithSeparator("|", id, clientId, _myInfo.ToNetworkString());
                    byte[] b = Helpers.ByteArrayHelper.ConvertStringToByteArray(a, Enums.EncodingType.ASCII).GetResult();
                    client.Send(DataType.P2PDataSend, b, id, true);
                }
                else
                {

                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", nameof(RequestP2PConnect)).Error(ex, "Error at P2PConnect");
            }
        }
        private TCPClient InitNewconnection(string id)
        {
            _resetEvent.Reset();
            TCPClient client = new TCPClient(id);
            VTCPClientManagerService.Add(id, client);
            Connect(client);
            bool flag = _resetEvent.WaitOne(5000);
            if (flag)
            {
                return client;
            }
            else
            {
                VTCPClientManagerService.Remove(id);
                return null;
            }

        }
        #endregion
        #region Events
        private void P2PRequestConnectEventHandler(object sender, P2PClientDataReceived e)
        {
            var id = Encoding.ASCII.GetString(e.Data);
            var client = InitNewconnection(id);
            client.Send(DataType.P2PAcceptConnect, e.Data);
        }
        private void PartnerAcceptP2PConnect(object sender, P2PClientDataReceived e)
        {
            _resetEvent.Set();
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
            string id = Helpers.ByteArrayHelper.ConvertByteArrayToString(e.Data, 8, 8, Enums.EncodingType.ASCII).GetResult();
            byte[] data = new byte[e.Data.Length - 16];
            Buffer.BlockCopy(e.Data, 16, data, 0, e.Data.Length - 16);
            if(_remoteViewModel.TryGetValue(id, out var a))
            {
                a.DataReceived(e.Type, data);
            }
        }
        private void ScreenHookEventHandler(object sender, ScreenEvent e)
        {
            foreach(var connector in _connector)
            {
                Screen(connector, e);
            }
        }
        private void Screen(string partnerId, ScreenEvent e)
        {
            try
            {
                if (e.Data.Count == 0 || e.TotalSize == 0)
                {
                    Log.ForContext("FileName", GetType().Name).Error("Screen missing some value");
                    return;
                }
                byte[] screenHeader = new byte[21];
                Buffer.BlockCopy(BitConverter.GetBytes(e.TotalSize + 21), 0, screenHeader, 0, 4);
                screenHeader[4] = (byte)e.Type;
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(partnerId), 0, screenHeader, 5, 8);
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(MyId), 0, screenHeader, 13, 8);

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
                //TCPClient.AddWorkGroup(tasks);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", GetType().Name).Error(ex, "ScreenHookEventHandler error");
            }
        }
        private void TCPClientManagerEventHandler(object sender, P2PClientDataReceived e)
        {
            if(sender  is TCPClient client)
            {
                Console.WriteLine(client.SocketId);
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
                    case DataType.P2PRequestConnect:
                        Console.WriteLine("Request connect");
                        P2PRequestConnectEventHandler(sender, e);
                        break;
                    case DataType.P2PAcceptConnect:
                        PartnerAcceptP2PConnect(sender, e);
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
                ConnectionManager.UpdateMyInfo(data);
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
