using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.Enums;

namespace VRemoteDesktop.Services.ScreenCapture.Events
{
    public class FrameEventArgs : EventArgs
    {
        public FrameEventArgs(VScreenType type, RegionFrame regionFrame)
        {
            Type = type;
            RegionFrame = regionFrame;
        }
        public VScreenType Type { get; set; }
        public RegionFrame RegionFrame { get; set; }
    }
}
