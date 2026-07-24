using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.RemoteDesktop.Events
{
    public class RemoteDesktopSessionDisconnectEventArgs : EventArgs
    {
        public RemoteDesktopSessionDisconnectEventArgs(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; private set; }
    }
}
