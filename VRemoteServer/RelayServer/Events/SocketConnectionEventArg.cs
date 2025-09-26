using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;
using VRemoteServer.RelayServer.Networking;

namespace VRemoteServer.RelayServer.Events
{
    public class SocketConnectionEventArg: EventArgs
    {
        public SocketConnectionEventArg(SocketDataType type)
        {
            Type = type;
        }
        public SocketConnectionEventArg(SocketDataType type, string id)
        {
            Type = type;
            Id = id;
        }
        public SocketConnectionEventArg(SocketDataType type, string id, byte[] data) 
        {
            Type = type;
            Id = id;
            Data = data;
            Length = 0;
        }
        public SocketConnectionEventArg(SocketDataType type, string id, int offset, int length)
        {
            Type = type;
            Id = id;
            Offset = offset;
            Length = length;
        }
        public SocketDataType Type { get;set; }
        public string Id { get; set; } = string.Empty;
        public byte[] Data { get; set; } = null;
        public int Offset { get; set; } = 0;
        public int Length { get; set; } = 0;
    }
}
