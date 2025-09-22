using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.DTOs
{
    internal class RemoteConnection
    {
        public RemoteConnection(string connectionId, SocketConnection sender)
        {
            ConnectionId = connectionId;
            Controller = sender;
        }
        public RemoteConnection(SocketConnection controller, SocketConnection controlled)
        {
            Controller = controller;
            Controlled = controlled;
        }
        public RemoteConnection(string connectionId, SocketConnection controller, SocketConnection controlled)
        {
            ConnectionId = connectionId;
            Controller = controller;
            Controlled = controlled;
        }
        public string ConnectionId { get; set; }
        public SocketConnection Controller { get; set; }
        public SocketConnection Controlled { get; set; }
    }
}
