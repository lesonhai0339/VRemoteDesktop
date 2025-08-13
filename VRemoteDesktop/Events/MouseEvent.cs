using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Models;

namespace VRemoteDesktop.Events
{
    public class MouseEvent : EventArgs
    {
        public TaskObject Task { get; set; }
    }
}
