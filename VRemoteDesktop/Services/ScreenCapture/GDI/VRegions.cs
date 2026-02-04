using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.Interop;
using VRemoteDesktop.Services.SessionManagement.DTOs;
using static VRemoteDesktop.Services.ScreenCapture.Interop.CaptureApi;

namespace VRemoteDesktop.Services.ScreenCapture.GDI
{
    public class VRegions : VScreen
    {
        private int _disposed = 0;
        private readonly object _lock = new object();
        private const int INFLIGHT_LIMIT = 10;  //accept maximum 10 inflight packet
        private const long DELAY_TIME = 50; //50ms

        private int _inflight = 0;
        private int _bytePerPixel;
        private int _width;
        private int _height;

        private int _readyToSend = 0;
        private int _fullScreenReceived =0;
        private int _acceptFullScreen = 0;
        private bool _acceptRegionChanged = false;

        //Writer buffer
        private ImageSwapper _writer;

        //Reader buffer
        private ImageSwapper _reader;

        private BITMAPINFO _bitmapInfo;
        private VBufferSwapper _bufferSwapper;

        private long _lastSendTimestamp = Stopwatch.GetTimestamp();

        private int _totalColumns;
        private int _totalRows;
        private int _regionSize;
        private int _hasData = 0;

        private Rectangle[] _dirtyRegions;
        private int count = 0;
        public VRegions(int width, int height, int bytePerPixel, int regionSize = 16)
        {
            _width = width;
            _height = height;
            _bytePerPixel = bytePerPixel;
            _regionSize = regionSize;


            _totalColumns = (_width + (_regionSize - 1)) / _regionSize;
            _totalRows = (_height + (_regionSize - 1)) / _regionSize;
            _dirtyRegions= new Rectangle[_totalColumns * _totalRows];


            _bitmapInfo = base.InitBitmapInfo(_width, _height, (ushort)(_bytePerPixel * 8), 0);

            _writer = new ImageSwapper(_totalColumns, _totalRows, _regionSize);
            _reader = new ImageSwapper(_totalColumns, _totalRows, _regionSize);

            base.InitCaptureBuffer(ref _writer.HBitmap, ref _writer.MemDC, ref _writer.Bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _reader.HBitmap, ref _reader.MemDC, ref _reader.Bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            _bufferSwapper = new VBufferSwapper(_writer, _reader);
        }
        public  VBufferSwapper BufferSwapper => _bufferSwapper;
        public bool HasData => Interlocked.CompareExchange(ref _hasData, 1, 1) == 1;

        public bool CanWork 
        {
            get
            {
                lock (_lock)
                {
                    return _acceptRegionChanged;
                }
            }
        }
        public int GetRectangleIndex(Rectangle rect, int regionSize = 0)
        {
            if (rect.IsEmpty) 
                return -1;

            if (regionSize <= 0)
                regionSize = _regionSize;

            return ((rect.Y / regionSize) * _totalColumns) + (rect.X / regionSize);
        }
        public bool AcceptFullScreen()
        {
            if(Interlocked.CompareExchange(ref _acceptFullScreen, 1, 0) == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool ReadyToSend()
        {
            if (Interlocked.CompareExchange(ref _fullScreenReceived, 1, 1) != 1)
                return false;

            var now = Stopwatch.GetTimestamp();
            var elapsedMs = (now - _lastSendTimestamp) * 1000 / Stopwatch.Frequency;
            if (elapsedMs < DELAY_TIME)
                return false;

            lock (_lock)
            {
               
                //Console.WriteLine($"Inflight count: {_inflight}");
                if(_inflight < INFLIGHT_LIMIT)
                {
                    _lastSendTimestamp = now;
                    _inflight++;
                    return true;
                }
                else
                {
                    if (elapsedMs > 3 * 1000)
                    {
                        //Console.WriteLine("Timeout reset inflight");
                        //reset if exceed timeout
                        _lastSendTimestamp = now;
                        _inflight--;
                        return true;
                    }
                    return false;
                }
            }
            //return Interlocked.Increment(ref _inflight) < INFLIGHT_LIMIT;
        }
        public void FullScreenCompleted()
        {
            Interlocked.Exchange(ref _fullScreenReceived, 1);
        }
        public void SendCompleted()
        {
            lock (_lock)
            {
                if(_inflight > 0)
                {
                    _inflight--;
                }
            }
            //Interlocked.Decrement(ref _inflight);
        }
        /*  public bool ReadyToSend()
          {
              long current = Stopwatch.GetTimestamp();
              double elapsedSeconds = (double)(current - _lastSendTimestamp) / Stopwatch.Frequency;

              if (elapsedSeconds > 3.0)
              {
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
          }*/
        //public bool SetBusy()
        //{
        //    if(Interlocked.CompareExchange(ref _readyToSend, 1, 0 )== 0)
        //    {
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}

       
        public void BeginAccept()
        {
            lock (_lock)
            {
                _acceptRegionChanged = true;
            }
        }
        public void Add(RegionFrame regions)
        {
            Interlocked.Exchange(ref _hasData, 1);  
            //lock (_lock)
            //{
            //    foreach (var region in regions.Bounds)
            //    {
            //        int index = GetRectangleIndex(region);
            //        if(index != -1)
            //        {
            //            _bufferSwapper.GetWriteBuffer().Rectangles[index] = region;
            //            //_writer[index] = region;
            //            //_writer.Rectangles[index] = region;
            //        }
            //        //var key = (long)region.X << 32 | (uint)region.Y;
            //        //_regions[key] = region;
            //    }
            //}
        }
        public ScreenDataDto GetData()
        {
            if (Interlocked.CompareExchange(ref _readyToSend, 1, 0) != 0)
            {
                Console.WriteLine("Busy");
                return null;
            }

            var reader = _bufferSwapper.GetDataBuffer();
            if (reader == null)
                return null;
            Console.WriteLine($"Reader: {reader.Bits}");

            try
            {

                lock (reader.Lock) 
                {
                    count = reader.ChangedRegions.Count;
                    if (count > 0)
                    {
                        reader.ChangedRegions.CopyTo(_dirtyRegions);
                        reader.ChangedRegions.Clear();
                    }
                }

                if (count == 0) return null;

                int rentLength = GetScreenDataLength(_dirtyRegions, count, _bytePerPixel);
                byte[] buffer = VArrayPool.Rent(rentLength);
                int offset = 0;
                for (int i = 0; i < count; i++)
                {
                    base.GetRegionsData(
                        ref offset,
                        buffer,
                        reader.Bits,//data,
                        _width,
                        _dirtyRegions[i].X,
                        _dirtyRegions[i].Y,
                        _dirtyRegions[i].Width,
                        _dirtyRegions[i].Height,
                        _bytePerPixel);

                }
                return new ScreenDataDto(buffer, 0, offset + 1);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetData error: {ex.Message}");
                //Error release _inflight
                SendCompleted();
                return null;
            }
            finally
            {
                _bufferSwapper.Free();
                Interlocked.Exchange(ref _readyToSend, 0);
                Interlocked.Exchange(ref _hasData, 0);
            }
        }
        public void MergeDirtyRegions(Rectangle[] regions, List<Rectangle> changedRegions, double threshold = 0.8)
        {
            changedRegions.Clear();
            var baseRect = regions[0];

            //regions[0] = Rectangle.Empty;

            for (int i = 1; i < regions.Length; i++)
            {
                var rect = regions[i];
                //regions[i] = Rectangle.Empty;

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
                    changedRegions.Add(baseRect);
                    baseRect = rect;
                }

            }

            // Finally, add last base rectangle to groups result
            changedRegions.Add(baseRect);
        }
        public override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            ////Clear writer DIBSection resources
            //if (_writer_hBitmap != IntPtr.Zero)
            //    CaptureApi.DeleteObject(_writer_hBitmap);

            //if (_writer_memDC != IntPtr.Zero)
            //    CaptureApi.ReleaseDC(IntPtr.Zero, _writer_memDC);

            //_writer_bits = IntPtr.Zero;
            //_writer_hBitmap = IntPtr.Zero;
            //_writer_memDC = IntPtr.Zero;


            ////Clear reader DIBSection resources
            //if (_reader_hBitmap != IntPtr.Zero)
            //    CaptureApi.DeleteObject(_reader_hBitmap);

            //if (_reader_memDC != IntPtr.Zero)
            //    CaptureApi.ReleaseDC(IntPtr.Zero, _reader_memDC);

            //_reader_bits = IntPtr.Zero;
            //_reader_hBitmap = IntPtr.Zero;
            //_reader_memDC = IntPtr.Zero;

            if (disposing)
            {
                _writer.Free();
                _reader.Free();
                //Array.Clear(_writer, 0 , _writer.Length);
                //Array.Clear(_reader, 0, _reader.Length);

                //_writer = null;
                //_reader = null;
            }
        }
    }
}
