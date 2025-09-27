using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;

namespace VRemoteServer.RelayServer.Events
{
    public class SocketConnectionManagerEventArg: EventArgs 
    {
        public SocketConnectionManagerEventArg(SocketConnectionManagerEventType type)
        {
            Type= type;
            ConnectionEvent = null;
        }
        public SocketConnectionManagerEventArg(SocketConnectionManagerEventType type, SocketConnectionEventArg connectionEvent)
        {
            Type = type;
            ConnectionEvent = connectionEvent; 
        }
        public SocketConnectionManagerEventType Type { get; set; }
        public SocketConnectionEventArg ConnectionEvent { get; set; }
    }
}
