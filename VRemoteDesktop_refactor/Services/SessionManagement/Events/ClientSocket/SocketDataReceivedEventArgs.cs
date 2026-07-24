using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;

namespace Vsign4.VRemoteDesktop.Services.SessionManagement.Events.ClientSocket
{
    public class SocketDataReceivedEventArgs : EventArgs
    {
        public SocketDataReceivedEventArgs(SocketDataType type, byte[] data)
        {
            Type = type;
            Data = data;
        }

        public SocketDataType Type { get; set; }
        public byte[] Data { get; set; }
    }
}
