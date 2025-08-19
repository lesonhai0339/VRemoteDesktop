using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteServer.Models;

namespace VRemoteDesktop.Events
{
    public class ClientConnectionEventArgs: EventArgs
    {
        public ClientConnectionEventArgs(ClientInfo connectionInfo)
        {
            ConnectionInfo = connectionInfo;
        }

        public ClientInfo ConnectionInfo { get; set; }
    }
}
