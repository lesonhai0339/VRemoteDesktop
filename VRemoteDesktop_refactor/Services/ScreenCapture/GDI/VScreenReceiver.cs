using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.DTOs;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Interop;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Utils;
using Vsign4.VRemoteDesktop.Utils;

namespace Vsign4.VRemoteDesktop.Services.ScreenCapture.GDI
{
    public interface IVScreenReceiver
    {
        IntPtr Bits { get; }
        Interop.CaptureApi.BITMAPINFO BITMAPINFO { get; }
        IntPtr MemDC { get; }
        IntPtr ScreenHDC { get; }
        int Width { get; }
        int Height { get; }
        int Stride { get; }
        PixelFormat PixelFormat { get; }
        List<Rectangle> DecompressedRawData(byte[] data, int offset, int length);
        void Dispose();
    }
    public class VScreenReceiver : VScreen, IVScreenReceiver, IDisposable
    {
        /// <summary>
        /// <para><see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-emf/a5e722e3-891a-4a67-be1a-ed5a48a7fda1"/></para>
        /// </summary>
        private const uint DIB_RGB_COLORS = 0;

        private readonly object _lock = new object();
        private readonly int _width;
        private readonly int _height;
        private readonly int _regionSize;
        private readonly int _bytePerPixel;
        //private readonly Rectangle[] _rectangles;
        private List<Rectangle> _rectangles;

        private readonly Interop.CaptureApi.BITMAPINFO _bitmapInfo;
        private readonly PixelFormat _pixelFormat;
        private readonly ScreenTask _screenTask;

        private int _disposed = 0;
        //bitmap 1
        private IntPtr _hBitmap;
        private IntPtr _bits;        // points to raw pixels
        private IntPtr _memDC;

        private readonly Bitmap _bitmap;
        public VScreenReceiver(ScreenTask screenTask, int width, int height, int bytePerPixel, PixelFormat pixelFormat, int regionSize = 16)
            : base(width, height, bytePerPixel, regionSize)
        {
            _width = width;
            _height = height;
            _regionSize = regionSize;
            _bytePerPixel = bytePerPixel;

            //_rectangles = base.InitRectangle(_width, _height);
            _rectangles = new List<Rectangle>();
            _bitmapInfo = base.InitBitmapInfo(_width, _height, (ushort)(_bytePerPixel * 8), 0);
            _screenTask = screenTask;
            _pixelFormat = pixelFormat;

            base.InitCaptureBuffer(ref _hBitmap, ref _memDC, ref _bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            _bitmap = new Bitmap(_width, _height, base.GetStride1(_width, _bytePerPixel), _pixelFormat, _bits);
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
        public Interop.CaptureApi.BITMAPINFO BITMAPINFO
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
                if (_bits != IntPtr.Zero)
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
        #region Methods
        public List<Rectangle> DecompressedRawData(byte[] data, int offset, int length)
        {
            try
            {
                int decompressLength = Compressor.DeCompressedLZ4(data, offset, length, _screenTask.Buffer);
                MergeRegionToImage(_screenTask.Buffer, decompressLength);
                return MergeRegions(_rectangles);
            }
            catch (Exception ex)
            {
                // Corrupt/oversized compressed data must not crash the receive pipeline.
                Logger.Log.ForContext("FileName", GetType().Name).Error(ex, "DecompressedRawData error");
                return new List<Rectangle>();
            }
        }
        private unsafe void MergeRegionToImage(byte[] packet, int actualLength)
        {
            _rectangles.Clear();
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

                    // Validate the region against the destination bounds AND the amount of
                    // decompressed data available BEFORE the native copy. A corrupt/truncated
                    // region would otherwise read past the valid data or (via overflow) write
                    // out of bounds. A bad region means the stream is desynced, so stop here.
                    long regionBytes = (long)w * h * _bytePerPixel;
                    if (w <= 0 || h <= 0 || x < 0 || y < 0
                        || x > _width - w || y > _height - h
                        || regionBytes <= 0 || offset + regionBytes > actualLength)
                    {
                        break;
                    }

                    offset += base.MergeRegionToSource(srcPtr, x, y, w, h, _bits, _width, _height, _bytePerPixel);

                    lock (_lock)
                    {
                        _rectangles.Add(new Rectangle(x, y, w, h));
                    }
                }
            }
        }
        #endregion
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

            try
            {
                if (disposing)
                {
                    if (_bitmap != null)
                    {
                        _bitmap.Dispose();
                    }
                }
            }
            catch { }
        }
    }
}
