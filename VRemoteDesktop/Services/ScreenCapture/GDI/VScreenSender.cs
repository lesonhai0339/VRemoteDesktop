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
        private object _lock = new object();
        private const uint DIB_RGB_COLORS = 0;
        private const int BYTE_PER_PIXEL = 3;   
        private const int REGION_SIZE = 16;
        private const int RANGE = 5;


        private int _disposed;

        private int _width;
        private int _height;
        private readonly int _fps;
        private readonly double _waitTime;

        private long _order = 0;
        private readonly ConcurrentDictionary<string, CapturedFrame> _frames = new ConcurrentDictionary<string, CapturedFrame>();

        private Task _captureTask;
        private Task _queueTask;


        private Rectangle[] _rectangles;
        private BackgroundWorker _worker;
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
        private int prevIdx = 2;

        private IntPtr _screenDC;

        public event EventHandler<VScreenSenderEventArgs> OnScreenCaptured;
        public event EventHandler<DirtyRegionEventArgs> OnDirtyRegionChanged;
        public VScreenSender(int fps = 10)
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _fps = fps;
            _waitTime = 1000 / _fps;
            InitializeSenderComponents();
        }
        public bool Start()
        {
            if (_screenDC == IntPtr.Zero)
                _screenDC = CaptureApi.GetDC(IntPtr.Zero);

            if (_cancellationTokenSource.IsCancellationRequested)
                return false;

            _completedEvent.Reset();

            _cancellationTokenSource = new CancellationTokenSource();
            _captureTask = Task.Factory.StartNew(()=>
                Capturing(_cancellationTokenSource.Token), TaskCreationOptions.LongRunning);

            _queueTask = Task.Factory.StartNew(() =>
                HandlerFrames(_cancellationTokenSource.Token), TaskCreationOptions.LongRunning);

            return true;
        }

        private void HandlerFrames(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                FreeFrame(_frames);
               if(_frames.Count > 0)
                {
                    var frame = _frames.OrderBy(x => x.Key).FirstOrDefault();

                    if (frame.Value != null)
                        OnScreenCaptured(this, new VScreenSenderEventArgs(frame.Key, frame.Value));
                }
                Thread.Sleep(10);
            }
        }

        private void FreeFrame(ConcurrentDictionary<string, CapturedFrame> frames)
        {
            var framesToRemove = frames.Where(frame => 
                (Environment.TickCount - frame.Value.FrameId) > TimeSpan.FromSeconds(5).TotalMilliseconds).Select(frame => frame.Key)
                .ToList();

            foreach(var key in framesToRemove)
            {
                frames.TryRemove(key, out _);
            }
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

            //Get buffer length and rent buffer from pool * 2 because compressed data also place on this buffer
            int bufferLength = base.GetScreenDataLength(_width, _height, BYTE_PER_PIXEL);
            byte[] buffer = VArrayPool.Rent(bufferLength);

            int compressedBufferLength = (int)(bufferLength * 1.2); //Compressor.GetMaxOutputLength(bufferLength);
            byte[] compressedBuffer = VArrayPool.Rent(bufferLength);
            try
            {
                // Get bitmap data
                base.GetFullScreenData(ref offset, buffer, _allBits[frontIdx], _width, 0, 0, _width, _height);

                //Compress
                int compressedLength = Compressor.CompressedLZ4(buffer, offset, compressedBuffer, compressedBufferLength);

                if (compressedLength > 0)
                {
                    if (OnScreenCaptured != null)
                    {
                        CapturedFrame frame = new CapturedFrame(VScreenSenderEventType.FullScreen, compressedBuffer, 0, compressedLength);
                        string fullScreenId = new string('0', 10);
                        OnScreenCaptured(this, new VScreenSenderEventArgs(fullScreenId, frame));
                    }
                }
                else
                {
                    VArrayPool.Return(compressedBuffer);
                }
            }
            finally
            {
                VArrayPool.Return(buffer);
            }    
        }
    /*    private void GetChangedRegions(int range)
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
            var dittyRegions = base.MergeRegions(changedRectArray, 0.9);

            // Get buffer length and rent buffer from pool
            int bufferLength = base.GetScreenDataLength(dittyRegions, BYTE_PER_PIXEL);
            byte[] buffer = VArrayPool.Rent(bufferLength);

            int compressedBufferLength = (int)(bufferLength * 1.2); //Compressor.GetMaxOutputLength(bufferLength);
            byte[] compressedBuffer = VArrayPool.Rent(compressedBufferLength);

            try
            {
                int offset = 0;
                for (int i = 0; i < dittyRegions.Count; i++)
                {
                    var rect = dittyRegions[i];
                    base.GetRegionsData(
                        ref offset,
                        buffer,
                        _allBits[frontIdx],
                        _width,
                        rect.X,
                        rect.Y,
                        rect.Width,
                        rect.Height);
                }

                int compressedLength = Compressor.CompressedLZ4(buffer, offset, compressedBuffer, compressedBufferLength);
                if (compressedLength > 0)
                {
                    if (OnScreenCaptured != null)
                    {
                        CapturedFrame frame = new CapturedFrame(VScreenSenderEventType.RegionChange, compressedBuffer, 0, compressedLength);
                        if (!_frames.TryAdd(_packetId.ToString().PadLeft(10, '0'), frame))
                        {
                            VArrayPool.Return(compressedBuffer);
                        }
                        else
                        {
                            lock (_lock)
                            {
                                _packetId++;
                            }
                        }
                    }
                }
                else
                {
                    VArrayPool.Return(compressedBuffer);
                }
            }
            finally
            {
                VArrayPool.Return(buffer);
            }
        }*/
        private void GetChangedRegions(int range)
        {
            var changedRectArray = _rectangles.Where(x =>
                    IsRegionChange(
                        _allBits[prevIdx],
                        _allBits[frontIdx],
                        _width,
                        x.X,
                        x.Y,
                        x.Width,
                        x.Height)).ToArray();
            var dittyRegions = base.MergeRegions(changedRectArray, 0.9);

            int offset = 0;
            for (int i = 0; i < dittyRegions.Count; i++)
            {
                var rect = dittyRegions[i];

                int regionSize = base.GetScreenDataLength(rect.Width, rect.Height, BYTE_PER_PIXEL);
                byte[] regionBuffer = VArrayPool.Rent(regionSize);

                base.GetRegionsDataWithoutRectangle(
                    ref offset,
                    regionBuffer,
                    _allBits[frontIdx],
                    _width,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height);
                
                if(OnDirtyRegionChanged != null)
                {
                    OnDirtyRegionChanged.Invoke(this, new DirtyRegionEventArgs(
                        _order,
                        new RegionFrame(
                            rect.X,
                            rect.Y, 
                            rect.Width,
                            rect.Height,
                            regionBuffer,
                            regionSize
                        )));
                }
                lock (_lock)
                {
                    _order++;
                    if (_order == long.MaxValue)
                        _order = 0;
                }
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

            base.InitCaptureBuffer(ref _hBitmap, ref _memDC, ref _bits, _fileMappingPtr, pre , IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _hBitmap1, ref _memDC1, ref _bits1, _fileMappingPtr, cur, IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _hBitmap2, ref _memDC2, ref _bits2, _fileMappingPtr, next, IntPtr.Zero, _bitmapInfo);

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
            try
            {
                base.CaptureToBuffer(_allDCs[backIdx], _screenDC, 0, 0, _width, _height);
                //Console.WriteLine($"{backIdx} - {frontIdx} - {prevIdx}");

                // 0, 1, 2 -> 2 , 0, 1 -> 1, 2, 0 -> ...
                int tempPrev = prevIdx;
                prevIdx = frontIdx;
                frontIdx = backIdx;
                backIdx = tempPrev;
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Capture err: ", ex);
            }
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
                    Capturing();
                    GetChangedRegions(RANGE);
                    Thread.Sleep(1);
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


            if(_screenDC != IntPtr.Zero)
                CaptureApi.ReleaseDC(IntPtr.Zero, _screenDC);

            if (disposing)
            {
                Cancel();
            }
        }
    }
}
