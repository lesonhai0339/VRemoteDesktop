using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Domains;
using VRemoteServer.RelayServer.DTOs;
using VRemoteServer.RelayServer.Events;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.Services
{
    /// <summary>
    /// Manager remote desktop connection between two socket client
    /// </summary>
    public interface IRemoteControlManager : IBaseManagement<RemoteConnection>
    {
        bool AddController(string id, SocketConnection controller);
        bool AddControlled(string id, SocketConnection controlled, out RemoteConnection remoteConnection);
        SocketConnection GetPartner(SocketConnection me);
        IEnumerable<SocketConnection> GetPartners(SocketConnection me);
        IEnumerable<RemoteConnection> GetRemoteConnectionBySocketConnection(SocketConnection me);
        bool GetPartner(SocketConnection me, out SocketConnection partner);
        bool GetPartner(string id, SocketConnection me, out SocketConnection partner);
    }
    public class RemoteControlManager : BaseManagement<RemoteConnection>, IRemoteControlManager, IDisposable 
    {
        public bool AddController(string id, SocketConnection controller)
        {
            RemoteConnection remoteConnection = new RemoteConnection(id, controller);
            if (base.Add(id, remoteConnection))
            {
                return true;
            }
            return false;
        }

        public bool AddControlled(string id, SocketConnection controlled, out RemoteConnection remoteConnection)
        {
            remoteConnection = null;

            if (base.Get(id, out remoteConnection))
            {
                remoteConnection.Controlled = controlled;
                return true;
            }
            return false;
        }

        public SocketConnection GetPartner(SocketConnection me)
        {
            var found = Get(v => ReferenceEquals(v.Controller, me) | ReferenceEquals(v.Controlled, me));
            if (found == null) return null;
            return ReferenceEquals(found.Controller, me) ? found.Controlled : found.Controller;
        }

        public IEnumerable<SocketConnection> GetPartners(SocketConnection me)
        {
            var found = GetAll(v => ReferenceEquals(v.Controller, me) | ReferenceEquals(v.Controlled, me));
            if (found == null && found.Count() > 0) return null;
            return found.Select(x => ReferenceEquals(x.Controller, me) ? x.Controlled : x.Controller);
        }

        public IEnumerable<RemoteConnection> GetRemoteConnectionBySocketConnection(SocketConnection me)
        {
            var remoteConnections = GetAll(v => ReferenceEquals(v.Controller, me) | ReferenceEquals(v.Controlled, me));
            return remoteConnections;
        }

        public bool GetPartner(SocketConnection me, out SocketConnection partner)
        {
            partner = null;
            var found = Get(v => ReferenceEquals(v.Controller, me) | ReferenceEquals(v.Controlled, me));

            if (found == null)
                return false;

            partner = ReferenceEquals(found.Controller, me) ? found.Controlled : found.Controller;
            return true;
        }

        public bool GetPartner(string id, SocketConnection me,  out SocketConnection partner)
        {
            partner = null;
            if(Get(id, out var remoteConnection))
            {
                partner = ReferenceEquals(remoteConnection.Controller, me) ? remoteConnection.Controlled : remoteConnection.Controller;
                return true;
            }
            else
            {
                return false;
            }
        }

        public override IEnumerable<RemoteConnection> GetByObject(object obj)
        {
            if(obj is SocketConnection connection)
            {
                return base._keyValuePairs.Values.Where(x 
                    => ReferenceEquals(x.Controller, connection) 
                    || ReferenceEquals(x.Controlled, connection));
            }
            return null;
        }
    }
}
