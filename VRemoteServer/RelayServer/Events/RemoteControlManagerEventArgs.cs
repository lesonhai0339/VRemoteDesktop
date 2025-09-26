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
        public RemoteControlManagerEventArgs(SocketDataType type, string socketId, string partnerId = null, int dataOffset = 0, int dataLength = 0, byte[] data = null)
        {
            Type = type;
            SocketId = socketId;
            PartnerId = partnerId;
            DataOffset = dataOffset;
            DataLength = dataLength;
            Data = data;
        }

        public SocketDataType Type { get; set; }
        public string SocketId { get; set; }
        public string PartnerId { get; set; }
        public byte[] Data { get; set; }
        public int DataOffset { get; set; }
        public int DataLength { get; set; }

    }
}
