using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
       
        private readonly string DEFAULT_SERVER_IP = AppSettingHelper.GetValue("RemoteServerIP");
        private readonly string DEFAULT_SERVER_PORT = AppSettingHelper.GetValue("RemoteServerPort");
        private volatile bool _disposed;

        private readonly IClientInfoManager _clientInfo;
        private readonly GlobalHookService _globalHook;
        private readonly VClientManager _vClientManager;
        private ManualResetEvent _reset;

        public event EventHandler<KeyboardEventArgs> KeyboardEvent;
        public event EventHandler<P2PClientDataReceived> DataReceivedEvent;
        public RemoteDesktopService(GlobalHookService globalHook, VClientManager vClientManager, IClientInfoManager clientInfo)
        {
            _disposed = false;
            _clientInfo = clientInfo;
            _reset = new ManualResetEvent(false);

            _globalHook = globalHook;
            _vClientManager = vClientManager;

            _globalHook.ScreenCaptureChanged += ScreenCaptureEventHandler;
            _globalHook.KeyboardReceived += KeyboardEventHandler;
            _vClientManager.ClientDataReceived += ClientDataReceivedEventHandler;
            StartKeyboardListener();

        }
        #region Properties
        public bool Disposed => _disposed;
        #endregion
        #region Methods
        public ClientInfo GetMe()
        {
            return _clientInfo.GetMyInfo();
        }
        public void Login(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            var client = _vClientManager.GetByKey(id);
            if(client != null)
            {
                byte[] encoder = ByteArrayHelper.ConvertStringToByteArray(GetMe().ToNetworkString(), Enums.EncodingType.ASCII).GetResult();
                client.Send(SocketDataType.Login, encoder, null);
            }
        }
        public void P2PConnect(string partnerId, string partnerPassword)
        {
            if (string.IsNullOrWhiteSpace(partnerId) || string.IsNullOrWhiteSpace(partnerPassword)) return;

            _reset.Reset();
            string connectionId = StringHelper.RandomStringNumber(8);
            var newConnection = NewClient(connectionId, VClientType.Sender);
            if (newConnection == null)
            {
                return;
            }
            newConnection.Connect(DEFAULT_SERVER_IP, int.Parse(DEFAULT_SERVER_PORT));
            _reset.WaitOne(5000);
            string dataString = StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPARATOR, newConnection.SocketId, partnerId, partnerPassword, GetMe().ToNetworkString());
            byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(dataString, EncodingType.ASCII).GetResult();
            newConnection.Send(SocketDataType.P2PRequestConnect, dataBytes, partnerId, true);
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
                StopScreenCapture();
            }
            else
            {
                if (!_vClientManager.HasClientOfType(VClientType.Receiver))
                    StopScreenCapture();
            }
        }
        public VClient GetClientById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var client = _vClientManager.GetByKey(id);
            return client;
        }
        public VClient NewClient(string id, VClientType type)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            var newClient = _vClientManager.New(id, type);
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
        private void P2PRequestConnectHandler(object sender, P2PClientDataReceived e)
        {
            if (_clientInfo.IsAuthenticated(e.Data, out ClientInfo partnerInfo, out string connectionId))
            {
                var newClient = _vClientManager.New(connectionId, VClientType.Receiver);
                newClient.Connect(DEFAULT_SERVER_IP, int.Parse(DEFAULT_SERVER_PORT));
                newClient.UpdatePartnerInfo(partnerInfo);
                byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(GetMe().ToNetworkString(), EncodingType.ASCII).GetResult();

                newClient.Send(SocketDataType.P2PAcceptConnect, dataBytes, newClient.SocketId, true);
                DataReceivedEvent?.Invoke(newClient, e);

                var screen = _globalHook.GetFirstScreen();
                int length = screen.Sum(x=> x.Length);
                SendScreen(newClient, SocketDataType.Screen, screen, length);

                if (_vClientManager.HasClientOfType(VClientType.Receiver))
                    StartScreenCapture();
            }
            else
            {
                if(sender is VClient client)
                {
                    string id = ByteArrayHelper.ConvertByteArrayToString(e.Data, 0, RandomLength.SOCKET_ID_LENGTH, EncodingType.ASCII).GetResult();
                    byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(id, EncodingType.ASCII).GetResult();
 
                    client.Send(SocketDataType.P2PRejectConnect, dataBytes, client.SocketId, true);
                }
            }
        }
        private void ProcessP2PConnectAccepted(object sender, P2PClientDataReceived e)
        {
            try
            {
                if(sender is VClient client)
                {
                    ClientInfo partnerInfo = null;

                    string data = ByteArrayHelper.ConvertByteArrayToString(e.Data, EncodingType.ASCII).GetResult();
                    string[] stringArray = StringHelper.StringToStringArrayWithSeparator(data, DefaultValue.DEFAULT_SEPARATOR);
                    if(stringArray.Length == DefaultClientInfo.CLIENT_INFO_MIN_FIELDS)
                    {
                        if (StringHelper.StringValidate(stringArray))
                        {
                            partnerInfo = new ClientInfo
                            {
                                Id = stringArray[DefaultClientInfo.CLIENT_INFO_ID_INDEX],
                                Password = stringArray[DefaultClientInfo.CLIENT_INFO_PASSWORD_INDEX],
                                ComputerName = stringArray[DefaultClientInfo.CLIENT_INFO_COMPUTER_NAME_INDEX],
                                Width = int.Parse(stringArray[DefaultClientInfo.CLIENT_INFO_WIDTH_INDEX]),
                                Height = int.Parse(stringArray[DefaultClientInfo.CLIENT_INFO_HEIGHT_INDEX]),
                                MajorVersion = stringArray[DefaultClientInfo.CLIENT_INFO_MAJOR_VERSION_INDEX],
                                MinorVersion = stringArray[DefaultClientInfo.CLIENT_INFO_MINOR_VERSION_INDEX],
                                Ip = stringArray[DefaultClientInfo.CLIENT_INFO_IP_INDEX],
                                Port = stringArray[DefaultClientInfo.CLIENT_INFO_PORT_INDEX],
                                PublicIP = stringArray[DefaultClientInfo.CLIENT_INFO_PUBLIC_IP_INDEX],
                            };
                            client.UpdatePartnerInfo(partnerInfo);
                            return;
                        }
                    }
                    client.UpdatePartnerInfo(partnerInfo);
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "P2P connect error ");
            }
        }
        private void SendScreenChangedToClient(object sender, ScreenCaptureEventArgs e)
        {
            var connections = _vClientManager.Connections;
            TaskObject[] tasks = GetScreenSnapshotData(e.Type, e.Data, e.TotalSize);

            foreach(var connection in connections)
            {
                if (connection.Value.ClientType == VClientType.Receiver)
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
        private TaskObject[] GetScreenSnapshotData(SocketDataType type, List<byte[]> data, int totalSize)
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
        private void SendScreen(VClient  client, SocketDataType type, List<byte[]> data, int totalSize)
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
                client.AddWorkGroup(tasks, QueuePriority.Medium);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "ScreenHookEventHandler error");
            }
        }
        private void MouseReceivedEventHandler(object sender, P2PClientDataReceived e)
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
        private void KeyboardReceivedEventHandler(object sender, P2PClientDataReceived e)
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
        private void ProcessP2PDisconnect(object sender, P2PClientDataReceived e)
        {
            if (sender is VClient client)
            {
                RemoveClientById(client.SocketId);
            }
        }
        #endregion
        #region Events
        private void ScreenCaptureEventHandler(object sender, ScreenCaptureEventArgs e)
        {
            SendScreenChangedToClient(sender, e);
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
        private void ClientDataReceivedEventHandler(object sender, P2PClientDataReceived e)
        {
            switch (e.Type)
            {
                case SocketDataType.Connect:
                    _reset.Set();
                    DataReceivedEvent?.Invoke(sender, e);
                    break;
                case SocketDataType.Clipboard:
                    SetClipboard(e.Data);
                    break;
                case SocketDataType.P2PRequestConnect:
                    P2PRequestConnectHandler(sender, e);
                    break;
                case SocketDataType.P2PAcceptConnect:
                    ProcessP2PConnectAccepted(sender, e);
                    DataReceivedEvent?.Invoke(sender, e);
                    break;
                case SocketDataType.Mouse:
                    MouseReceivedEventHandler(sender, e);
                    break;
                case SocketDataType.Keyboard:
                    KeyboardReceivedEventHandler(sender, e);
                    break;
                case SocketDataType.P2PDisconnect:
                    ProcessP2PDisconnect(sender, e);
                    break;
                case SocketDataType.Error:
                    break;
                default:
                    DataReceivedEvent?.Invoke(sender, e);
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
                    _vClientManager.ClientDataReceived -= ClientDataReceivedEventHandler;

                _globalHook?.Dispose();
                _vClientManager?.Dispose();
                _reset?.Dispose();
                _disposed = true;
            }
        }
    }
}