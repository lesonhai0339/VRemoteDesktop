using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using VRemoteDesktop.Services.ScreenCapture.DTOs;
using VRemoteDesktop.Services.ScreenCapture.Interop;

namespace VRemoteDesktop.Services.ScreenCapture.GDI
{
    public class VRegions : IDisposable
    {
        private readonly object _lock = new object();   
        private readonly ConcurrentDictionary<long, Rectangle> _regions = new ConcurrentDictionary<long, Rectangle>();
        private int _bytePerPixel;
        private int _width;
        private int _height;
        private IntPtr _sourceImage;
        private int _readyToSend;
        private bool _canWork = false;
        private long _lastSendTimestamp = Stopwatch.GetTimestamp();
        public VRegions(int width, int height, int bytePerPixel)
        {
            _width = width;
            _height = height;
            _bytePerPixel = bytePerPixel;
        }
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
        public void Add(RegionFrame frames)
        {
            _sourceImage = frames.Pointer;
            foreach (var frame in frames.Bounds)
            {
                var key = (long)frame.X << 32 | (uint)frame.Y;
                _regions[key] = frame;
            }
        }
        public byte[] GetRegionData()
        {
            //No source image
            if (_sourceImage == IntPtr.Zero)
                return null;
            //Not ready to capture
            if (!_canWork)
                return null;

            Rectangle[] regionsSnapshot;
            lock (_lock)
            {
                regionsSnapshot = _regions.Values.ToArray();
                _regions.Clear();
            }

            var regions = MergeRegions(regionsSnapshot);
            var bufferLength = GetRegionsDataLength(regions, _bytePerPixel);
            byte[] buffer = VArrayPool.Rent(bufferLength);
            int offset = 0;
            for (int i = 0; i < regions.Count; i++)
            {
                var region = regions[i];
                GetRegionsData(ref offset, buffer, _sourceImage, region.X, region.Y, region.Width, region.Height, _bytePerPixel);
            }
            return buffer;
        }
        public List<Rectangle> MergeRegions(Rectangle[] regions, double threshold = 0.9)
        {
            if (regions == null || regions.Length == 0)
                return new List<Rectangle>();

            var dirtyRegionsSorted = regions.OrderBy(x => x.Top)
                .ThenBy(x => x.Left)
                .ToList();

            List<Rectangle> groups = new List<Rectangle>();

            // Using first rectangle as base
            var baseRect = dirtyRegionsSorted[0];

            // For-loop to try to merge other rectangles
            for (int i = 1; i < dirtyRegionsSorted.Count; i++)
            {
                // Get current rectangle    
                var rect = dirtyRegionsSorted[i];

                // Union base rectangle and current rectangle
                var area = Rectangle.Union(baseRect, rect);

                // Get acreage of union area
                var areaUnion = area.Width * area.Height;
                // Get acreage sum of base rectangle and current rectangle
                var areaSum = baseRect.Width * baseRect.Height + rect.Width * rect.Height;

                // Calculate ratio between union area and acreage sum
                var ratio = (double)areaSum / areaUnion;

                // If ratio > threshold, assign union area to base rectangle and continue try to merge next rectangle
                if (ratio > threshold)
                {
                    baseRect = area;
                }
                // Else, add base rectangle to groups result and assign current as a new base rectangle
                else
                {
                    groups.Add(baseRect);
                    baseRect = rect;
                }
            }

            // Finally, add last base rectangle to groups result
            groups.Add(baseRect);

            return groups;
        }
        public int GetRegionsDataLength(List<Rectangle> rectangles, int bytePerPixel)
        {
            return rectangles.Sum(x => GetRegionDataLength(x.Width, x.Height, bytePerPixel)); //Only send raw bytes + 16 is header
        }
        public int GetRegionDataLength(Rectangle bounds, int bytePerPixel)
        {
            return GetRegionDataLength(bounds.Width, bounds.Height, bytePerPixel);
        }
        public int GetRegionDataLength(int width, int height, int bytePerPixel)
        {
            return (width * height * bytePerPixel) + 16; //Only send raw bytes + 16 is header
        }
        public virtual unsafe void GetRegionsData(
        ref int offset,
        byte[] destination,
        IntPtr source,
        int regionX,
        int regionY,
        int regionWidth,
        int regionHeight,
        int bytePerPixel = 3)
        {
            fixed (byte* bst = destination)
            {
                byte* basePtr = bst + offset;
                //Add header info of region(x,y,w,h)
                uint* d = (uint*)basePtr;
                *d++ = (uint)regionX;
                *d++ = (uint)regionY;
                *d++ = (uint)regionWidth;
                *d++ = (uint)regionHeight;

                //calculate stride of big screen and regions
                int srcStride = GetStride(_width, bytePerPixel);
                uint dstStride = (uint)(regionWidth * bytePerPixel);

                if (offset + (dstStride * regionHeight) + 16 > destination.Length)
                {
                    throw new IndexOutOfRangeException("Buffer quá nhỏ để chứa dữ liệu vùng này!");
                }

                unsafe
                {
                    //get address of source and destination 
                    byte* src = (byte*)source;
                    byte* dst = (byte*)d;

                    //for-loop to write row from source to destination
                    for (int row = 0; row < regionHeight; row++)
                    {
                        int sy = (row + regionY) * srcStride; //reverser screen data , see more at InitCaptureBuffer() ->  bmi.Header.biHeight = -height;
                        int sx = regionX * bytePerPixel; //offset in row

                        IntPtr srcAdd = (IntPtr)(src + sy + sx); //formular => address = base + rowOffset + colOffset 

                        IntPtr dstAdd = (IntPtr)(dst + (row * dstStride)); //formular => address = base + (row * rowStride) 

                        CaptureApi.memcpy(dstAdd, srcAdd, (UIntPtr)dstStride);
                    }
                }
                offset += (int)(dstStride * regionHeight) + 16; //Data + header(16 bytes for x,y,w,h)
            }
        }
        public virtual int GetStride(int width, int bytePerPixel)
        {
            return (((width * bytePerPixel * 8) + 31) & ~31) >> 3;
        }
        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}
