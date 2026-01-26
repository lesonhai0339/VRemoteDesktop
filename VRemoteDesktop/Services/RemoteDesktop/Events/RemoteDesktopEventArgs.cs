using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.RemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class RemoteDesktopEventArgs:  EventArgs
    {
        public RemoteDesktopEventArgs(ResponseType type, bool flag = true, byte[] data= null, string message = "")
        {
            Type = type;
            Data = data;
            Message = message;
        }
        public ResponseType Type { get; set; }
        public byte[] Data { get; set; }
        public string Message { get; set; }
    }
}
