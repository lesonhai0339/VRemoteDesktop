using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class ChatControlRemoveEventArgs: EventArgs
    {
        public ChatControlRemoveEventArgs(ChatControlType type, string controlKey)
        {
            Type = type;
            ControlKey = controlKey;
        }
        public ChatControlType Type { get; set; }
        public string ControlKey { get; set; }
    }
}
