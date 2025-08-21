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
            VClient client = new VClient(_id, Enums.VClientType.Sender);
            _remoteDesktopService.AddClient(_id, client);
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
                _resetEvent.Reset();

                string connectionId = StringHelper.RandomStringNumber(8);
                var newConnection = NewConnect(connectionId, VClientType.Sender);

                _resetEvent.Reset();
                newConnection.P2PConnect(id, password, _remoteDesktopService.GetMe().ToNetworkString());
                bool flag = _resetEvent.WaitOne(5000);
                if (flag)
                {
                    newConnection.P2PInitConnection(id, password, _remoteDesktopService.GetMe().ToNetworkString());
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
        private VClient NewConnect(string id, VClientType type)
        {
            _resetEvent.Reset();
            var client = _remoteDesktopService.NewClient(id, type);
            Connect(client);
            bool flag = _resetEvent.WaitOne(5000);
            if (flag)
            {
                return client;
            }
            else
            {
                _remoteDesktopService.RemoveClientById(id);
                return null;
            }

        }
        #endregion
        #region Events
        private void PartnerAcceptP2PConnect(object sender, P2PClientDataReceived e)
        {
            _resetEvent.Set();
            ClientAcceptRequestRemote?.Invoke(sender, new EventArgs());
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
            _remoteDesktopService.StartScreenCapture();
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
                    case DataType.P2PDataSend:
                        P2PDataSendEventHandler(sender, e);
                        break;
                    case DataType.Mouse:
                        MouseReceivedEventHandler(sender, e);
                        break;
                    case DataType.Keyboard:
                        KeyboardReceivedEventHandler(sender, e);
                        break;
                    case DataType.P2PDisconnect:
                        ProcessP2PDisconnect(sender, e);
                        break;
                    case DataType.Error:
                        _resetEvent.Set();
                        break;
                    default:
                        break;
                }
            }
        }
        private void ProcessP2PDisconnect(object sender, P2PClientDataReceived e)
        {
            if(sender is VClient client)
            {
                _remoteDesktopService.RemoveClientById(client.SocketId);
            }
        }
        private void KeyboardReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            try
            {
                var keyEvent = VirtualKeyboard.BytesToCustomKeyboardEvent(e.Data);
                VirtualKeyboard.ProcessKeyboardReceived(keyEvent.Key, keyEvent.Type);

            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(MouseReceivedEventHandler)).Error(ex, "Error processing keyboard data");
            }
        }
        private void MouseReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            try
            {
                var me = _remoteDesktopService.GetMe();
                var mouseEvent = VirtualMouse.BytesToCustomMouseEvent(e.Data, me.Width, me.Height);

                bool flag = VirtualMouse.MouseEvent(mouseEvent);
                if (!flag)
                {
                    Log.ForContext("FileName", nameof(MouseReceivedEventHandler)).Error("Mouse event failed");
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", nameof(MouseReceivedEventHandler)).Error(ex, "Error processing mouse data");
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
