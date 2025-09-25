using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Enums
{
    public enum ServerEventType
    {
        NewConnection,
        ReceivedData,
        LostConnection
    }
}
