using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class ChatDisconnectedEventArgs:  EventArgs
    {
        public ChatDisconnectedEventArgs(string socketId)
        {
            SocketId = socketId;
        }

        public string SocketId { get; set; }
    }
}
