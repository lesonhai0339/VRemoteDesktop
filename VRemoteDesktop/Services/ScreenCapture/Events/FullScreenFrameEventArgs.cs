using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Services.ScreenCapture.DTOs;

namespace VRemoteDesktop.Services.ScreenCapture.Events
{
    public class FullScreenFrameEventArgs : EventArgs 
    {
        public FullScreenFrameEventArgs(ScreenType type, FullScreenFrame fullScreenFrame)
        {
            Type = type;
            FullScreenFrame = fullScreenFrame;
        }   
        public ScreenType Type { get; set; }
        public FullScreenFrame FullScreenFrame { get; set; }
    }
}
