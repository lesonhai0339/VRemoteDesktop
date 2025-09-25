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
        public RemoteConnectionEventArg(SocketConnectionEventType type)
        {
            Type = type;
            Id = string.Empty;
            DataType = SocketDataType.None;
            Data = null;
        }
        public RemoteConnectionEventArg(SocketConnectionEventType type, string id, SocketDataType dataType, byte[] data)
        {
            Type = type;
            Id = id;
            DataType = dataType;
            Data = data;
        }
        public SocketConnectionEventType Type { get; set; }
        public string Id { get; set; }
        public SocketDataType DataType { get; set; }
        public byte[] Data { get; set; }
    }
}
