using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class ChatControlUpdateEventArgs: EventArgs
    {
        public ChatControlUpdateEventArgs(ChatControlType type, Action action)
        {
            Type = type;
            Action = action;
        }
        public ChatControlType Type { get; set; }
        public Action Action { get; set; }
    }
}
