using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class SocketDisposeEventArgs: EventArgs
    {
        public SocketDisposeEventArgs(string socketId)
        {
            SocketId = socketId;
        }
        public string SocketId { get; set; }
    }
}
