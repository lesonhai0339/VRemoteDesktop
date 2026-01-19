using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture.DTOs;

namespace VRemoteDesktop.Services.ScreenCapture.GDI
{
    public class VRegions: IDisposable
    {
        private readonly ConcurrentDictionary<long, RegionFrame> _remainingFrames = new ConcurrentDictionary<long, RegionFrame>();
        private DateTimeOffset _lastTimeGet;
        public VRegions() { }

        public void GetRegions()
        {
            //
        }
        public void Add(long order, RegionFrame frame)
        {
            if(Find(frame, out var existed))
            {
                _remainingFrames.TryRemove(existed.Key, out _);
            }
            _remainingFrames[order] = frame;
        }
        public bool Find(RegionFrame frame, out KeyValuePair<long, RegionFrame> existedFrame)
        {
            existedFrame = _remainingFrames.FirstOrDefault(x => frame.Equals(x.Value.Bounds));
            if(existedFrame.Value != null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        public bool Contains(Rectangle rect1, Rectangle rect2)
        {
            return rect1.Contains(rect2) || rect2.Contains(rect1); 
        }

        public bool CanMerge(Rectangle rect1, Rectangle rect2, out Rectangle result)
        {
            result = Rectangle.Empty;
            var rect = Rectangle.Union(rect1, rect2);
            var area = rect.Width * rect.Height;

            var s = (rect1.Width * rect1.Height) + (rect2.Width * rect2.Height);
            if(((double)s / area) >= 0.9)
            {
                result = rect;
                return true;
            }
            else
            {
                return false;
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
