using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.Client;
using VRemoteDesktop.Services.Keyboard;
using VRemoteDesktop.Services.Machine.DTOs;
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.SessionManagement;
using VRemoteDesktop.Services.SessionManagement.Enums;
using VRemoteDesktop.Services.SessionManagement.Events.ClientSession;
using VRemoteDesktop.Utils;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public partial class RemoteService : IDisposable
    {
        private readonly object _lock = new object();
        private const int SESSION_ID_LENGTH = 8;
        private const string SEPARATOR = "|";
        private const string SUCCESS = "1";
        private const string FAILED = "0";
        private readonly string DEFAULT_SERVER_IP = AppSettingHelper.GetValue("ServerIP");
        private readonly string DEFAULT_LOGIN_PORT = AppSettingHelper.GetValue("LoginPort");
        private readonly string DEFAULT_REMOTE_PORT = AppSettingHelper.GetValue("RemotePort");
        private volatile bool _disposed;
        private bool _isCapturing = false;

#if DEBUG
        private readonly IVScreenSender _screenSender;
        private readonly IKeyboardService _keyboardService;

#endif
        private readonly IMachineProfile _machineProfile;
        private readonly SessionManager _sessionManager;
        private ManualResetEvent _reset;

        private Dictionary<SocketDataType, Action<object, EventArgs>> _eventHandlers;


        public event EventHandler<KeyboardEventArgs> OnSessionKeyboard;
        public event EventHandler<RemoteDesktopEventArgs> OnSessionData;
        public RemoteService(IVScreenSender screenSender, SessionManager sessionManager, IMachineProfile machineProfile, IKeyboardService keyboardService)
        {
            _disposed = false;
            _machineProfile = machineProfile;
            _reset = new ManualResetEvent(false);

            _keyboardService = keyboardService;
            _sessionManager = sessionManager;

            _screenSender = screenSender;
            _screenSender.OnFrame += OnRegionEventHandler;


            _keyboardService.KeyPressed += KeyPressedEventHandler;
            _sessionManager.SessionDataReceived += ClientSessionDataReceivedEventHandler;
            _sessionManager.SessionClosed += ClientSessionClosedEventHandler;
            StartKeyboardListener();
        }
        

        #region Properties
        public bool Disposed => _disposed;
        #endregion
        #region Methods
        
        public bool GetServerIP(out string serverIp)
        {            
            //Implement after. Now return default server ip
            serverIp = DEFAULT_SERVER_IP;
            return true;
        }
        public bool GetServerPort(out int serverPort)
        {
            //Implement after. Now return default server ip
            serverPort = int.Parse(DEFAULT_LOGIN_PORT);
            return true;
        }
        ////Relay connect
        //public bool P2PConnect(string partnerId, string partnerPassword)
        //{
        //    if (string.IsNullOrWhiteSpace(partnerId) || string.IsNullOrWhiteSpace(partnerPassword)) return false;

        //    string connectionId = StringHelper.RandomStringNumber(8);
        //    var newConnection = NewClient(connectionId, VClientType.Sender, false);

        //    if (newConnection == null)
        //    {
        //        return false;
        //    }

        //    if (newConnection.TryConnect(ip: DEFAULT_SERVER_IP, port: int.Parse(DEFAULT_SERVER_PORT)))
        //    {
        //        string dataString = StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPARATOR, newConnection.SessionId, partnerId, partnerPassword, GetMe().ToNetworkString());
        //        byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(dataString, EncodingType.ASCII).GetResult();
        //        newConnection.Send(SocketDataType.RemoteControlRequestToConnect, dataBytes, newConnection.SessionId, true);
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}
        ////P2P connect
        //public void P2PConnect(ClientSession session, string partnerId, string partnerPassword)
        //{
        //    if (string.IsNullOrWhiteSpace(partnerId) || string.IsNullOrWhiteSpace(partnerPassword)) return;

        //    P2PConnectInfo connectInfo = new P2PConnectInfo(partnerId, partnerPassword);

        //    var result = ByteArrayHelper.ConvertStringToByteArray(connectInfo.ToNetworkString(), EncodingType.ASCII);
        //    if (result.IsSuccess)
        //    {
        //        session.Send(SocketDataType.P2PConnect, result.Data, session.SessionId, true);
        //    }
        //    else
        //    {
        //        RespondEvent?.Invoke(session, new RemoteDesktopEventArgs(SocketDataType.P2PInvalidConnectData, false, new byte[0]));
        //    }
        //}


        #endregion
        #region Events
      

        private void ClientSessionDataReceivedEventHandler(object sender, ClientSessionDataReceivedEventArgs e)
        {
            try
            {
                switch (e.Type)
                {
                    case SocketDataType.Connect:
                        ConnectCallback(sender, e);
                        break;
                    case SocketDataType.LoginResponse:
                        LoginEventHandler(sender, e);
                        break;
                    case SocketDataType.RequestRemoteConnect:
                        CreateRemoteConnection(e.Data);
                        break;
                    case SocketDataType.GetPartnerInfoSuccess:
                        GetPartnerInfoSuccessCallback(e.Data);
                        break;
                    case SocketDataType.GetPartnerInfoFailed:
                        GetPartnerInfoFailedCallback(e.Data);
                        break;
                    default:
                        break;
                }

            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, string.Format("Error handling {0}: {1}", e.Type, ex.Message));
            }
        }

    



        /*   #endregion
           #region Handlers
           private void ForwardEvent(object sender, EventArgs e)
           {
               RespondEvent?.Invoke(sender, (RemoteDesktopEventArgs)e);
           }


           private void P2PConnect(object sender, EventArgs e)
           {
               var ev = e as RemoteDesktopEventArgs;

               if (ev == null)
                   throw new InvalidOperationException("Invalid event args for P2PConnect");

               string id = Encoding.ASCII.GetString(ev.Data);

               //Create a new VClient and listen for incoming connection, if success return VClient else remove VClient and return null
               var remoteClient = _sessionManager.AddNewAndListen(id, VClientType.Receiver, false);

               //P2P handshake success, send accept connect
               if (remoteClient != null)
               {
                   remoteClient.Send(SocketDataType.P2PAcceptConnect, new byte[0], id, true);
               }
               else
               {
                   throw new Exception("Cannot create new VClient or P2PListen failed for P2PConnect");
               }
           }
           private void P2PConnectDataRespond(object sender, EventArgs e)
           {
               var session = sender as ClientSession;
               if (session == null)
                   throw new InvalidOperationException("Invalid sender for P2PConnectDataRespond");

               var ev = e as RemoteDesktopEventArgs;
               if (ev == null)
                   throw new InvalidOperationException("Invalid event args for P2PConnectDataRespond");

               string data = ByteArrayHelper.ConvertByteArrayToString(ev.Data, EncodingType.ASCII).GetResult();
               string[] dataParsed = StringHelper.StringToStringArrayWithSeparator(data, DefaultValue.DEFAULT_SEPARATOR);

               P2PNetworkInfo networkInfo = new P2PNetworkInfo();
               if (networkInfo.TryParseData(dataParsed))
               {
                   string connectIp = _clientInfo.IsTheSameNetWork(networkInfo.PublicIP) ? networkInfo.LocalIP : networkInfo.PublicIP;

                   var remoteControlClient = _sessionManager.New(networkInfo.Id, VClientType.Sender, false);
                   if (int.TryParse(networkInfo.Port, out int port))
                   {
                       bool respond = remoteControlClient.TryConnect(ip: connectIp, port: port, retry: 3, timeout: 1000);

                       //P2P connect failed
                       if (!respond)
                           RespondEvent?.Invoke(session, new RemoteDesktopEventArgs(SocketDataType.P2PLoginRespond, false, new byte[0]));
                   }
                   else
                   {
                       Logger.Log.ForContext("FileName", GetType().Name).Error("P2PConnectDataRespond: Invalid Port");
                   }
               }
               else
               {
                   Logger.Log.ForContext("FileName", GetType().Name).Error("P2PConnectDataRespond: Invalid P2PNetworkInfo data");
               }
           }
           private void P2PRequestToConnectAccepted(object sender, EventArgs e)
           {
               var session = sender as ClientSession;
               if (session == null)
                   throw new InvalidOperationException("Invalid sender for P2PRequestToConnectAccepted");

               string dataString = StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPARATOR, session.SessionId, GetMe().ToNetworkString());
               byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(dataString, EncodingType.ASCII).GetResult();

               session.Send(SocketDataType.P2PLogin, dataBytes, session.SessionId, true);
           }
           private void P2PLogin(object sender, EventArgs e)
           {
               var session = sender as ClientSession;
               if (session == null)
                   throw new InvalidOperationException("Invalid sender for P2PLogin");

               var ev = e as RemoteDesktopEventArgs;
               if (ev == null)
                   throw new InvalidOperationException("Invalid eventArgs for P2PLogin");

               string rawData = ByteArrayHelper.ConvertByteArrayToString(ev.Data, EncodingType.ASCII).GetResult();
               string[] dataArray = StringHelper.StringToStringArrayWithSeparator(rawData, DefaultValue.DEFAULT_SEPARATOR);

               if (dataArray.Length == DefaultClientInfo.CLIENT_INFO_MIN_FIELDS + 1) //+1 for connection id
               {
                   ClientInfo partnerInfo = new ClientInfo();
                   if (partnerInfo.TryParseData(dataArray.Skip(1).ToArray())) //Skip connection id
                   {
                       session.UpdatePartnerInfo(partnerInfo);
                       _clientInfo.AddPartner(partnerInfo);

                       byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(GetMe().ToNetworkString(), EncodingType.ASCII).GetResult();
                       session.Send(SocketDataType.P2PLoginRespond, dataBytes, session.SessionId, true);
                       return;
                   }
               }
               session.Send(SocketDataType.P2PLoginRespond, new byte[0], session.SessionId, true);
               session.UpdatePartnerInfo(null);
           }
           private void P2PLoginRespond(object sender, EventArgs e)
           {

               var session = sender as ClientSession;
               if (session == null)
                   throw new InvalidOperationException("Invalid sender for P2PLoginRespond");

               var ev = e as RemoteDesktopEventArgs;
               if (ev == null)
                   throw new InvalidOperationException("Invalid eventArgs for P2PLoginRespond");

               string rawData = ByteArrayHelper.ConvertByteArrayToString(ev.Data, EncodingType.ASCII).GetResult();
               string[] dataArray = StringHelper.StringToStringArrayWithSeparator(rawData, DefaultValue.DEFAULT_SEPARATOR);

               if (dataArray.Length == DefaultClientInfo.CLIENT_INFO_MIN_FIELDS)
               {
                   ClientInfo partnerInfo = new ClientInfo();
                   if (partnerInfo.TryParseData(dataArray))
                   {
                       session.UpdatePartnerInfo(partnerInfo);
                       _clientInfo.AddPartner(partnerInfo);

                       session.Send(SocketDataType.Ready, new byte[0], session.SessionId, true);

                       RespondEvent?.Invoke(session, ev);
                       return;
                   }
               }
           }
           private void RelayConnect(object sender, EventArgs e)
           {
               var ev = e as RemoteDesktopEventArgs;
               if (ev == null)
                   throw new InvalidOperationException("Invalid eventArgs for RelayConnect");

               if (_clientInfo.IsAuthenticated(ev.Data, out ClientInfo partnerInfo, out string connectionId))
               {
                   var remoteControlClient = _sessionManager.New(connectionId, VClientType.Receiver, false);

                   //Init screen capture
                   _screenSender.InitializeSenderComponents();
                   StartScreenCapture();


                   remoteControlClient.TryConnect(DEFAULT_SERVER_IP, int.Parse(DEFAULT_SERVER_PORT));
                   remoteControlClient.UpdatePartnerInfo(partnerInfo);

                   byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(GetMe().ToNetworkString(), EncodingType.ASCII).GetResult();

                   remoteControlClient.Send(SocketDataType.RemoteControlAcceptedRequestToConnect, dataBytes, remoteControlClient.SessionId, true);

                   RespondEvent?.Invoke(remoteControlClient, ev);
               }
               else
               {
                   var clientSession = sender as ClientSession;
                   if (clientSession != null)
                   {
                       string id = ByteArrayHelper.ConvertByteArrayToString(ev.Data, 0, RandomLength.SOCKET_ID_LENGTH, EncodingType.ASCII).GetResult();
                       byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(id, EncodingType.ASCII).GetResult();

                       clientSession.Send(SocketDataType.RemoteControlRefusedRequestToConnect, dataBytes, clientSession.SessionId, true);
                   }
               }
           }
           private void RelayRequestToConnectAccepted(object sender, EventArgs e)
           {
               var session = sender as ClientSession;
               if (session == null)
                   throw new InvalidOperationException("Invalid sender for RelayRequestToConnectAccepted");

               var ev = e as RemoteDesktopEventArgs;
               if (ev == null)
                   throw new InvalidOperationException("Invalid eventArgs for RelayRequestToConnectAccepted");

               ClientInfo partnerInfo = null;

               string data = ByteArrayHelper.ConvertByteArrayToString(ev.Data, EncodingType.ASCII).GetResult();
               string[] stringArray = StringHelper.StringToStringArrayWithSeparator(data, DefaultValue.DEFAULT_SEPARATOR);

               if (stringArray.Length == DefaultClientInfo.CLIENT_INFO_MIN_FIELDS)
               {
                   partnerInfo = new ClientInfo();
                   if (partnerInfo.TryParseData(stringArray))
                   {
                       session.UpdatePartnerInfo(partnerInfo);

                       session.Send(SocketDataType.Ready, new byte[0], session.SessionId, true);
                   }
               }
               else
               {
                   //When partnerInfo is null this method will call dispose method
                   session.UpdatePartnerInfo(partnerInfo);
               }
               ForwardEvent(sender, e);
           }
           private void ReadyToRemote(object sender, EventArgs e)
           {
               var session = sender as ClientSession;
               if (session == null)
                   throw new InvalidOperationException("Invalid sender for ReadyToRemote");

               session.Connected = true;
               if (session == null)
                   throw new InvalidOperationException("Invalid sender for ReadyToRemote");

               RespondEvent?.Invoke(session, ((RemoteDesktopEventArgs)e));


   #if DEBUG
               _screenSender.GetFullScreen(session.Image);
               return;
   #endif

               ////Send first screen
               //List<byte[]> screen;
               //lock (_lock)
               //{
               //    screen = _globalHook.GetFirstScreen();
               //}
               //int length = screen.Sum(x => x.Length);

               //var header = client.HeaderGenerate(type: SocketDataType.ScreenSend, socketId: client.SocketId, dataSize: length);
               //client.Send(SocketDataType.ScreenSend, header, client.SocketId, false);

               //for (int i = 0; i < screen.Count; i++)
               //{
               //    client.Send(SocketDataType.ScreenSend, screen[i], client.SocketId, false);
               //}
               ////SendScreen(client, SocketDataType.ScreenSend, screen, length);
           }
           /// <summary>
           /// Error, Note
           /// </summary>
           /// <param name="sender"></param>
           /// <param name="e"></param>
           /// <exception cref="InvalidOperationException"></exception>
           private void ClientDisconnected(object sender, EventArgs e)
           {
               var clientSession = sender as ClientSession;
               if (clientSession == null)
                   throw new InvalidOperationException("Invalid sender for ClientDisconnected");

               var ev = e as RemoteDesktopEventArgs;
               if (ev == null)
                   throw new InvalidOperationException("Invalid eventArgs for ClientDisconnected");


               bool flag = _clientInfo.RemovePartner(clientSession.PartnerInfo.Id);

               RemoveClientById(clientSession.SessionId);

               RespondEvent?.Invoke(sender, new RemoteDesktopEventArgs(ev.Type, false, ev.Data));
           }
          */
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

                StopKeyboardListener();
                if (_keyboardService != null)
                {
                    _keyboardService.KeyPressed -= KeyPressedEventHandler;
                }
                if (_sessionManager != null)
                {
                    _sessionManager.SessionDataReceived -= ClientSessionDataReceivedEventHandler;
                    _sessionManager.SessionClosed -= ClientSessionClosedEventHandler;
                }

                _keyboardService?.Dispose();
                _sessionManager?.Dispose();
                _reset?.Dispose();
                _disposed = true;
            }
        }
    }
}
