using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Threading;
using VRemoteDesktop.Services.ScreenCapture.Interop;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public class VScreenReceiver : VScreen
    {
        private const uint DIB_RGB_COLORS = 0;
        private const int BYTE_PER_PIXEL = 3;
        private const int REGION_SIZE = 16;

        private int _disposed;

        private int _width;
        private int _height;
        private Rectangle[] _rectangles;
        private int count = 0;

        //bitmap 1
        private IntPtr _hBitmap;
        private IntPtr _bits;        // points to raw pixels
        private IntPtr _memDC;

        Bitmap _bitmap;
        public VScreenReceiver(int width, int height)
        {
            InitializeReceiverComponents(width, height);
        }
        private void InitializeReceiverComponents(int partnerWidth, int partnerHeight)
        {
            _width = partnerWidth;
            _height = partnerHeight;

            base.InitCaptureBuffer(_width, _height, ref _hBitmap, ref _memDC, ref _bits, IntPtr.Zero, 0, IntPtr.Zero);
            _rectangles = base.InitRectangle(_width, _height);

            _bitmap = new Bitmap(_width, _height, base.GetStride1(_width, BYTE_PER_PIXEL), PixelFormat.Format24bppRgb, _bits);
        }
        public unsafe void ParsePacketToRegionsChange(byte[] packet, int actualLength)
        {
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

                }
            }
#if DEBUG
            Test11(GetStride1(_width, BYTE_PER_PIXEL), _bits);
#endif
        }
        public unsafe void MergeFullScreenToBitmap(byte[] packet, int actualLength)
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
            Test11(dstStride, (IntPtr)dst, true);
#endif
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