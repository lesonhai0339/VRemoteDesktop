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
using System.Windows.Forms.VisualStyles;
using VRemoteDesktop.Enums;
using VRemoteDesktop.Helpers;
using static VRemoteDesktop.Interop.Win32Apis;
using static VRemoteDesktop.Utils.Logger;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public class VScreenSender: VScreen
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
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
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

            base.InitFileMapping(ref _fileMappingPtr);

            uint pre = 0; //10MB
            uint cur = 10 * 1024 * 1024; ; //10MB
            uint next = 20 * 1024 * 1024; //10MB 


            base.InitCaptureBuffer(_width, _height, ref _hBitmap, ref _memDC, ref _bits, _fileMappingPtr, pre, IntPtr.Zero);
            base.InitCaptureBuffer(_width, _height, ref _hBitmap1, ref _memDC1, ref _bits1, _fileMappingPtr, cur, IntPtr.Zero);
            base.InitCaptureBuffer(_width, _height, ref _hBitmap2, ref _memDC2, ref _bits2, _fileMappingPtr, next, IntPtr.Zero);

            _allDCs = new IntPtr[] { _memDC, _memDC1, _memDC2 };
            _allBits = new IntPtr[] { _bits, _bits1, _bits2 };

            _rectangles = base.InitRectangle(_width, _height);

            _worker = new BackgroundWorker();
            _worker.DoWork += Handler;
        }
        //Chua xong
        public void GetFullScreen()
        {
            int offset = 0;
            base.GetFullScreenData(ref offset, _bufferPool, _allBits[frontIdx], _width, 0, 0, _width, _height);
        }
        private void Handler(object sender, DoWorkEventArgs e)
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                base.CaptureToBuffer(_allDCs[backIdx], IntPtr.Zero, 0, 0, _width, _height);
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
                            _width,
                            x.X,
                            x.Y,
                            x.Width,
                            x.Height)).ToArray();

                var result = base.MergeRegions(changedRectArray, 0.8);
                Thread.Sleep(1000);
            }
        }
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
            if (_hBitmap1 != IntPtr.Zero)
                CaptureApis.DeleteObject(_hBitmap);

            if (_memDC1 != IntPtr.Zero)
                CaptureApis.DeleteDC(_memDC1);

            _bits1 = IntPtr.Zero;

            // Free resources bitmap 3
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
