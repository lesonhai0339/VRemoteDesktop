using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RemoteClient.Remote
{
    public class VRectangle
    {
        private Rectangle _rectangle;
        public VRectangle() 
        {
            _rectangle = default;
        }
        #region Properties
        public int X
        {
            get => _rectangle.X;
            set => _rectangle.X = value;
        }
        public int Y
        {
            get => _rectangle.Y;
            set => _rectangle.Y = value;
        }
        public int Width
        {
            get => _rectangle.Width;
            set => _rectangle.Width = value;
        }
        public int Height
        {
            get => _rectangle.Height;
            set => _rectangle.Height = value;
        }
        #endregion
        #region Methods
        public void Intersect(int x, int y, int width, int height)
        {
            _rectangle.Intersect(new Rectangle(x, y, width, height));
        }
        public bool IntersectWith(int x, int y, int width, int height)
        {
            return _rectangle.IntersectsWith(new Rectangle(x, y, width, height));
        }
        public bool IntersectWith(VRectangle vRectangle)
        {
            return _rectangle.IntersectsWith(vRectangle._rectangle);
        }
        public bool Contains(VRectangle vRectangle)
        {
            return _rectangle.Contains(vRectangle._rectangle);
        }
        public void FromLTRB(int left, int top, int right, int bottom)
        {
            _rectangle = Rectangle.FromLTRB(left, top, right, bottom);
        }
        public void FromXYWH(int x, int y, int width, int height)
        {
            _rectangle = new Rectangle(x, y, width, height);
        }
        #endregion

    }
}
