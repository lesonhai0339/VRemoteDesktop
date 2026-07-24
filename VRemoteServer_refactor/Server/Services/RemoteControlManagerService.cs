using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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
using VRemoteServer.RelayServer.DTOs.Requests;
using VRemoteServer.RelayServer.DTOs.Responses;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Helpers;
using VRemoteServer.RelayServer.Networking;
using VRemoteServer.Server.Options;

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
        void RemoveRemoteConnection(string id, SocketConnection connection);
        bool InitRemoteConnection(string id, SocketConnection controller);
        bool EstablishedRemoteConnection(string id, SocketConnection controlled, out RemoteConnection remoteConnection);
        void P2PConnectFailed(SocketConnection connection, string connectionId);
        void AddOrUpdateRemoteControl(string connectionId, SocketConnection connection, SocketPacket packet);
        void InitServer();
        Task StartServer(IPEndPoint ep, CancellationToken token );
        void CancelServer();
        void Send(SocketConnection connection, byte[] data);
        void Send(SocketConnection connection, int offset, int length);
        event EventHandler<RemoteControlManagerEventArgs> RemoteControlManagerEvent;
        void Dispose();
    }
    public class RemoteControlManagerService : IRemoteControlManagerService, IDisposable ,IAsyncDisposable
    {
        private int _disposed =0;

        private int _retry;
        private int _timeout;
        private int _headerLength;
        private Task _timeoutTask;
        private Task _timeoutTask2;

        private readonly ConcurrentDictionary<string , long> _acceptId = new();

        private readonly IRemoteControlServer _remoteControlServer;
        private readonly IRemoteControlManager _remoteConnectionManager;
        private CancellationTokenSource _cancel = new();
        public event EventHandler<RemoteControlManagerEventArgs> RemoteControlManagerEvent;
        public RemoteControlManagerService(IRemoteControlServer remoteControlServer, IRemoteControlManager remoteConnectionManager, IOptions<ServerOptions> options)
        {
            _retry = options.Value.RetryTime;
            _timeout = options.Value.MaxTimeout;
            _headerLength = options.Value.HeaderLength;

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
                        var timeoutIds = _acceptId.Where(x => (now - x.Value) > _timeout * 1000).Select(x => x.Key).ToList();
                        foreach (string timeoutId in timeoutIds)
                        {
                            _acceptId.TryRemove(timeoutId, out _);
                        }
                        await Task.Delay(3000);
                    }
                }
                catch { }
            }, _cancel.Token);
            _timeoutTask2 = Task.Run(async () =>
            {
                try
                {
                    while (!_cancel.IsCancellationRequested)
                    {
                        RemoveNotActiveConnections();
                        await Task.Delay(30000);
                    }
                }
                catch { }
            }, _cancel.Token);
        }

        #region Properties
        #endregion
        #region Methods
        private void RemoveNotActiveConnections()
        {
            long now = Environment.TickCount64;
            var expired = _remoteConnectionManager.Where(x => ((now - x.CreateTime) > 30_000) && (x.Controller == null || x.Controlled == null)).ToArray();
            foreach(var item in expired)
            {
                var controller = item.Value?.Controller;   
                var controlled = item.Value?.Controlled;   
                if(controller != null)
                {
                    Send(controller, SocketDataType.RemoteControlDisconnect);
                }
                if (controlled != null)
                {
                    Send(controlled, SocketDataType.RemoteControlDisconnect);
                }
                _remoteConnectionManager.Remove(item.Key);  
            }
        }
        public void InitServer()
        {
            _remoteControlServer.Init();
        }

        public async Task StartServer(IPEndPoint ep, CancellationToken token)
        {
            if (ep == null)
                throw new ArgumentNullException(nameof(ep));

            await _remoteControlServer.Start(ep, token);
        }

        public void CancelServer()
        {
            _remoteControlServer.Cancel();
        }
        public void AddOrUpdateRemoteControl(string connectionId, SocketConnection connection, SocketPacket packet)
        {

            try
            {
                var credentials = ParseRequestToConnectHeader(connection, packet.Offset, packet.Length);
                if(credentials == null)
                {
                    Send(connection, SocketDataType.RemoteLoginFailed);
                    return; 
                }

                var existed = _remoteConnectionManager.GetFirst(x => x.ConnectionId.Equals(connectionId));
                if (existed != null)
                {
                    if(credentials.Type == ControlType.Controller && existed.Controller == null && existed.Controlled != null)
                    {
                        existed.Controller = connection;
                    }
                    else if(credentials.Type == ControlType.Controlled && existed.Controlled == null && existed.Controller != null)
                    {
                        existed.Controlled = connection;
                    }

                    if (existed.ReadyToRemote())
                    {
                        Send(existed.Controller, SocketDataType.ReadyToRemoteController);
                        Send(existed.Controlled, SocketDataType.ReadyToRemoteControlled);
                        Log.ForContext("FileName", "TURN").Information($@"Established: {existed.ConnectionId} - {existed.Controller.IP} - {existed.Controlled.IP} - {DateTimeOffset.UtcNow.ToString("HH:mm:ss-dd/MM/yyyy")}");
                    }
                }
                else
                {
                    if (!_acceptId.ContainsKey(connectionId))
                        throw new TimeoutException("Id Exceed time");

                    var remoteConnection = new RemoteConnection(connectionId, credentials.Type, connection);
                    _remoteConnectionManager.AddOrUpdate(connectionId, remoteConnection);
                }
            }
            catch
            {
                throw;
            }
            finally
            {
                _acceptId.TryRemove(connectionId, out _);
            }
        }
        private ConnectionCredentials ParseRequestToConnectHeader(SocketConnection connection, int dataOffset, int dataLength)
        {
            try
            {
                var (length, type, connectionId) = PacketFactory.GetHeaderDataFromPacket(connection.Reader.Buffer, dataOffset, dataLength);
                if (length < 0 || type == SocketDataType.None || string.IsNullOrEmpty(connectionId))
                {
                    return null;
                }
                return JsonConvert.DeserializeObject<ConnectionCredentials>(Encoding.ASCII.GetString(connection.Reader.Buffer, dataOffset + _headerLength, dataLength - _headerLength));
            }
            catch
            {
                return null;
            }
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

        public void RemoveRemoteConnection(string id, SocketConnection connection)
        {
            var existed = _remoteConnectionManager.GetFirst(x => x.ConnectionId.Equals(id));
            if(existed != null)
            {
                var partner = _remoteConnectionManager.GetPartner(connection);
                if(partner != null)
                {
                    Send(partner, SocketDataType.RemoteControlDisconnect);
                }

                // Actually tear down the room, otherwise the RemoteConnection lingers in the
                // manager forever with a stale/dead socket reference.
                _remoteConnectionManager.Remove(id);
            }
        }

        public bool CreateRoomId(out string id)
        {
            int retry = 0;
            string tempId = RandomString.RandomStringNumber(8);
            while (_acceptId.ContainsKey(tempId) && retry < _retry)
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
        public void Send(SocketConnection connection, SocketDataType type)
        {
            try
            {
                var packet = PacketFactory.CreatePacket(type);
                _remoteControlServer.Send(connection, packet);
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
                var remoteControlConnections = _remoteConnectionManager.GetByObject(connection);
                foreach(var remoteControlConnection in remoteControlConnections)
                {
                    RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(type: SocketDataType.RemoteControlDisconnect, remoteControlConnection.ConnectionId));
                }
            }
        }

        private void RemoteControlEventHandler(object sender, SocketConnectionEventArg e)
        {
            if (sender is SocketConnection connection)
            {
                connection.UpdateTime();
                RemoteControlManagerEvent?.Invoke(connection, new RemoteControlManagerEventArgs(type: e.Type, connectionId: e.Id, new SocketPacket(e.Data, e.Offset, e.Length)));
            }
        }

        #endregion
        public void Dispose()
        {
            DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            try
            {
                _cancel.Cancel();
                try
                {
                    var pending = new[] { _timeoutTask, _timeoutTask2 }.Where(t => t != null).ToArray();
                    if (pending.Length > 0)
                        await Task.WhenAll(pending);
                }
                catch { }
                _timeoutTask = null;
                _timeoutTask2 = null;

                _cancel?.Dispose();
                _cancel = null;

                if (_remoteControlServer != null)
                {
                    _remoteControlServer.ServerEvent -= RemoteControlEventHandler;
                    _remoteControlServer.ServerErrorEvent -= ServerErrorEventHandler;
                }

                //TODO: dispose here
                _remoteControlServer?.Dispose();
                _remoteConnectionManager?.Dispose();
            }
            catch (Exception ex) 
            {
                Log.ForContext("FileName", this.GetType().Name).Error(ex, "RemoteControlService err ");
            }
        }
    }
}
