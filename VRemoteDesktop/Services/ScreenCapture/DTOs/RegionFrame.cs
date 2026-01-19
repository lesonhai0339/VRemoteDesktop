using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class RegionFrame
    {
        public RegionFrame(int x, int y, int width, int height, byte[] buffer, int size)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Buffer = buffer;
            Size = size;
        }
    
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Buffer { get; set; }
        public int Size { get; set; }
    }
}
