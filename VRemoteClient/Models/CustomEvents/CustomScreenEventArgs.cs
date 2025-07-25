using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.CustomEvents
{
    public class CustomScreenEventArgs: EventArgs
    {
        public CustomScreenEventArgs() { }
        public CustomScreenEventArgs(RemoteType type, int totalSize)
        {
            Type = type;
            TotalSize = totalSize;
        }
        public CustomScreenEventArgs(RemoteType type, List<byte[]> data, int totalSize)
        {
            Type = type;
            Data = data;
            TotalSize = totalSize;
        }

        public RemoteType Type { get; set; } = RemoteType.None;
        public List<byte[]> Data { get; set; } = new List<byte[]>();
        public int TotalSize { get; set; } = 0;
    }
}
