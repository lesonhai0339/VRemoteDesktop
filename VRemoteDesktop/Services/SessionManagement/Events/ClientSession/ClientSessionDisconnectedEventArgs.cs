using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.SessionManagement.Events.ClientSession
{
    public class ClientSessionDisconnectedEventArgs: EventArgs
    {
        public ClientSessionDisconnectedEventArgs(string sessionId)
        {
            SessionId = sessionId;
        }

            public string SessionId { get; set; }
    }
}
