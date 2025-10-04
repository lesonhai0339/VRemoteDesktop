using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public enum RemoteDesktopErrorType
    {
        None,
        ConnectionFailed,
        AuthenticationFailed,
        NetworkError,
        UnknownError,
        SelfConnect
    }
    public class RemoteDesktopErrorEventArgs: EventArgs
    {
        public RemoteDesktopErrorEventArgs(RemoteDesktopErrorType type, string message = null)
        {
            ErrorType = type;
            Message = message;
        }
        public RemoteDesktopErrorType ErrorType { get; set; } = RemoteDesktopErrorType.None;
        public string Message { get; set; }
    }
}
