using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class P2PFileSendEventArgs: EventArgs
    {
        public P2PFileSendEventArgs(SendFileType type, byte[] data)
        {
            Type = type;
            Data = data;
        }

        public SendFileType Type { get; set; }
        public byte[] Data { get; set; }
    }
}
