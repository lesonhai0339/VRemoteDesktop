using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRemoteServer.RelayServer.Enums;

namespace VRemoteServer.RelayServer.Events
{
    public class LoginErrorEventArgs: EventArgs
    {
        public LoginErrorEventArgs(Exception exception, string note)
        {
            Exception = exception;
            Note = note;
        }        public Exception Exception { get; set; }
        public string Note { get; set; }
    }  
}
