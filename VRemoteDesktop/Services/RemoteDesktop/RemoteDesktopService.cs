using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;
using VRemoteDesktop.Helpers;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.ConnectionManager;
using VRemoteDesktop.Services.SystemService;
using VRemoteDesktop.Services.VTCPClient;
using VRemoteDesktop.Utils;
using VRemoteServer.Models;

namespace VRemoteDesktop.Services.RemoteDesktop
{
    public class RemoteDesktopService : IDisposable
    {
       private readonly object _lock = new object();
        private readonly string DEFAULT_SERVER_IP = AppSettingHelper.GetValue("ServerIP");
        private readonly string DEFAULT_SERVER_PORT = AppSettingHelper.GetValue("RemotePort");
        private volatile bool _disposed;
        private bool _isCapturting = false; 

        private readonly IClientInfoManager _clientInfo;
        private readonly GlobalHookService _globalHook;
        private readonly VClientManager _vClientManager;
        private ManualResetEvent _reset;

        public event EventHandler<KeyboardEventArgs> KeyboardEvent;
        public event EventHandler<RemoteDesktopEventArgs> RespondEvent;
        public RemoteDesktopService(GlobalHookService globalHook, VClientManager vClientManager, IClientInfoManager clientInfo)
        {
            _disposed = false;
            _clientInfo = clientInfo;
            _reset = new ManualResetEvent(false);

            _globalHook = globalHook;
            _vClientManager = vClientManager;

            _globalHook.ScreenCaptureChanged += ScreenCaptureEventHandler;
            _globalHook.KeyboardReceived += KeyboardEventHandler;
            _vClientManager.ClientDataReceived += EventReceived;
            _vClientManager.ClientClosed += VClientClosedEventHandler;
            StartKeyboardListener();

        }
        #region Properties
        public bool Disposed => _disposed;
        #endregion
        #region Methods
        private void VClientClosedEventHandler(object sender, EventArgs e)
        {
           if(sender is VClient client)
            {
                try
                {
                    _clientInfo.RemovePartner(client.Partner?.Id);
                    _vClientManager.Remove(client.SocketId);
                }
                catch (Exception ex)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "VClientClosedEventHandler error ");
                }
            }
        }
        public ClientInfo GetMe()
        {
            return _clientInfo.GetMyInfo();
        }
        public void UpdateMyInfo(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            _clientInfo.UpdateMyInfo(data);
        }
        public void StartKeyboardListener()
        {
            _globalHook.StartKeyboardListener();
        }
        public void StopKeyboardListener()
        {
            _globalHook.StopKeyboardListener();
        }
        public void AddKeyboardListenerOnFormByHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            _globalHook.AddKeyboardHook(handle);
        }
        public void RemoveKeyboardListenerOnFormByHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            _globalHook.RemoveKeyboardHook(handle);
        }
        public string GetClipboardString()
        {
            return _globalHook.GetClipboard(); ;
        }
        public bool SetClipboard(byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            return _globalHook.SetClipboard(data); ;
        }
        public bool SetClipboard(byte[] data, int index, int length)
        {
            if (data == null || data.Length == 0)
                return false;
            if (index < 0)
                return false;
            if (length < 0)
                return false;

            return _globalHook.SetClipboard(data, index, length); ;
        }
        public void StartScreenCapture()
        {
            _globalHook.StartScreenCapture();
        }
        public void StopScreenCapture()
        {
            _globalHook.StopScreenCapture();
        }
        public void RemoveClientById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _vClientManager.Remove(id);
            if (_vClientManager.Connections.Count == 0)
            {
                lock (_lock)
                {
                    StopScreenCapture();
                    _isCapturting = false;
                }
            }
            else
            {
                if (!_vClientManager.HasClientOfType(VClientType.Receiver))
                {
                    lock (_lock)
                    {
                        StopScreenCapture();
                        _isCapturting = false;
                    }
                }
            }
        }
        public VClient GetClientById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var client = _vClientManager.GetByKey(id);
            return client;
        }
        public VClient NewClient(string id, VClientType type, bool isHost)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var newClient = _vClientManager.New(id, type, isHost);
            if (_vClientManager.Connections.Count > 0)
            {
                if (_vClientManager.HasClientOfType(VClientType.Receiver))
                    StartScreenCapture();
            }
            return newClient;
        }
        public ConcurrentDictionary<string, VClient> GetClients()
        {
            return _vClientManager.Connections;
        }
        public void Login(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var client = _vClientManager.GetByKey(id);
            if (client != null)
            {
                byte[] encoder = ByteArrayHelper.ConvertStringToByteArray(GetMe().ToNetworkString(), Enums.EncodingType.ASCII).GetResult();
                client.Send(SocketDataType.Login, encoder, null);
            }
        }
        public bool CheckRemoteConnected(string id)
        {
            return _clientInfo.IsExistPartner(id);
        }
        #region TURN
        public bool P2PConnect(string partnerId, string partnerPassword)
        {
            if (string.IsNullOrWhiteSpace(partnerId) || string.IsNullOrWhiteSpace(partnerPassword)) return false;

            _reset.Reset();
            string connectionId = StringHelper.RandomStringNumber(8);
            var newConnection = NewClient(connectionId, VClientType.Sender, false);
            if (newConnection == null)
            {
                return false;
            }
            if(newConnection.TryConnect(ip: DEFAULT_SERVER_IP, port: int.Parse(DEFAULT_SERVER_PORT)))
            {
                string dataString = StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPARATOR, newConnection.SocketId, partnerId, partnerPassword, GetMe().ToNetworkString());
                byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(dataString, EncodingType.ASCII).GetResult();
                newConnection.Send(SocketDataType.RemoteControlRequestToConnect, dataBytes, newConnection.SocketId, true);
                return true;
            }
            else
            {
                return false;
            } 
        }
        private void TURNRequestConnectHandler(object sender, RemoteDesktopEventArgs e)
        {
            if (_clientInfo.IsAuthenticated(e.Data, out ClientInfo partnerInfo, out string connectionId))
            {
                var remoteControlClient = _vClientManager.New(connectionId, VClientType.Receiver, false);
                remoteControlClient.TryConnect(DEFAULT_SERVER_IP, int.Parse(DEFAULT_SERVER_PORT));
                remoteControlClient.UpdatePartnerInfo(partnerInfo);

                byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(GetMe().ToNetworkString(), EncodingType.ASCII).GetResult();

                remoteControlClient.Send(SocketDataType.RemoteControlAcceptedRequestToConnect, dataBytes, remoteControlClient.SocketId, true);
                RespondEvent?.Invoke(remoteControlClient, e);

                //Not use
                ////Split this
                //var screen = _globalHook.GetFirstScreen();
                //int length = screen.Sum(x=> x.Length);
                //SendScreen(remoteControlClient, SocketDataType.RemoteControlScreenSend, screen, length);

                //if (_vClientManager.HasClientOfType(VClientType.Receiver))
                //    StartScreenCapture();
            }
            else
            {
                if (sender is VClient client)
                {
                    string id = ByteArrayHelper.ConvertByteArrayToString(e.Data, 0, RandomLength.SOCKET_ID_LENGTH, EncodingType.ASCII).GetResult();
                    byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(id, EncodingType.ASCII).GetResult();

                    client.Send(SocketDataType.RemoteControlRefusedRequestToConnect, dataBytes, client.SocketId, true);
                }
            }
        }
        private void TURNConnectAccepted(object sender, RemoteDesktopEventArgs e)
        {
            try
            {
                if (sender is VClient client)
                {
                    ClientInfo partnerInfo = null;

                    string data = ByteArrayHelper.ConvertByteArrayToString(e.Data, EncodingType.ASCII).GetResult();
                    string[] stringArray = StringHelper.StringToStringArrayWithSeparator(data, DefaultValue.DEFAULT_SEPARATOR);
                    if (stringArray.Length == DefaultClientInfo.CLIENT_INFO_MIN_FIELDS)
                    {
                        partnerInfo = new ClientInfo();
                        if (partnerInfo.TryParseData(stringArray))
                        {
                            client.UpdatePartnerInfo(partnerInfo);

                            client.Send(SocketDataType.Ready, new byte[0], client.SocketId, true);
                            return;
                        }
                    }
                    //When partnerInfo is null this method will call dispose method
                    client.UpdatePartnerInfo(partnerInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "P2P connect error ");
            }
        }
        #endregion
        #region Peer-To-Peer
        public void P2PConnect(VClient client, string partnerId, string partnerPassword)
        {
            if (string.IsNullOrWhiteSpace(partnerId) || string.IsNullOrWhiteSpace(partnerPassword)) return;

            _reset.Reset();
            string id = StringHelper.RandomStringNumber(8);
            string dataString = StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPARATOR,id, partnerId ,partnerPassword);
            byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(dataString, EncodingType.ASCII).GetResult();
            client.Send(SocketDataType.P2PConnect, dataBytes, client.SocketId, true);
        }
        private void P2PRequestConnectHandler(object sender, RemoteDesktopEventArgs e)
        {
            try
            {
                string id = Encoding.ASCII.GetString(e.Data);
                var remoteClient = _vClientManager.New(id, VClientType.Receiver, false);

                bool flag = remoteClient.Listen();
                if (flag)
                {
                    //Success, Send login Info
                    remoteClient.Send(SocketDataType.P2PAcceptConnect, new byte[0], id, true);
                }
                else
                {
                    //Failed, use TURN server
                    remoteClient.Dispose();
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "P2PRequestConnectHandler error ");
            }
        }
        private void P2PRespondRequestConnect(object sender, RemoteDesktopEventArgs e)
        {
            if(sender is VClient client)
            {
                try
                {
                    string data = ByteArrayHelper.ConvertByteArrayToString(e.Data, EncodingType.ASCII).GetResult();
                    //three variable: id, public ip, public port
                    string[] dataArray = StringHelper.StringToStringArrayWithSeparator(data, DefaultValue.DEFAULT_SEPARATOR);

                    string ipToConnect = string.Empty;
                    //Check if current client and partner in the same network   
                    if (_clientInfo.IsTheSameNetWork(dataArray[1]))
                    {
                        //In the same network, use private ip   
                        ipToConnect = dataArray[2];
                    }
                    else
                    {
                        //Different network, use public ip
                        ipToConnect = dataArray[1];
                    }
                    var remoteControlClient = _vClientManager.New(dataArray[0], VClientType.Sender, false);
                    bool respond = remoteControlClient.TryConnect(ip: ipToConnect, port: int.Parse(dataArray[3]), retry: 3, waitRespondTime: 1000);
                    if (!respond)
                    {
                        //Failed
                        RespondEvent?.Invoke(client, new RemoteDesktopEventArgs(SocketDataType.P2PLoginFailed, respond, new byte[0]));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "P2P connect error ");
                }
            }
        }
        private void P2PAcceptedToConnect(object sender, RemoteDesktopEventArgs e)
        {
            if(sender is VClient client)
            {
                try
                {
                    string dataString = StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPARATOR, client.SocketId, GetMe().ToNetworkString());
                    byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(dataString, EncodingType.ASCII).GetResult();
                    client.Send(SocketDataType.P2PLogin, dataBytes, client.SocketId, true);
                    _reset.Set();   
                }
                catch (Exception ex)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "P2P connect error ");
                }
            }
        }
        private void P2PDataTransfer(object sender, RemoteDesktopEventArgs e)
        {
            if(sender is VClient client)
            {
                try
                {
                    string rawData = ByteArrayHelper.ConvertByteArrayToString(e.Data, EncodingType.ASCII).GetResult();  
                    string[] dataArray = StringHelper.StringToStringArrayWithSeparator(rawData, DefaultValue.DEFAULT_SEPARATOR);
                    if(dataArray.Length == DefaultClientInfo.CLIENT_INFO_MIN_FIELDS + 1)
                    {
                        ClientInfo partnerInfo = new ClientInfo();
                        if (partnerInfo.TryParseData(dataArray.Skip(1).ToArray()))
                        {
                            client.UpdatePartnerInfo(partnerInfo);
                            _clientInfo.AddPartner(partnerInfo);    

                            string dataString = StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPARATOR, GetMe().ToNetworkString());
                            byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(dataString, EncodingType.ASCII).GetResult();
                            client.Send(SocketDataType.P2PLoginSucceed, dataBytes, client.SocketId, true);
                            return;
                        }
                    }
                    client.Send(SocketDataType.P2PLoginFailed, new byte[0], client.SocketId, true);
                    client.UpdatePartnerInfo(null); 
                }
                catch (Exception ex)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "P2PDataTransfer error ");
                }
            }
        }
        private void P2PLoginRespond(object sender, RemoteDesktopEventArgs e)
        {
            if (sender is VClient client)
            {
                if(e.Type == SocketDataType.P2PLoginSucceed)
                {
                    string rawData = ByteArrayHelper.ConvertByteArrayToString(e.Data, EncodingType.ASCII).GetResult();
                    string[] dataArray = StringHelper.StringToStringArrayWithSeparator(rawData, DefaultValue.DEFAULT_SEPARATOR);
                    if (dataArray.Length == DefaultClientInfo.CLIENT_INFO_MIN_FIELDS)
                    {
                        ClientInfo partnerInfo = new ClientInfo();
                        if (partnerInfo.TryParseData(dataArray))
                        {
                            client.UpdatePartnerInfo(partnerInfo);
                            _clientInfo.AddPartner(partnerInfo);

                            client.Send(SocketDataType.Ready, new byte[0], client.SocketId, true);

                            RespondEvent?.Invoke(client, e);
                            return;
                        }
                    }
                }
                RespondEvent?.Invoke(client, e);
            }
        }
        #endregion
        private void ProcessP2PDisconnect(object sender, RemoteDesktopEventArgs e)
        {
            if (sender is VClient client)
            {
                try
                {
                    _clientInfo.RemovePartner(client.Partner?.Id);  
                    RemoveClientById(client.SocketId);
                    if (client.Partner != null)
                    {
                        _clientInfo.RemovePartner(client.Partner.Id);
                    }
                }
                catch { }
            }
        }
        #region Screen
        private void FirstSendScreen(object sender, RemoteDesktopEventArgs e)
        {
            if (sender is VClient client)
            {
                List<byte[]> screen;
                lock (_lock)
                {
                    screen = _globalHook.GetFirstScreen();
                }
                int length = screen.Sum(x => x.Length);

                var header = client.HeaderGenerate(type: SocketDataType.ScreenSend, socketId: client.SocketId, dataSize: length);
                client.Send(SocketDataType.ScreenSend, header, client.SocketId, false);

                for (int i = 0; i < screen.Count; i++)
                {
                    client.Send(SocketDataType.ScreenSend, screen[i], client.SocketId, false);
                }
                //SendScreen(client, SocketDataType.ScreenSend, screen, length);

                RespondEvent?.Invoke(client, e);
            }
        }
        private void FirstScreenSendSucceeded(object sender)
        {
            if (sender is VClient client)
            {
                client.ScreenSucceeded = true;
            }

            if (_vClientManager.HasClientOfType(VClientType.Receiver))
            {
                lock (_lock)
                {
                     if(!_isCapturting)
                {
                    _isCapturting = true;
                    StartScreenCapture();
                }
                }
            }
        }
        private void SendScreen(VClient client, SocketDataType type, List<byte[]> data, int totalSize)
        {
            try
            {
                if (data.Count == 0 || totalSize == 0)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error("Screen missing some value");
                    return;
                }
                var header = client.HeaderGenerate(type: type, socketId: client.SocketId, dataSize: totalSize);

                List<TaskObject> tasks = new List<TaskObject>();
                tasks.Add(new TaskObject
                {
                    TaskType = type,
                    Data = header,
                    IsSendHeader = false,
                    SessionId = client.SocketId
                });

                //data
                for (int i = 0; i < data.Count; i++)
                {
                    var task = new TaskObject
                    {
                        TaskType = type,
                        Data = data[i],
                        IsSendHeader = false
                    };

                    tasks.Add(task);
                }
                client.AddWorkGroup(tasks, QueuePriority.High);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "ScreenHookEventHandler error");
            }
        }
        private void SendScreenRegionsChanged(object sender, ScreenCaptureEventArgs e)
        {
            var connections = _vClientManager.Connections;
            TaskObject[] tasks = ConvertRawScreenRegionsChangedToArrayObject(e.Type, e.Data, e.TotalSize);

            foreach (var connection in connections)
            {
                if (connection.Value.ClientType == VClientType.Receiver && connection.Value.ScreenSucceeded)
                {
                    var header = connection.Value.HeaderGenerate(type: e.Type, socketId: connection.Value.SocketId, dataSize: e.TotalSize);

                    var payload = new TaskObject
                    {
                        TaskType = e.Type,
                        Data = header,
                        SessionId = connection.Value.SocketId,
                        IsSendHeader = false
                    };
                    var newTasks = new TaskObject[tasks.Length + 1];
                    newTasks[0] = payload;
                    Array.Copy(tasks, 0, newTasks, 1, tasks.Length);

                    connection.Value.AddWorkGroup(newTasks, QueuePriority.Medium);
                }
            }
        }
        private TaskObject[] ConvertRawScreenRegionsChangedToArrayObject(SocketDataType type, List<byte[]> data, int totalSize)
        {
            try
            {
                if (data.Count == 0 || totalSize == 0)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error("Screen missing some value");
                    return null;
                }
                TaskObject[] tasks = new TaskObject[data.Count];
                //data
                for (int i = 0; i < data.Count; i++)
                {
                    var task = new TaskObject
                    {
                        TaskType = type,
                        Data = data[i],
                        IsSendHeader = false,
                    };

                    tasks[i] = task;
                }
                return tasks;
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "ScreenHookEventHandler error");
                return null;
            }
        }
        #endregion
        #endregion
        #region Events
        private void MouseReceivedEventHandler(object sender, RemoteDesktopEventArgs e)
        {
            try
            {
                _globalHook.MouseReceivedEventHandler(GetMe().Width, GetMe().Height, e.Data);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", nameof(MouseReceivedEventHandler)).Error(ex, "Error processing mouse data");
            }
        }
        private void KeyboardReceivedEventHandler(object sender, RemoteDesktopEventArgs e)
        {
            try
            {
                _globalHook.KeyboardReceivedEventHandler(e.Data);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", nameof(KeyboardReceivedEventHandler)).Error(ex, "Error processing keyboard data");
            }
        }
        private void ScreenCaptureEventHandler(object sender, ScreenCaptureEventArgs e)
        {
            SendScreenRegionsChanged(sender, e);
        }
        private void KeyboardEventHandler(object sender, KeyboardEventArgs e)
        {
            if (_globalHook.CheckClipboard(e, out var data, out var type))
            {
                foreach (var connection in _vClientManager.Connections)
                {
                    if (connection.Value.ClientType == VClientType.Receiver)
                        connection.Value.AddWork(new TaskObject
                        {
                            TaskType = type,
                            Data = data,
                            SessionId = connection.Value.SocketId,
                            IsSendHeader = true,
                            ChunkFileInfo = null
                        }, QueuePriority.High);
                }
            }
            else
            {
                KeyboardEvent?.Invoke(sender, e);
            }
        }
        private void EventReceived(object sender, RemoteDesktopEventArgs e)
        {
            switch (e.Type)
            {
                case SocketDataType.Connect:
                    _reset.Set();
                    RespondEvent?.Invoke(sender, e);
                    break;
                case SocketDataType.Disconnect:
                    ProcessP2PDisconnect(sender, e);
                    RespondEvent?.Invoke(sender, e);
                    break;
                case SocketDataType.Login:
                case SocketDataType.LoginFailed:
                case SocketDataType.Error:
                    RespondEvent?.Invoke(sender, e);
                    break;

                case SocketDataType.P2PConnect:
                    P2PRequestConnectHandler(sender, e);
                    break;
                case SocketDataType.P2PDataRespond:
                    P2PRespondRequestConnect(sender, e);
                    break;
                case SocketDataType.P2PInvalidConnectData:
                    RespondEvent?.Invoke(sender, e);
                    break;
                case SocketDataType.P2PAcceptConnect:
                    P2PAcceptedToConnect(sender, e);
                    break;
                case SocketDataType.P2PLogin:
                    P2PDataTransfer(sender, e);
                    break;
                case SocketDataType.P2PLoginSucceed:
                    P2PLoginRespond(sender, e);
                    break;


                case SocketDataType.ClipboardSend:
                    SetClipboard(e.Data);
                    break;
                case SocketDataType.RemoteControlRequestToConnect:
                    TURNRequestConnectHandler(sender, e);
                    break;
                case SocketDataType.RemoteControlAcceptedRequestToConnect:
                    TURNConnectAccepted(sender, e);
                    RespondEvent?.Invoke(sender, e);
                    break;
                case SocketDataType.RemoteControlRefusedRequestToConnect:
                    break;
                case SocketDataType.RemoteControlConnectFailed:
                    RespondEvent?.Invoke(sender, e);
                    ProcessP2PDisconnect(sender, e);
                    break;
                case SocketDataType.MouseSend:
                    MouseReceivedEventHandler(sender, e);
                    break;
                case SocketDataType.RemoteControlScreenSend:
                    KeyboardReceivedEventHandler(sender, e);
                    break;
                case SocketDataType.RemoteControlDisconnect:
                    ProcessP2PDisconnect(sender, e);
                    break;
                case SocketDataType.Ready:
                    FirstSendScreen(sender, e);
                    break;
                case SocketDataType.ScreenOk:
                    FirstScreenSendSucceeded(sender);
                    break;
                case SocketDataType.RemoteControlDataSendFailed:
                    break;         
                default:
                    Logger.Log.ForContext("FileName", GetType().Name).Error("Invalid event type: "+ e.Type);
                    break;
            }
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

                StopKeyboardListener();
                if (_globalHook != null)
                {
                    _globalHook.ScreenCaptureChanged -= ScreenCaptureEventHandler;
                    _globalHook.KeyboardReceived -= KeyboardEventHandler;
                }
                if (_vClientManager != null)
                {
                    _vClientManager.ClientDataReceived -= EventReceived;
                    _vClientManager.ClientClosed -= VClientClosedEventHandler;
                }

                _globalHook?.Dispose();
                _vClientManager?.Dispose();
                _reset?.Dispose();
                _disposed = true;
            }
        }
    }
}