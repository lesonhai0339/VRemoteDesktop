using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteClient.Models.Enums;

namespace VRemoteClient.Models.DTOs
{
    public class MouseReceived
    {
        public int SenderWidth { get;set; }
        public int SenderHeight { get; set; }
        public int ReceiverWidth { get; set; }
        public int ReceiverHeight { get; set; }

        public MouseMessage Button { get; set; }
        public MouseType Action { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}
