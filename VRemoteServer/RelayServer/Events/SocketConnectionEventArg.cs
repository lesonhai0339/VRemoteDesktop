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
        public SocketConnectionEventArg(SocketConnectionEventType eventType)
        {
            EventType = eventType;
        }
        public SocketConnectionEventArg(SocketConnectionEventType eventType , SocketDataType type)
        {
            EventType = eventType;
            Type = type;
        }
        public SocketConnectionEventArg(SocketConnectionEventType eventType, SocketDataType type, string id)
        {
            EventType = eventType;
            Type = type;
            Id = id;
        }
        public SocketConnectionEventArg(SocketConnectionEventType eventType, SocketDataType type, string id, byte[] data)
        {
            EventType = eventType;
            Type = type;
            Id = id;
            Data = data;
            Length = 0;
        }
        public SocketConnectionEventArg(SocketConnectionEventType eventType,SocketDataType type, string id, int offset, int length)
        {
            EventType = eventType;
            Type = type;
            Id = id;
            Offset = offset;
            Length = length;
        }
        public SocketConnectionEventType EventType { get; set; }
        public SocketDataType Type { get;set; }
        public string Id { get; set; } = string.Empty;
        public byte[] Data { get; set; } = null;
        public int Offset { get; set; } = 0;
        public int Length { get; set; } = 0;
    }
}
