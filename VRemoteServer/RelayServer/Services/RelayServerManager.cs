using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.Models;
using VRemoteServer.RelayServer.DTOs;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;

namespace VRemoteServer.RelayServer.Services
{
    public interface IRelayServerManager
    {
        void InitServer();
        void StartServer(IPEndPoint ep = null);
        void Dispose();
    }
    public class RelayServerManager : IRelayServerManager, IDisposable
    {
        private bool _disposed; 
        private readonly ISocketConnectionManager _socketConnectionManager;
        private readonly IRemoteConnectionManager _remoteConnectionManager;
        private readonly IServer _server;
        private readonly Dictionary<SocketDataType, Action<SocketConnection, string, byte[]>> _methods;
        public RelayServerManager(ISocketConnectionManager connectionManager,
            IRemoteConnectionManager remoteConnectionManager,
            IServer server)
        {
            _disposed = false;
            _socketConnectionManager = connectionManager;
            _remoteConnectionManager = remoteConnectionManager;
            _server = server;

            //Register events
            _server.ServerEvent += ServerEventHandler;


            //Register methods
            _methods = new Dictionary<SocketDataType, Action<SocketConnection, string, byte[]>>
            {
                {SocketDataType.Connect, ProcessConnect },
                {SocketDataType.Login, ProcessLogin},
            };
        }
        #region Methods
        public void InitServer()
        {
            try
            {
                _server.Init();
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "InitServer Failed");
            }
        }
        public void StartServer(IPEndPoint ep = null)
        {
            try
            {
                if (ep == null)
                {
                    ep = new IPEndPoint(IPAddress.Any, DefaultValue.Common.DEFAULT_PORT);
                }
                _server.Start(ep);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "StartServer Failed");
            }
        }
        #endregion
        #region Events
        private void ServerEventHandler(object sender, ServerEventArg e)
        {
            if(sender is SocketConnection connection)
            {
                ProcessSocketData(connection, e);
            }
        }
        private void ProcessSocketData(SocketConnection connection, ServerEventArg e)
        {
            try
            {
                if (e.Offset < 0)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(e.Offset)), "ProcessSocketData error");
                    return;
                }
                if (e.Length < 0)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(e.Length)), "ProcessSocketData error");
                    return;
                }
                if (connection.SAEA == null || connection.SAEA.Buffer == null)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentNullException(nameof(connection.SAEA)), "ProcessSocketData error");
                    return;
                }
                var buffer = connection.SAEA.Buffer;
                int offset = e.Offset + PACKET_SIZE_INDEX;
                int dataLength = e.Length - PACKET_HEADER_LENGTH;

                byte[] data = new byte[dataLength];

                //Packet size
                int packetSize = BitConverter.ToInt32(buffer, offset);
                offset += PACKET_SIZE_LENGTH;
                if (packetSize != e.Length)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new ArgumentException(nameof(e)), "Missing some data");
                    return;
                }

                //Packet type
                SocketDataType type = (SocketDataType)buffer[offset];
                offset += PACKET_TYPE_LENGTH;

                //Id
                string id = Encoding.ASCII.ByteArrayToString(buffer, offset, PACKET_ID_LENGTH);
                offset += PACKET_ID_LENGTH;

                //Payload
                Buffer.BlockCopy(buffer, offset, data, 0, dataLength);

                //Direct to specific method by SocketDataType
                if (_methods.TryGetValue(type, out var method))
                {
                    method(connection, id, data);
                }
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, $"ProcessSocketData error on IP: {connection.IP}");
            }
        }
        private void ProcessConnect(SocketConnection connection, string arg2, byte[] arg3)
        {
            //TODO
        }
        private void ProcessLogin(SocketConnection connection,string id, byte[] data)
        {
            try
            {
                if(_socketConnectionManager.NewConnectionInfo(data, connection, out var connectionInfo))
                {
                    ProcessLoginSucceeded(connection, connectionInfo);
                    Log.ForContext("FileName", this.GetType().Name).Information($"Login success on IP: {connection.IP}");
                }
                else
                {
                    ProcessLoginFailed(connection);
                }
            }
            catch (Exception ex)
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
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "ProcessLoginFailed error");
            }
        }

        private void Send(SocketConnection connection, byte[] data)
        {
            try
            {
                _server.Send(connection.SAEA, data);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
            }
        }
        private void RemoteSend(SocketConnection connection, byte[] data)
        {
            try
            {
                var partner = _remoteConnectionManager.GetPartner(connection);
                if (partner == null)
                {
                    Log.ForContext("FileName", this.GetType().Name).Error(new InvalidOperationException(nameof(partner)), "Cannot found partner");
                    return;
                }
                _server.Send(partner.SAEA, data);
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
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
            if (!disposing || _disposed) return;

            try
            {

                try
                {
                    if (_server != null)
                        _server.ServerEvent -= ServerEventHandler;

                    _socketConnectionManager?.Dispose();
                    _remoteConnectionManager?.Dispose();
                    _server?.Dispose();
                    _methods.Clear();
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
