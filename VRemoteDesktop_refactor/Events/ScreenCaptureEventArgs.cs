using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.SessionManagement.Enums;

namespace Vsign4.VRemoteDesktop.Events
{
    public class ScreenCaptureEventArgs : EventArgs
    {
        public ScreenCaptureEventArgs() 
        {
            Type = SocketDataType.None;
            Data = new List<byte[]>();
            TotalSize = 0;
        }
        public ScreenCaptureEventArgs(SocketDataType type, int totalSize)
        {
            Type = type;
            Data = new List<byte[]>();
            TotalSize = totalSize;
        }
        public ScreenCaptureEventArgs(SocketDataType type, List<byte[]> data, int totalSize)
        {
            Type = type;
            Data = data;
            TotalSize = totalSize;
        }

        public SocketDataType Type { get; set; } 
        public List<byte[]> Data { get; set; }
        public int TotalSize { get; set; } 
    }
}
