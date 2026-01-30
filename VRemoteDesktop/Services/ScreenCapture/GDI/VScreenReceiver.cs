using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
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
        Rectangle DecompressedRawData(byte[] data, int offset, int length);
        void Dispose();
    }   
    public class VScreenReceiver : VScreen, IVScreenReceiver, IDisposable
    {
        private readonly object _lock = new object();
        private const uint DIB_RGB_COLORS = 0;
        private int _disposed;

        private readonly int _width;
        private readonly int _height;
        private readonly int _regionSize;
        private readonly int _bytePerPixel;
        private Rectangle[] _rectangles;
        private int count = 0;

        private BITMAPINFO _bitmapInfo;

        //bitmap 1
        private IntPtr _hBitmap;
        private IntPtr _bits;        // points to raw pixels
        private IntPtr _memDC;

        private readonly ScreenTask _screenTask;

        private Bitmap _bitmap;
        private readonly PixelFormat _pixelFormat;
        public VScreenReceiver(ScreenTask screenTask, int width, int height, int bytePerPixel, PixelFormat pixelFormat, int regionSize = 16)
        {
            _screenTask = screenTask;
            _width = width;
            _height = height;
            _regionSize = regionSize;
            _bytePerPixel = bytePerPixel;
            _pixelFormat = pixelFormat;
            InitializeReceiverComponents();
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
                return base.GetStride1(_width, _bytePerPixel);
            }
        }   
        public PixelFormat PixelFormat
        {
            get
            {
                return _pixelFormat;
            }
        }
        #endregion
        private void InitializeReceiverComponents()
        {
            _bitmapInfo = base.InitBitmapInfo(_width, _height, (ushort)(_bytePerPixel * 8), 0);
            base.InitCaptureBuffer(ref _hBitmap, ref _memDC, ref _bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            _rectangles = base.InitRectangle(_width, _height);

            _bitmap = new Bitmap(_width, _height, base.GetStride1(_width, _bytePerPixel), _pixelFormat, _bits);
        }
        public Rectangle DecompressedRawData(byte[] data, int offset, int length)
        {
            int decompressLength = Compressor.DeCompressedLZ4(data, offset, length, _screenTask.Buffer);
            return MergeRegionToImage(_screenTask.Buffer, decompressLength);
        }
        private unsafe Rectangle MergeRegionToImage(byte[] packet, int actualLength)
        {
            Rectangle rect = Rectangle.Empty;
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

                    offset += base.MergeRegionToSource(srcPtr, x, y, w, h, _bits, _width, _height, _bytePerPixel);

                    if (rect.IsEmpty)
                    {
                        rect = new Rectangle(x, y, w, h);
                    }
                    else
                    {
                        rect = Rectangle.Union(rect, new Rectangle(x, y, w, h));
                    }
                }
            }
            return rect;
        }
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