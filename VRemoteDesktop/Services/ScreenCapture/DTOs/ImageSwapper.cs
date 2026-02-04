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
        public ImageSwapper(int col, int row, int size)
        {
            _col = col;
            _row = row;
            _size = size;

            HBitmap = IntPtr.Zero;
            Bits = IntPtr.Zero;
            MemDC = IntPtr.Zero;


            var total = new Rectangle[col * row];
            ChangedRegions = new List<Rectangle>(col * row);
        }

        private readonly object _lock = new object();   
        private int _col;
        private int _row;
        private int _size;
        public IntPtr HBitmap;
        public IntPtr MemDC;

        public object Lock => _lock;    
        public IntPtr Bits;
        private List<Rectangle> _changedRegions { get; set; }
        public List<Rectangle> ChangedRegions
        {
            get
            {
                lock (_lock)
                {
                    return _changedRegions;
                }
            }
            set
            {
                lock (_lock)
                {
                    _changedRegions = value;
                }
            }
        }
        public void Add(RegionFrame regions)
        {
            lock (_lock)
            {
                foreach (var region in regions.Bounds)
                {
                    _changedRegions.Add(region);    
                }
            }
        }
        public void Clear()
        {
            lock (_lock)
            {
                _changedRegions.Clear();
            }
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

            ChangedRegions.Clear();
        }
    }
}
