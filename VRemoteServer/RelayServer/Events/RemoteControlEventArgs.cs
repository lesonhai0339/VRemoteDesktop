using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;

namespace VRemoteServer.RelayServer.Events
{
    public class RemoteControlEventArgs: EventArgs
    {
        public RemoteControlEventArgs(ServerEventType type, int offset, int length)
        {
            Type = type;
            Offset = offset;
            Length = length;
        }

        public ServerEventType Type { get; set; }
        public int Offset { get; set; }
        public int Length { get; set; }
    }
}
