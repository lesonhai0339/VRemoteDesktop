using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSocket
{
    public class SocketDisconnectedEventArgs : EventArgs
    {
        public SocketDisconnectedEventArgs(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; set; }
    }
}
