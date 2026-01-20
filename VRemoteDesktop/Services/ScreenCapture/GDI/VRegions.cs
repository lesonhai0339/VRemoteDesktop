using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.Interop;
using static VRemoteDesktop.Services.ScreenCapture.Interop.CaptureApi;

namespace VRemoteDesktop.Services.ScreenCapture.GDI
{
    public class VRegions : VScreen
    {
        private readonly object _lock = new object();   
        private readonly ConcurrentDictionary<long, Rectangle> _regions = new ConcurrentDictionary<long, Rectangle>();
        private int _bytePerPixel;
        private int _width;
        private int _height;
        private IntPtr _sourceImage;
        private int _readyToSend;
        private bool _canWork = false;

        private IntPtr _hBitmap;
        private IntPtr _bits;   
        private IntPtr _memDC;
        private BITMAPINFO _bitmapInfo;

        private long _lastSendTimestamp = Stopwatch.GetTimestamp();
        public VRegions(int width, int height, int bytePerPixel)
        {
            _width = width;
            _height = height;
            _bytePerPixel = bytePerPixel;


            _bitmapInfo = base.InitBitmapInfo(_width, _height, (ushort)(_bytePerPixel * 8), 0);

            base.InitCaptureBuffer(ref _hBitmap, ref _memDC, ref _bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
        }
        public  IntPtr Buffer => _bits;
        public int Count => _regions.Count;
        public bool CanWork 
        {
            get
            {
                lock (_lock)
                {
                    return _canWork;
                }
            }
        }
        public bool ReadyToSend()
        {
            long current = Stopwatch.GetTimestamp();
            double elapsedSeconds = (double)(current - _lastSendTimestamp) / Stopwatch.Frequency;

            if (elapsedSeconds > 3.0)
            {
                Console.WriteLine("Regions Timeout, continue");
                Interlocked.Exchange(ref _readyToSend, 0);
            }
            bool acquired = Interlocked.CompareExchange(ref _readyToSend, 1, 0) == 0;

            if (acquired)
            {
                _lastSendTimestamp = current;
            }
            return acquired;
        }

        public void SendCompleted()
        {
            Interlocked.Exchange(ref _readyToSend, 0);
        }
        public void BeginAccept()
        {
            lock (_lock)
            {
                _canWork = true;
            }
        }
        public void Add(RegionFrame regions)
        {
            foreach (var region in regions.Bounds)
            {
                var key = (long)region.X << 32 | (uint)region.Y;
                _regions[key] = region;
            }
        }
        public override void Dispose(bool disposing)
        {
        }
    }
}
