using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.SessionManagement.Events
{
    public class SocketDisposingEventArgs: EventArgs
    {
        public SocketDisposingEventArgs(string sessionId)
        {
            SessionId = sessionId;
        }

            public string SessionId { get; set; }
    }
}
