using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using static VRemoteDesktopServer.Enums;

namespace VRemoteDesktopServer
{
    internal class ConnectionsManager
    {
        private Dictionary<string, Connection> _remoteConnections;
        private readonly object _lockObject = new object();

        public ConnectionsManager()
        {
            _remoteConnections = new Dictionary<string, Connection>();
        }
        public void Add(string id, Socket owner, Socket partner)
        {
            lock (_lockObject)
            {
                if(_remoteConnections.TryGetValue(id, out var existingConnection))
                {
                    existingConnection.Owner = owner ?? existingConnection.Owner;
                    existingConnection.Partner = partner ?? existingConnection.Partner;
                }
                else
                {
                    Connection newCon = new Connection(
                        id: id,
                        owner: owner,
                        partner: partner
                    );
                    _remoteConnections[id] = newCon;
                }
            }    
        }
        public void Remove(string id)
        {
            lock (_lockObject)
            {
                if (_remoteConnections.TryGetValue(id, out var connection))
                {
                    connection.Dispose();
                    _remoteConnections.Remove(id);
                }
            }
        }
        public bool IsExisted(string id)
        {
            lock (_lockObject)
            {
                if (_remoteConnections.TryGetValue(id, out var connection))
                {
                    return true;
                }
                return false;
            }
        }
        public Connection Get(string id)
        {
            lock (_lockObject)
            {
                if (_remoteConnections.TryGetValue(id, out var connection))
                {
                    return connection;
                }
                return null;
            }
        }
        public bool UpdatePing(string id, RemoteType isOwner)
        {
            lock (_lockObject)
            {
                if(_remoteConnections.TryGetValue(id, out var connection))
                {
                    if (isOwner == RemoteType.OWNER)
                    {
                        connection.LastOwnerPing = DateTime.Now;
                    }
                    else
                    {
                        connection.LastPartnerPing = DateTime.Now;
                    }
                    return true;
                }
                return false;
            }
        }
        public void Dispose()
        {
            lock (_lockObject)
            {
                foreach (var connection in _remoteConnections.Values)
                {
                    connection.Dispose();
                }
                _remoteConnections.Clear();
            }
        }
    }
    public class Connection: IDisposable
    {
        public Connection()
        {

        }
        public Connection(string id, Socket owner, Socket partner)
        {
            ConnectionId = id;
            Owner = owner;
            Partner = partner;
            LastOwnerPing = null;
            LastPartnerPing = null;
        }
        public string ConnectionId { get; set; }
        public Socket Partner { get; set; }
        public Socket Owner { get; set; }
        public DateTime? LastPartnerPing { get; set; }
        public DateTime? LastOwnerPing { get; set; }

        public void Dispose()
        {
            Owner?.Close();
            Partner?.Close();
            Owner?.Dispose();
            Partner?.Dispose();
        }
    }
}
