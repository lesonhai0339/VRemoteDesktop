using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class ChatControlAddedEventArgs: EventArgs
    {
        public ChatControlAddedEventArgs(ChatControlType type, Control control)
        {
            Type = type;
            Control = control;
        }
        public ChatControlType Type { get; set; }
        public Control Control { get; set; }
    }
}
