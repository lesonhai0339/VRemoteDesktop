using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Threading;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.Enums;
using VRemoteDesktop.Services.ScreenCapture.Interop;
using VRemoteDesktop.Services.ScreenCapture.Utils;
using static VRemoteDesktop.Services.ScreenCapture.Interop.CaptureApi;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public interface IVScreenReceiver
    {
        IntPtr Bits { get; }
        BITMAPINFO BITMAPINFO { get; }
        IntPtr MemDC { get; }
        IntPtr ScreenHDC { get; }
        int Width { get; }
        int Height { get; }
        int Stride { get; }
        PixelFormat PixelFormat { get; }
        List<Rectangle> DecompressedRawData(byte[] data, int offset, int length, bool isFullScreen = false);
    }   
    public class VScreenReceiver : VScreen, IVScreenReceiver, IDisposable
    {
        private const uint DIB_RGB_COLORS = 0;
        private const int BYTE_PER_PIXEL = 3;
        private const int REGION_SIZE = 16;

        private int _disposed;

        private int _width;
        private int _height;
        private Rectangle[] _rectangles;
        private int count = 0;

        private BITMAPINFO _bitmapInfo;

        //bitmap 1
        private IntPtr _hBitmap;
        private IntPtr _bits;        // points to raw pixels
        private IntPtr _memDC;

        private readonly ScreenTask _screenTask;

        private Bitmap _bitmap;
        public VScreenReceiver(int width, int height, ScreenTask screenTask)
        {
            _screenTask = screenTask;
            InitializeReceiverComponents(width, height);
        }
        #region Properties
        public IntPtr Bits
        {
            get
            {
                return _bits;
            }
        }
        public IntPtr MemDC
        {
            get
            {
                return _memDC;
            }
        }
        public BITMAPINFO BITMAPINFO
        {
            get
            {
                return _bitmapInfo;
            }
        }
        public IntPtr ScreenHDC
        {
            get
            {
                if(_bits != IntPtr.Zero)
                {
                    return _bits;  
                }
                throw new InvalidOperationException("Screen HDC is empty");
            }
        }
        public int Width
        {
            get
            {
                return _width;
            }
        }
        public int Height
        {
            get
            {
                return _height;
            }
        }
        public int Stride
        {
            get
            {
                return base.GetStride1(_width, BYTE_PER_PIXEL);
            }
        }   
        public PixelFormat PixelFormat
        {
            get
            {
                return PixelFormat.Format24bppRgb;
            }
        }
        #endregion
        private void InitializeReceiverComponents(int partnerWidth, int partnerHeight)
        {
            _width = partnerWidth;
            _height = partnerHeight;

            _bitmapInfo = base.InitBitmapInfo(Width, Height, BYTE_PER_PIXEL * 8, 0);

            base.InitCaptureBuffer(ref _hBitmap, ref _memDC, ref _bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            _rectangles = base.InitRectangle(_width, _height);

            _bitmap = new Bitmap(_width, _height, base.GetStride1(_width, BYTE_PER_PIXEL), PixelFormat.Format24bppRgb, _bits);
        }
        public List<Rectangle> DecompressedRawData(byte[] data, int offset, int length, bool isFullScreen = false)
        {
            int decompressLength = Compressor.DeCompressedLZ4(data, offset, length, _screenTask.Buffer);
            if (isFullScreen)
            {
                return MergeFullScreenToBitmap(_screenTask.Buffer, decompressLength);
            }
            else
            {
                return ParsePacketToRegionsChange(_screenTask.Buffer, decompressLength);
            }
        }   
        private unsafe List<Rectangle> ParsePacketToRegionsChange(byte[] packet, int actualLength)
        {
            List<Rectangle> rectangles = new List<Rectangle>();
            fixed (byte* pPacket = packet)
            {
                int offset = 0;
                while (offset + 16 <= actualLength)
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
                    IntPtr srcPtr = (IntPtr)d;

                    offset += 16;

                    offset += base.MergeRegionToSource(srcPtr, x, y, w, h, _bits, _width, _height);

                    rectangles.Add(new Rectangle(x, y, w, h));
                }
            }
#if DEBUG
            //Test11(GetStride1(_width, BYTE_PER_PIXEL), _bits);
#endif
           return rectangles;
        }
        private unsafe List<Rectangle> MergeFullScreenToBitmap(byte[] packet, int actualLength)
        {
            byte* dst = (byte*)_bits;
            int srcStride = _width * 3;
            int dstStride = base.GetStride1(_width, BYTE_PER_PIXEL);

            fixed (byte* pSrc = packet)
            {       
                for (int y = 0; y < _height; y++)
                {
                    IntPtr srcRow = (IntPtr)(pSrc + (y * srcStride));
                    IntPtr dstRow = (IntPtr)(dst + (y * dstStride));

                    CaptureApi.memcpy(dstRow, srcRow, (UIntPtr)srcStride);
                }
            }
#if DEBUG
            //Test11(dstStride, (IntPtr)dst, true);
#endif
            return new List<Rectangle> { new Rectangle(0, 0, _width, _height) };
        }
#if DEBUG
        private void Test11(int stride, IntPtr source, bool isFullScreen = false)
        {
            string name = isFullScreen ? "FullScreen" : "RegionChange";
            _bitmap.Save($"D:\\VinhHy\\Images\\08_01_2025\\{name}_{count}.png", ImageFormat.Png);
            count++;
        }
#endif
        public override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            // Free resources bitmap 1
            if (_hBitmap != IntPtr.Zero)
                CaptureApi.DeleteObject(_hBitmap);

            if (_memDC != IntPtr.Zero)
                CaptureApi.DeleteDC(_memDC);

            _bits = IntPtr.Zero;

            if (disposing)
            {
            }
        }
    }
}