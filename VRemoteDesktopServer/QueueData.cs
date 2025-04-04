using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static VRemoteDesktopServer.Enums;

namespace VRemoteDesktopServer
{
    public class QueueData
    {
        public QueueData()
        {

        }
        public QueueData(string id, RemoteType type, byte[] byteData)
        {
            Id = id;
            Type = type;
            ByteData = byteData;
        }
        public string Id { get; set; }
        public RemoteType Type { get; set; }
        public byte[] ByteData { get; set; }
    }
}
