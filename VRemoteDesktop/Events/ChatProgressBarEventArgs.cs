using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;

namespace VRemoteDesktop.Events
{
    public class ChatProgressBarEventArgs: EventArgs
    {
        public ChatProgressBarEventArgs(ProgressBarEnum type)
        {
            Type = type;
            Data = null;
        }
        public ChatProgressBarEventArgs(ProgressBarEnum type, object data)
        {
            Type = type;
            Data = data;
        }
        public ProgressBarEnum Type { get; set; }
        public object Data { get; set; }
    }
}
