using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.SessionManagement.DTOs;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSocket
{
    public class SocketSendCompletedEventArgs : EventArgs
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
