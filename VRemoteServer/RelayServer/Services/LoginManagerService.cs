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
        bool RemoveLogin(SocketConnection connection);
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
        private readonly Dictionary<SocketDataType, Action<SocketConnection, byte[]>> _loginMethods;

        public event EventHandler<LoginEventArgs> LoginManagerEvent;
        public LoginManagerService(ILoginServer loginServer, ILoginManager loginConnectionManager)
        {
            _disposed = false;
            _loginServer = loginServer;
            _loginConnectionManager = loginConnectionManager;

            _loginMethods = new Dictionary<SocketDataType, Action<SocketConnection, byte[]>>
            {
                {SocketDataType.Login, ProcessLogin},
                {SocketDataType.Disconnect,  ProcessDisconnected}
            };

            //Register event
            _loginServer.ServerEvent += LoginEventHandler;
            _loginServer.ServerErrorEvent += ServerErrorEventHandler;
        }
        #region Properties
        #endregion
        #region Methods
        public bool TryGetLoggedConnection(string id, out ConnectionInfo connectionInfo)
            => _loginConnectionManager.Get(id, out connectionInfo);
        public bool GetConnectionsInfoBySocketConnection(SocketConnection connection, out List<ConnectionInfo> connectionsInfo)
            => _loginConnectionManager.GetConnectionsInfoBySocketConnection(connection, out connectionsInfo);
        public bool RemoveLogin(SocketConnection connection)
            => _loginConnectionManager.RemoveLoginInfoBySocketConnection(connection);
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

        private void ProcessLoginDataReceived(SocketConnection connection, int dataOffset, int dataLength)
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

                if(_loginMethods.TryGetValue(type, out var method))
                {
                    method(connection, data);
                }
                else
                {
                    Log.ForContext("FileName", this.GetType().Name).Error("Packet type does not match any method, ignore");
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessSocketData error on IP: {connection.IP}");
            }
        }
        private void ProcessLogin(SocketConnection connection, byte[] data)
        {
            try
            {
                if (_loginConnectionManager.NewConnectionInfo(data, connection, out var connectionInfo))
                {
                    ProcessLoginSucceeded(connection, connectionInfo);
                    Log.ForContext("FileName", this.GetType().Name).Information($"Login success on IP: {connection.IP}");
                }
                else
                {
                    ProcessLoginFailed(connection);
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "Login error");
            }
        }
        private void ProcessLoginSucceeded(SocketConnection connection, ConnectionInfo connectionInfo)
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
        private void ProcessLoginFailed(SocketConnection connection)
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
        private void ProcessDisconnected(SocketConnection connection, byte[] data)
        {
            LoginManagerEvent?.Invoke(connection, new LoginEventArgs(ServerEventType.ConnectionDisconnected));
        }
        private void Send(SocketConnection connection, byte[] data)
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
                if (_loginConnectionManager.RemoveLoginInfoBySocketConnection(connection))
                {
                    Console.WriteLine("Remove login info success");
                }
                else
                {
                    Console.WriteLine("Remove login info failed");
                }
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
                    _loginMethods?.Clear();
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
