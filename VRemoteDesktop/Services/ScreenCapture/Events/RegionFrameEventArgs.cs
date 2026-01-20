using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Services.ScreenCapture.DTOs;

namespace VRemoteDesktop.Services.ScreenCapture.Events
{
    public class RegionFrameEventArgs : EventArgs
    {
        public RegionFrameEventArgs(ScreenType type, RegionFrame regionFrame)
        {
            Type = type;
            RegionFrame = regionFrame;
        }
        public ScreenType Type { get; set; }
        public RegionFrame RegionFrame { get; set; }
    }
}
