using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.CustomEvents
{
    public class CustomMouseEventArgs : EventArgs
    {
        public int X { get; set; }
        public int Y { get; set; }
        public WindowsMouseMessage Button { get; set; }
        public MouseState Action { get; set; }
    }
}
