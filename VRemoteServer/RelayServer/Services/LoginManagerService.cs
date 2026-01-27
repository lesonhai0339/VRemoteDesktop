using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.DTOs;
using VRemoteServer.RelayServer.DTOs.Responses;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;

namespace VRemoteServer.RelayServer.Services
{
    public interface ILoginManagerService
    {
        void TaskCompleted(SocketConnection connection, byte[] data);
        Task InitRemoteConnection(ConnectionInfo controller, ConnectionInfo controlled, string id);
        void GetPartnerInfoFailed(SocketConnection connection, string message);
        int NumberOfConnections { get; }
        void P2PConnectFailed(SocketConnection connection);
        void Send(SocketConnection connection, byte[] data);
        bool SendWithRespond(SocketConnection connection, byte[] data);
        bool GetFirst(string id, string password, out ConnectionInfo connectionInfo);
        /// <summary>
        /// Received Ping packet from client and send back Pong packet
        /// </summary>
        /// <param name="connection"></param>
        void Ping(SocketConnection connection);
        bool Add(SocketConnection connection, byte[] data, out ConnectionInfo connectionInfo);
        void LoginResponse(SocketConnection connection, ConnectionInfo connectionInfo, bool succeed);
        void RemoveLogin(SocketConnection connection);
        bool GetConnectionsInfoBySocketConnection(SocketConnection connection, out List<ConnectionInfo> connectionsInfo);
        bool TryGetLoggedConnection(string id, out ConnectionInfo connectionInfo);
        void InitServer();
        Task StartServer(IPEndPoint ep);
        void CancelServer();
        event EventHandler<LoginEventArgs> LoginManagerEvent;
        void Dispose();
    }
    public class LoginManagerService : ILoginManagerService, IDisposable
    {
        private const int TIMEOUT = 5; 
        private bool _disposed;


        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _tasks = new();


        private readonly ILoginServer _loginServer;
        private readonly ILoginManager _loginConnectionManager;

        public event EventHandler<LoginEventArgs> LoginManagerEvent;

        private CancellationTokenSource _cancel = new();
        public LoginManagerService(ILoginServer loginServer, ILoginManager loginConnectionManager)
        {
            _disposed = false;
            _loginServer = loginServer;
            _loginConnectionManager = loginConnectionManager;

            //Register event
            _loginServer.ServerEvent += LoginEventHandler;
            _loginServer.ServerErrorEvent += ServerErrorEventHandler;
        }
        #region Methods
        public int NumberOfConnections => _loginConnectionManager.Count;


        public async Task InitRemoteConnection(ConnectionInfo controller, ConnectionInfo controlled, string id)
        {
            TaskCompletionSource<bool> task = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_tasks.TryAdd(id, task))
            {
                //Notify to controller that cannot establish connection
                Send(controller.SocketConnection, SocketDataType.GetPartnerInfoFailed);
                return;
            }

            //Send controller network info to controlled
            var controllerInfo = new PartnerNetworkInfo(
                sessionId: id, 
                partnerId: controller.Id,   
                partnerPassword: string.Empty,   
                publicIP: controller.PublicIP, 
                localIP: controller.Ip, 
                port: controller.Port,
                width: controller.Width,
                height: controller.Height,
                computerName: controller.ComputerName,
                major: controller.MajorVersion,
                minor: controller.MinorVersion);

            var controllerInfoByteArray = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(controllerInfo));
            var controllerPacket = PacketFactory.CreatePacket(SocketDataType.RequestRemoteConnect, data: controllerInfoByteArray);
       
            bool queue = SendWithRespond(controlled.SocketConnection, controllerPacket);
            if (!queue)
            {
                //Add to send queue failed, remove task and notify to controller
                _tasks.TryRemove(id, out _);
                Send(controller.SocketConnection, SocketDataType.GetPartnerInfoFailed);
                return;
            }

            bool isSuccess = false;
            try
            {
                //Waiting controlled received request connect packet and send ack
                isSuccess = await task.Task.WaitAsync(TimeSpan.FromSeconds(TIMEOUT));
            }
            catch { /*Timeout, disposed,...*/ }
            finally
            {
                _tasks.TryRemove(id, out _);
            }

            if (!isSuccess)
            {
                //Timeout
                Send(controller.SocketConnection, SocketDataType.GetPartnerInfoFailed);
                return;
            }

            //Send controlled network info to controller
            var controlledInfo = new PartnerNetworkInfo(
                sessionId: id,
                partnerId: controlled.Id,
                partnerPassword: controlled.Password,
                publicIP: controlled.PublicIP,
                localIP: controlled.Ip,
                port: controlled.Port,
                width: controlled.Width,
                height: controlled.Height,
                computerName: controlled.ComputerName,
                major: controlled.MajorVersion,
                minor: controlled.MinorVersion);

            var controlledInfoByteArray = Encoding.ASCII.GetBytes(JsonConvert.SerializeObject(controlledInfo));
            var controlledPacket = PacketFactory.CreatePacket(SocketDataType.GetPartnerInfoSuccess, data: controlledInfoByteArray);

            SendWithRespond(controller.SocketConnection, controlledPacket);
        }
        public void TaskCompleted(SocketConnection connection, byte[] data)
        {
            var connectionId = Encoding.ASCII.GetString(data);
            if (_tasks.TryGetValue(connectionId, out var task))
            {
                task.TrySetResult(true);
            }
        }
        public void GetPartnerInfoFailed(SocketConnection connection, string message)
        {
            var packet = PacketFactory.CreatePacket(SocketDataType.GetPartnerInfoFailed, data:  Encoding.ASCII.GetBytes(message));
            SendWithRespond(connection, packet);
        }
        public void InitServer()
        {
            _loginServer.Init();
        }

        public async Task StartServer(IPEndPoint ep)
        {
            if (ep == null)
                throw new ArgumentNullException(nameof(ep));

            await _loginServer.Start(ep);
        }

        public void CancelServer()
        {
            _loginServer.Cancel();
        }

        public void Ping(SocketConnection connection)
        {
            connection.UpdateTime();
            Send(connection, SocketDataType.Pong);
        }
        public void LoginResponse(SocketConnection connection, ConnectionInfo connectionInfo, bool succeed)
        {
            string sessionId = succeed ? connectionInfo.Id : null;

            LoginResponse response = new LoginResponse(succeed, (succeed ? connectionInfo.PublicIP : null));
            var responseString = JsonConvert.SerializeObject(response);

            var packet = PacketFactory.CreatePacket(SocketDataType.LoginResponse, sessionId, Encoding.ASCII.GetBytes(responseString));
            SendWithRespond(connection, packet);
        }
        public void P2PConnectFailed(SocketConnection connection)
        {
            Send(connection, SocketDataType.P2PInvalidConnectData);
        }

        public bool GetFirst(string id, string password, out ConnectionInfo connectionInfo)
        {
            connectionInfo = _loginConnectionManager.GetFirst(c => c.Id == id && (c.Password == password || c.DefaultPassword == password));
            return connectionInfo != null;
        }

        public bool TryGetLoggedConnection(string id, out ConnectionInfo connectionInfo)
            => _loginConnectionManager.Get(id, out connectionInfo);

        public bool GetConnectionsInfoBySocketConnection(SocketConnection connection, out List<ConnectionInfo> connectionsInfo)
            => _loginConnectionManager.GetConnectionsInfoBySocketConnection(connection, out connectionsInfo);

        public void RemoveLogin(SocketConnection connection)
        {
            if (_loginConnectionManager.RemoveLoginInfoBySocketConnection(connection))
            {
                Send(connection, SocketDataType.Disconnect);
            }
        }

        public void ProcessLoginDataReceived(SocketConnection connection, int dataOffset, int dataLength)
        {
            try
            {
                if (dataOffset < 0)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(dataOffset)), "ProcessSocketData error");
                    return;
                }
                if (dataLength < 0)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(dataLength)), "ProcessSocketData error");
                    return;
                }
                if (connection.Reader == null || connection.Reader.Buffer == null)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(connection.Reader)), "ProcessSocketData error");
                    return;
                }

                var buffer = connection.Reader.Buffer;
                int offset = dataOffset + PACKET_SIZE_INDEX;
                int payloadLength = dataLength - PACKET_HEADER_LENGTH;

                byte[] data = new byte[payloadLength];

                //Packet size
                int packetSize = BitConverter.ToInt32(buffer, offset);
                offset += PACKET_SIZE_LENGTH;
                if (packetSize != dataLength)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentException(nameof(dataLength)), "Missing some data");
                    return;
                }

                //Packet type
                SocketDataType type = (SocketDataType)buffer[offset];
                offset += PACKET_TYPE_LENGTH;

                //Id
                string id = Encoding.ASCII.ByteArrayToString(buffer, offset, PACKET_ID_LENGTH);
                offset += PACKET_ID_LENGTH;

                //Payload
                Buffer.BlockCopy(buffer, offset, data, 0, payloadLength);

                LoginManagerEvent?.Invoke(connection, new LoginEventArgs(type, data));
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessSocketData error on IP: {connection.IP}");
            }
        }

        public bool Add(SocketConnection connection, byte[] data, out ConnectionInfo connectionInfo)
        {
            connectionInfo = null;
            try
            {
                if (_loginConnectionManager.NewConnectionInfo(data, connection, out connectionInfo))
                {
                    connection.SetTimeout(60); // set timeout for socket login is 60 seconds
                    return true;
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Login error");
            }
            return false;
        }

        
        public void Send(SocketConnection connection, SocketDataType type)
        {
            try
            {
                var packet = PacketFactory.CreatePacket(type);
                _loginServer.Send(connection, packet);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
            }
        }
        public void Send(SocketConnection connection, byte[] data)
        {
            try
            {
                _loginServer.Send(connection, data);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
            }
        }

        public bool SendWithRespond(SocketConnection connection, byte[] data)
        {
            try
            {
                 return _loginServer.SendWithRespond(connection, data);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion
        #region Events
        private void LoginEventHandler(object sender, SocketConnectionEventArg e)
        {
            if (sender is SocketConnection connection)
            {
                ProcessLoginDataReceived(connection, e.Offset, e.Length);
            }
        }
        private void ServerErrorEventHandler(object sender, LoginErrorEventArgs e)
        {
            if(sender is SocketConnection connection)
            {
                LoginManagerEvent?.Invoke(connection, new LoginEventArgs(SocketDataType.Disconnect));
            }
        }
        #endregion
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);  
        }
        public virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed) return;
            try
            {
                _cancel.Cancel();
                try
                {
                    if (_loginServer != null)
                    {
                        _loginServer.ServerEvent -= LoginEventHandler;
                        _loginServer.ServerErrorEvent -= ServerErrorEventHandler;
                    }

                    _loginServer?.Dispose();
                    _loginConnectionManager?.Dispose();
                }
                catch { }
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
