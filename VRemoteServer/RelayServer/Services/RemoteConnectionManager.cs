using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Domains;
using VRemoteServer.RelayServer.DTOs;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.Services
{
    /// <summary>
    /// Manager remote desktop connection between two socket client
    /// </summary>
    public interface IRemoteConnectionManager: IBaseManagement<RemoteConnection>
    {
        SocketConnection GetPartner(SocketConnection owner);
    }
    public class RemoteConnectionManager : BaseManagement<RemoteConnection>, IRemoteConnectionManager, IDisposable 
    {
        public SocketConnection GetPartner(SocketConnection owner)
        {
            var found = Get(v => ReferenceEquals(v.Controller, owner) | ReferenceEquals(v.Controlled, owner));
            if (found == null) return null;
            return ReferenceEquals(found.Controller, owner) ? found.Controlled : found.Controller;
        }
    }
}
