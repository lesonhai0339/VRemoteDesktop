using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;

namespace VRemoteServer.RelayServer.Events
{
    public class LoginEventArgs: EventArgs   
    {
        public LoginEventArgs(SocketDataType type)
        {
            Type = type;
        }
        public LoginEventArgs(SocketDataType type, byte[] data)
        {
            Type = type;
            Data = data; 
        }
        public LoginEventArgs(SocketDataType type, int offset, int length)
        {
            Type = type;
            Offset = offset;
            Length = length;
        }

        public SocketDataType Type { get; set; }
        public byte[] Data { get; set; } = null;
        public int Offset { get; set; } = 0;
        public int Length { get; set; } = 0;
    }
}
