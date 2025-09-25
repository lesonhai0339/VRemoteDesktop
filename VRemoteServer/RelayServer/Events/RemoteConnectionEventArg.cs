using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Events
{
    public class RemoteConnectionEventArg: EventArgs
    {
        public RemoteConnectionEventArg(SocketConnectionEventArg socketConnectionEvent)
        {
            SocketConnectionEvent = socketConnectionEvent;
        }
        public SocketConnectionEventArg SocketConnectionEvent { get;set; }  
    }
}
