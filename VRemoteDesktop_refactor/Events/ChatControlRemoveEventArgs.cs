using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Presenters.Enums;

namespace Vsign4.VRemoteDesktop.Events
{
    public class ChatControlRemoveEventArgs : EventArgs
    {
        public ChatControlRemoveEventArgs(string connectionId, ChatControlType type, string controlKey)
        {
            ConnectionId = connectionId;
            Type = type;
            ControlKey = controlKey;
        }
        public string ConnectionId { get; set; }
        public ChatControlType Type { get; set; }
        public string ControlKey { get; set; }
    }
}
