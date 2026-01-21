using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Services.VTCPClient.Events
{
    public class SocketDataReceivedEventArgs: EventArgs
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
