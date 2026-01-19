using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture.DTOs;

namespace VRemoteDesktop.Services.ScreenCapture.Events
{
    public class DirtyRegionEventArgs: EventArgs
    {
        public DirtyRegionEventArgs(long order, RegionFrame regionFrame)
        {
            Order = order;
            RegionFrame = regionFrame;
        }
        public long Order { get; set; }
        public RegionFrame RegionFrame { get; set; }
    }
}
