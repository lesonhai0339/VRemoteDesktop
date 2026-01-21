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
using static VRemoteDesktop.Services.ScreenCapture.Interop.CaptureApi;
using System.Collections.Concurrent;
using VRemoteDesktop.Services.ScreenCapture.GDI;
using System.Threading.Tasks;
using VRemoteDesktop.Services.ScreenCapture.Events;
using System.Collections.Generic;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public interface IVScreenSender
    {
        event EventHandler<FrameEventArgs> OnFrame;
        bool IsCapturing { get; }
        void AddSessionBuffer(string id, IntPtr buffer);
        void RemoveSessionBuffer(string id);
        bool Start();
        bool Stop();
        void GetFullScreen(IntPtr image);
        void Cancel();
    }
    public class VScreenSender : VScreen, IVScreenSender, IDisposable
    {
        private object _lock = new object();
        private const uint DIB_RGB_COLORS = 0;
        private const int BYTE_PER_PIXEL = 3;
        private const int REGION_SIZE = 16;
        private const int RANGE = 5;


        private int _disposed;
        private int _isCapturing = 0;

        private int _width;
        private int _height;
        private readonly int _fps;
        private readonly double _waitTime;

        private readonly ConcurrentDictionary<string, CapturedFrame> _frames = new ConcurrentDictionary<string, CapturedFrame>();

        private Task _captureTask;


        private ConcurrentDictionary<string ,IntPtr> _clientSessionBitmap;
        private Rectangle[] _rectangles;
        private CancellationTokenSource _cancellationTokenSource;
        private ManualResetEventSlim _completedEvent = new ManualResetEventSlim(false);

        private BITMAPINFO _bitmapInfo;

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
        private int nextIdx = 2;

        private IntPtr _screenDC;

        public event EventHandler<FrameEventArgs> OnFrame;
        public VScreenSender(int fps = 10)
        {
            _clientSessionBitmap = new ConcurrentDictionary<string, IntPtr>();
            _cancellationTokenSource = new CancellationTokenSource();
            _fps = fps;
            _waitTime = 1000 / _fps;
            InitializeSenderComponents();
        }
        public bool IsCapturing => _isCapturing == 1;
        public IntPtr SourceImage
        {
            get
            {
                lock (_lock)
                {
                    return _allBits[frontIdx];
                }
            }
        }
        public void AddSessionBuffer(string id, IntPtr buffer)
        {
            lock (_lock)
            {
                _clientSessionBitmap.TryAdd(id, buffer);
            }
        }
        public void RemoveSessionBuffer(string id)
        {
            lock (_lock)
            {
                _clientSessionBitmap.TryRemove(id,out _);
            }
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

            _bitmapInfo = base.InitBitmapInfo(_width, _height, BYTE_PER_PIXEL * 8, 0);

            base.InitCaptureBuffer(ref _hBitmap, ref _memDC, ref _bits, _fileMappingPtr, pre, IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _hBitmap1, ref _memDC1, ref _bits1, _fileMappingPtr, cur, IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _hBitmap2, ref _memDC2, ref _bits2, _fileMappingPtr, next, IntPtr.Zero, _bitmapInfo);

            _allDCs = new IntPtr[] { _memDC, _memDC1, _memDC2 };
            _allBits = new IntPtr[] { _bits, _bits1, _bits2 };

            _rectangles = base.InitRectangle(_width, _height);

            _screenDC = CaptureApi.GetDC(IntPtr.Zero);
            Capturing();
        }
        public bool Start()
        {
            if (_screenDC == IntPtr.Zero)
                _screenDC = CaptureApi.GetDC(IntPtr.Zero);

            if (_cancellationTokenSource.IsCancellationRequested)
                return false;

            _completedEvent.Reset();
            _cancellationTokenSource = new CancellationTokenSource();
            _captureTask = Task.Factory.StartNew(() =>
                Capturing(_cancellationTokenSource.Token), TaskCreationOptions.LongRunning);

            Interlocked.Exchange(ref _isCapturing, 1);
            return true;
        }

        public bool Stop()
        {
            if (_cancellationTokenSource != null)
                _cancellationTokenSource.Cancel();

            var flag = _completedEvent.Wait(3000);

            if (_screenDC != IntPtr.Zero)
                CaptureApi.ReleaseDC(IntPtr.Zero, _screenDC);

            Interlocked.Exchange(ref _isCapturing, 0);
            return flag;
        }
        public void GetFullScreen(IntPtr image)
        {
            int offset = 0;

            try
            {
                base.CopyFullScreenSourceToDest(ref offset, image, _allBits[frontIdx], _width, 0, 0, _width, _height);
                if (offset > 0)
                {
                    if (OnFrame != null)
                    {
                        RegionFrame frame = new RegionFrame(new Rectangle[] { new Rectangle(0, 0, _width, _height) });
                        OnFrame(this, new FrameEventArgs(VRemoteDesktop.Enums.ScreenType.FULL_SCREEN, frame));
                    }
                }
            }
            catch(Exception ex)
            {
                Logger.Log.ForContext("FileName", "VScreenSender").Error(ex.Message);
            }
        }
        private void GetChangedRegions(int range)
        {
            var dirtyRegions = _rectangles.Where(x =>
                    IsRegionChangeUseLong(
                        _allBits[nextIdx],
                        _allBits[frontIdx],
                        _width,
                        x.X,
                        x.Y,
                        x.Width,
                        x.Height)).ToArray();

            if (dirtyRegions.Length > 0)
            {
                foreach (var key in _clientSessionBitmap.Keys.ToArray())
                {
                    if(_clientSessionBitmap.TryGetValue(key, out var buffer))
                    {
                        foreach (var rect in dirtyRegions)
                        {
                            base.CopySourceToDest(
                                _allBits[frontIdx],
                                buffer,
                                rect.X,
                                rect.Y,
                                rect.Width,
                                rect.Height,
                                _width, 3);
                        }
                    }     
                }
                if (OnFrame != null)
                {
                    OnFrame.Invoke(this, new FrameEventArgs(VRemoteDesktop.Enums.ScreenType.DIRTY_REGIONS, new RegionFrame(dirtyRegions)));
                }
            }
        }

        private void Capturing()
        {
            try
            {
                base.CaptureToBuffer(_allDCs[backIdx], _screenDC, 0, 0, _width, _height);
                //Console.WriteLine($"{backIdx} - {frontIdx} - {prevIdx}");

                // 0, 1, 2 -> 2 , 0, 1 -> 1, 2, 0 -> ...
                int tempPrev = nextIdx;

                nextIdx = frontIdx;
                frontIdx = backIdx;

                backIdx = tempPrev;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Capture err: ", ex);
            }
        }
        private void Capturing(CancellationToken token)
        {
            double time = 0.0;
            Stopwatch st = new Stopwatch();
            while (!token.IsCancellationRequested)
            {
                st.Restart();
                try
                {
                    Capturing();
                    GetChangedRegions(RANGE);
                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Background error: {ex.Message}");
                }
                finally
                {
                    st.Stop();
                    if (st.Elapsed.TotalMilliseconds < _waitTime)
                    {
                        time = _waitTime - st.Elapsed.TotalMilliseconds;
                        Thread.Sleep(TimeSpan.FromMilliseconds(time));
                    }
                }
            }
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


            if (_screenDC != IntPtr.Zero)
                CaptureApi.ReleaseDC(IntPtr.Zero, _screenDC);

            if (disposing)
            {
                Cancel();
            }
        }
    }
}
