using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.RemoteDesktop.Enums;

namespace Vsign4.VRemoteDesktop.Services.RemoteDesktop.Events
{
    public class RemoteDesktopEventArgs : EventArgs
    {
        public RemoteDesktopEventArgs(ResponseType type, string message)
        {
            Type = type;
            SessionId = string.Empty;
            Data = null;
            Message = message;
        }
        public RemoteDesktopEventArgs(ResponseType type, string sessionId, bool flag = true, byte[] data = null, string message = "")
        {
            Type = type;
            SessionId = sessionId;
            Data = data;
            Message = message;
        }
        public ResponseType Type { get; set; }
        public string SessionId { get; set; }
        public byte[] Data { get; set; }
        public string Message { get; set; }
    }
}
