using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class P2PScreenEventArgs: EventArgs
    {
        public P2PScreenEventArgs(DataType type, byte[] data)
        {
            Type = type;
            Data = data;
        }
        public DataType Type { get; set; }
        public byte[] Data { get; set; }
    }
}
