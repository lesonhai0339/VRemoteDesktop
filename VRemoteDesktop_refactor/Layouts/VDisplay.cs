using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Vsign4.VRemoteDesktop.Services.ScreenCapture.Interop;

namespace Vsign4.VRemoteDesktop.Layouts
{
    public partial class VDisplay : UserControl
    {
        private readonly object _lock = new object();
        private double _scale;
        private int _imageWidth;
        private int _imageHeight;
        private int _offsetX;
        private int _offsetY;
        private IntPtr _bits;
        private CaptureApi.BITMAPINFO _bitmapInfo;
        private CaptureApi.PAINTSTRUCT _paintStruct;
        private CaptureApi.RECT _rect;
        private int _stride;
        public VDisplay()
        {
            InitializeComponent();
            this.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.SetStyle(
               ControlStyles.AllPaintingInWmPaint |
               ControlStyles.UserPaint |
               ControlStyles.Opaque, true);

            this.DoubleBuffered = false;
        }
        public double ImageScale 
        {
            get
            {
                lock (_lock)
                {
                    return _scale;
                }
            }   
        }
        public int ImageHeight
        {
            get
            {
                lock (_lock)
                {
                    return _imageHeight;
                }   
            }
        }
        public int ImageOffsetX
        {
            get
            {
                lock (_lock)
                {
                    return _offsetX;
                }   
            }
        }
        public int ImageOffsetY
        {
            get
            {
                lock (_lock)
                {
                    return _offsetY;
                }       
            }
        }
        public void Setup(int width, int height, int stride, IntPtr bits, CaptureApi.BITMAPINFO bitmapInfo)
        {
            _imageWidth = width;
            _imageHeight = height;
            _bits = bits;
            _bitmapInfo = bitmapInfo;
            _stride = stride;
            ReCalculateScale();

            //this.Invalidate();

            //CaptureApi.BeginPaint(this.Handle, out _paintStruct);
            //bool flag = CaptureApi.GetClientRect(this.Handle, out _rect);

            //if (!flag)
            //{
            //    EndPaint(this.Handle, ref _paintStruct);
            //    //TODO
            //}

        }
        private void ReCalculateScale()
        {
            lock (_lock)
            {
                double scaleWidth = (double)ClientSize.Width / _imageWidth;
                double scaleHeight = (double)ClientSize.Height / _imageHeight;
                _scale = Math.Min(scaleWidth, scaleHeight);

                int imgWidth = (int)(_imageWidth * _scale);
                int imgHeight = (int)(_imageHeight * _scale);

                _offsetX = (ClientSize.Width - imgWidth) / 2;
                _offsetY = (ClientSize.Height - imgHeight) / 2;
            }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_bits == IntPtr.Zero)
            {
                base.OnPaint(e);
                return;
            }
            IntPtr hdc = e.Graphics.GetHdc();

            try
            {
                CaptureApi.RECT rect;
                if (!CaptureApi.GetClientRect(this.Handle, out rect))
                {
                    return;
                }

                int srcX = (int)((e.ClipRectangle.X - _offsetX) / _scale);
                int srcY = (int)((e.ClipRectangle.Y - _offsetY) / _scale);

                srcX = Math.Max(0, srcX);
                srcY = Math.Max(0, srcY);

                int srcWidth = (int)Math.Min(_imageWidth - srcX, Math.Ceiling(e.ClipRectangle.Width / _scale));
                int srcHeight = (int)Math.Min(_imageHeight - srcY, Math.Ceiling(e.ClipRectangle.Height / _scale));

                CaptureApi.SetStretchBltMode(hdc, 4); //HALFTONE

                //Console.WriteLine($"Draw coordinate: X:{e.ClipRectangle.X} - Y:{e.ClipRectangle.Y} - W:{e.ClipRectangle.Width} - H:{e.ClipRectangle.Height}");
                //Console.WriteLine($"Region Coordinate after transform: X:{srcX} - Y:{srcY} - W:{srcWidth} - H:{srcHeight}");
                //Console.WriteLine($"\n{new string('-', 10)}\n");

                lock (_lock)
                {
                    int result = CaptureApi.StretchDIBits(
                    hdc,
                    e.ClipRectangle.X,
                    e.ClipRectangle.Y,
                    e.ClipRectangle.Width,
                    e.ClipRectangle.Height,
                    0, //at form is ((image.X * scale) + _offsetX)
                    0, //at form is ((image.Y * scale) + _offsetY)
                    _imageWidth,  //at form is (image.Width * scale)
                    _imageHeight,  //at form is (image.Height * scale)                
                    _bits,
                    ref _bitmapInfo,
                    0,
                    0x00CC0020);
                    if (result == 0)
                    {
                        int error = Marshal.GetLastWin32Error();
                        System.Diagnostics.Debug.WriteLine(string.Format("StretchDIBits failed with error: {ex}", error));
                    }
                }      
            }
            finally
            {
                e.Graphics.ReleaseHdc(hdc);
            }
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            // Re-calculate scale on size change

            ReCalculateScale();

            using (Graphics g = this.CreateGraphics())
            {
                g.Clear(Color.Black);
            }

            this.Invalidate();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_bits == IntPtr.Zero)
            {
                base.OnPaintBackground(e);
            }
        }
    }
}
