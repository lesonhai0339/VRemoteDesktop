using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.DTOs;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;

namespace VRemoteServer.RelayServer.Services
{
    public interface ILoginManagerService
    {
        void P2PLoginFailed(SocketConnection connection, string connectionId);
        void Send(SocketConnection connection, byte[] data);
        bool SendWithRespond(SocketConnection connection, byte[] data);
        bool GetFirst(string id, string password, out ConnectionInfo connectionInfo);
        void Ping(SocketConnection connection);
        bool Add(SocketConnection connection, byte[] data, out ConnectionInfo connectionInfo);
        void LoginSucceeded(SocketConnection connection, ConnectionInfo connectionInfo);
        void LoginFailed(SocketConnection connection);
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
        private bool _disposed;
        private readonly ILoginServer _loginServer;
        private readonly ILoginManager _loginConnectionManager;

        public event EventHandler<LoginEventArgs> LoginManagerEvent;
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
        public void Ping(SocketConnection connection)
        {
            connection.UpdateTime();
        }
        public bool GetFirst(string id, string password, out ConnectionInfo connectionInfo)
        {
            connectionInfo = null;
            Func<ConnectionInfo, bool> predicate = (c) => c.Id == id && c.Password == password;
            connectionInfo = _loginConnectionManager.GetFirst(predicate); 
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
                byte[] packet = PacketFactory.CreatePacket(SocketDataType.Disconnect, EMPTY_ID);
                Send(connection, packet);
            }
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
        public void P2PLoginFailed(SocketConnection connection, string connectionId)
        {
            byte[] packet = PacketFactory.CreatePacket(SocketDataType.P2PLoginFailed, connectionId);
            Send(connection, packet);
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
        public void LoginSucceeded(SocketConnection connection, ConnectionInfo connectionInfo)
        {
            try
            {
                byte[] data = Encoding.ASCII.StringToByteArray(connectionInfo.ToNetworkString());
                byte[] packet = PacketFactory.CreatePacket(SocketDataType.Login, connectionInfo.Id, data);
                Send(connection, packet);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "ProcessLoginFailed error");
            }
        }
        public void LoginFailed(SocketConnection connection)
        {
            try
            {
                byte[] packet = PacketFactory.CreatePacket(SocketDataType.LoginFailed, EMPTY_ID);
                Send(connection, packet);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "ProcessLoginFailed error");
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
