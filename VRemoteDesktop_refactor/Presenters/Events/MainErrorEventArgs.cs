using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Presenters.Events
{
    public class MainErrorEventArgs : EventArgs
    {
        public MainErrorEventArgs(Exception ex = null, string message = "")
        {
            Ex = ex;
            Message = message;
        }

        public Exception Ex { get; set; }
        public string Message { get; set; }
    }
}
