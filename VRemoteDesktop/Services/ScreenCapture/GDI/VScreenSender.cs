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
using System.Drawing.Imaging;

namespace VRemoteDesktop.Services.ScreenCapture
{
    public interface IVScreenSender
    {
        bool IsCapturing { get; }
        bool InitializeSenderComponents();
        void AddSessionBuffer(string id, VBufferSwapper swapper);
        void RemoveSessionBuffer(string id);
        bool Start();
        bool Stop();
        void GetFullScreen(VBufferSwapper swapper);
        void Cancel();
        void Dispose();
        event EventHandler<FrameEventArgs> OnFrame;

    }
    public class VScreenSender : VScreen, IVScreenSender, IDisposable
    {
        /// <summary>
        /// <para><see href="https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-emf/a5e722e3-891a-4a67-be1a-ed5a48a7fda1"/></para>
        /// </summary>
        private const uint DIB_RGB_COLORS = 0;
        private const int RANGE = 5; // Acceptable color diff between previous and current frame.

        private object _lock = new object();
        private readonly int _width;
        private readonly int _height;
        private readonly int _bytePerPixel;
        private readonly int _regionSize;
        private readonly int _limitFramePerSecond;
        private readonly double _waitTimeBetweenFrame;
        private readonly Rectangle[] _rectangles;

        private readonly ConcurrentDictionary<string, VBufferSwapper> _clientSessionBufferSwapper;
        private readonly ManualResetEventSlim _completedEvent = new ManualResetEventSlim(false);

        private int _disposed = 0;
        private int _isCapturing = 0;
        private int _initialized = 0;
        private Rectangle[] _dirtyRegions;
        private Rectangle[] _mergedRegions;
        private Task _captureTask;

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

        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        public event EventHandler<FrameEventArgs> OnFrame;
        public VScreenSender(int width, int height, int bytePerPixel, int framePerSecond = 10, int regionSize = 16):
            base(width, height, bytePerPixel, regionSize)
        {
            _width = width;
            _height = height;
            _bytePerPixel = bytePerPixel;
            _regionSize = regionSize;

            _limitFramePerSecond = framePerSecond;
            _waitTimeBetweenFrame = Math.Ceiling((double)1000 / _limitFramePerSecond);

            _rectangles = base.InitRectangle(_width, _height);

            //dirty regions
            var cols = (_width + (_regionSize - 1)) / _regionSize;
            var rows = (_height + (_regionSize - 1)) / _regionSize;
            _dirtyRegions = new Rectangle[cols * rows];
            _mergedRegions = new Rectangle[cols * rows];

            _clientSessionBufferSwapper = new ConcurrentDictionary<string, VBufferSwapper>();
        }
        #region Properties
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
        #endregion
        #region Methods
        public bool InitializeSenderComponents()
        {
            if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
                return false;

            int DIBSectionBuffer = base.GetStride1(_width, _bytePerPixel) * _height;
            int totalBufferAllocated = DIBSectionBuffer * 3; //using triple buffer to swap
            base.InitFileMapping(fileMappingPtr: ref _fileMappingPtr, allocateSize: (uint)totalBufferAllocated);

            uint pre = (uint)(0 * DIBSectionBuffer);
            uint cur = (uint)(1 * DIBSectionBuffer);
            uint next = (uint)(2 * DIBSectionBuffer);

            _bitmapInfo = base.InitBitmapInfo(_width, _height, (ushort)(_bytePerPixel * 8), 0);

            base.InitCaptureBuffer(ref _hBitmap, ref _memDC, ref _bits, _fileMappingPtr, pre, IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _hBitmap1, ref _memDC1, ref _bits1, _fileMappingPtr, cur, IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _hBitmap2, ref _memDC2, ref _bits2, _fileMappingPtr, next, IntPtr.Zero, _bitmapInfo);

            _allDCs = new IntPtr[] { _memDC, _memDC1, _memDC2 };
            _allBits = new IntPtr[] { _bits, _bits1, _bits2 };


            _screenDC = CaptureApi.GetDC(IntPtr.Zero);
            Capturing();
            return true;
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
            {
                CaptureApi.ReleaseDC(IntPtr.Zero, _screenDC);
                _screenDC = IntPtr.Zero;
            }
            Interlocked.Exchange(ref _isCapturing, 0);
            return flag;
        }
        public void Cancel()
        {
            _cancellationTokenSource.Cancel();
        }
        public void AddSessionBuffer(string id, VBufferSwapper swapper)
        {
            lock (_lock)
            {
                _clientSessionBufferSwapper.TryAdd(id, swapper);
            }
        }
        public void RemoveSessionBuffer(string id)
        {
            lock (_lock)
            {
                _clientSessionBufferSwapper.TryRemove(id, out _);
            }
        }
        /// <summary>
        /// Copy raw image(bytes) from frontIdx to raw bytes image(ppvBits in CreateDIBSection) and call event
        /// </summary>
        /// <param name="image"></param>
        public void GetFullScreen(VBufferSwapper bufferSwapper)
        {
            int writerOffset = 0;
            try
            {
                var writer = bufferSwapper.BeginWrite();
                if (writer == null)
                    return;
                try
                {
                    writer.AddFullScreen(new Rectangle(0, 0, _width, _height));
                    lock (writer.Lock)
                    {
                        base.CopyFullScreenSourceToDest(ref writerOffset, writer.Bits, _allBits[frontIdx], _width, 0, 0, _width, _height, _bytePerPixel);
                    }
                }
                finally
                {
                    bufferSwapper.EndWrite();
                }
                if (writerOffset > 0)
                {
                    if (OnFrame != null)
                    {
                        OnFrame(this, new FrameEventArgs(VScreenType.FullScreen));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log.ForContext("FileName", "VScreenSender").Error(ex.Message);
            }
            finally
            {
                //bufferSwapper.Free();
            }
        }
        private void GetDirtyRegion(Rectangle[] sourceRectangles, Rectangle[] dstRegions, IntPtr pre, IntPtr cur, int fullWidth, int bytePerPixel)
        {
            int length = sourceRectangles.Length;
            for(int i = 0; i< length; i++)
            {
                ref var rect = ref sourceRectangles[i];
                bool flag = IsRegionChangeUseLong(
                    pre, 
                    cur, 
                    fullWidth,
                    rect.X,
                    rect.Y,
                    rect.Width,
                    rect.Height, 
                    bytePerPixel);


                int index = base.GetRectangleIndex(rect);
                if(index >= 0)
                {
                    if (flag)
                    {
                        dstRegions[index] = rect;
                    }
                    else
                    {
                        dstRegions[index] = Rectangle.Empty;
                    }
                }       
            }
        }
        private void GetChangedRegions(int range)
        {
            GetDirtyRegion(_rectangles, _dirtyRegions, _allBits[nextIdx], _allBits[frontIdx], _width, _bytePerPixel);
            //_dirtyRegions.Clear();
            //_dirtyRegions = _rectangles.Where(x =>
            //        IsRegionChangeUseLong(
            //            _allBits[nextIdx],
            //            _allBits[frontIdx],
            //            _width,
            //            x.X,
            //            x.Y,
            //            x.Width,
            //            x.Height,
            //            _bytePerPixel)).ToList();
            int regionsCount = this.MergeDirtyRegionsModern(_dirtyRegions, _mergedRegions, 0.9);
            if (regionsCount <= 0)
                return;

            foreach (var clientBuffer in _clientSessionBufferSwapper)
            {
                var bufferSwapper = clientBuffer.Value;
                var writer = bufferSwapper.BeginWrite();
                if (writer == null)
                    return;
                try
                {
                    writer.Add(new RegionFrame(_mergedRegions, 0, regionsCount));
                    lock (writer.Lock)
                    {
                        foreach (var rect in _mergedRegions)
                        {
                            base.CopySourceToDest(
                                _allBits[frontIdx],
                                writer.Bits,
                                rect.X,
                                rect.Y,
                                rect.Width,
                                rect.Height,
                                _width,
                                _bytePerPixel);
                        }
                    }
                }
                finally
                {
                    bufferSwapper.EndWrite();
                }
            }
            if (OnFrame != null)
            {
                OnFrame.Invoke(this, new FrameEventArgs(VScreenType.DirtyRegions));
            }
        }
        public int MergeDirtyRegionsModern(Rectangle[] sourceRegions, Rectangle[] dstRegions, double threshold = 0.8)
        {
            int count = 0;
            int length = sourceRegions.Length;

            int i = 0;
            while (i < length && sourceRegions[i].IsEmpty) i++;
            if (i >= length) return 0;

            var baseRect = sourceRegions[i];
            sourceRegions[i] = Rectangle.Empty;
            i++;
            for (; i < length; i++)
            {
                var rect = sourceRegions[i];

                if (rect.IsEmpty)
                    continue;

                var area = Rectangle.Union(baseRect, rect);

                var areaUnion = area.Width * area.Height;
                var areaSum = baseRect.Width * baseRect.Height + rect.Width * rect.Height;

                if (areaSum >= threshold * areaUnion)
                {
                    baseRect = area;
                }
                else
                {
                    dstRegions[count++] = baseRect;
                    baseRect = rect;
                }

                sourceRegions[i] = Rectangle.Empty;
            }

            // Finally, add last base rectangle to groups result
            dstRegions[count++] = baseRect;
            return count;
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
                    if (st.Elapsed.TotalMilliseconds < _waitTimeBetweenFrame)
                    {
                        time = _waitTimeBetweenFrame - st.Elapsed.TotalMilliseconds;
                        Thread.Sleep(TimeSpan.FromMilliseconds(time));
                    }
                }
            }
        }
        private void HandlerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            _completedEvent.Set();
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
                _clientSessionBufferSwapper.Clear();
            }
        }
    }
}
