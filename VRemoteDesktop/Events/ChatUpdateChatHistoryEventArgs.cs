using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace VRemoteDesktop.Events
{
    public enum ChatUpdateChatHistoryEventType
    {
        AddMessage,
        ClearHistory,
        LoadHistory
    }
    public class ChatUpdateChatHistoryEventArgs: EventArgs
    {
        public ChatUpdateChatHistoryEventArgs(ChatUpdateChatHistoryEventType type, string connectionId, object[] messages)
        {
            Type = type;
            ConnectionId = connectionId;
            Messages = messages;
        }
        public ChatUpdateChatHistoryEventType Type { get; set; }
        public string ConnectionId { get; set; }    
        public object[] Messages { get; set; }
    }
}
