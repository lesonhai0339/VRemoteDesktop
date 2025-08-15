using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class P2PMouseEventArgs : EventArgs
    {
        public P2PMouseEventArgs(byte[] data)
        {
            Data = data;
        }
        public byte[] Data { get; set; }
    }
}
