using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class P2PClipboardEventArgs: EventArgs
    {
        public P2PClipboardEventArgs(byte[] data)
        {
            Data = data;
        }
        public byte[] Data { get; set; }
    }
}
