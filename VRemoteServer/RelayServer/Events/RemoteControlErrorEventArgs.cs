using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VRemoteServer.RelayServer.Events
{
    public class RemoteControlErrorEventArgs : EventArgs
    {
        public RemoteControlErrorEventArgs(Exception exception, string note)
        {
            Exception = exception;
            Note = note;
        }
        public Exception Exception { get; set; }
        public string Note { get; set; }
    }
}
