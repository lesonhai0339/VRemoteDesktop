using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class RegionFrame
    {
        public RegionFrame(int x, int y, int w, int h, byte[] buffer, int size)
        : this(new Rectangle(x, y, w, h), buffer, size) { }
        public RegionFrame(Rectangle bounds, byte[] buffer, int size)
        {
            Bounds = bounds;
            Buffer = buffer;
            Size = size;
        }
        public Rectangle Bounds { get; set; }   
        public byte[] Buffer { get; set; }
        public int Size { get; set; }
    }
}
