using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.SessionManagement.Events
{
    public class SocketConnectedEventArgs: EventArgs
    {
        public SocketConnectedEventArgs(string sessionId, bool connected)
        {
            SessionId = sessionId;
            Connected = connected;
        }

        public string SessionId { get; set; }
        public bool Connected { get; set; }
    }
}
