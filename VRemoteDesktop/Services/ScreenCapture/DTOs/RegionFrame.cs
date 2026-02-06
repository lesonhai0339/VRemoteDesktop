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
        public RegionFrame(List<Rectangle> bounds, int start, int length)
        : this(bounds.ToArray(), start, length) { }
        public RegionFrame(Rectangle[] bounds, int start, int length)
        {
            Bounds = bounds;
            Start = start;
            Length = length;
        }
        public Rectangle[] Bounds { get; private set; }
        public int Start { get; private set; }
        public int Length { get; set; }
    }
}
