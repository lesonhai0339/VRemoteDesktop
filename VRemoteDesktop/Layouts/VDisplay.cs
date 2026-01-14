using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using VRemoteDesktop.Services.ScreenCapture.Interop;
using static VRemoteDesktop.Services.ScreenCapture.Interop.CaptureApi;

namespace VRemoteDesktop.Layouts
{
    public partial class VDisplay : UserControl
    {
        public int baseWidth;
        public int baseHeight;
        private IntPtr _bits;
        private BITMAPINFO _bitmapInfo;
        private PAINTSTRUCT _paintStruct;
        private RECT _rect;
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
        public void Setup(int width, int height, IntPtr bits, BITMAPINFO bitmapInfo)
        {
            baseWidth = width;
            baseHeight = height;
            _bits = bits;
            _bitmapInfo = bitmapInfo;

            this.Invalidate();

            CaptureApi.BeginPaint(this.Handle, out _paintStruct);
            bool flag = CaptureApi.GetClientRect(this.Handle, out _rect);

            if (!flag)
            {
                EndPaint(this.Handle, ref _paintStruct);
                //TODO
            }

        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_bits == IntPtr.Zero)
            {
                base.OnPaint(e);
                return;
            }
            var r = e.ClipRectangle;

            // Calculate scale and get min
            double scaleWidth = (double)this.Width / baseWidth;
            double scaleHeight = (double)this.Height / baseHeight;
            double scale = Math.Min(scaleWidth, scaleHeight);

            int scaledWidth = (int)(baseWidth * scale);
            int scaledHeight = (int)(baseHeight * scale);

            int offsetX = (this.Width - scaledWidth) / 2;
            int offsetY = (this.Height - scaledHeight) / 2;


            IntPtr hdc = e.Graphics.GetHdc();

            try
            {
                RECT rect;
                if (!CaptureApi.GetClientRect(this.Handle, out rect))
                {
                    return;
                }

                int result = CaptureApi.StretchDIBits(
                    hdc,                  
                    offsetY,                     
                    offsetY,                      
                    scaledWidth,             
                    scaledHeight,
                    0,
                    0,
                    baseWidth,
                    baseHeight,                
                    _bits,                 
                    ref _bitmapInfo,      
                    0,                      
                    0x00CC0020);           

                if (result == 0)
                {
                    int error = Marshal.GetLastWin32Error();
                    System.Diagnostics.Debug.WriteLine($"StretchDIBits failed with error: {error}");
                }
            }
            finally
            {
                e.Graphics.ReleaseHdc(hdc);
            }
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
