using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class P2PScreenEventArgs: EventArgs
    {
        public P2PScreenEventArgs(ScreenType type, byte[] data)
        {
            Type = type;
            Data = data;
        }
        public ScreenType Type { get; set; }
        public byte[] Data { get; set; }
    }
}
