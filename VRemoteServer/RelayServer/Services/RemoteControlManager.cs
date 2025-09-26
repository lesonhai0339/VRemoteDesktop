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
        event EventHandler<RemoteConnectionEventArg> remoteSocketManagerEvent;
    }
    public class RemoteControlManager : BaseManagement<RemoteConnection>, IRemoteConnectionManager, IDisposable 
    {
        public event EventHandler<RemoteConnectionEventArg> remoteSocketManagerEvent;
        public bool AddController(string id, SocketConnection controller)
        {
            RemoteConnection remoteConnection = new RemoteConnection(id, controller);
            if (base.Add(id, remoteConnection))
            {
                controller.SocketConnectionEvent += SocketConnectionEventHandler;
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
                controlled.SocketConnectionEvent += SocketConnectionEventHandler;
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
        public override bool Remove(string id)
        {
            if(Get(id, out var remoteConnection))
            {
                if(remoteConnection.Controller != null)
                {
                    remoteConnection.Controller.SocketConnectionEvent -= SocketConnectionEventHandler;
                }
                if (remoteConnection.Controlled != null)
                {
                    remoteConnection.Controller.SocketConnectionEvent -= SocketConnectionEventHandler;
                }
            }
            return base.Remove(id);
        }
        public override void Dispose()
        {
            foreach(var remoteConnection in this.GetAll())
            {
                if (remoteConnection.Controller != null)
                {
                    remoteConnection.Controller.SocketConnectionEvent -= SocketConnectionEventHandler;
                }
                if (remoteConnection.Controlled != null)
                {
                    remoteConnection.Controller.SocketConnectionEvent -= SocketConnectionEventHandler;
                }
            }
            base.Dispose();
        }
        private void SocketConnectionEventHandler(object sender, SocketConnectionEventArg e)
        {
            remoteSocketManagerEvent?.Invoke(sender, new RemoteConnectionEventArg(e.Type, e.Id, e.Data, e.Offset, e.Length));
        }
    }
}
