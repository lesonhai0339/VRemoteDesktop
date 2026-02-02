using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture.Interop;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class ImageSwapper
    {
        public ImageSwapper(Rectangle[] rects)
        {
            Rectangles = rects;
            HBitmap = IntPtr.Zero;
            Bits = IntPtr.Zero;
            MemDC = IntPtr.Zero;
        }
        public IntPtr HBitmap;
        public IntPtr Bits;
        public IntPtr MemDC;
        public Rectangle[] Rectangles { get; set; }

        public void Free()
        {
            if (HBitmap != IntPtr.Zero)
                CaptureApi.DeleteObject(HBitmap);

            if (MemDC != IntPtr.Zero)
                CaptureApi.ReleaseDC(IntPtr.Zero, MemDC);

            Bits = IntPtr.Zero;
            HBitmap = IntPtr.Zero;
            MemDC = IntPtr.Zero;

            Array.Clear(Rectangles, 0, Rectangles.Length);
        }
    }
}
