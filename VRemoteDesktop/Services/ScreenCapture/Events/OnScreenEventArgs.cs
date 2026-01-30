using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Enums
{
    public class OnScreenEventArgs: EventArgs
    {
        public OnScreenEventArgs(bool isFullScreen, Rectangle rectangle)
        {
            IsFullScreen = isFullScreen;
            Rectangle = rectangle;    
        }
        public bool IsFullScreen { get; set; }  
        public Rectangle Rectangle { get; set; }   
    }
}
