using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;

namespace VRemoteServer.RelayServer.Events
{
    public class RemoteConnectionEventArg: EventArgs
    {
        public RemoteConnectionEventArg(SocketDataType type)
        {
            Type = type;
            Id = string.Empty;
            Data = null;
        }
        public RemoteConnectionEventArg(SocketDataType type, string id, byte[] data, int offset, int length)
        {
            Type = type;
            Id = id;
            Data = data;
            Offset = offset;
            Length = length;
        }
        public SocketDataType Type { get; set; }
        public string Id { get; set; }
        public byte[] Data { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }
}
