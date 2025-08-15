using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class P2PKeyboardEventArgs: EventArgs
    {
        public P2PKeyboardEventArgs(byte[] data)
        {
            Data = data;
        }
        public byte[] Data { get; set; }
    }
}
