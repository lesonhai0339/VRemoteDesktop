using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.GDI;

namespace VRemoteDesktop.Services.ScreenCapture.Enums
{

    public class VScreenSenderEventArgs : EventArgs
    {
        public VScreenSenderEventArgs(CapturedFrame frame, string id = "0000000000")
        {
            Id = id;
            Frame = frame;

        }
        public string Id { get; set; }
        public CapturedFrame Frame { get; set; }
    }
}
