using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public class P2PDisconnectEventArgs: EventArgs
    {
        public P2PDisconnectEventArgs(bool isDisconnected)
        {
            IsDisconnected = isDisconnected; 
        }
        public bool IsDisconnected { get; set; }
    }
}
