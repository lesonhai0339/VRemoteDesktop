using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Presenters.Enums;

namespace Vsign4.VRemoteDesktop.Events
{
    public class ChatControlUpdateEventArgs : EventArgs
    {
        public ChatControlUpdateEventArgs(string connectionId, ChatControlType type, string fileId)
        {
            ConnectionId = connectionId;
            Type = type;
            FileId = fileId;
        }
        public string ConnectionId { get; set; }
        public ChatControlType Type { get; set; }
        public string FileId { get; set; }
    }
}
