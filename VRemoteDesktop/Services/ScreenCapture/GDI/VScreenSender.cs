using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using VRemoteDesktop.Models;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.Enums;
using VRemoteDesktop.Services.ScreenCapture.Interop;
using VRemoteDesktop.Utils;
using VRemoteDesktop.Services.ScreenCapture.Utils;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public interface IVScreenSender
    {
        event EventHandler<VScreenSenderEventArgs> OnScreenCaptured;
        bool Start();
        bool Stop();
        void GetFullScreen();
        void Cancel();
    }
    public class VScreenSender: VScreen, IVScreenSender, IDisposable
    {
        private const uint DIB_RGB_COLORS = 0;
        private const int BYTE_PER_PIXEL = 3;   
        private const int REGION_SIZE = 16;
        private const int RANGE = 5;


        private int _disposed;

        private int _width;
        private int _height;
        private readonly int _fps;
        private readonly double _waitTime;

        private Rectangle[] _rectangles;
        private BackgroundWorker _worker;
        private CancellationTokenSource _cancellationTokenSource;
        private ManualResetEventSlim _completedEvent = new ManualResetEventSlim(false); 

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

        private IntPtr _screenDC;
        private readonly ScreenTask _screenTask;

        public event EventHandler<VScreenSenderEventArgs> OnScreenCaptured;
        public VScreenSender(ScreenTask screenTask, int fps = 10)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _fps = fps;
            _waitTime = 1000 / _fps;
            _screenTask = screenTask;
            InitializeSenderComponents();
        }
        public bool Start()
        {
            if (_screenDC == IntPtr.Zero)
                _screenDC = CaptureApi.GetDC(IntPtr.Zero);

            if (_worker.IsBusy)
                return false;

            _completedEvent.Reset();

            _cancellationTokenSource = new CancellationTokenSource();
            _worker.RunWorkerAsync();
            return true;
        }
        public bool Stop()
        {
            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();

            var flag = _completedEvent.Wait(3000);

            if (_screenDC != IntPtr.Zero)
                CaptureApi.ReleaseDC(IntPtr.Zero, _screenDC);

            return flag;
        }
        public void GetFullScreen()
        {
            int offset = 0;
            base.GetFullScreenData(ref offset, _screenTask.Buffer, _allBits[frontIdx], _width, 0, 0, _width, _height);
            int compressedLength = Compressor.CompressedLZ4(_screenTask.Buffer, offset);
            //Console.WriteLine($"Regions: Source Length: {offset + 1} - Compressed Length: {compressedLength}");
            if(offset > 0)
            {
                if (OnScreenCaptured != null)
                {
                    _screenTask.Add(dataOffset: 0, dataLength: offset + 1, compressedOffset: offset + 1, compressLength: compressedLength);
                    OnScreenCaptured(this, new VScreenSenderEventArgs(
                        type: VScreenSenderEventType.FullScreen,
                        screenTask: _screenTask));
                }
                else
                {
                    _screenTask.Complete();
                }
            }
            else
            {
                _screenTask.Complete();
            }
        }
        private void GetChangedRegions(int range)
        {
            //First
            var changedRectArray = _rectangles.Where(x =>
                    IsRegionChange(
                        _allBits[prevIdx],
                        _allBits[frontIdx],
                        _width,
                        x.X,
                        x.Y,
                        x.Width,
                        x.Height)).ToArray();
            var result = base.MergeRegions(changedRectArray, 0.8);
            int offset = 0;
            for (int i = 0; i < result.Count; i++)
            {
                var rect = result[i];
                base.GetRegionsData(
                    ref offset,
                    _screenTask.Buffer,
                    _allBits[frontIdx],
                    _width,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height);
            }
            int compressedLength = Compressor.CompressedLZ4(_screenTask.Buffer, offset);
            //Console.WriteLine($"Regions: Source Length: {offset} - Compressed Length: {compressedLength}");
            if (offset > 0)
            {
                if (OnScreenCaptured != null)
                {
                    _screenTask.Add(dataOffset: 0, dataLength: offset + 1, compressedOffset: offset + 1, compressLength: compressedLength);
                    OnScreenCaptured(this, new VScreenSenderEventArgs(
                      type: VScreenSenderEventType.RegionChange,
                      screenTask: _screenTask));
                }
                else
                {
                    _screenTask.Complete(); 
                }
            }
            else
            {
                _screenTask.Complete();
            }
        }
        private int RegionTotalByteTake(Rectangle rect)
        {
            int header = 16; //x,y,w,h(int32)
            int payload = rect.Width * rect.Height * BYTE_PER_PIXEL;
            return header + payload;
        }
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
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
            _worker.RunWorkerCompleted += HandlerCompleted;


            _screenDC = CaptureApi.GetDC(IntPtr.Zero);
            Capturing();
        }
        private void Capturing()
        {
            base.CaptureToBuffer(_allDCs[backIdx], _screenDC, 0, 0, _width, _height);
            //Console.WriteLine($"{backIdx} - {frontIdx} - {prevIdx}");

            // 0, 1, 2 -> 2 , 0, 1 -> 1, 2, 0 -> ...
            int tempPrev = prevIdx;
            prevIdx = frontIdx;
            frontIdx = backIdx;
            backIdx = tempPrev;
        }
        private void Handler(object sender, DoWorkEventArgs e)
        {
            double time = 0.0;
            Stopwatch st = new Stopwatch();
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                st.Restart();
                try
                {
                    if (_screenTask.Wait(200))
                    {
                        _screenTask.Reset();
                        Capturing();
                        GetChangedRegions(RANGE);
                        Thread.Sleep(1);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine($"Background error: {ex.Message}");
                }
                finally
                {
                    st.Stop();
                    if(st.Elapsed.TotalMilliseconds < _waitTime)
                    {
                        time = _waitTime - st.Elapsed.TotalMilliseconds;
                        Thread.Sleep(TimeSpan.FromMilliseconds(time));
                    }
                }
            }
            e.Cancel = true;
        }
        private void HandlerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _completedEvent.Set();
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


            // Free resources bitmap 2
            if (_hBitmap1 != IntPtr.Zero)
                CaptureApi.DeleteObject(_hBitmap1);

            if (_memDC1 != IntPtr.Zero)
                CaptureApi.DeleteDC(_memDC1);

            _bits1 = IntPtr.Zero;

            // Free resources bitmap 3
            if (_hBitmap2 != IntPtr.Zero)
                CaptureApi.DeleteObject(_hBitmap2);

            if (_memDC2 != IntPtr.Zero)
                CaptureApi.DeleteDC(_memDC2);

            _bits2 = IntPtr.Zero;

            // Free file mapping
            if (_fileMappingPtr != IntPtr.Zero)
                CaptureApi.CloseHandle(_fileMappingPtr);


            if(_screenDC != IntPtr.Zero)
                CaptureApi.ReleaseDC(IntPtr.Zero, _screenDC);

            if (disposing)
            {
                Cancel();
            }
        }
    }
}
