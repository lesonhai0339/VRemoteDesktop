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
using VRemoteDesktop.Services.ScreenCapture.Enums;
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

        private int _fullScreenCompleted = 0; //0: uncompleted, 1: completed
        private int _hasData;

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
        private bool _busy = false;

        private Rectangle[] _dirtyRegions;
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

            _writer = new ImageSwapper(_width, _height, _regionSize);
            _reader = new ImageSwapper(_width, _height, _regionSize);

            base.InitCaptureBuffer(ref _writer.HBitmap, ref _writer.MemDC, ref _writer.Bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            base.InitCaptureBuffer(ref _reader.HBitmap, ref _reader.MemDC, ref _reader.Bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);
            _bufferSwapper = new VBufferSwapper(_writer, _reader);
        }

        public bool FullScreenCompleted => Thread.VolatileRead(ref _fullScreenCompleted) == 1;
        public void SetFullScreenCompleted()
        {
            Interlocked.Exchange(ref _fullScreenCompleted, 1);
        }
        public bool HasData => Thread.VolatileRead(ref _hasData) == 1;
        public void SetHasData()
        {
            Interlocked.Exchange(ref _hasData, 1);
        }


        public object Lock => _lock;
        public  VBufferSwapper BufferSwapper => _bufferSwapper;

        [Obsolete("Not use")]
        public int GetRectangleIndex(Rectangle rect, int regionSize = 0)
        {
            if (rect.IsEmpty) 
                return -1;

            if (regionSize <= 0)
                regionSize = _regionSize;

            return ((rect.Y / regionSize) * _totalColumns) + (rect.X / regionSize);
        }

        public bool ReadyToSend()
        {
            if (Thread.VolatileRead(ref _fullScreenCompleted) != 1)
                return false;

            var now = Stopwatch.GetTimestamp();
            var elapsedMs = (now - _lastSendTimestamp) * 1000 / Stopwatch.Frequency;
            if (elapsedMs < DELAY_TIME)
                return false;

            lock (_lock)
            {
               
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
                        //reset if exceed timeout
                        _lastSendTimestamp = now;
                        _inflight--;
                        return true;
                    }
                    return false;
                }
            }
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
        }
        public ScreenDataDto GetData(VScreenType type)
        {
            _bufferSwapper.Swap();
            //var reader = _bufferSwapper.GetDataBuffer();
            //if (reader == null)
            //    return null;
            try
            {
                var reader = _bufferSwapper.BeginRead();
                if (reader == null)
                    return null;

                Rectangle[] regionsToProcess = null;
                Rectangle fullScreenToProcess = Rectangle.Empty;

                lock (reader.Lock)
                {
                    if (type == VScreenType.FullScreen)
                    {
                        fullScreenToProcess = reader.FullScreen;
                    }
                    else
                    {
                        regionsToProcess = reader.ChangedRegions;
                    }
                }
                if (fullScreenToProcess.IsEmpty && (regionsToProcess == null || regionsToProcess.Length == 0))
                    return null;

                int dirtyRegionsCount = 0;
                lock (_lock)
                {
                    dirtyRegionsCount = (type == VScreenType.FullScreen)
                                        ? GetDirtyRegions(fullScreenToProcess)
                                        : GetDirtyRegions(regionsToProcess);
                }

                if (dirtyRegionsCount == 0)
                {
                    _bufferSwapper.EndRead();
                    return null;
                }

                int rentLength = GetScreenDataLength(_dirtyRegions, dirtyRegionsCount, _bytePerPixel);
                byte[] buffer = VArrayPool.Rent(rentLength);
                int offset = 0;
                for (int i = 0; i < dirtyRegionsCount; i++)
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
                //_bufferSwapper.Free();
                Interlocked.Exchange(ref _hasData, 0);
            }
        }
        private int GetDirtyRegions(Rectangle[] source)
        {
            int count = 0;
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i].Width > 0 && source[i].Height > 0)
                {
                    _dirtyRegions[count] = source[i];
                    count++;
                }
            }
            return count;
        }
        private int GetDirtyRegions(Rectangle source)
        {
            int count = 0;
            if (source.Width > 0 && source.Height > 0)
            {
                _dirtyRegions[count] = source;
                count++;
            }
            return count;
        }
        public void ReadCompleted()
        {

            var reader = _bufferSwapper.GetRead();
            if (reader == null)
                return;
            _bufferSwapper.EndRead();   
        }
        public override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;
            try
            {
                if (disposing)
                {
                    Array.Clear(_dirtyRegions, 0, _dirtyRegions.Length);

                    if (_writer != null)
                        _writer.Free();

                    if (_reader != null)
                        _reader.Free();

                    if (_bufferSwapper != null)
                        _bufferSwapper.Dispose();
                }
            }
            catch { }
        }
    }
}
