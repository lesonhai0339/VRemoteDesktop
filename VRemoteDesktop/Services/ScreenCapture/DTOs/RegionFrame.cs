using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class RegionFrame
    {
        public RegionFrame(List<Rectangle> bounds, IntPtr pointer)
        : this(bounds.ToArray(), pointer) { }
        public RegionFrame(Rectangle[] bounds, IntPtr pointer)
        {
            Bounds = bounds;
            Pointer = pointer;
        }
        public Rectangle[] Bounds { get; set; }
        public IntPtr Pointer { get; set; }
    }
}
