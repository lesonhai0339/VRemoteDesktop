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
        public RemoteControlManagerEventArgs(ServerEventType type, string id, byte[] data)
        {
            Type = type;
            Id = id;
            Data = data;
        }

        public ServerEventType Type { get; set; }
        public string Id { get; set; }
        public byte[] Data { get; set; }
    }
}
