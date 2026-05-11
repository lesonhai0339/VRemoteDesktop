using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using VRemoteDesktop.Services.ScreenCapture.GDI;
using VRemoteDesktop.Services.ScreenCapture.Interop;

namespace VRemoteDesktop.Services.ScreenCapture.DTOs
{
    public class ImageSwapper
    {
        public ImageSwapper(int width, int height, int size)
        {
            HBitmap = IntPtr.Zero;
            Bits = IntPtr.Zero;
            MemDC = IntPtr.Zero;


            _width = width;
            _height = height;
            _size = size;

            _col = (_width + (_size - 1)) / _size;
            _row = (_height + (_size - 1)) / _size;



            _changedRegions = new Rectangle[_col * _row];
        }

        private readonly object _lock = new object();
        private int _width;
        private int _height;
        private int _col;
        private int _row;
        private int _size;
        public IntPtr HBitmap;
        public IntPtr MemDC;

        public object Lock => _lock;    
        public IntPtr Bits;
        private Rectangle _fullScreen;
        private Rectangle[] _changedRegions { get; set; }
        public Rectangle FullScreen
        {
            get { return _fullScreen; }
        }
        public Rectangle[] ChangedRegions
        {
            get
            {
                return _changedRegions;
            }
        }
        public void AddFullScreen(Rectangle region)
        {
            lock (_lock)
            {
                if (!region.IsEmpty)
                    _fullScreen = region;
            }
        }
        public void Add(RegionFrame regions)
        {
            //_changedRegions được sắp xếp và add theo dạng grid(full screen chia ra các regions với size(default 16x16)) 
            //nhưng regions add vào lại là các merged regions(có kích thước => grid nên cần tính toán số grid mà region phủ)
            lock (_lock)
            {
                int start = regions.Start;
                int length = regions.Length;
                for (int r = start; r<length; r++)
                {
                    ref var region = ref regions.Bounds[r];

                    //luôn chia hết cho 16 nên có thể tính trực tiếp
                    int startCol = region.X;
                    int startRow = region.Y;

                    int numCols = (region.Width + 15) >> 4;
                    int numRows = (region.Height + 15) >> 4;

                    for (int i= 0; i < numRows; i++)
                    {
                        int y = startRow + (i * _size);

                        bool isLastRow = (y + _size > _height);

                        int currentHeight = isLastRow ? (_height - y) : _size;

                        for (int j= 0; j < numCols; j++)
                        {
                            int x = startCol + (j * _size);
                            Rectangle curRegion;

                            bool isLastCol = (x + _size > _width);

                            if (isLastRow || isLastCol)
                            {
                                int currentWidth = isLastCol ? (_width - x) : _size;
                                curRegion = new Rectangle(x, y, currentWidth, currentHeight);
                            }
                            else
                            {
                                curRegion = new Rectangle(x, y, _size, _size);
                            }

                            int index = GetRectangleIndex(curRegion);
                            if (index >= 0 && index < _changedRegions.Length)
                                _changedRegions[index] = curRegion;
                        }
                    }
                }
            }
        }
        public int GetRectangleIndex(int x, int y)
        {
            if (x < 0 || y < 0)
                return -1;

            return ((x / _size) * _col) + (y / _size);
        }
        public int GetRectangleIndex(Rectangle rect)
        {
            if (rect.IsEmpty)
                return -1;

            return ((rect.Y / _size) * _col) + (rect.X / _size);
        }
        public void Clear()
        {
            Array.Clear(_changedRegions, 0, _changedRegions.Length);
        }
        public void Free()
        {
            if (MemDC != IntPtr.Zero)
            {
                CaptureApi.DeleteDC(MemDC);
                //CaptureApi.ReleaseDC(IntPtr.Zero, MemDC);
            }
            if (HBitmap != IntPtr.Zero)
                CaptureApi.DeleteObject(HBitmap);

            Bits = IntPtr.Zero;
            HBitmap = IntPtr.Zero;
            MemDC = IntPtr.Zero;

            Array.Clear(ChangedRegions, 0, ChangedRegions.Length);
        }
    }
}
