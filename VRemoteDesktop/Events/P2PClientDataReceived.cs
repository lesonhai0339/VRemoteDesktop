using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class P2PClientDataReceived:  EventArgs
    {
        public P2PClientDataReceived(SocketDataType type, bool flag, byte[] data)
        {
            Type = type;
            Flag = flag;
            Data = data;
        }
        public SocketDataType Type { get; set; }
        public bool Flag { get; set; }
        public byte[] Data { get; set; }
    }
}
