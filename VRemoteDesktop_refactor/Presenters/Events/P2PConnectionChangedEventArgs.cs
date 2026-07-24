using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Presenters.Events
{
    public class P2PConnectionChangedEventArgs: EventArgs
    {
        public P2PConnectionChangedEventArgs(string message)
        {
            Message = message;
        }

            public string Message { get; set; }
    }
}
