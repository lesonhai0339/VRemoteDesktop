using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class ChatDisconnectedEventArgs:  EventArgs
    {
        public ChatDisconnectedEventArgs(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; set; }
    }
}
