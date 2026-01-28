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
        private int _bytePerPixel;
        private int _width;
        private int _height;

        private int _readyToSend;
        private int _fullScreenReceived;
        private bool _acceptRegionChanged = false;

        private IntPtr _hBitmap;
        private IntPtr _bits;   
        private IntPtr _memDC;
        private BITMAPINFO _bitmapInfo;

        private long _lastSendTimestamp = Stopwatch.GetTimestamp();

        private Rectangle[] _writer;
        private Rectangle[] _reader;
        private int _totalColumns;
        private int _totalRows;
        private int _regionSize;
        private bool _hasData = false;


        private List<Rectangle> _tempRect;
        //Note: loại bỏ việc chờ gói ack từ đối tác(làm chậm) và tạo thêm 1 DIBSection để thay phiên nhận data từ VScreenSender(đảm bảo trong lúc Getdata() dử liệu không bị thay đổi vì VScreenSender
        //ghi data và DIBSection thứ 2. Tạo 1 class quản lý việc swap giữa 2 DIBSection này thay vì chỉ truyền pointer vào VScreenSender. Với cách này có thể đảm bảo tính toàn vẹn dữ liệu 
        //mà vẫn có tốc độ cao.
        public VRegions(int width, int height, int bytePerPixel =3, int regionSize = 16)
        {
            _fullScreenReceived = 0;
            _width = width;
            _height = height;
            _bytePerPixel = bytePerPixel;
            _regionSize = regionSize;


            _totalColumns = (_width + (_regionSize - 1)) / _regionSize;
            _totalRows = (_height + (_regionSize - 1)) / _regionSize;
            _writer = new Rectangle[_totalColumns * _totalRows];
            _reader = new Rectangle[_totalColumns * _totalRows];
            _tempRect = new List<Rectangle>(_totalColumns * _totalRows);

            _bitmapInfo = base.InitBitmapInfo(_width, _height, (ushort)(_bytePerPixel * 8), 0);
            base.InitCaptureBuffer(ref _hBitmap, ref _memDC, ref _bits, IntPtr.Zero, 0, IntPtr.Zero, _bitmapInfo);

        }
        public  IntPtr Buffer => _bits;
        public bool HasData
        {
            get
            {
                lock (_lock)
                {
                    return _hasData;
                }
            }
        }
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
            if(Interlocked.CompareExchange(ref _fullScreenReceived, 1, 0) == 0)
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
        public bool SetBusy()
        {
            if(Interlocked.CompareExchange(ref _readyToSend, 1, 0 )== 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public void SendCompleted()
        {
            Interlocked.Exchange(ref _readyToSend, 0);
        }
        public void BeginAccept()
        {
            if(Interlocked.CompareExchange(ref _fullScreenReceived, 1, 1) == 1)
            {
                lock (_lock)
                {
                    _acceptRegionChanged = true;
                }
            }        
        }
        public void Add(RegionFrame regions)
        {
            lock (_lock)
            {
                _hasData = true;
                foreach (var region in regions.Bounds)
                {
                    int index = GetRectangleIndex(region);
                    if(index != -1)
                    {
                        _writer[index] = region;    
                    }
                    //var key = (long)region.X << 32 | (uint)region.Y;
                    //_regions[key] = region;
                }
            }
        }
        public ScreenDataDto GetData()
        {
            //if (Interlocked.CompareExchange(ref _readyToSend, 0, 0) != 0)
            //    return null;

            lock (_lock)
            {
                if (!_hasData)
                    return null;
                _hasData = false;
                var temp = _writer;
                _writer = _reader;
                _reader = temp;
            }

            MergeDirtyRegions(_reader, 0.9);

            if (_tempRect.Count == 0) return null;

            int rentLength = GetScreenDataLength(_tempRect, _bytePerPixel);
            byte[] buffer = VArrayPool.Rent(rentLength);
            int offset = 0;
            foreach(var region in _tempRect)
            {
                base.GetRegionsData(
                    ref offset,
                    buffer,
                    _bits,
                    _width,
                    region.X,
                    region.Y,
                    region.Width,
                    region.Height,
                    _bytePerPixel);
            }
            return new ScreenDataDto(buffer, 0, offset + 1);
        }
        public void MergeDirtyRegions(Rectangle[] regions, double threshold = 0.8)
        {
            _tempRect.Clear();
            var baseRect = regions[0];

            regions[0] = Rectangle.Empty;

            for (int i = 1; i < regions.Length; i++)
            {
                var rect = regions[i];

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
                    _tempRect.Add(baseRect);
                    baseRect = rect;
                }

                regions[i] = Rectangle.Empty;
            }

            // Finally, add last base rectangle to groups result
            _tempRect.Add(baseRect);
        }
        public override void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
                return;

            //Clear DIBSection resources
            if (_hBitmap != IntPtr.Zero)
                CaptureApi.DeleteObject(_hBitmap);

            if (_memDC != IntPtr.Zero)
                CaptureApi.ReleaseDC(IntPtr.Zero, _memDC);

            _bits = IntPtr.Zero;
            _hBitmap = IntPtr.Zero;
            _memDC = IntPtr.Zero;

            if (disposing)
            {
                Array.Clear(_writer, 0 , _writer.Length);
                Array.Clear(_reader, 0, _reader.Length);
                _tempRect.Clear();

                _tempRect = null;
                _writer = null;
                _reader = null;
            }
        }
    }
}
