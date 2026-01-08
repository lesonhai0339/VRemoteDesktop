using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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
        private readonly object _lock = new object();
        private const int BUFFER_SIZE = 20 * 1024 * 1024; //20MB
        private const uint DIB_RGB_COLORS = 0;
        private const int BYTE_PER_PIXEL = 3;   
        private const int REGION_SIZE = 16;  


        private int _disposed;
        private IntPtr _bufferPool;
        private IntPtr _testPool;
        private IntPtr _hBitmap;
        private IntPtr _bits;        // points to raw pixels
        private IntPtr _memDC;
        private Rectangle _bounds;
        private Rectangle[] rectangles;
        private BackgroundWorker _worker;
        private Bitmap _bitmap;
        private int count = 0;
        public VScreen()
        {
            _bounds = Screen.PrimaryScreen.Bounds;

            _bitmap = new Bitmap(_bounds.Width, _bounds.Height, PixelFormat.Format24bppRgb);

            int cols = (_bounds.Width + REGION_SIZE - 1) / REGION_SIZE;
            int rows = (_bounds.Height + REGION_SIZE - 1) / REGION_SIZE;

            rectangles = new Rectangle[cols * rows];

            InitRectangle(_bounds.Width, _bounds.Height);


            _bufferPool = Marshal.AllocHGlobal(BUFFER_SIZE);
            _testPool = Marshal.AllocHGlobal(BUFFER_SIZE);
            InitCaptureBuffer(_bounds.Width, _bounds.Height);

            _worker = new BackgroundWorker();
            _worker.DoWork += Handler;
        }
        public void Test()
        {
            CaptureToBuffer();
            SaveScreen(_bounds.Width, _bounds.Height, BYTE_PER_PIXEL, _bits, _bufferPool);
            SaveToBitmap();

            if (!_worker.IsBusy)
                _worker.RunWorkerAsync();


            while (true)
            {
                Console.WriteLine("\n------------------------\n");
                Thread.Sleep(1000);
            }
        } 
        public unsafe void SaveToBitmap()
        {
            MergeRegionToSource((IntPtr)_bits, 0, 0 , _bounds.Width, _bounds.Height);
            _bitmap.Save($"D:\\VinhHy\\Images\\08_01_2025\\{count}.png", ImageFormat.Png);
            count++;
        }
        public void SaveRegion(int x, int y, int w, int h)
        {
            if (_bitmap == null) return;

            MergeRegionToSource(_bits, x, y, w, h);   
        }
        public void MergeRegionToSource(IntPtr source , int x, int y, int width, int height)
        {

            BitmapData bmpData =  _bitmap.LockBits(new Rectangle(x, y, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);    

            int srcStride = GetStride(_bounds.Width, BYTE_PER_PIXEL);
            unsafe
            {
                byte* srcBase = (byte*)source;
                byte* dstBase = (byte*)bmpData.Scan0;   

                for(int row = 0; row < height; row++)
                {
                    int sy = y + height - row - 1;
                    var srcPtr = srcBase + ((sy * srcStride) + (x * BYTE_PER_PIXEL));
                    var dstPtr = dstBase + (row * bmpData.Stride);

                    CaptureApis.memcpy((IntPtr)dstPtr, (IntPtr)srcPtr, (UIntPtr)(width * BYTE_PER_PIXEL));    
                }
            }
            _bitmap.UnlockBits(bmpData);
        }
        public int MergeRegionToSource1(IntPtr regionChanged, int x, int y, int width, int height)
        {

            BitmapData bmpData = _bitmap.LockBits(new Rectangle(x, y, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int srcStride = GetStride(_bounds.Width, BYTE_PER_PIXEL);
            unsafe
            {
                byte* srcBase = (byte*)regionChanged;
                byte* dstBase = (byte*)bmpData.Scan0;

                for (int row = 0; row < height; row++)
                {
                    int sy = y + height - row - 1;
                    var srcPtr = srcBase + ((sy * srcStride) + (x * BYTE_PER_PIXEL));
                    var dstPtr = dstBase + (row * bmpData.Stride);

                    CaptureApis.memcpy((IntPtr)dstPtr, (IntPtr)srcPtr, (UIntPtr)(width * BYTE_PER_PIXEL));
                }
            }
            _bitmap.UnlockBits(bmpData);

             return width * height * BYTE_PER_PIXEL;
        }
        /// <summary>
        /// Only use for test
        /// </summary>
        /// <param name="source"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public int MergeRegionToSource2(IntPtr source, int x, int y, int width, int height)
        {

            BitmapData bmpData = _bitmap.LockBits(new Rectangle(x, y, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int srcStride = GetStride(width, BYTE_PER_PIXEL);
            unsafe
            {
                byte* srcBase = (byte*)source;
                byte* dstBase = (byte*)bmpData.Scan0;

                for (int row = 0; row < height; row++)
                {
                    int sy = height - row - 1;
                    var srcPtr = srcBase + (sy * srcStride);
                    var dstPtr = dstBase + (row * bmpData.Stride);

                    CaptureApis.memcpy((IntPtr)dstPtr, (IntPtr)srcPtr, (UIntPtr)(width * BYTE_PER_PIXEL));
                }
            }
            _bitmap.UnlockBits(bmpData);

            return height * srcStride;
        }
        #region Methods
        #region Worker
        private void Handler(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                int offset = 0;
                CaptureToBuffer();

                var changedRectArray = rectangles.Where(x => IsRegionChangeInRange(5, _bufferPool, _bits, x.X, x.Y, x.Width, x.Height)).ToArray();
                MergeRect(changedRectArray);
                /*for (int i = 0; i < rectangles.Length; i++)
                {
                    if (IsRegionChangeInRange(5, _bufferPool, 
                        _bits, 
                        rectangles[i].X, 
                        rectangles[i].Y, 
                        rectangles[i].Width, 
                        rectangles[i].Height
                    ))
                    {
                        //SaveRegion(rectangles[i].X, rectangles[i].Y, rectangles[i].Width, rectangles[i].Height);
                        GetRegionData(ref offset, _testPool, _bits,
                            rectangles[i].X,
                            rectangles[i].Y,
                            rectangles[i].Width,
                            rectangles[i].Height
                        );
                    }
                }
                if(offset > 0)
                {
                    byte[] regionsData = new byte[offset];
                    Marshal.Copy(_testPool, regionsData, 0, regionsData.Length);
                    ParsePacketToRegionsChange(regionsData);

                    SaveToBitmap();
                }


*/
                //SaveToBitmap();
                //SaveScreen(_bounds.Width, _bounds.Height, BYTE_PER_PIXEL, _bits, _bufferPool);
                Thread.Sleep(1000);
            }
        }
        #endregion
        #region Initialize
        private void InitRectangle(int width, int height)
        {
            int index = 0;
            for (int i = 0; i < height; i += REGION_SIZE)
            {
                for (int j = 0; j < width; j += REGION_SIZE)
                {
                    int w = Math.Min(REGION_SIZE, width - j);
                    int h = Math.Min(REGION_SIZE, height - i);

                    rectangles[index++] = new Rectangle(j, i, w, h);
                }
            }
        }
        public void MergeRect(Rectangle[] rectangles)
        {
            var groups = rectangles.GroupBy(x => x.Top / REGION_SIZE)
                .ToDictionary(g => g.Key, g => g.ToArray())
                .Select(k => k.Value)
                .ToList();

        }
        private Rectangle CanMerge(Rectangle[] regions, int y, int height)
        {
            var xMin = regions.Min(r => r.Left);
            var xMax = regions.Max(r => r.Right);
            return new Rectangle(xMin, y, xMax - xMin, height);
        }
        private void InitCaptureBuffer(int width, int height)
        {
            var bmi = new BITMAPINFO();
            bmi.Header.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.Header.biWidth = width;
            bmi.Header.biHeight = -height; //top-down
            bmi.Header.biPlanes = 1;
            bmi.Header.biBitCount = 24; //24 bits per pixel
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
        #endregion
        #region converter
        unsafe void Convert32To24(
           byte* src,    // _bits from 32bpp DIB
           byte* dst,    // destination buffer
           int width,
           int height)
        {
            int srcStride = GetStride(width , 4);
            int dstStride = GetStride(width , 3);

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
           byte* dst,    //destination buffer
           int width,
           int height)
        {
            int srcStride = GetStride(width, 4);
            int dstStride = GetStride(width, 2);

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
            int srcStride = GetStride(width, 2);
            int dstStride = GetStride(width, 3);

            for (int y = 0; y < height; y++)
            {
                ushort* s = (ushort*)(src + y * srcStride);
                byte* d = dst + y * dstStride;

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
        #endregion
        private int GetStride(int width, int bytePerPixel)
        {
            return ((width * bytePerPixel) + 3) & ~3;   
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
            int srcStride = GetStride(_bounds.Width , BYTE_PER_PIXEL);
            uint dstStride = (uint)GetStride(regionWidth , BYTE_PER_PIXEL);

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

                    IntPtr srcAdd = (IntPtr)(src + sy + sx); //formular => address = base + rowOffset + colOffset 

                    IntPtr dstAdd = (IntPtr)(dst + (row * dstStride)); //formular => address = base + (row * rowStride) 

                    CaptureApis.memcpy(dstAdd, srcAdd, (UIntPtr)dstStride);
                }
            }
            offset += (int)(dstStride * regionHeight) + 16;
        }
        public unsafe void ParsePacketToRegionsChange(byte[] packet)
        {
            fixed (byte* pPacket = packet)
            {
                int offset = 0;
                int length = packet.Length;
                while (offset + 16 <= length)
                {
                    // get pointer to current position
                    byte* src = pPacket + offset;

                    // move to uint pointer, each d++ move 4 bytes
                    uint* d = (uint*)src;

                    int x = (int)*d++;
                    int y = (int)*d++;
                    int w = (int)*d++;
                    int h = (int)*d++;

                    // back to single byte pointer, each d++ move 1 byte
                    IntPtr dst = (IntPtr)d;

                    offset += 16;

                    offset += MergeRegionToSource2(dst, x, y, w, h);

                    Console.WriteLine($"Expected: {packet.Length} -  Current: {offset}");
                }
            }
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

                    byte* pOld = (oBase + sy + sx);
                    byte* pNew = (nBase + sy + sx);

                    for (int col = 0; col < regionWidth; col++)
                    {
                        if (pOld[0] != pNew[0] ||
                            pOld[1] != pNew[1] ||
                            pOld[2] != pNew[2])
                        {
                            return true;
                        }

                        pOld += BYTE_PER_PIXEL;
                        pNew += BYTE_PER_PIXEL; 
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// Compare region with range, range is acceptable difference color value. Example range = 5, if R,G,B difference less than 5, it is considered unchanged
        /// </summary>
        /// <param name="range"></param>
        /// <param name="oldScreen"></param>
        /// <param name="newScreen"></param>
        /// <param name="regionX"></param>
        /// <param name="regionY"></param>
        /// <param name="regionWidth"></param>
        /// <param name="regionHeight"></param>
        /// <returns></returns>
        public unsafe bool IsRegionChangeInRange(int range, IntPtr oldScreen, IntPtr newScreen, int regionX, int regionY, int regionWidth, int regionHeight)
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

                    byte* pOld = (oBase + sy + sx);
                    byte* pNew = (nBase + sy + sx);

                    for (int col = 0; col < regionWidth; col++)
                    {
                        if (Math.Abs(pOld[0] - pNew[0]) > range ||
                            Math.Abs(pOld[1] - pNew[1]) > range ||
                            Math.Abs(pOld[2] - pNew[2]) > range)
                                return true;

                        pOld += BYTE_PER_PIXEL;
                        pNew += BYTE_PER_PIXEL;
                    }
                }
            }
            return false;
        }
        private unsafe void SaveScreen(int width, int height, int bytePerPixel, IntPtr src, IntPtr dst)
        {
            int stride = ((width * bytePerPixel) + 3) & ~3;
            int count = stride * height;
            CaptureApis.memcpy(dst, src, (UIntPtr)count);
        }
        #endregion
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

            // Khong the dung cai nay phai dung DeleteObject
            if (_hBitmap != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_hBitmap);
                _hBitmap = IntPtr.Zero;
            }
            // Khong the dung cai nay, phai dung DeleteDC
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
            if (_bufferPool != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_bufferPool);
                _bufferPool = IntPtr.Zero;
            }
            if (_testPool != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_testPool);
                _testPool = IntPtr.Zero;
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
