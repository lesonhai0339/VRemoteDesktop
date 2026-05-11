using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;

namespace VRemoteServer.RelayServer.Events
{
    public class RemoteControlManagerEventArgs : EventArgs
    {
        public RemoteControlManagerEventArgs(SocketDataType type, string connectionId, object data = null)
        {
            Type = type;
            ConnectionId = connectionId;
            Data = data;
        }

        public SocketDataType Type { get; set; }
        public string ConnectionId { get; set; }
        public object Data { get; set; }
    }
}
