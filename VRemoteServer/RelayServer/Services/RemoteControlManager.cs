using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Helpers;
using static VRemoteServer.RelayServer.Helpers.DefaultValue.SocketConnectionDefault;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.Services
{
    public interface IRemoteControlManager
    {
        bool AddRemoteConnection(string id, SocketConnection controller);
        void CloseConnection(SocketConnection connection);
        void InitServer();
        Task StartServer(IPEndPoint ep);
        void CancelServer();
        void Send(SocketConnection connection, byte[] data);
        void Send(SocketConnection connection, int offset, int length);
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
                { SocketDataType.P2PAcceptConnect, null },
                { SocketDataType.P2PRejectConnect, null },
                { SocketDataType.P2PDataSend, null },
                { SocketDataType.Screen, null },
                { SocketDataType.ScreenOk, null },
                { SocketDataType.Chunks, null },
                { SocketDataType.ChunksOk, null },
                { SocketDataType.Keyboard, null },
                { SocketDataType.Clipboard, null },
                { SocketDataType.Mouse, null },
                { SocketDataType.Chat, null },
            };
        }
        #region Properties
        #endregion
        #region Methods
        public bool AddRemoteConnection(string id, SocketConnection controller)
            => _remoteConnectionManager.AddController(id, controller);
        public void CloseConnection(SocketConnection connection)
            => _remoteControlServer.Close(connection);
        private void ParsePacketToData(SocketConnection connection, int dataOffset, int dataLength)
        {
            try
            {
                var (length, type, connectionId) = PacketFactory.GetHeaderDataFromPacket(connection.Reader.Buffer, dataOffset, dataLength);
                Console.WriteLine("TotalLength Received: " + length);

                if (length < 0 || type == SocketDataType.None || string.IsNullOrEmpty(connectionId))
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
                string[] info = Encoding.ASCII.ByteArrayToStringWithSeparator(connection.Reader.Buffer, dataOffset + PACKET_HEADER_LENGTH, dataLength - PACKET_HEADER_LENGTH, DefaultValue.Common.SEPARATOR);
                RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(ServerEventType.P2PRequestConnect, socketId, info[1],  dataOffset, dataLength));
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
        public void Send(SocketConnection connection, int offset, int length)
        {
            try
            {
                _remoteControlServer.Send(connection, offset, length);
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
