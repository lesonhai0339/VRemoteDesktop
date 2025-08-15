using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class P2PChatEventArgs: EventArgs
    {
        public P2PChatEventArgs(byte[] data)
        {
            Data = data;
        }

        public byte[] Data { get; set; }
    }
}
