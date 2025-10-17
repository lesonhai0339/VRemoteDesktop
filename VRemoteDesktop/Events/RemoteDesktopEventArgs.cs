using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class RemoteDesktopEventArgs:  EventArgs
    {
        public RemoteDesktopEventArgs(SocketDataType type, bool flag = true, byte[] data= null)
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
