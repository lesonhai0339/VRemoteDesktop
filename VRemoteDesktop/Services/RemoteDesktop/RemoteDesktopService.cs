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
using VRemoteDesktop.Services.ScreenCapture;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.Enums;
using VRemoteDesktop.Services.ScreenCapture.Events;
using VRemoteDesktop.Services.ScreenCapture.GDI;
using VRemoteDesktop.Services.SessionManagement;
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
        private bool _isCapturing = false;

#if DEBUG
        private readonly IVScreenSender _screenSender;
#endif
        private readonly IClientInfoManager _clientInfo;
        private readonly GlobalHookService _globalHook;
        private readonly SessionManager _sessionManager;
        private ManualResetEvent _reset;

        private Dictionary<SocketDataType, Action<object, EventArgs>> _eventHandlers;

        public event EventHandler<KeyboardEventArgs> KeyboardEvent;
        public event EventHandler<RemoteDesktopEventArgs> RespondEvent;
        public RemoteDesktopService(IVScreenSender screenSender ,GlobalHookService globalHook, SessionManager sessionManager, IClientInfoManager clientInfo)
        {
            Initialize();

            _disposed = false;
            _clientInfo = clientInfo;
            _reset = new ManualResetEvent(false);

            _globalHook = globalHook;
            _sessionManager = sessionManager;

#if DEBUG
            _screenSender = screenSender;
            _screenSender.OnFrame += OnRegionEventHandler;
            if (!_screenSender.IsCapturing)
            {
                _screenSender.Start();
            }

#endif

            _globalHook.ScreenCaptureChanged += ScreenCaptureEventHandler;
            _globalHook.KeyboardReceived += KeyboardEventHandler;
            _sessionManager.SessionDataReceived += EventReceived;
            _sessionManager.SessionClosed += ClientSessionClosedEventHandler;
            StartKeyboardListener();  
        }

        private void OnRegionEventHandler(object sender, FrameEventArgs e)
        {
            try
            {
                if(e.Type == ScreenType.FULL_SCREEN)
                {
                    _sessionManager.AddScreen(VClientType.Receiver, e.RegionFrame);
                }
                else if(e.Type == ScreenType.DIRTY_REGIONS)
                {
                    _sessionManager.AddDirtyRegions(VClientType.Receiver, e.RegionFrame);
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "OnDirtyRegionError").Error(ex.Message);
            }
        }
#if DEBUG

#endif
        private void Initialize()
        {
            _eventHandlers = new Dictionary<SocketDataType, Action<object, EventArgs>>
            {
                //login
                { SocketDataType.Connect, ForwardEvent},
                { SocketDataType.Disconnect, ClientDisconnected},
                { SocketDataType.Login, LoginRespond},
                { SocketDataType.LoginFailed, ForwardEvent},
                { SocketDataType.Error, ForwardEvent},

                //p2p
                { SocketDataType.P2PConnect, P2PConnect},
                { SocketDataType.P2PDataRespond, P2PConnectDataRespond},
                { SocketDataType.P2PAcceptConnect, P2PRequestToConnectAccepted},
                { SocketDataType.P2PLogin, P2PLogin},
                { SocketDataType.P2PLoginRespond, P2PLoginRespond},
                { SocketDataType.P2PInvalidConnectData, ForwardEvent},

                //turn server
                { SocketDataType.RemoteControlRequestToConnect, RelayConnect},
                { SocketDataType.RemoteControlAcceptedRequestToConnect, RelayRequestToConnectAccepted},
                { SocketDataType.RemoteControlRefusedRequestToConnect, ForwardEvent},
                { SocketDataType.RemoteControlConnectFailed, ClientDisconnected},
                { SocketDataType.RemoteControlDisconnect, ClientDisconnected},

                //low-level
                { SocketDataType.ClipboardSend, ClipboardReceived},
                { SocketDataType.MouseSend, MouseReceived},
                { SocketDataType.KeyboardSend, KeyboardReceived},
                { SocketDataType.Ready, ReadyToRemote},
                { SocketDataType.ScreenOk, ScreenCompleted}
            };
        }

        private void ScreenCompleted(object sender, EventArgs e)
        {
            var clientSession = sender as ClientSession;
            if(clientSession != null)
            {
                _screenSender.AddSessionBuffer(clientSession.SessionId , clientSession.Image);
            }
        }

        #region Properties
        public bool Disposed => _disposed;
        #endregion
        #region Methods
        public ClientInfo GetMe()
        {
            try
            {
                return _clientInfo.GetMyInfo();
            }
            catch(Exception ex)
            {
                throw;
            }
        }
        public void UpdateMyInfo(byte[] data)
        {
            if (data == null || data.Length == 0) return;

            try
            {
                _clientInfo.UpdateMyInfo(data);
            }
            catch(Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "UpdateMyInfo error");
            }
        }    
        public void AddKeyboardListenerOnFormByHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;
            try
            {
                _globalHook.AddKeyboardHook(handle);
            }
            catch(Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "AddKeyboardListenerOnFormByHandle error");
            }
        }
        public void RemoveKeyboardListenerOnFormByHandle(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return;

            try
            {
                _globalHook.RemoveKeyboardHook(handle);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "RemoveKeyboardListenerOnFormByHandle error");
            }
        }
        public string GetClipboardString()
        {
            try
            {
                return _globalHook.GetClipboard(); ;
            }
            catch
            {
                return string.Empty;
            }
        }
        public bool SetClipboard(byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            try
            {
                return _globalHook.SetClipboard(data); ;
            }
            catch
            {
                return false;
            }
        }
        public bool SetClipboard(byte[] data, int index, int length)
        {
            if (data == null || data.Length == 0)
                return false;

            if (index < 0)
                return false;

            if (length < 0)
                return false;

            try
            {
                return _globalHook.SetClipboard(data, index, length);
            }
            catch
            {
                return false;
            }
        }
        public ClientSession GetClientById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;

            try
            {
                var session = _sessionManager.GetByKey(id);
                return session;
            }
            catch
            {
                return null;
            }
        }
        public ClientSession NewClient(string id, VClientType type, bool isHost)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id)) return null;

                var session = _sessionManager.New(id, type, isHost);
                if (_sessionManager.Connections.Count > 0)
                {
                    if (_sessionManager.HasClientOfType(VClientType.Receiver))
                        StartScreenCapture();
                }
                return session;
            }
            catch
            {
                return null;
            }
        }
        public void Login(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            try
            {
                var session = _sessionManager.GetByKey(id);
                if (session != null)
                {
                    byte[] encoder = ByteArrayHelper.ConvertStringToByteArray(GetMe().ToNetworkString(), Enums.EncodingType.ASCII).GetResult();
                    session.Send(SocketDataType.Login, encoder, null);
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "Login error");
            }
        }
        public bool CheckRemoteConnected(string id)
        {
            return _clientInfo.IsExistPartner(id);
        }

        //Relay connect
        public bool P2PConnect(string partnerId, string partnerPassword)
        {
            if (string.IsNullOrWhiteSpace(partnerId) || string.IsNullOrWhiteSpace(partnerPassword)) return false;

            string connectionId = StringHelper.RandomStringNumber(8);
            var newConnection = NewClient(connectionId, VClientType.Sender, false);

            if (newConnection == null)
            {
                return false;
            }

            if (newConnection.TryConnect(ip: DEFAULT_SERVER_IP, port: int.Parse(DEFAULT_SERVER_PORT)))
            {
                string dataString = StringHelper.StringBuilderWithSeparator(DefaultValue.DEFAULT_SEPARATOR, newConnection.SessionId, partnerId, partnerPassword, GetMe().ToNetworkString());
                byte[] dataBytes = ByteArrayHelper.ConvertStringToByteArray(dataString, EncodingType.ASCII).GetResult();
                newConnection.Send(SocketDataType.RemoteControlRequestToConnect, dataBytes, newConnection.SessionId, true);
                return true;
            }
            else
            {
                return false;
            }
        }
        //P2P connect
        public void P2PConnect(ClientSession session, string partnerId, string partnerPassword)
        {
            if (string.IsNullOrWhiteSpace(partnerId) || string.IsNullOrWhiteSpace(partnerPassword)) return;

            P2PConnectInfo connectInfo = new P2PConnectInfo(partnerId, partnerPassword);

            var result = ByteArrayHelper.ConvertStringToByteArray(connectInfo.ToNetworkString(), EncodingType.ASCII);
            if (result.IsSuccess)
            {
                session.Send(SocketDataType.P2PConnect, result.Data, session.SessionId, true);
            }
            else
            {
                RespondEvent?.Invoke(session, new RemoteDesktopEventArgs(SocketDataType.P2PInvalidConnectData, false, new byte[0]));
            }
        }

        private void StartKeyboardListener()
        {
            try
            {
                _globalHook.StartKeyboardListener();
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "StartKeyboardListener error");
            }
        }
        private void StopKeyboardListener()
        {
            try
            {
                _globalHook.StopKeyboardListener();
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "StopKeyboardListener error");
            }
        }
        private void StartScreenCapture()
        {
#if DEBUG
            _screenSender.Start();
            return;
#endif
            _globalHook.StartScreenCapture();
        }
        private void StopScreenCapture()
        {
#if DEBUG
            _screenSender.Stop();
            return;
#endif
            _globalHook.StopScreenCapture();
        }
        private void RemoveClientById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            _sessionManager.Remove(id);
            if (_sessionManager.Connections.Count == 0)
            {
                lock (_lock)
                {
                    StopScreenCapture();
                    _isCapturing = false;
                }
            }
            else
            {
                if (!_sessionManager.HasClientOfType(VClientType.Receiver))
                {
                    lock (_lock)
                    {
                        StopScreenCapture();
                        _isCapturing = false;
                    }
                }
            }
        }
        private ConcurrentDictionary<string, ClientSession> GetClients()
        {
            return _sessionManager.Connections;
        }
        #endregion
        #region Events
        private void ScreenCaptureEventHandler(object sender, ScreenCaptureEventArgs e)
        {
            try
            {
            }
            catch { /*Ignore*/ }
        }
        private void KeyboardEventHandler(object sender, KeyboardEventArgs e)
        {
            if (_globalHook.CheckClipboard(e, out var data, out var type))
            {
                foreach (var connection in _sessionManager.Connections)
                {
                    if (connection.Value.SessionType == VClientType.Receiver)
                        connection.Value.AddWork(QueuePriority.High, new TaskObject
                        {
                            TaskType = type,
                            Data = data,
                            SessionId = connection.Value.SessionId,
                            IsSendHeader = true,
                            ChunkFileInfo = null
                        });
                }
            }
            else
            {
                KeyboardEvent?.Invoke(sender, e);
            }
        }
        private void EventReceived(object sender, RemoteDesktopEventArgs e)
        {
            if(_eventHandlers.TryGetValue(e.Type, out var handle))
            {
                try
                {
                    handle(sender, e);
                }
                catch(Exception ex)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, string.Format("Error handling {0}: {1}", e.Type, ex.Message));
                }
            };
        }
        #endregion
        #region Handlers
        private void ForwardEvent(object sender, EventArgs e)
        {
            RespondEvent?.Invoke(sender, (RemoteDesktopEventArgs)e);
        }

        //Screen
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
        private void ClipboardReceived(object sender, EventArgs e)
        {
            SetClipboard(((RemoteDesktopEventArgs)e).Data);
        }

        //Mouse
        private void MouseReceived(object sender, EventArgs e)
        {
            try
            {
                _globalHook.MouseReceivedEventHandler(GetMe().Width, GetMe().Height, ((RemoteDesktopEventArgs)e).Data);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", nameof(MouseReceived)).Error(ex, "Error processing mouse data");
            }
        }

        //Keyboard
        private void KeyboardReceived(object sender, EventArgs e)
        {
            try
            {
                _globalHook.KeyboardReceivedEventHandler(((RemoteDesktopEventArgs)e).Data);
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", nameof(KeyboardReceived)).Error(ex, "Error processing keyboard data");
            }
        }

        //Connect
        private void LoginRespond(object sender, EventArgs e)
        {
            var ev = e as RemoteDesktopEventArgs;
            if (ev == null)
                throw new InvalidOperationException("Invalid event args for LoginRespond");

            try
            {
                UpdateMyInfo(ev.Data);
                RespondEvent?.Invoke(sender, new RemoteDesktopEventArgs(SocketDataType.Login, true));
            }
            catch
            {
                RespondEvent?.Invoke(sender, new RemoteDesktopEventArgs(SocketDataType.LoginFailed, false));
            }
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
            if(networkInfo.TryParseData(dataParsed))
            {
                string connectIp = _clientInfo.IsTheSameNetWork(networkInfo.PublicIP) ? networkInfo.LocalIP : networkInfo.PublicIP;

                var remoteControlClient = _sessionManager.New(networkInfo.Id, VClientType.Sender, false);
                if(int.TryParse(networkInfo.Port, out int port))
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
        private void ClientSessionClosedEventHandler(object sender, EventArgs e)
        {
            var clientSession = sender as ClientSession;
            if (clientSession != null)
            {
                try
                {
                    //clientSession.IsP2PConnected = false;

                    if (clientSession.IsHost)
                    {
                        RespondEvent?.Invoke(sender, new RemoteDesktopEventArgs(SocketDataType.Disconnect, false, new byte[0]));
                    }

                    _clientInfo.RemovePartner(clientSession.PartnerInfo?.Id);
                    _sessionManager.Remove(clientSession.SessionId);

                    //Unregister received capture
                    if(clientSession.SessionType == VClientType.Receiver)
                    {
                        _screenSender.RemoveSessionBuffer(clientSession.SessionId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "VClientClosedEventHandler error ");
                }
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
                if (_sessionManager != null)
                {
                    _sessionManager.SessionDataReceived -= EventReceived;
                    _sessionManager.SessionClosed -= ClientSessionClosedEventHandler;
                }

                _globalHook?.Dispose();
                _sessionManager?.Dispose();
                _reset?.Dispose();
                _disposed = true;
            }
        }
    }
}