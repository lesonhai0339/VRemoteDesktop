using Serilog;
using System;
using System.Collections.Concurrent;
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
    public interface IRemoteControlManagerService
    {
        bool CreateRoomId(out string id);
        SocketConnection GetPartner(SocketConnection me);
        bool GetPartner(SocketConnection me, out SocketConnection partner);
        bool GetPartner(string id, SocketConnection me, out SocketConnection partner);
        IEnumerable<SocketConnection> GetPartners(SocketConnection me);
        IEnumerable<RemoteConnection> GetRemoteConnectionsBySocketConnection(SocketConnection connection);
        bool RemoveRemoteConnection(string id);
        bool InitRemoteConnection(string id, SocketConnection controller);
        bool EstablishedRemoteConnection(string id, SocketConnection controlled, out RemoteConnection remoteConnection);
        void P2PConnectFailed(SocketConnection connection, string connectionId);
        void InitServer();
        Task StartServer(IPEndPoint ep);
        void CancelServer();
        void Send(SocketConnection connection, byte[] data);
        void Send(SocketConnection connection, int offset, int length);
        event EventHandler<RemoteControlManagerEventArgs> RemoteControlManagerEvent;
        void Dispose();
    }
    public class RemoteControlManagerService : IRemoteControlManagerService, IDisposable
    {
        private const int MAX_RETRY = 3;
        private const int TIMEOUT = 300;
        private bool _disposed;
        private Task _timeoutTask;
        private readonly ConcurrentDictionary<string , long> _acceptId = new();
        private readonly IRemoteControlServer _remoteControlServer;
        private readonly IRemoteControlManager _remoteConnectionManager;
        private CancellationTokenSource _cancel = new();
        public event EventHandler<RemoteControlManagerEventArgs> RemoteControlManagerEvent;
        public RemoteControlManagerService(IRemoteControlServer remoteControlServer, IRemoteControlManager remoteConnectionManager)
        {
            _disposed = false;
            _remoteControlServer = remoteControlServer;
            _remoteConnectionManager = remoteConnectionManager;

            //Register events
            _remoteControlServer.ServerEvent += RemoteControlEventHandler;
            _remoteControlServer.ServerErrorEvent += ServerErrorEventHandler;
            _timeoutTask = Task.Run(async () =>
            {
                try
                {
                    while (!_cancel.IsCancellationRequested)
                    {
                        var now = Environment.TickCount64;
                        var timeoutIds = _acceptId.Where(x => (now - x.Value) > TIMEOUT * 1000).Select(x => x.Key).ToList();
                        foreach (string timeoutId in timeoutIds)
                        {
                            _acceptId.TryRemove(timeoutId, out _);
                        }
                        await Task.Delay(3000);
                    }
                }
                catch { }
            }, _cancel.Token);

        }
        #region Properties
        #endregion
        #region Methods
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

        public IEnumerable<SocketConnection> GetPartners(SocketConnection me)
            => _remoteConnectionManager.GetPartners(me);

        public IEnumerable<RemoteConnection> GetRemoteConnectionsBySocketConnection(SocketConnection connection)
            => _remoteConnectionManager.GetRemoteConnectionBySocketConnection(connection);

        public bool RemoveRemoteConnection(string id)
            => _remoteConnectionManager.Remove(id);

        public bool CreateRoomId(out string id)
        {
            int retry = 0;
            string tempId = RandomString.RandomStringNumber(8);
            while (_acceptId.ContainsKey(tempId) && retry < MAX_RETRY)
            {
                tempId = RandomString.RandomStringNumber(8);
                retry++;
            }
            if (_acceptId.TryAdd(tempId, Environment.TickCount64))
            {
                id = tempId;
                return true;
            }

            id = null;
            return false;
        }
        private void ParseRequestToConnectHeader(SocketConnection connection, int dataOffset, int dataLength)
        {
            try
            {
                var (length, type, connectionId) = PacketFactory.GetHeaderDataFromPacket(connection.Reader.Buffer, dataOffset, dataLength);
                if (length < 0 || type == SocketDataType.None || string.IsNullOrEmpty(connectionId))
                {
                    return;
                }
                RemoteControlRequestToConnect(connection, connectionId, dataOffset, dataLength);
            }
            catch (Exception ex)
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

                RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(type: SocketDataType.RemoteControlRequestToConnect, socketId: socketId, partnerId: info[1], data: data));
            }
            catch (Exception ex)
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, $"RemoteControlRequestToConnect error");
            }
        }

        public void P2PConnectFailed(SocketConnection connection, string connectionId)
        {
            byte[] packet = PacketFactory.CreatePacket(SocketDataType.RemoteControlConnectFailed, connectionId);
            Send(connection, packet);
        }

        public void Send(SocketConnection connection, byte[] data)
        {
            try
            {
                _remoteControlServer.Send(connection, data);
            }
            catch { }
        }

        public void Send(SocketConnection connection, int offset, int length)
        {
            try
            {
                _remoteControlServer.Send(connection, offset, length);
            }
            catch { }
        }

        #endregion
        #region Events
        private void ServerErrorEventHandler(object sender, RemoteControlErrorEventArgs e)
        {
            if(sender is SocketConnection connection)
            {
               RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(type: SocketDataType.RemoteControlDisconnect));
            }
        }

        private void RemoteControlEventHandler(object sender, SocketConnectionEventArg e)
        {
            if (sender is SocketConnection connection)
            {
                connection.UpdateTime();
                if(e.Type == SocketDataType.RemoteControlRequestToConnect)
                {
                    ParseRequestToConnectHeader(connection, e.Offset, e.Length);
                }
                else
                {
                    RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(type: e.Type, socketId: e.Id, data: e.Data, dataOffset: e.Offset, dataLength: e.Length));
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
                _cancel.Cancel();

                if(_remoteControlServer != null)
                {
                    _remoteControlServer.ServerEvent -= RemoteControlEventHandler;
                    _remoteControlServer.ServerErrorEvent -= ServerErrorEventHandler;
                }

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
