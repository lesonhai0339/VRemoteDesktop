using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    public interface IRemoteControlManager
    {
        SocketConnection GetPartner(SocketConnection me);
        bool GetPartner(SocketConnection me, out SocketConnection partner);
        bool GetPartner(string id, SocketConnection me, out SocketConnection partner);
        bool RemoveRemoteConnection(string id);
        bool InitRemoteConnection(string id, SocketConnection controller);
        bool EstablishedRemoteConnection(string id, SocketConnection controlled, out RemoteConnection remoteConnection);
        void CloseConnection(SocketConnection connection);
        void InitServer();
        Task StartServer(IPEndPoint ep);
        void CancelServer();
        void Send(SocketConnection connection, byte[] data);
        void Send(SocketConnection connection, byte[] data, bool acceptReceive);
        void Send(SocketConnection connection, int offset, int length, bool acceptReceive);
        event EventHandler<RemoteControlManagerEventArgs> RemoteControlManagerEvent;
        void Dispose();
    }
    public class RemoteControlManager : IRemoteControlManager, IDisposable
    {
        private bool _disposed;
        private readonly IRemoteControlServer _remoteControlServer;
        private readonly IRemoteConnectionManager _remoteConnectionManager;
        private readonly Dictionary<SocketDataType, Action<SocketConnection, string, int, int>> _remoteControlMethods;
        public event EventHandler<RemoteControlManagerEventArgs> RemoteControlManagerEvent;
        public RemoteControlManager(IRemoteControlServer remoteControlServer, IRemoteConnectionManager remoteConnectionManager)
        {
            _disposed = false;
            _remoteControlServer = remoteControlServer;
            _remoteConnectionManager = remoteConnectionManager;

            //Register events
            _remoteControlServer.ServerEvent += RemoteControlEventHandler;
            _remoteConnectionManager.remoteSocketManagerEvent += RemoteSocketManagerEventHandler;

            _remoteControlMethods = new Dictionary<SocketDataType, Action<SocketConnection, string, int, int>>
            {
                { SocketDataType.P2PRequestConnect, RemoteControlRequestToConnect },
                { SocketDataType.P2PAcceptConnect, RemoteControlAcceptedToConnect },
                { SocketDataType.P2PRejectConnect, RemoteControlRefusedToConnect },
                { SocketDataType.P2PDataSend, RemoteControlP2PDataTransfer },
                { SocketDataType.Screen, RemoteControlP2PDataTransfer },
                { SocketDataType.ScreenOk, RemoteControlP2PDataTransfer },
                { SocketDataType.Chunks, RemoteControlP2PDataTransfer },
                { SocketDataType.ChunksOk, RemoteControlP2PDataTransfer },
                { SocketDataType.Keyboard, RemoteControlP2PDataTransfer },
                { SocketDataType.Clipboard, RemoteControlP2PDataTransfer },
                { SocketDataType.Mouse, RemoteControlP2PDataTransfer },
                { SocketDataType.Chat, RemoteControlP2PDataTransfer },
            };
        }

        private void RemoteControlP2PDataTransfer(SocketConnection connection, string connectionId, int offset, int length)
        {
            RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(type: SocketDataType.P2PDataSend, socketId: connectionId, dataOffset: offset, dataLength: length));
        }

        private void RemoteControlRefusedToConnect(SocketConnection connection, string arg2, int arg3, int arg4)
        {
            throw new NotImplementedException();
        }

        private void RemoteControlAcceptedToConnect(SocketConnection connection, string socketId, int offset, int length)
        {
            RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(type: SocketDataType.P2PAcceptConnect, socketId: socketId, dataOffset: offset, dataLength: length));
        }
        #region Properties
        #endregion
        #region Methods
        public bool InitRemoteConnection(string id, SocketConnection controller)
            => _remoteConnectionManager.AddController(id, controller);
        public bool EstablishedRemoteConnection(string id, SocketConnection controlled, out RemoteConnection remoteConnection)
            => _remoteConnectionManager.AddControlled(id, controlled, out remoteConnection);
        public SocketConnection GetPartner(SocketConnection me)
            => _remoteConnectionManager.GetPartner(me);
        public bool GetPartner(SocketConnection me, out SocketConnection partner)
            => _remoteConnectionManager.GetPartner(me, out partner);
        public bool GetPartner(string id, SocketConnection me, out SocketConnection partner)
            => _remoteConnectionManager.GetPartner(id, me, out partner);
        public bool RemoveRemoteConnection(string id)
            => _remoteConnectionManager.Remove(id);
        public void CloseConnection(SocketConnection connection)
            => _remoteControlServer.Close(connection);
        private void ParsePacketToData(SocketConnection connection, int dataOffset, int dataLength)
        {
            try
            {
                var (length, type, connectionId) = PacketFactory.GetHeaderDataFromPacket(connection.Reader.Buffer, dataOffset, dataLength);
                if(length < 0 || type == SocketDataType.None || string.IsNullOrEmpty(connectionId))
                {
                    Log.ForContext("FileName", this.GetType().Name).Error("ParsePacketToData: Invalid packet header, ignore packet");
                    return;
                }    

                if(_remoteControlMethods.TryGetValue(type, out var matchMethod))
                {
                    matchMethod(connection, connectionId, dataOffset, dataLength);
                }
                else
                {
                    Console.WriteLine($"Unknow method: " + type);
                    Log.ForContext("FileName", this.GetType().Name).Error("ParsePacketToData: Packet type doesn't matched any method, ignore");
                }
            }
            catch(Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "ParsePacketToData error");
            }
        }
        private void RemoteControlRequestToConnect(SocketConnection connection, string socketId, int dataOffset, int dataLength)
        {
            try
            {
                byte[] data = new byte[dataLength];
                Buffer.BlockCopy(connection.Reader.Buffer, dataOffset, data, 0, dataLength);
                string[] info = Encoding.ASCII.ByteArrayToStringWithSeparator(data, PACKET_HEADER_LENGTH, data.Length - PACKET_HEADER_LENGTH, DefaultValue.Common.SEPARATOR);

                RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(type: SocketDataType.P2PRequestConnect, socketId: socketId, partnerId: info[1], data: data));
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, $"RemoteControlRequestToConnect error");
            }
        }
        public void InitServer()
        {
            _remoteControlServer.Init();
        }
        public async Task StartServer(IPEndPoint ep)
        {
            if (ep == null)
                throw new ArgumentNullException(nameof(ep));

            await _remoteControlServer.Start(ep);
        }
        public void CancelServer()
        {
            _remoteControlServer.Cancel();
        }
        public void Send(SocketConnection connection, byte[] data)
        {
            try
            {
                _remoteControlServer.Send(connection, data);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
            }
        }
        public void Send(SocketConnection connection, byte[] data, bool acceptReceive)
        {
            try
            {
                _remoteControlServer.Send(connection, data, acceptReceive);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
            }
        }
        public void Send(SocketConnection connection, int offset, int length, bool acceptReceive)
        {
            try
            {
                _remoteControlServer.Send(connection, offset, length, acceptReceive);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
            }
        }
        private void Receive(SocketConnection connection)
        {
            try
            {
                _remoteControlServer.Receive(connection);
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteSend error");
            }
        }
        #endregion
        #region Events
        private void RemoteControlEventHandler(object sender, RemoteControlEventArgs e)
        {
            if (sender is SocketConnection connection)
            {
                ParsePacketToData(connection, e.Offset, e.Length);
            }
            else
            {
                //TODO: invalid object
                Log.ForContext("FileName", this.GetType().Name).Error("RemoteControlEventHandler invalid object");
            }
        }
        /// <summary>
        /// This methods called when <see cref="SocketConnection.CalCuLateData(int, int)"/> received data and call 
        /// <see cref="SocketConnection.SocketConnectionEvent"/> for each <see cref="SocketConnection"/>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void RemoteSocketManagerEventHandler(object sender, RemoteConnectionEventArg e)
        {
            if(sender is SocketConnection connection)
            {
                //TODO
                Console.WriteLine($"Received {e.Data.Length} bytes on connection id: {e.Id}");
            }
            else
            {
                //TODO: invalid object
                Log.ForContext("FileName", this.GetType().Name).Error("RemoteSocketManagerEventHandler invalid object");
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
                if(_remoteControlServer != null)
                    _remoteControlServer.ServerEvent -= RemoteControlEventHandler;
                if(_remoteConnectionManager != null)
                    _remoteConnectionManager.remoteSocketManagerEvent -= RemoteSocketManagerEventHandler;


                //TODO: dispose here
                _remoteControlServer?.Dispose();
                _remoteConnectionManager?.Dispose();
            }
            finally
            {
                _disposed = true;
            }
        }
    }
}
