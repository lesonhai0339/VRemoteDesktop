using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.DTOs
{
    public class RemoteConnection
    {
        public RemoteConnection(string connectionId ,ControlType type, SocketConnection connection)
        {
            ConnectionId = connectionId;
            if(type == ControlType.Controller)
            {
                Controller = connection;
            }
            else
            {
                Controlled = connection;
            }
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
        public long CreateTime { get; set; } = Environment.TickCount64;
        public bool ReadyToRemote()
        {
            return Controller != null && Controller != null;
        }
    }
}
