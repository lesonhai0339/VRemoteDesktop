using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.RemoteDesktop.Events
{
    public class RemoteDesktopErrorEventArgs: EventArgs
    {
        public RemoteDesktopErrorEventArgs(string sessionId, string errorMessage)
        {
            SessionId = sessionId;
            ErrorMessage = errorMessage;
        }
    
        public string SessionId { get; set; }   
        public string ErrorMessage { get; set; }
    }
}
