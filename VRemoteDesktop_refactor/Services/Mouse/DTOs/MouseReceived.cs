using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.Mouse.Enums;

namespace Vsign4.VRemoteDesktop.Services.Mouse.DTOs
{
    public class MouseReceived
    {
        public int SenderWidth { get; set; }
        public int SenderHeight { get; set; }
        public int ReceiverWidth { get; set; }
        public int ReceiverHeight { get; set; }

        public WindowsMouseMessage Button { get; set; }
        public MouseAction Action { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}
