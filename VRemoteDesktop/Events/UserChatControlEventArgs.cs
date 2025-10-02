using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{

    public class UserChatControlEventArgs: EventArgs
    {
        public UserChatControlEventArgs(UserChatControlEventType type, string id, byte[] data)
        {
            Type = type;
            Id = id;
            Data = data;
        }

        public UserChatControlEventType Type { get; set; }
        public string Id { get; set; }
        public byte[] Data { get; set; }
    }
}
