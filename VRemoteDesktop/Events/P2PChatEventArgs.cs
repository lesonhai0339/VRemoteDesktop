using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class P2PChatEventArgs: EventArgs
    {
        public P2PChatEventArgs(SocketDataType type, byte[] data)
        {
            Type = type;
            Data = data;
        }
        public SocketDataType Type { get; set; }
        public byte[] Data { get; set; }
    }
}
