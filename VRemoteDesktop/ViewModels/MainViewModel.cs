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
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.Mouse;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;
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
        private GlobalHookService _globalHook;
        private VTCPClientManagerService _vtcpClientManagerService;
        private ConnectionManager _connectionManager;
        private ConcurrentDictionary<string, RemoteViewModel> _remoteViewModel;
        public event EventHandler<ClientConnectionEventArgs> ClientAcceptRequestRemote;
        public MainViewModel(GlobalHookService globalHook,VTCPClientManagerService vtcpClientManagerService, ConnectionManager connectionManager)
        {
            _globalHook = globalHook;
            VTCPClientManagerService = vtcpClientManagerService;
            _connectionManager = connectionManager;

            _myInfo = ConnectionManager.Me;
            MyId = _myInfo.Id;
            MyPassword = _myInfo.Password;
            IsConnected = false;
            _resetEvent = new ManualResetEvent(false);
            _remoteViewModel = new ConcurrentDictionary<string, RemoteViewModel>();
            Init();
            _globalHook.ScreenCaptureChanged += ScreenHookEventHandler;
        }
        private void Init()
        {
            _id = StringHelper.RandomStringNumber(8);
            VClient client = new VClient(_id);
            VTCPClientManagerService.Add(_id, client);
        }
        #region Properties
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
            var client = VTCPClientManagerService.GetByKey(_id);
            client.Login(_myInfo.ToNetworkString());
        }
        public void P2PHandshake(string id, string password)
        {
            try
            {
                _resetEvent.Reset();

                string connectionId = StringHelper.RandomStringNumber(8);
                var newConnection = NewConnect(connectionId);

                _resetEvent.Reset();
                newConnection.P2PHandshake(id);
                bool flag = _resetEvent.WaitOne(5000);
                if (flag)
                {
                    newConnection.P2PInitConnection(id, password, _myInfo.ToNetworkString());
                }
                else
                {

                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", nameof(P2PHandshake)).Error(ex, "Error at P2PConnect");
            }
        }
        private VClient NewConnect(string id)
        {
            _resetEvent.Reset();
            var client = VTCPClientManagerService.New(id);
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
            var client = NewConnect(id);
            byte[] encoder = Helpers.ByteArrayHelper.ConvertStringToByteArray(_myInfo.ToNetworkString(), Enums.EncodingType.ASCII).GetResult();
            client.Send(DataType.P2PAcceptConnect, encoder , id, true);
        }
        private void PartnerAcceptP2PConnect(object sender, P2PClientDataReceived e)
        {
            string data = ByteArrayHelper.ConvertByteArrayToString(e.Data, 13, e.Data.Length - 13, Enums.EncodingType.ASCII).GetResult();
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
            _resetEvent.Set();
            ClientAcceptRequestRemote?.Invoke(sender, new ClientConnectionEventArgs(connecter));
        }

        private void P2PDataSendEventHandler(object sender, P2PClientDataReceived e)
        {
            //string data = ByteArrayHelper.ConvertByteArrayToString(e.Data, 8, e.Data.Length - 8, Enums.EncodingType.ASCII).GetResult();
            //string[] stringArray = Helpers.StringHelper.StringToStringArrayWithSeparator(data, "|");
            //ClientInfo connecter = new ClientInfo
            //{
            //    Id = stringArray[0],
            //    Password = stringArray[1],
            //    ComputerName = stringArray[2],
            //    Width = int.Parse(stringArray[3]),
            //    Height = int.Parse(stringArray[4]),
            //    MajorVersion = stringArray[5],
            //    MinorVersion = stringArray[6],
            //    Ip = stringArray[7],
            //    Port = stringArray[8],
            //    PublicIP = stringArray[9],
            //};
            //ClientAcceptRequestRemote?.Invoke(connecter);
            _globalHook.StartScreenCapture();
        }
        private void ScreenReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            string id = Helpers.ByteArrayHelper.ConvertByteArrayToString(e.Data, 8, 8, Enums.EncodingType.ASCII).GetResult();
            byte[] data = new byte[e.Data.Length - 16];
            Buffer.BlockCopy(e.Data, 16, data, 0, e.Data.Length - 16);
            if(_remoteViewModel.TryGetValue(id, out var a))
            {
                a.DataReceived(e.Type, data);
            }
        }
        private void ScreenHookEventHandler(object sender, ScreenCaptureEventArgs e)
        {
            foreach(var connection in VTCPClientManagerService.Connections)
            {
                Screen(connection.Key, connection.Value, e);
            }
        }
        private void Screen(string connectionId, VClient client, ScreenCaptureEventArgs e)
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
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(connectionId), 0, screenHeader, 5, 8);
                Buffer.BlockCopy(Encoding.ASCII.GetBytes(_myInfo.Id), 0, screenHeader, 13, 8);


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
                client.AddWorkGroup(tasks, DataType.Screen);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", GetType().Name).Error(ex, "ScreenHookEventHandler error");
            }
        }
        private void TCPClientManagerEventHandler(object sender, P2PClientDataReceived e)
        {
            if(sender  is VClient client)
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
                    case DataType.P2PDataSend:
                        P2PDataSendEventHandler(sender, e);
                        break;
                    case DataType.Chunks:
                    case DataType.Screen:
                        ScreenReceivedEventHandler(sender, e);
                            break;
                    case DataType.Mouse:
                        MouseReceivedEventHandler(sender, e);
                        break;
                    case DataType.Keyboard:
                        KeyboardReceivedEventHandler(sender, e);
                        break;
                    case DataType.Clipboard:
                        ClipboardReceivedEventHandler(sender,e);
                        break;
                    default:
                        break;
                }

            }
        }
        private void ClipboardReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            var data = VirtualClipboard.DecodeClipboard(e.Data, 8, e.Data.Length - 8);
            _globalHook.SetClipboard(data);
        }
        private void KeyboardReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            try
            {
                int length = e.Data.Length - 8;
                byte[] keyboard = new byte[length];
                Buffer.BlockCopy(e.Data, 8, keyboard, 0, length);

                var keyEvent = VirtualKeyboard.BytesToCustomKeyboardEvent(keyboard);
                VirtualKeyboard.ProcessKeyboardReceived(keyEvent.Key, keyEvent.Type);

            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing keyboard data");
            }
        }
        private void MouseReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            try
            {
                byte[] mouse = new byte[e.Data.Length - 8];
                Buffer.BlockCopy(e.Data, 8, mouse, 0, e.Data.Length - 8);

                var mouseEvent = VirtualMouse.BytesToCustomMouseEvent(mouse, _myInfo.Width, _myInfo.Height);

                bool flag = VirtualMouse.MouseEvent(mouseEvent);
                if (!flag)
                {
                    Log.ForContext("FileName", "RemoteClient").Error("Mouse event failed");
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", "RemoteClient").Error(ex, "Error processing mouse data");
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
