using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Presenters.DTOs
{
    public enum ConnectStatusEnum
    {
        None,
        Connecting,
        Connected,
        Disconnected,
    }
    public class ConnectStatus
    {
        public ConnectStatus(ConnectStatusEnum type, string message)
        {
            Type = type;
            Message = message;
        }

        public ConnectStatusEnum Type { get; private set; }
        public string Message { get; private set; }
    }
}
