using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RemoteClient.Remote
{
    public static class Libraries
    {
        [DllImport("gdi32.dll")]
        public static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);
        [DllImport("user32.dll")]
        public static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    }

    public class VCaptureScreen
    {
        const int BLOCK_SIZE  = 32;

        private VRegion _vRegion;
        public VCaptureScreen()
        {
            _vRegion = new VRegion();
        }
        public void Test()
        {
            var a = Capture();
            var b = SplitToRegions(a);
            foreach(var i in b)
            {
                _vRegion.Union(i.X, i.Y, i.Width, i.Height);
                Bitmap block = new Bitmap(i.Width, i.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using(Graphics g= Graphics.FromImage(block))
                {
                    IntPtr bitmapDHC = g.GetHdc();
                    IntPtr screenHDC = Libraries.GetDC(IntPtr.Zero);

                    _vRegion.GetBounds((int)bitmapDHC);
                }
            }
        }
        public Bitmap Capture()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            using (Graphics g = Graphics.FromImage(bitmap))
            {
                IntPtr bitmapHDC = g.GetHdc();  
                IntPtr screenHdc = Libraries.GetDC(IntPtr.Zero);

                uint sourceCopy = 0x00CC0020;
                Libraries.BitBlt(bitmapHDC, 0, 0, bounds.Width, bounds.Height, screenHdc, bounds.X, bounds.Y, sourceCopy);

                g.ReleaseHdc(bitmapHDC);
                Libraries.ReleaseDC(IntPtr.Zero, screenHdc);
            }
            return bitmap;
        }
        public Bitmap Crop(Bitmap sourceBitmap, Rectangle region)
        {
            region = Rectangle.Intersect(region, new Rectangle(0,0, sourceBitmap.Width, sourceBitmap.Height));

            if (region.IsEmpty) return null;

             Bitmap croppedBitmap = new Bitmap(region.Width, region.Height, sourceBitmap.PixelFormat);

            using (Graphics g = Graphics.FromImage(croppedBitmap))
            {
                g.DrawImage(sourceBitmap,
                    new Rectangle(0, 0, region.Width, region.Height),
                    region,
                    GraphicsUnit.Pixel);
            }

            return croppedBitmap;
        }
        public List<Rectangle> SplitToRegions(Bitmap bitmap)
        { 
            List<Rectangle> regions = new List<Rectangle>();
            for(int Y = 0; Y< bitmap.Height; Y += BLOCK_SIZE)
            {
                for(int X = 0; X < bitmap.Width; X += BLOCK_SIZE)
                {
                    int width = Math.Min(BLOCK_SIZE, bitmap.Width - X);
                    int height = Math.Min(BLOCK_SIZE, bitmap.Height - Y);
                    if (width > 0 && height > 0)
                    {
                        regions.Add(new Rectangle(X, Y, width, height));
                    }
                } 
            }
            return regions;
        }
    }
}
