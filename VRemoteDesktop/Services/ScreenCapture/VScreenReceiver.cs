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
    public class VScreenReceiver : VScreen
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

        // Filed mapping buffer pool
        private IntPtr _fileMappingPtr;

        //bitmap 1
        private IntPtr _hBitmap;
        private IntPtr _bits;        // points to raw pixels
        private IntPtr _memDC;

#if DEBUG
        private IntPtr _bufferPool;
#endif
        public VScreenReceiver()
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



        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
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

            base.InitCaptureBuffer(_width, _height, ref _hBitmap, ref _memDC, ref _bits, IntPtr.Zero, 0, IntPtr.Zero);
            _rectangles = base.InitRectangle(_width, _height);
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

        public override void Dispose(bool disposing)
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

    }
}