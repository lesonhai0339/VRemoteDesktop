using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class RegionFrame
    {
        public RegionFrame(List<Rectangle> bounds)
        : this(bounds.ToArray()) { }
        public RegionFrame(Rectangle[] bounds)
        {
            Bounds = bounds;
        }
        public Rectangle[] Bounds { get; private set; }
    }
}
