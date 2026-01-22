using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Services.SessionManagement.Events
{
    public class SocketSendCompletedEventArgs: EventArgs
    {
        public SocketSendCompletedEventArgs(string sessionId, Sendstate state = null)
        {
            SessionId = sessionId;
            State = state;
        }
        public string SessionId { get; set; }
        public Sendstate State { get; set; }

    }
}
