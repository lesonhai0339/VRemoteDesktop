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
    public interface IRemoteConnectionManager: IBaseManagement<RemoteConnection>
    {
        bool AddController(string id, SocketConnection controller);
        bool AddControlled(string id, SocketConnection controlled, out RemoteConnection remoteConnection);
        SocketConnection GetPartner(SocketConnection me);
        bool GetPartner(SocketConnection me, out SocketConnection partner);
        bool GetPartner(string id, SocketConnection me, out SocketConnection partner);
    }
    public class RemoteControlManager : BaseManagement<RemoteConnection>, IRemoteConnectionManager, IDisposable 
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
    }
}
