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
        public CustomScreenEventArgs(ResponseType type, int totalSize)
        {
            Type = type;
            TotalSize = totalSize;
        }
        public CustomScreenEventArgs(ResponseType type, List<byte[]> data, int totalSize)
        {
            Type = type;
            Data = data;
            TotalSize = totalSize;
        }

        public ResponseType Type { get; set; } = ResponseType.None;
        public List<byte[]> Data { get; set; } = new List<byte[]>();
        public int TotalSize { get; set; } = 0;
    }
}
