using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class ScreenCaptureEventArgs : EventArgs
    {
        public ScreenCaptureEventArgs() { }
        public ScreenCaptureEventArgs(SocketDataType type, int totalSize)
        {
            Type = type;
            TotalSize = totalSize;
        }
        public ScreenCaptureEventArgs(SocketDataType type, List<byte[]> data, int totalSize)
        {
            Type = type;
            Data = data;
            TotalSize = totalSize;
        }

        public SocketDataType Type { get; set; } = SocketDataType.None;
        public List<byte[]> Data { get; set; } = new List<byte[]>();
        public int TotalSize { get; set; } = 0;
    }
}
