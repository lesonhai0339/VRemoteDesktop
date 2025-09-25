using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;

namespace VRemoteServer.RelayServer.Events
{
    public class RemoteControlManagerEventArgs : EventArgs
    {
        public RemoteControlManagerEventArgs(ServerEventType type, string socketId, string partnerId, int dataOffset, int dataLength)
        {
            Type = type;
            SocketId = socketId;
            PartnerId = partnerId;
            DataOffset = dataOffset;
            DataLength = dataLength;
        }

        public ServerEventType Type { get; set; }
        public string SocketId { get; set; }
        public string PartnerId { get; set; }

        public int DataOffset { get; set; }
        public int DataLength { get; set; }

    }
}
