using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Events
{
    public enum ProgressbarEnum
    {
        Finished,
        Timeout,
        Stop,
        Error
    }
    public class ChatProgressBarEventArgs: EventArgs
    {
        public ChatProgressBarEventArgs(ProgressbarEnum type)
        {
            Type = type;
            Data = null;
        }
        public ChatProgressBarEventArgs(ProgressbarEnum type, object data)
        {
            Type = type;
            Data = data;
        }
        public ProgressbarEnum Type { get; set; }
        public object Data { get; set; }
    }
}
