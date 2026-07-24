using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Enums;

namespace Vsign4.VRemoteDesktop.Services.ScreenCapture.Events
{
    public class FrameEventArgs : EventArgs
    {
        public FrameEventArgs(VScreenType type)
        {
            Type = type;
        }
        public VScreenType Type { get; set; }
    }
}
