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
        public ChatUpdateChatHistoryEventArgs(ChatUpdateChatHistoryEventType type, List<Control> controls)
        {
            Type = type;
            Controls = controls;
        }
        public ChatUpdateChatHistoryEventType Type { get; set; }
        public List<Control> Controls { get; set; }
    }
}
