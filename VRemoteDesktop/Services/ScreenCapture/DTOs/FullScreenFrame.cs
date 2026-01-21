using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class FullScreenFrame
    {
        public Rectangle Bounds { get; set; }
        public byte[] Buffer { get; set; }
        public int Length { get; set; }
        private int _refCount;
        public FullScreenFrame(Rectangle bounds, byte[] buffer, int length)
        {
            Bounds = bounds;
            Buffer = buffer;
            Length = length;
            _refCount = 0;
        }
        public void InRef()
        {
            Interlocked.Increment(ref _refCount);
        }
        public void DeRef()
        {
            if(Interlocked.Decrement(ref _refCount) <= 0)
            {
                Free();
            }
        }
        private void Free()
        {
            VArrayPool.Return(Buffer);
        }

    }
}
