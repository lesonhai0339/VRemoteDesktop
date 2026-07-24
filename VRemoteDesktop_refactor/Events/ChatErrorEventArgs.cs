using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Events
{
    public enum ChatErrorLevel
    {
        Info,
        Warning,
        Critical
    }
    public class ChatErrorEventArgs : EventArgs
    {
        public ChatErrorEventArgs(ChatErrorLevel level, Exception ex, string message = "")
        {
            Level = level;
            Ex = ex;
            Message = message;
        }
        public ChatErrorLevel Level { get; set; }
        public Exception Ex { get; set; }
        public string Message { get; set; }
    }
}
