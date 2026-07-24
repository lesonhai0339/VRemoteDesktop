using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace Vsign4.VRemoteDesktop.Services.ScreenCapture.Events
{
    public class OnScreenEventArgs : EventArgs
    {
        public OnScreenEventArgs(bool isFullScreen, List<Rectangle> rectangles)
        {
            IsFullScreen = isFullScreen;
            Rectangles = rectangles ;
        }
        public bool IsFullScreen { get; set; }
        public List<Rectangle> Rectangles { get; set; }
    }
}
