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
        public VScreenSenderEventArgs(string id, CapturedFrame frame)
        {
            Id = id;
            Frame = frame;

        }
        public string Id { get; set; }  
        public CapturedFrame Frame { get; set; }
    }
}
