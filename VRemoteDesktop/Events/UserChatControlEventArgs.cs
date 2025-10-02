using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{

    public class UserChatControlEventArgs: EventArgs
    {
        public UserChatControlEventArgs(UserChatControlEventType type, string id)
        {
            Type = type;
            Id = id;
        }

        public UserChatControlEventType Type { get; set; }
        public string Id { get; set; }
    }
}
