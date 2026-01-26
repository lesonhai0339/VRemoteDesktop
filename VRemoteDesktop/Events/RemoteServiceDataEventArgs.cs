using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class RemoteServiceDataEventArgs: EventArgs
    {
        public RemoteServiceDataEventArgs(SessionResponseType type, bool isSuccess)
        {
            Type = type;
            IsSuccess = isSuccess;
        }
        public SessionResponseType Type { get; set; }
        public bool IsSuccess { get; set; }   
    }
}
