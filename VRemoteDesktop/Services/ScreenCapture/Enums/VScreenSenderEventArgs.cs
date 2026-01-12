using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture.DTOs;

namespace VRemoteDesktop.Services.ScreenCapture.Enums
{
    public enum VScreenSenderEventType
    {
        FullScreen,
        RegionChange    
    }
    public class VScreenSenderEventArgs: EventArgs
    {
        public VScreenSenderEventArgs(
            VScreenSenderEventType type, 
            ScreenTask screenTask)
        {
            Type = type;
            ScreenTask = screenTask;
        }   
        public VScreenSenderEventType Type { get; set; }
        public ScreenTask ScreenTask { get; set; }
    }
}
