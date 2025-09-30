using System;
using System.Collections.Concurrent;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Events;

namespace VRemoteDesktop.Services.VTCPClient
{
    public class VClientManager : IDisposable
    {
        private bool _disposed = false;
        private readonly ConcurrentDictionary<string, VClient> _connections;
        public EventHandler<P2PClientDataReceived> ClientDataReceived;
        public VClientManager()
        {
            _connections = new ConcurrentDictionary<string, VClient>();
        }
        public ConcurrentDictionary<string, VClient> Connections => _connections;
        public bool HasClientOfType(VClientType type)
        {
            foreach(var connection in _connections)
            {
                if(connection.Value.ClientType == type)
                    return true;
            }
            return false;
        }
        public void Add(string id, VClient client)
        {
            if (!_connections.TryAdd(id, client))
            {
                (client as IDisposable)?.Dispose();
                throw new InvalidOperationException(string.Format("Client with Id:{0} already exists", id));
            }
            if(client != null)
            {
                client.TCPClientReceived += TCPClientResponseEventHandler;
                client.SocketDisposing += SocketDisposingEventHandler;
            }
        }

        private void SocketDisposingEventHandler(object sender, SocketDisposeEventArgs e)
        {
            if(sender is VClient client)
            {
                Remove(client);
            }
        }
        public bool Remove(string id)
        {
            if (_connections.TryRemove(id, out var client))
            {
                client.TCPClientReceived -= TCPClientResponseEventHandler;
                client.SocketDisposing -= SocketDisposingEventHandler;
                (client as IDisposable)?.Dispose();
                return true;
            }
            throw new InvalidOperationException(string.Format("Cannot remove connection with Id:{0}", id));
        }
        public bool Remove(VClient client)
        {
            if(client != null)
            {
                client.TCPClientReceived -= TCPClientResponseEventHandler;
                client.SocketDisposing -= SocketDisposingEventHandler;
                if (_connections.TryRemove(client.SocketId, out _))
                {
                    return true;
                }
            }
            throw new InvalidOperationException(string.Format("Cannot remove connection with Id:{0}", client.SocketId));
        }
        public VClient GetByKey(string id)
        {
            if (_connections.TryGetValue(id, out var client))
            {
                return client;
            }
            throw new InvalidOperationException(string.Format("Connection with Id:{0} does not exists", id));
        }
        public VClient New(string id, VClientType type)
        {
            if(_connections.TryGetValue(id, out var existed))
            {
                return existed;
            }

            VClient client = new VClient(id, type);
            Add(id, client);
            return client;
        }
        private void TCPClientResponseEventHandler(object sender, P2PClientDataReceived e)
        {
            ClientDataReceived?.Invoke(sender, e);
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if(_disposed) return;
                foreach(var connection in _connections)
                {
                    connection.Value?.Dispose();
                }
                _connections.Clear();
                _disposed = true;
            }
        }
    }
}
