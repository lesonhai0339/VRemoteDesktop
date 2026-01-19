using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Services.ScreenCapture.DTOs;

namespace VRemoteDesktop.Services.ScreenCapture.Events
{
    public class ScreenEventArgs: EventArgs
    {
        public ScreenEventArgs(ScreenType type, RegionFrame regionFrame, long order = 0)
        {
            Type = type;
            Order = order;
            RegionFrame = regionFrame;
        }
        public  ScreenType Type { get; set; }
        public long Order { get; set; }
        public RegionFrame RegionFrame { get; set; }
    }
}
