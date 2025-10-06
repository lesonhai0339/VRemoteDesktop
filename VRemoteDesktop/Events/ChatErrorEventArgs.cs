using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public enum ChatErrorLevel
    {
        Info,
        Warning,
        Critical
    }
    public class ChatErrorEventArgs: EventArgs
    {
        public ChatErrorEventArgs(ChatErrorLevel level, Exception ex)
        {
            Level = level;
            Ex = ex;
        }
        public ChatErrorLevel Level { get; set; }
        public Exception Ex { get; set; }
    }
}
