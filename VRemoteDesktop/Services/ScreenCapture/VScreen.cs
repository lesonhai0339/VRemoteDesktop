using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Helpers;
using static VRemoteDesktop.Interop.Win32Apis;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public class VScreen: IDisposable
    {
        private const int BUFFER_SIZE = 20 * 1024 * 1024; //20MB
        private const uint DIB_RGB_COLORS = 0;
        private const int BYTE_PER_PIXEL = 4;   
        private int _disposed;
        private IntPtr _bufferPool;
        private IntPtr _hBitmap;
        private IntPtr _bits;        // points to raw pixels
        private IntPtr _memDC;
        private Rectangle _bounds;
        private Rectangle[] rects;
        private BackgroundWorker _worker;
        public VScreen()
        {
            _bounds = Screen.PrimaryScreen.Bounds;

            int size = 16;
            int cols = (_bounds.Width + size - 1) / size;
            int rows = (_bounds.Height + size - 1) / size;

            rects = new Rectangle[cols * rows];

            InitRectangle(_bounds.Width, _bounds.Height);


            _bufferPool = Marshal.AllocHGlobal(BUFFER_SIZE);
            InitCaptureBuffer(_bounds.Width, _bounds.Height);

            _worker = new BackgroundWorker();
            _worker.DoWork += Handler;
        }
        private void Handler(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                CaptureToBuffer();
                foreach (var rect in rects)
                {
                    var flag = IsRegionChange(_bufferPool, _bits, rect.X, rect.Y, rect.Width, rect.Height);
                    Console.WriteLine(flag);
                }
                SaveScreen(_bounds.Width, _bounds.Height, BYTE_PER_PIXEL, _bits, _bufferPool);
                Thread.Sleep(1000);
            }
        }

        public void Test()
        {
            if (!_worker.IsBusy)
                _worker.RunWorkerAsync();


            while (true)
            {
                Console.WriteLine("\n------------------------\n");
                Thread.Sleep(1000);
            }
        }
        private void InitRectangle(int width, int height)
        {
            int size = 16;
            int index = 0;
            for (int i = 0; i < height; i += size)
            {
                for (int j = 0; j < width; j += size)
                {
                    int w = Math.Min(size, width - j);
                    int h = Math.Min(size, height - i);

                    rects[index++] = new Rectangle(j, i, w, h);
                }
            }
        }
        private unsafe void SaveScreen(int width, int height, int bytePerPixel, IntPtr src, IntPtr dst)
        {
            int stride = ((width * bytePerPixel) + 3) & ~3;
            int count = stride * height;
            CaptureApis.memcpy(dst, src, (UIntPtr)count);
        }
        public unsafe void GetRegionData(ref int offset, IntPtr destination, IntPtr source, int regionX, int regionY, int regionWidth, int regionHeight)
        {
            //Get the base pointer
            byte* basePtr = (byte*)destination + offset;

            //Add header info of region(x,y,w,h)
            uint* d = (uint*)basePtr;   
            *d++ = (uint)regionX;
            *d++ = (uint)regionY;
            *d++ = (uint)regionWidth;
            *d++ = (uint)regionHeight;

            //calculate stride of big screen and regions
            int srcStride = ((_bounds.Width * BYTE_PER_PIXEL) + 3) & ~3;
            uint dstStride = (uint)(regionWidth * BYTE_PER_PIXEL);

            unsafe
            {
                //get address of source and destination 
                byte* src = (byte*)source;
                byte* dst = (byte*)d;

                //for-loop to write row from source to destination
                for (int row = 0; row < regionHeight; row++)
                {
                    int sy = (row + regionY) * srcStride; //reverser screen data , see more at InitCaptureBuffer() ->  bmi.Header.biHeight = -height;
                    int sx = regionX * BYTE_PER_PIXEL; //offset in row

                    IntPtr srcAddr = (IntPtr)(src + sy + sx); //formular => address = base + rowOffset + colOffset 

                    IntPtr dstAddr = (IntPtr)(dst + (row * dstStride)); //formular => address = base + (row * rowStride) 

                    CaptureApis.memcpy(dstAddr, srcAddr, (UIntPtr)dstStride);
                }
            }
            offset += (int)(dstStride * regionHeight) + 16;
        }
        public unsafe bool IsRegionChange(IntPtr oldScreen, IntPtr newScreen, int regionX, int regionY, int regionWidth, int regionHeight)
        {
            int stride = ((_bounds.Width * BYTE_PER_PIXEL) + 3) & ~3;
            unsafe
            {
                byte* oBase = (byte*)oldScreen;
                byte* nBase = (byte*)newScreen;

                for (int row = 0; row < regionHeight; row++)
                {
                    //get the start off row
                    int sy = (row + regionY) * stride;
                    int sx = regionX * BYTE_PER_PIXEL;

                    uint* pOld = (uint*)(oBase + sy + sx);
                    uint* pNew = (uint*)(nBase + sy + sx);

                    for (int col = 0; col < regionWidth; col++)
                    {
                        if (pOld[col] != pNew[col])
                            return true;
                    }
                }
            }
            return false;
        }
        private void InitCaptureBuffer(int width, int height)
        {
            var bmi = new BITMAPINFO();
            bmi.Header.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.Header.biWidth = width;
            bmi.Header.biHeight = -height; //top-down
            bmi.Header.biPlanes = 1;
            bmi.Header.biBitCount = 32; //32 bits per pixel
            bmi.Header.biCompression = 0; //BI_RGB

            IntPtr screenDC = CaptureApis.GetDC(IntPtr.Zero);
            _hBitmap = CaptureApis.CreateDIBSection(
              screenDC,
              ref bmi,
              DIB_RGB_COLORS,
              out _bits,
              IntPtr.Zero,
              0);

            _memDC = CaptureApis.CreateCompatibleDC(screenDC);
            CaptureApis.SelectObject(_memDC, _hBitmap);

            CaptureApis.ReleaseDC(IntPtr.Zero, screenDC);
        }
        public void Something(int width, int height)
        {
            int srcStride = ((width * 4) + 3) & ~3;
            int dataLength = srcStride * height;
        }
        public void CaptureToBuffer()
        {
            IntPtr screenDC = CaptureApis.GetDC(IntPtr.Zero);

            CaptureApis.BitBlt(
                _memDC,
                0, 0,
                _bounds.Width,
                _bounds.Height,
                screenDC,
                _bounds.X,
                _bounds.Y,
                0x00CC0020); // SRCCOPY

            CaptureApis.ReleaseDC(IntPtr.Zero, screenDC);
        }
        public void Get24bppBuffer()
        {
            int width = _bounds.Width;
            int height = _bounds.Height;
            Bitmap bitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            BitmapData bmp = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            unsafe
            {
           
                byte* dstPtr = (byte*)bmp.Scan0;
                Convert32To24((byte*)_bits, dstPtr, width, height);


                byte[] ddd = new byte[bmp.Stride * bmp.Height];
                Marshal.Copy(bmp.Scan0, ddd, 0, ddd.Length);
                Console.WriteLine($"Before compress: {ddd.Length}");
                var compressed = ByteArrayHelper.CompressDeflate(ddd);
                Console.WriteLine($"After compressed: {compressed.Data.Length}");
            }

            bitmap.UnlockBits(bmp);
        }
        unsafe void Convert32To24(
        byte* src,    // _bits from 32bpp DIB
        byte* dst,    // your destination buffer
        int width,
        int height)
        {
            int srcStride = width * 4;
            int dstStride = (width * 3 + 3) & ~3;

            for (int y = 0; y < height; y++)
            {
                byte* s = src + y * srcStride;
                byte* d = dst + y * dstStride;

                for (int x = 0; x < width; x++)
                {
                    d[0] = s[0]; // B
                    d[1] = s[1]; // G
                    d[2] = s[2]; // R

                    s += 4;
                    d += 3;
                }
            }
        }
        unsafe void Convert32To565(
           byte* src,    // _bits from 32bpp DIB
           byte* dst,    // your destination buffer
           int width,
           int height)
        {
            int srcStride = ((width * 4) + 3) & ~3;
            int dstStride = ((width * 2) + 3) & ~3;

            for (int y = 0; y < height; y++)
            {
                byte* s = src + y * srcStride;
                ushort* d = (ushort*)(dst + y * dstStride);

                for (int x = 0; x < width; x++)
                {
                    byte b = s[0];
                    byte g = s[1];
                    byte r = s[2];

                    ushort rgb565 =
                         (ushort)(
                             ((r >> 3) << 11) |  // 5 bits red
                             ((g >> 2) << 5) |  // 6 bits green
                             (b >> 3)            // 5 bits blue
                         );

                    *d++ = rgb565;
                    s += 4;
                }
            }
        }
        unsafe void RGB565ToRGB24(byte* src, byte* dst, int width, int height)
        {
            int srcStride = ((width * 2) + 3) & ~3;
            int dstStride = ((width * 3) + 3) & ~3;

            for (int y = 0; y < height; y++)
            {
                ushort* s = (ushort*)(src + y * dstStride);
                byte* d = dst + y * srcStride;

                for (int x = 0; x < width; x++)
                {
                    ushort pixel = *s++;

                    int r5 = (pixel >> 11) & 0x1F;
                    int g6 = (pixel >> 5) & 0x3F;
                    int b5 = pixel & 0x1F;

                    byte r8 = (byte)((r5 << 3) | (r5 >> 2));
                    byte g8 = (byte)((g6 << 2) | (g6 >> 4));
                    byte b8 = (byte)((b5 << 3) | (b5 >> 2));

                    //d[0] = b8;
                    //d[1] = g8;
                    //d[2] = r8;
                    d[0] = r8;
                    d[1] = g8;
                    d[2] = b8;

                    d += 3;
                }
            }
        } 
        #region Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            //Khong the dung cai nay phai dung DeleteObject
            if (_hBitmap != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_hBitmap);
                _hBitmap = IntPtr.Zero;
            }
            //Khong the dung cai nay, phai dung DeleteDC
            if (_memDC != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_memDC);
                _memDC = IntPtr.Zero;
            }


            if (_bits != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_bits);
                _bits = IntPtr.Zero;
            }
           

            if (disposing)
            {
            }
        }

        ~VScreen()
        {
            Dispose(false);
        }
        #endregion
    }
}
