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
using VRemoteDesktop.Enums;
using VRemoteDesktop.Helpers;
using static VRemoteDesktop.Interop.Win32Apis;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public class VScreenSender: IDisposable
    {
        private const uint DIB_RGB_COLORS = 0;
        private const int BYTE_PER_PIXEL = 3;   
        private const int REGION_SIZE = 16;  

        private int _disposed;

        private int _width;
        private int _height;
        private VScreenType _type;
        private Rectangle[] _rectangles;
        private BackgroundWorker _worker;
        private CancellationTokenSource _cancellationTokenSource;
        private int count = 0;

        #region Resources for sender
        // Filed mapping buffer pool
        private IntPtr _fileMappingPtr;

        //bitmap 1
        private IntPtr _hBitmap;
        private IntPtr _bits;        // points to raw pixels
        private IntPtr _memDC;

        //bitmap 2
        private IntPtr _hBitmap1;
        private IntPtr _bits1;        // points to raw pixels
        private IntPtr _memDC1;

        //bitmap 3
        private IntPtr _hBitmap2;
        private IntPtr _bits2;        // points to raw pixels
        private IntPtr _memDC2;


        // Manager 3 bitmap
        IntPtr[] _allDCs = new IntPtr[3];
        IntPtr[] _allBits = new IntPtr[3];
        private int backIdx = 0;
        private int frontIdx = 1;
        private int prevIdx = 2;
        #endregion

        #region Resources for receiver
        //bitmap 4
        private IntPtr _receiverHBitmap;
        private IntPtr _receiverBits;        // points to raw pixels
        private IntPtr _receiverMemDC;
        #endregion


#if DEBUG
        private IntPtr _bufferPool;
#endif
        public VScreenSender()
        {
            _cancellationTokenSource = new CancellationTokenSource();
#if DEBUG
            _bufferPool = Marshal.AllocHGlobal(10 * 1024 * 1024);
#endif
        }
        public void Test()
        {
            if (_type == VScreenType.Sender)
            {
                if (!_worker.IsBusy)
                    _worker.RunWorkerAsync();
            }

            int a = 0;
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                Console.WriteLine($"\n-------------{a}-----------\n");
                Thread.Sleep(1000);
                a++;
                if (a % 5 == 0)
                {
                    //Cancel();
                    GetFullScreen();

                }
                if (a == 30)
                    Cancel();
            }
        }



        #region Common
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
        private void InitCaptureBuffer(int width, int height, ref IntPtr hBitmap, ref IntPtr memDC, ref IntPtr bits, IntPtr sectionPool, uint offset)
        {
            var bmi = new BITMAPINFO();
            bmi.Header.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            bmi.Header.biWidth = width;
            bmi.Header.biHeight = -height; //top-down
            bmi.Header.biPlanes = 1;
            bmi.Header.biBitCount = 24; //24 bits per pixel
            bmi.Header.biCompression = 0; //BI_RGB

            IntPtr screenDC = CaptureApis.GetDC(IntPtr.Zero);
            hBitmap = CaptureApis.CreateDIBSection(
              screenDC,
              ref bmi,
              DIB_RGB_COLORS,
              out bits,
              sectionPool,
              offset);

            if (hBitmap == IntPtr.Zero)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Exception("CreateDIBSection failed with error: " + error);
            }

            memDC = CaptureApis.CreateCompatibleDC(screenDC);
            CaptureApis.SelectObject(memDC, hBitmap);

            CaptureApis.ReleaseDC(IntPtr.Zero, screenDC);
        }
        /// <summary>
        /// Init rectangles array by width and height of full screen
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        private void InitRectangle(int width, int height)
        {
            int index = 0;
            for (int i = 0; i < height; i += REGION_SIZE)
            {
                for (int j = 0; j < width; j += REGION_SIZE)
                {
                    int w = Math.Min(REGION_SIZE, width - j);
                    int h = Math.Min(REGION_SIZE, height - i);

                    _rectangles[index++] = new Rectangle(j, i, w, h);
                }
            }
        }
        private int GetStride(int width, int bytePerPixel)
        {
            return ((width * bytePerPixel) + 3) & ~3;
        }
        #endregion



        #region Sender
        public void ChangeToSender()
        {
            _type = VScreenType.Sender;
            InitializeSenderComponents();
            //Start worker to schedule capture screen
        }
        private void InitializeSenderComponents()
        {
            var bound = Screen.PrimaryScreen.Bounds;
            _width = bound.Width;
            _height = bound.Height;

            // Create shared memory for DIBSection https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createfilemappinga
            // It is hSection in https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection
            _fileMappingPtr = CaptureApis.CreateFileMappingA(
                new IntPtr(-1),
                IntPtr.Zero,
                0x40, //PAGE_EXECUTE_READWRITE
                0,
                30 * 1024 * 1024, //Allocate 30MB shared memory
                null
                );

            uint pre = 0; //10MB
            uint cur = 10 * 1024 * 1024; ; //10MB
            uint next = 20 * 1024 * 1024; //10MB 

            InitCaptureBuffer(_width, _height, ref _hBitmap, ref _memDC, ref _bits, _fileMappingPtr, pre);
            InitCaptureBuffer(_width, _height, ref _hBitmap1, ref _memDC1, ref _bits1, _fileMappingPtr, cur);
            InitCaptureBuffer(_width, _height, ref _hBitmap2, ref _memDC2, ref _bits2, _fileMappingPtr, next);

            _allDCs = new IntPtr[] { _memDC, _memDC1, _memDC2 };
            _allBits = new IntPtr[] { _bits, _bits1, _bits2 };


            int cols = (_width + REGION_SIZE - 1) / REGION_SIZE;
            int rows = (_height + REGION_SIZE - 1) / REGION_SIZE;

            _rectangles = new Rectangle[cols * rows];
            InitRectangle(_width, _height);


            _worker = new BackgroundWorker();
            _worker.DoWork += Handler;
        }
        //Chua xong
        public void GetFullScreen()
        {
            int offset = 0;
            GetFullScreenData(ref offset, _bufferPool, _allBits[frontIdx], 0, 0, _width, _height);
            MergeRegionToSource2(_bufferPool, 0, 0, _width, _height);   
        }
        // dung de test
        public int MergeRegionToSource2(IntPtr source, int x, int y, int width, int height)
        {
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            int srcStride = GetStride(width, BYTE_PER_PIXEL);
            unsafe
            {
                byte* srcBase = (byte*)source;
                byte* dstBase = (byte*)bmpData.Scan0;

                for (int row = 0; row < height; row++)
                {
                    //int sy = height - row - 1;
                    int sy = row;
                    var srcPtr = srcBase + (sy * srcStride);
                    var dstPtr = dstBase + (row * bmpData.Stride);

                    CaptureApis.memcpy((IntPtr)dstPtr, (IntPtr)srcPtr, (UIntPtr)(width * BYTE_PER_PIXEL));
                }
            }
            bmp.UnlockBits(bmpData);
            bmp.Save($"D:\\VinhHy\\Images\\08_01_2025\\{count}.png", ImageFormat.Png);
            count++;
            return height * srcStride;
        }
        public void CaptureToBuffer(IntPtr memDC)
        {
            IntPtr screenDC = CaptureApis.GetDC(IntPtr.Zero);

            CaptureApis.BitBlt(
                memDC,
                0, 0,
                _width,
                _height,
                screenDC,
                0, //full screen then x = 0
                0, // full screen then y = 0
                0x00CC0020); // SRCCOPY

            CaptureApis.ReleaseDC(IntPtr.Zero, screenDC);
            CaptureApis.GdiFlush();
        }
        public List<Rectangle> MergeDirtyRegions(Rectangle[] dirtyRegions, double threshold = 0.8)
        {
            if (dirtyRegions == null || dirtyRegions.Length == 0)
                return new List<Rectangle>();

            var dirtyRegionsSorted = dirtyRegions.OrderBy(x => x.Top)
                .ThenBy(x => x.Left)
                .ToList();

            List<Rectangle> groups = new List<Rectangle>();

            // Using first rectangle as base
            var baseRect = dirtyRegionsSorted[0];

            // For-loop to try to merge other rectangles
            for (int i = 1; i < dirtyRegionsSorted.Count; i++)
            {
                // Get current rectangle    
                var rect = dirtyRegionsSorted[i];

                // Union base rectangle and current rectangle
                var area = Rectangle.Union(baseRect, rect);

                // Get acreage of union area
                var areaUnion = area.Width * area.Height;
                // Get acreage sum of base rectangle and current rectangle
                var areaSum = baseRect.Width * baseRect.Height + rect.Width * rect.Height;

                // Calculate ratio between union area and acreage sum
                var ratio = (double)areaSum / areaUnion;

                // If ratio > threshold, assign union area to base rectangle and continue try to merge next rectangle
                if (ratio > threshold)
                {
                    baseRect = area;
                }
                // Else, add base rectangle to groups result and assign current as a new base rectangle
                else
                {
                    groups.Add(baseRect);
                    baseRect = rect;
                }
            }

            // Finally, add last base rectangle to groups result
            groups.Add(baseRect);

            return groups;
        }
        public unsafe void GetRegionsData(ref int offset, IntPtr destination, IntPtr source, int regionX, int regionY, int regionWidth, int regionHeight)
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
            int srcStride = GetStride(_width, BYTE_PER_PIXEL);
            uint dstStride = (uint)GetStride(regionWidth, BYTE_PER_PIXEL);

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
        public unsafe void GetFullScreenData(ref int offset, IntPtr destination, IntPtr source, int regionX, int regionY, int regionWidth, int regionHeight)
        {
            byte* dst = (byte*)destination + offset;

            int srcStride = GetStride(_width, BYTE_PER_PIXEL);
            uint dstStride = (uint)regionWidth * BYTE_PER_PIXEL; //Only send raw data without padding

            unsafe
            {
                //get address of source and destination 
                byte* src = (byte*)source;

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
            offset += (int)(dstStride * regionHeight);
        }
        public unsafe bool IsRegionChange(IntPtr oldScreen, IntPtr newScreen, int regionX, int regionY, int regionWidth, int regionHeight)
        {
            int stride = GetStride(_width , BYTE_PER_PIXEL);
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
        public unsafe bool IsRegionChangeInRange(int range, IntPtr oldScreen, IntPtr newScreen, int regionX, int regionY, int regionWidth, int regionHeight)
        {
            int stride = GetStride(_width, BYTE_PER_PIXEL);
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
        //Chua xong
        private void Handler(object sender, DoWorkEventArgs e)
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {

                CaptureToBuffer(_allDCs[backIdx]);
                Console.WriteLine($"{backIdx} - {frontIdx} - {prevIdx}");

                // 0, 1, 2 -> 2 , 0, 1 -> 1, 2, 0 -> ...
                int tempPrev = prevIdx;
                prevIdx = frontIdx;
                frontIdx = backIdx;
                backIdx = tempPrev;

                var changedRectArray = _rectangles.Where(x =>
                        IsRegionChangeInRange(
                            5,
                            _allBits[prevIdx],
                            _allBits[frontIdx],
                            x.X,
                            x.Y,
                            x.Width,
                            x.Height)).ToArray();

                var result = MergeDirtyRegions(changedRectArray);
                Thread.Sleep(1000);
            }
        }
        #endregion





        #region Receiver
        public void ChangeToReceiver(int partnerWidth, int partnerHeight)
        {
            _type = VScreenType.Receiver;
            InitializeReceiverComponents(partnerWidth, partnerHeight);
        }
        private void InitializeReceiverComponents(int partnerWidth, int partnerHeight)
        {
            _width = partnerWidth;
            _height = partnerHeight;

            InitCaptureBuffer(_width, _height, ref _hBitmap, ref _memDC, ref _bits, IntPtr.Zero, 0);

            int cols = (_width + REGION_SIZE - 1) / REGION_SIZE;
            int rows = (_height + REGION_SIZE - 1) / REGION_SIZE;

            _rectangles = new Rectangle[cols * rows];
            InitRectangle(_width, _height);
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

                    //offset += MergeRegionToSource(dst, x, y, w, h);
                }
            }
        }
        public int MergeRegionToSource(IntPtr source, IntPtr dest, int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x + width > _width || y + height > _height)
                return 0;

            int srcStride = width * BYTE_PER_PIXEL; //No padding in source buffer
            int dstStride = GetStride(_width, BYTE_PER_PIXEL);  
            unsafe
            {
                byte* srcBase = (byte*)source;
                byte* dstBase = (byte*)dest;

                byte* dst = dstBase + (y * dstStride) + (x * BYTE_PER_PIXEL);
                for (int row = 0; row < height; row++)
                {
                    var srcPtr = srcBase + (row * srcStride);
                    var dstPtr = dst + (row * dstStride);

                    CaptureApis.memcpy((IntPtr)dstPtr, (IntPtr)srcPtr, (UIntPtr)(width * BYTE_PER_PIXEL));
                }
            }
            return height * srcStride;
        }
        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            // Free resources bitmap 1
            if (_hBitmap != IntPtr.Zero)
                CaptureApis.DeleteObject(_hBitmap);

            if (_memDC != IntPtr.Zero)
                CaptureApis.DeleteDC(_memDC);

            _bits = IntPtr.Zero;


            // Free resources bitmap 2
            if (_hBitmap != IntPtr.Zero)
                CaptureApis.DeleteObject(_hBitmap);

            if (_memDC != IntPtr.Zero)
                CaptureApis.DeleteDC(_memDC);

            _bits1 = IntPtr.Zero;

            // Free resources bitmap 2
            if (_hBitmap2 != IntPtr.Zero)
                CaptureApis.DeleteObject(_hBitmap2);

            if (_memDC2 != IntPtr.Zero)
                CaptureApis.DeleteDC(_memDC2);

            _bits2 = IntPtr.Zero;

            // Free file mapping
            if (_fileMappingPtr != IntPtr.Zero)
                CaptureApis.CloseHandle(_fileMappingPtr);


#if DEBUG
            if (_bufferPool != IntPtr.Zero)
                Marshal.FreeHGlobal(_bufferPool);
#endif

            if (disposing)
            {
                Cancel();
            }
        }

        ~VScreenSender()
        {
            Dispose(false);
        }
    }
}
