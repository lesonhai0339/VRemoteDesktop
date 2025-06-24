using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class VRegion
    {
        private Region _region;
        private bool _isInited;
        private bool _isDispose;
        private RectangleF[] _rectangles;
        public VRegion() 
        {
            _isInited = false;
            _rectangles = null;
        }
        #region Properties
        public bool IsInited
        {
            get { return _isInited; }
            set { _isInited = value; }
        }
        public struct Vrect
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
        }
        public struct VRectAPI
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        public int RectsUBound
        {
            get
            {
                int result;
                if(_rectangles == null)
                {
                    return -1;
                }
                else
                {
                    result = _rectangles.GetUpperBound(0);
                }
                return result;
            }
        }
        #endregion
        #region Methods
        public VRectangle GetBounds(int hdc)
        {
            VRectangle result;
            if (!_isInited)
            {
                result = null;
            }
            else
            {
                Graphics graphic = Graphics.FromHdc((IntPtr)hdc);
                Rectangle rectangle = Rectangle.Round(_region.GetBounds(graphic));
                VRectangle vRectangle = new VRectangle();
                vRectangle.FromXYWH(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
                graphic.Dispose();
                result = vRectangle;
            }
            return result;
        }
        public void Union(int x, int y, int width, int height)
        {
            if (!_isInited)
            {
                InitRegion(x, y, width, height);
                return;
            }
            _region.Union(new Rectangle(x, y, width, height));
        }
        public void Union(VRegion vRegion)
        {
            if (vRegion._isInited)
            {
                if (!_isInited)
                {
                    method_1(vRegion._region);
                    return;
                }
                _region.Union(vRegion._region);
            }

        }
        internal void method_0(Region region_1)
        {
            if (!_isInited)
            {
                method_1(region_1);
                return;
            }
            _region.Union(region_1);
        }
        private void InitRegion(int x, int y, int width, int height)
        {
            Rectangle rect = new Rectangle(x, y, width, height);
            _region = new Region(rect);
            _isInited = true;
        }
        internal void InitTriangleRegion(int x0, int y0, int x1, int y1, int x2, int y2)
        {
            GraphicsPath graphic = new GraphicsPath();
            Point point0 = new Point(x0, y0);
            Point point1 = new Point(x1, y1);
            Point point2 = new Point(x2, y2);
            graphic.AddLine(point0, point1);
            graphic.AddLine(point0, point2);
            graphic.AddLine(point2, point1);
            _region = new Region(graphic);
            _isInited = true;
        }
        internal void method_1(Region region)
        {
            _region = region.Clone();
            _isInited = true;
        }
        public Vrect GetRect(int index)
        {
            checked
            {
                Vrect result;
                result.X = (int)Math.Round((double)_rectangles[index].X);
                result.Y = (int)Math.Round((double)_rectangles[index].Y);
                result.Width = (int)Math.Round((double)_rectangles[index].Width);
                result.Height = (int)Math.Round((double)_rectangles[index].Height);
                return result;
            }
        }
        public VRectangle GetVRectangle(int index)
        {
            VRectangle vRectangle = new VRectangle();
            checked
            {
                vRectangle.FromXYWH(
                    (int)Math.Round((double)_rectangles[index].X),
                    (int)Math.Round((double)_rectangles[index].Y),
                    (int)Math.Round((double)_rectangles[index].Width),
                    (int)Math.Round((double)_rectangles[index].Height)
                );

                return vRectangle;
            }
        }
        public void ScanRectangles()
        {
            if (!_isInited)
            {
                _rectangles = null;
                return;
            }
            _rectangles  = _region.GetRegionScans(new Matrix());
        }
        public int GetHrng(int int_0)
        {
            Graphics g = Graphics.FromHwnd((IntPtr)int_0);
            return (int)_region.GetHrgn(g);
        }
        public int GetTotalAcreage()
        {
            int total = 0;
            foreach (RectangleF rect in _rectangles)
            {
                total += (int)(rect.Width * rect.Height);
            }
            return total;
        }
        public void Clear()
        {
            if(_region != null)
            {
                _region.MakeEmpty();
            }
            _isInited = false;
        }
        public bool Contains(VRectAPI rectAPI, int int_0)
        {
            bool result;
            if (!_isInited)
            {
                return false;
            }
            else
            {
                Graphics g = Graphics.FromHdcInternal((IntPtr)int_0);
                Region region = _region.Clone();
                region.Union(new Rectangle(
                        rectAPI.Left,
                        rectAPI.Top,
                        rectAPI.Right - rectAPI.Left,
                        rectAPI.Bottom - rectAPI.Top
                    ));
                result = _region.Equals(region, g);
            }
            return result;
        }
        public bool Exclude(int x, int y, int width, int height)
        {
            bool result;
            if (!_isInited)
            {
                result = false;
            }
            else
            {
                _region.Exclude(new Rectangle(x, y, width, height));
                result = true;
            }
            return result;
        }
        public bool Exclude(VRegion region)
        {
            bool result;
            if (!_isInited)
            {
                result = false;
            }
            else
            {
                _region.Exclude(region._region);
                result = true;
            }
            return result;
        }
        public void CloneFromVRegion(VRegion region)
        {
            if (!region._isInited)
            {
                this.Clear();
                return;
            }
            _region = region._region.Clone();
            _isInited = true;
        }
        public void Intersect(int x,int y, int width, int height)
        {
            if (_isInited)
            {
                Clear();
                return;
            }
            _region.Intersect(new Rectangle(x, y, width, height));
        }
        public void Intersect(VRegion region)
        {
            if(region._isInited && _isInited)
            {
                _region.Intersect(region._region);
                return;
            }
            Clear();
        }
        protected virtual void Dispose(bool flag)
        {
            if(!_isDispose && _region != null)
            {
                _region.Dispose();
            }
            _isDispose = true;
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
