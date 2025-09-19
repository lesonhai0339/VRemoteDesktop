using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class ChatControlUpdateEventArgs: EventArgs
    {
        public ChatControlUpdateEventArgs(ChatControlType type, string fileId)
        {
            Type = type;
            FileId = fileId;
        }
        public ChatControlType Type { get; set; }
        public string FileId { get; set; }
    }
}
